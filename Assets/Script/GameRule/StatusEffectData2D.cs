using UnityEngine;

public enum StatusEffectCategory2D
{
    Buff,
    Debuff
}

/// <summary>
/// UI表示に必要なバフ/デバフ設定。
/// 既存のStatusConditionTypeとの連携も指定できます。
/// 持続時間0は治療・明示解除まで継続（円形ゲージは非表示）。
/// </summary>
[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Game/Status Effects/Effect Data 2D")]
public class StatusEffectData2D : ScriptableObject
{
    [Header("表示")]
    [SerializeField] private string displayName = "新しい状態効果";
    [SerializeField, TextArea(2, 6)] private string description = "効果の説明";
    [SerializeField] private Sprite icon;
    [SerializeField] private StatusEffectCategory2D category = StatusEffectCategory2D.Debuff;

    [Header("持続時間")]
    [Tooltip("秒。0=永続（解除するまで継続）。時間表示はせず、円形ゲージで残量を示します。")]
    [SerializeField, Min(0f)] private float durationSeconds = 0f;

    [Header("既存の状態異常と連動（任意）")]
    [Tooltip("Bleeding/Fracture/Poison/Burnのうち1種類だけ選択してください。Noneなら独立した一時効果として扱います。")]
    [SerializeField] private StatusConditionType linkedCondition = StatusConditionType.None;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public StatusEffectCategory2D Category => category;
    public float DurationSeconds => durationSeconds;
    public StatusConditionType LinkedCondition => linkedCondition;

    // Managerの登録漏れを知らせるため、実行時だけ使う表示用データ。
    // ProjectのScriptableObjectアセットは変更しません。
    internal void SetRuntimeFallback(string name, string detail, StatusConditionType condition)
    {
        displayName = name;
        description = detail;
        icon = null;
        category = StatusEffectCategory2D.Debuff;
        durationSeconds = 0f;
        linkedCondition = condition;
    }

    private void OnValidate()
    {
        durationSeconds = Mathf.Max(0f, durationSeconds);
    }
}
