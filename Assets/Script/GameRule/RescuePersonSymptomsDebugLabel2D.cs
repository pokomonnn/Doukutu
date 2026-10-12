using TMPro;
using UnityEngine;

/// <summary>
/// 任意のデバッグ表示。NPC用の新しいWorld Space TextMeshProを割り当てて使用。
/// 既存のNPC名・会話テキストを誤って上書きしないため自動探索は行いません。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonSymptomsDebugLabel2D : MonoBehaviour
{
    [SerializeField] private RescuePersonSymptoms2D symptoms;
    [SerializeField] private TMP_Text label;
    [SerializeField] private bool showWhenNoSymptoms = false;
    [SerializeField] private bool showWhenDead = false;

    private void Awake()
    {
        if (symptoms == null)
            symptoms = GetComponentInParent<RescuePersonSymptoms2D>();
    }

    private void OnEnable()
    {
        if (symptoms == null)
            symptoms = GetComponentInParent<RescuePersonSymptoms2D>();

        if (symptoms != null)
            symptoms.SymptomsChanged += HandleChanged;

        Refresh();
    }

    private void Start() { Refresh(); }

    private void OnDisable()
    {
        if (symptoms != null)
            symptoms.SymptomsChanged -= HandleChanged;
    }

    private void HandleChanged(StatusConditionType current, StatusConditionType stabilized)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (label == null || symptoms == null) return;

        bool dead = symptoms.IsDead;
        bool visible = (!dead || showWhenDead) &&
                       (showWhenNoSymptoms || symptoms.HasAnySymptoms);
        label.enabled = visible;
        if (!visible) return;

        label.text = dead ? "死亡" : symptoms.GetSymptomsSummary();
    }
}
