using System.Collections;
using UnityEngine;

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

    private Coroutine useCoroutine;
    private string currentUseTrigger;

    private void Awake()
    {
        FindReferences();
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
    }
}