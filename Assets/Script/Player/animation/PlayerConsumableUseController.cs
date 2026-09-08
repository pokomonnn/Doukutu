using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerWeightController))]
public class PlayerConsumableUseController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerWeightController playerWeightController;

    [SerializeField]
    private PlayerEquipmentVisualController
        equipmentVisualController;

    [Tooltip("未設定ならPlayer自身、または子オブジェクトから探します")]
    [SerializeField] private Animator playerAnimator;

    [Header("松明ショートカット")]
    [Tooltip("ONならキーボード上段の1キーで、所持中の松明を自動使用します。")]
    [SerializeField] private bool enableTorchShortcut = true;

    [Tooltip("ONならテンキー1でも松明を使用できます。")]
    [SerializeField] private bool allowNumpad1 = true;

    [Tooltip("PlayerのInventoryController。未設定なら自動取得します。")]
    [SerializeField] private InventoryController inventoryController;

    [Header("松明ショートカット：デバッグ")]
    [SerializeField] private bool showTorchShortcutLogs;

    private Coroutine useCoroutine;
    private string currentUseTrigger;

    private void Awake()
    {
        FindReferences();
    }

    private void Update()
    {
        if (!enableTorchShortcut ||
            !WasTorchShortcutPressed())
        {
            return;
        }

        FindReferences();

        // 消耗品使用演出中の連打で、使用時間を何度も上書きしない。
        if (useCoroutine != null)
        {
            LogTorchShortcut(
                "消耗品使用中のため、1キー入力を無視しました。"
            );
            return;
        }

        if (inventoryController == null)
        {
            LogTorchShortcutWarning(
                "InventoryControllerが見つからないため、松明を使用できません。"
            );
            return;
        }

        bool used = inventoryController.TryUseFirstTorch(
            out ItemUseResult result
        );

        if (used)
        {
            LogTorchShortcut("1キーで松明を自動使用しました。");
            return;
        }

        switch (result)
        {
            case ItemUseResult.TorchItemNotFound:
                LogTorchShortcut("松明を所持していません。");
                break;

            case ItemUseResult.TorchIsFull:
                LogTorchShortcut(
                    "松明残量が満タンのため使用しませんでした。"
                );
                break;

            default:
                LogTorchShortcut(
                    $"松明を使用できませんでした。Result={result}"
                );
                break;
        }
    }

    private void OnDisable()
    {
        if (useCoroutine != null)
        {
            StopCoroutine(useCoroutine);
            useCoroutine = null;
        }

        equipmentVisualController
            ?.SetWeaponHiddenForConsumableUse(false);
    }

    public void BeginConsumableUse(
        ConsumableItemData consumableData)
    {
        if (consumableData == null)
        {
            return;
        }

        FindReferences();

        float duration = consumableData.SlowdownDuration;

        // 時間が0なら、従来どおり速度低下・銃非表示なし
        if (duration <= 0f)
        {
            PlayUseAnimation(
                consumableData.UseAnimationTrigger
            );

            return;
        }

        // 回復中に別の回復アイテムを使った場合は、
        // 前の演出時間を中断して新しいアイテムの時間でやり直す
        if (useCoroutine != null)
        {
            StopCoroutine(useCoroutine);
            useCoroutine = null;
        }

        PlayUseAnimation(
            consumableData.UseAnimationTrigger
        );

        // 回復中は装備している銃を隠す
        equipmentVisualController
            ?.SetWeaponHiddenForConsumableUse(true);

        // 既存の移動速度低下処理を使う
        playerWeightController
            ?.ApplyConsumableUseSlowdown(
                duration,
                consumableData.UseMoveSpeedMultiplier
            );

        useCoroutine = StartCoroutine(
            UseRoutine(duration)
        );
    }

    private IEnumerator UseRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        equipmentVisualController
            ?.SetWeaponHiddenForConsumableUse(false);

        useCoroutine = null;
    }

    private void PlayUseAnimation(string triggerName)
    {
        if (playerAnimator == null ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentUseTrigger))
        {
            playerAnimator.ResetTrigger(currentUseTrigger);
        }

        currentUseTrigger = triggerName;

        playerAnimator.SetTrigger(currentUseTrigger);
    }

    private void FindReferences()
    {
        if (playerWeightController == null)
        {
            playerWeightController =
                GetComponent<PlayerWeightController>();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                GetComponent<PlayerEquipmentVisualController>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }

        if (playerAnimator == null)
        {
            playerAnimator =
                GetComponentInChildren<Animator>(true);
        }

        if (inventoryController == null)
        {
            inventoryController =
                GetComponent<InventoryController>();
        }

        if (inventoryController == null)
        {
            inventoryController =
                GetComponentInParent<InventoryController>();
        }

        if (inventoryController == null)
        {
            inventoryController =
                FindAnyObjectByType<InventoryController>();
        }
    }

    private bool WasTorchShortcutPressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return false;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            return true;
        }

        return allowNumpad1 &&
               keyboard.numpad1Key.wasPressedThisFrame;
    }

    private void LogTorchShortcut(string message)
    {
        if (!showTorchShortcutLogs)
        {
            return;
        }

        Debug.Log(
            $"[PlayerConsumableUseController][TorchShortcut] {message}",
            this
        );
    }

    private void LogTorchShortcutWarning(string message)
    {
        Debug.LogWarning(
            $"[PlayerConsumableUseController][TorchShortcut] {message}",
            this
        );
    }
}