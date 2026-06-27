using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class PlayerStatusConditionController : MonoBehaviour
{
    [Header("骨折による移動低下")]
    [Tooltip("骨折中の移動速度倍率。重量による速度倍率とも掛け算されます")]
    [SerializeField, Range(0.05f, 1f)]
    private float fractureMoveSpeedMultiplier = 0.55f;

    [Header("骨折SE")]
    [Tooltip("未設定なら、このオブジェクトのAudioSourceを自動取得します")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip fractureSound;

    [SerializeField, Range(0f, 1f)]
    private float fractureSoundVolume = 0.9f;

    public StatusConditionType ActiveConditions { get; private set; }

    public bool IsFractured =>
        HasCondition(StatusConditionType.Fracture);

    public float MoveSpeedMultiplier =>
        IsFractured ? fractureMoveSpeedMultiplier : 1f;

    // 将来の状態異常UIや、治療アイテム処理から購読するためのイベント
    public event Action<StatusConditionType> ConditionsChanged;
    public event Action<StatusConditionType> ConditionsAdded;
    public event Action<StatusConditionType> ConditionsRemoved;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public bool HasCondition(StatusConditionType condition)
    {
        if (condition == StatusConditionType.None)
        {
            return false;
        }

        return (ActiveConditions & condition) == condition;
    }

    public bool AddConditions(StatusConditionType conditions)
    {
        if (conditions == StatusConditionType.None)
        {
            return false;
        }

        StatusConditionType addedConditions =
            conditions & ~ActiveConditions;

        if (addedConditions == StatusConditionType.None)
        {
            return false;
        }

        ActiveConditions |= addedConditions;

        if ((addedConditions & StatusConditionType.Fracture) != 0)
        {
            PlayFractureSound();
        }

        ConditionsAdded?.Invoke(addedConditions);
        ConditionsChanged?.Invoke(ActiveConditions);

        return true;
    }

    public bool RemoveConditions(StatusConditionType conditions)
    {
        if (conditions == StatusConditionType.None)
        {
            return false;
        }

        StatusConditionType removedConditions =
            ActiveConditions & conditions;

        if (removedConditions == StatusConditionType.None)
        {
            return false;
        }

        ActiveConditions &= ~removedConditions;

        ConditionsRemoved?.Invoke(removedConditions);
        ConditionsChanged?.Invoke(ActiveConditions);

        return true;
    }

    // 治療アイテム側から呼びやすい別名
    public bool CureConditions(StatusConditionType conditions)
    {
        return RemoveConditions(conditions);
    }

    public void ClearAllConditions()
    {
        if (ActiveConditions == StatusConditionType.None)
        {
            return;
        }

        StatusConditionType removedConditions = ActiveConditions;
        ActiveConditions = StatusConditionType.None;

        ConditionsRemoved?.Invoke(removedConditions);
        ConditionsChanged?.Invoke(ActiveConditions);
    }

    private void PlayFractureSound()
    {
        if (audioSource == null || fractureSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            fractureSound,
            fractureSoundVolume
        );
    }

    private void OnValidate()
    {
        fractureMoveSpeedMultiplier = Mathf.Clamp(
            fractureMoveSpeedMultiplier,
            0.05f,
            1f
        );

        fractureSoundVolume = Mathf.Clamp01(
            fractureSoundVolume
        );
    }
}
