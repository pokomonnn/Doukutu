using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 洞窟内の「Eで回収」オブジェクト共通処理。
/// プレイヤーが複数の回収物に重なっている場合は、一番近い1個だけがEに反応します。
/// 既存のWorldItemPickup/ItemBoxInteractableとは独立しています。
/// </summary>
public abstract class CavePickupInteractable2D : MonoBehaviour
{
    [Header("操作")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    [Header("接近表示（未設定でも取得できます）")]
    [Tooltip("オブジェクトの子に置いたWorld Space用TextMeshProなどを設定してください。")]
    [SerializeField] private TMP_Text pickupPromptText;

    [Header("取得メッセージ（任意）")]
    [SerializeField] private InventoryToastUI toastUI;

    [Header("取得音（任意）")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupSoundVolume = 0.8f;

    private static readonly List<CavePickupInteractable2D> ActivePickups =
        new List<CavePickupInteractable2D>();

    private readonly Dictionary<Collider2D, Transform> playerColliders =
        new Dictionary<Collider2D, Transform>();

    // 同じフレームのE入力を他の回収物が再利用しないようにする。
    private static int lastHandledFrame = -1;

    private bool collected;

    protected virtual void Awake()
    {
        SetPromptVisible(false);
    }

    protected virtual void OnEnable()
    {
        if (!ActivePickups.Contains(this))
        {
            ActivePickups.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        ActivePickups.Remove(this);
        playerColliders.Clear();
        SetPromptVisible(false);
    }

    protected virtual void Update()
    {
        Transform player = FindNearbyPlayer();
        bool canUse = !collected &&
                      Time.timeScale > 0f &&
                      player != null &&
                      IsReady() &&
                      GetFocusedPickup(player) == this;

        if (pickupPromptText != null)
        {
            pickupPromptText.text = canUse
                ? "[" + pickupKey + "] " + GetPromptLabel()
                : string.Empty;
            SetPromptVisible(canUse);
        }

        if (canUse && Input.GetKeyDown(pickupKey) &&
            lastHandledFrame != Time.frameCount)
        {
            lastHandledFrame = Time.frameCount;
            Collect(player);
        }
    }

    protected abstract bool IsReady();
    protected abstract string GetPromptLabel();
    protected abstract void Collect(Transform player);

    protected void ShowMessage(string message)
    {
        if (toastUI == null)
        {
            toastUI = FindAnyObjectByType<InventoryToastUI>(
                FindObjectsInactive.Include);
        }

        if (toastUI != null && toastUI.gameObject.activeInHierarchy)
        {
            toastUI.Show(message);
        }
        else
        {
            Debug.Log("[CavePickup] " + message, this);
        }
    }

    protected void PlayPickupSound()
    {
        if (pickupSound == null)
        {
            return;
        }

        // 回収物を直後にDestroyしても最後まで聞こえる2D効果音。
        GameObject soundObject = new GameObject("CavePickup_OneShotAudio");
        soundObject.transform.position = transform.position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = pickupSoundVolume;
        source.clip = pickupSound;
        source.Play();
        Destroy(soundObject, Mathf.Max(0.1f, pickupSound.length + 0.1f));
    }

    protected void FinishCollection()
    {
        if (collected)
        {
            return;
        }

        collected = true;
        SetPromptVisible(false);
        Destroy(gameObject);
    }

    private Transform FindNearbyPlayer()
    {
        // 複数Colliderがプレイヤーに付いている場合にも対応する。
        foreach (KeyValuePair<Collider2D, Transform> entry in playerColliders)
        {
            if (entry.Key != null &&
                entry.Key.enabled &&
                entry.Key.gameObject.activeInHierarchy &&
                entry.Value != null)
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static CavePickupInteractable2D GetFocusedPickup(Transform player)
    {
        CavePickupInteractable2D closest = null;
        float bestDistanceSquared = float.PositiveInfinity;

        foreach (CavePickupInteractable2D pickup in ActivePickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled ||
                pickup.collected || !pickup.IsReady() ||
                pickup.FindNearbyPlayer() != player)
            {
                continue;
            }

            float distanceSquared =
                (pickup.transform.position - player.position).sqrMagnitude;

            if (closest == null ||
                distanceSquared < bestDistanceSquared - 0.0001f ||
                (Mathf.Abs(distanceSquared - bestDistanceSquared) < 0.0001f &&
                 pickup.GetInstanceID() < closest.GetInstanceID()))
            {
                closest = pickup;
                bestDistanceSquared = distanceSquared;
            }
        }

        return closest;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayerCollider(other);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (!playerColliders.ContainsKey(other))
        {
            RegisterPlayerCollider(other);
        }
    }

    private void RegisterPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        Transform player = null;
        PlayerMove movement = other.GetComponentInParent<PlayerMove>();
        if (movement != null)
        {
            player = movement.transform;
        }
        else
        {
            Transform current = other.transform;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(playerTag) &&
                    current.CompareTag(playerTag))
                {
                    player = current;
                    break;
                }
                current = current.parent;
            }
        }

        if (player != null)
        {
            playerColliders[other] = player;
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other != null)
        {
            playerColliders.Remove(other);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (pickupPromptText != null)
        {
            pickupPromptText.enabled = visible;
        }
    }

    protected virtual void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    protected virtual void OnValidate()
    {
        pickupSoundVolume = Mathf.Clamp01(pickupSoundVolume);
    }
}
