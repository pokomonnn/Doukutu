using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WorldItemPickup : MonoBehaviour
{
    [Header("拾う設定")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Tooltip("捨てた直後に、すぐ拾い直さないための待ち時間")]
    [SerializeField, Min(0f)] private float pickupDelay = 0.25f;

    [Header("拾う表示")]
    [SerializeField] private TMP_Text pickupPromptText;
    [SerializeField] private string pickupPromptLabel = "拾う";

    [SerializeField]
    private Vector3 pickupPromptLocalPosition =
        new Vector3(0f, 0.85f, 0f);

    [SerializeField] private bool hidePromptDuringPickupDelay = true;

    [Header("スタック数表示")]
    [Tooltip("DroppedItemの子に置いた StackAmountText を設定")]
    [SerializeField] private TMP_Text stackAmountText;

    [SerializeField] private string stackAmountPrefix = "×";

    [SerializeField]
    private Vector3 stackAmountLocalPosition =
        new Vector3(0.35f, -0.25f, 0f);

    [Tooltip("1個だけの時は個数を表示しない")]
    [SerializeField]
    private bool showStackAmountOnlyWhenMoreThanOne = true;

    [Header("サウンド")]
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip pickupSound;

    [SerializeField, Range(0f, 1f)]
    private float dropSoundVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float pickupSoundVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float soundSpatialBlend = 0f;

    [Header("参照")]
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private InventoryController inventoryController;

    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private InventoryItem droppedItem;
    private float canPickupAfterTime;
    private bool isPickingUp;

    private enum StackPickupResult
    {
        Failed,
        Partial,
        Complete
    }

    public InventoryItem DroppedItem => droppedItem;

    public bool IsPlayerInRange => playerColliders.Count > 0;

    public bool HasValidDroppedItem =>
    droppedItem != null &&
    droppedItem.ItemData != null;

    public WorldItemSaveData CreateSaveData()
    {
        if (!HasValidDroppedItem)
        {
            return null;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        Vector2 velocity = rb != null
            ? rb.linearVelocity
            : Vector2.zero;

        Vector3 position = transform.position;

        return new WorldItemSaveData
        {
            itemId = droppedItem.ItemData.ItemId,
            amount = droppedItem.Amount,
            isRotated = droppedItem.IsRotated,

            hasStoredMagazineAmmo =
                droppedItem.HasStoredMagazineAmmo,

            storedMagazineAmmo =
                droppedItem.StoredMagazineAmmo,

            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,

            velocityX = velocity.x,
            velocityY = velocity.y
        };
    }

    public bool RestoreFromSaveData(
        WorldItemSaveData saveData,
        ItemData itemData)
    {
        if (saveData == null || itemData == null)
        {
            return false;
        }

        int amount = Mathf.Clamp(
            saveData.amount,
            1,
            itemData.MaxStack
        );

        InventoryItem restoredItem = new InventoryItem(
            itemData,
            0,
            0,
            amount
        );

        if (saveData.isRotated &&
            restoredItem.CanRotate)
        {
            restoredItem.TryRotate();
        }

        if (saveData.hasStoredMagazineAmmo)
        {
            restoredItem.SetStoredMagazineAmmo(
                saveData.storedMagazineAmmo
            );
        }

        droppedItem = restoredItem;

        transform.position = new Vector3(
            saveData.positionX,
            saveData.positionY,
            saveData.positionZ
        );

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                saveData.velocityX,
                saveData.velocityY
            );

            rb.angularVelocity = 0f;
        }

        // ロード後はすぐ拾える状態にする
        canPickupAfterTime = Time.time;
        isPickingUp = false;

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();

        return true;
    }

    private void Awake()
    {
        if (itemSpriteRenderer == null)
        {
            itemSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        FindPickupPromptText();
        FindStackAmountText();

        ApplyPickupPromptPosition();
        ApplyStackAmountPosition();

        FindInventoryController();

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();
    }

    private void Update()
    {
        RefreshPickupPrompt();

        if (droppedItem == null ||
            droppedItem.ItemData == null ||
            isPickingUp ||
            !IsPlayerInRange ||
            Time.time < canPickupAfterTime)
        {
            return;
        }

        if (Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    public void Setup(InventoryItem item)
    {
        droppedItem = item;
        canPickupAfterTime = Time.time + pickupDelay;

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();

        PlayWorldSound(dropSound, dropSoundVolume);
    }

    public void SetVelocity(Vector2 velocity)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    public bool TryPickup()
    {
        if (droppedItem == null ||
            droppedItem.ItemData == null ||
            isPickingUp)
        {
            return false;
        }

        if (!FindInventoryController())
        {
            Debug.LogWarning(
                "WorldItemPickup: InventoryController が見つかりません。",
                this
            );

            return false;
        }

        isPickingUp = true;

        StackPickupResult result;

        if (droppedItem.CanStack)
        {
            result = TryPickupStackableItem();
        }
        else
        {
            result = TryPickupUniqueItem()
                ? StackPickupResult.Complete
                : StackPickupResult.Failed;
        }

        if (result == StackPickupResult.Failed)
        {
            isPickingUp = false;

            RefreshPickupPrompt();
            RefreshStackAmountText();

            Debug.Log(
                $"インベントリに空きがありません：" +
                $"{droppedItem.ItemData.DisplayName}",
                this
            );

            return false;
        }

        PlayWorldSound(pickupSound, pickupSoundVolume);

        // 一部しか拾えなかった時は、
        // 残りを地面に残して数字だけ更新する
        if (result == StackPickupResult.Partial)
        {
            isPickingUp = false;

            RefreshVisual();
            RefreshPickupPrompt();
            RefreshStackAmountText();

            return true;
        }

        if (pickupPromptText != null)
        {
            pickupPromptText.enabled = false;
        }

        if (stackAmountText != null)
        {
            stackAmountText.enabled = false;
        }

        Destroy(gameObject);
        return true;
    }

    private StackPickupResult TryPickupStackableItem()
    {
        int amountBeforePickup = droppedItem.Amount;

        inventoryController.TryAddItem(
            droppedItem.ItemData,
            amountBeforePickup,
            out int remainingAmount
        );

        int pickedUpAmount =
            amountBeforePickup - remainingAmount;

        if (pickedUpAmount <= 0)
        {
            return StackPickupResult.Failed;
        }

        droppedItem.RemoveAmount(pickedUpAmount);

        if (droppedItem.IsEmpty())
        {
            return StackPickupResult.Complete;
        }

        return StackPickupResult.Partial;
    }

    private bool TryPickupUniqueItem()
    {
        InventoryGrid grid = inventoryController.Grid;

        bool currentRotation =
            droppedItem.CanRotate &&
            droppedItem.IsRotated;

        if (TryFindSpace(
                grid,
                droppedItem,
                currentRotation,
                out Vector2Int position))
        {
            return inventoryController.TryMoveItem(
                droppedItem,
                position.x,
                position.y,
                currentRotation
            );
        }

        if (droppedItem.CanRotate)
        {
            bool alternateRotation = !currentRotation;

            if (TryFindSpace(
                    grid,
                    droppedItem,
                    alternateRotation,
                    out position))
            {
                return inventoryController.TryMoveItem(
                    droppedItem,
                    position.x,
                    position.y,
                    alternateRotation
                );
            }
        }

        return false;
    }

    private bool TryFindSpace(
        InventoryGrid grid,
        InventoryItem item,
        bool isRotated,
        out Vector2Int position)
    {
        position = Vector2Int.zero;

        if (grid == null || item == null)
        {
            return false;
        }

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.CanPlaceItem(
                        item,
                        x,
                        y,
                        isRotated))
                {
                    continue;
                }

                position = new Vector2Int(x, y);
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerColliders.Add(other);
        RefreshPickupPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
        RefreshPickupPrompt();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        if (other.transform.root.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMove>() != null;
    }

    private bool FindInventoryController()
    {
        if (inventoryController != null)
        {
            return true;
        }

        inventoryController =
            FindAnyObjectByType<InventoryController>();

        return inventoryController != null;
    }

    private void FindPickupPromptText()
    {
        if (pickupPromptText != null)
        {
            return;
        }

        pickupPromptText =
            FindTextByObjectName("PickupPrompt");
    }

    private void FindStackAmountText()
    {
        if (stackAmountText != null)
        {
            return;
        }

        stackAmountText =
            FindTextByObjectName("StackAmountText");
    }

    private TMP_Text FindTextByObjectName(string objectName)
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null &&
                text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private void RefreshVisual()
    {
        if (itemSpriteRenderer == null)
        {
            return;
        }

        if (droppedItem == null ||
            droppedItem.ItemData == null)
        {
            itemSpriteRenderer.sprite = null;
            return;
        }

        itemSpriteRenderer.sprite = droppedItem.ItemData.Icon;
    }

    private void RefreshPickupPrompt()
    {
        if (pickupPromptText == null)
        {
            return;
        }

        bool canShowAfterDelay =
            !hidePromptDuringPickupDelay ||
            Time.time >= canPickupAfterTime;

        bool shouldShow =
            droppedItem != null &&
            droppedItem.ItemData != null &&
            !isPickingUp &&
            IsPlayerInRange &&
            canShowAfterDelay;

        if (shouldShow)
        {
            pickupPromptText.text =
                $"{pickupKey}:{pickupPromptLabel}";
        }

        pickupPromptText.enabled = shouldShow;
    }

    private void RefreshStackAmountText()
    {
        if (stackAmountText == null)
        {
            return;
        }

        bool isStackItem =
            droppedItem != null &&
            droppedItem.ItemData != null &&
            droppedItem.CanStack;

        bool shouldShow =
            isStackItem &&
            (!showStackAmountOnlyWhenMoreThanOne ||
             droppedItem.Amount > 1);

        if (shouldShow)
        {
            stackAmountText.text =
                $"{stackAmountPrefix}{droppedItem.Amount}";
        }

        stackAmountText.enabled = shouldShow;
    }

    private void ApplyPickupPromptPosition()
    {
        if (pickupPromptText == null)
        {
            return;
        }

        pickupPromptText.transform.localPosition =
            pickupPromptLocalPosition;
    }

    private void ApplyStackAmountPosition()
    {
        if (stackAmountText == null)
        {
            return;
        }

        stackAmountText.transform.localPosition =
            stackAmountLocalPosition;
    }

    private void PlayWorldSound(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject soundObject = new GameObject(
            $"OneShot_{clip.name}"
        );

        soundObject.transform.position = transform.position;

        AudioSource audioSource =
            soundObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = soundSpatialBlend;

        audioSource.Play();

        Destroy(
            soundObject,
            Mathf.Max(0.1f, clip.length)
        );
    }

    private void OnValidate()
    {
        pickupDelay = Mathf.Max(0f, pickupDelay);

        dropSoundVolume = Mathf.Clamp01(dropSoundVolume);
        pickupSoundVolume = Mathf.Clamp01(pickupSoundVolume);
        soundSpatialBlend = Mathf.Clamp01(soundSpatialBlend);

        if (string.IsNullOrWhiteSpace(pickupPromptLabel))
        {
            pickupPromptLabel = "拾う";
        }

        if (string.IsNullOrWhiteSpace(stackAmountPrefix))
        {
            stackAmountPrefix = "×";
        }

        FindPickupPromptText();
        FindStackAmountText();

        ApplyPickupPromptPosition();
        ApplyStackAmountPosition();
    }
}