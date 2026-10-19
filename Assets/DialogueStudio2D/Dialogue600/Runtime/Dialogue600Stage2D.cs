using UnityEngine;

/// <summary>ダンジョンの舞台を指定します。1=旧居住区、2=工房区、3=聖堂区。</summary>
[DisallowMultipleComponent]
public sealed class Dialogue600Stage2D : MonoBehaviour
{
    [SerializeField, Range(1, 3)] private int currentStage = 1;
    private static Dialogue600Stage2D active;
    public static int CurrentStage => active != null ? active.currentStage : 0;
    public static bool Matches(int stage) => stage <= 0 || (active != null && active.currentStage == stage);
    private void OnEnable() { active = this; }
    private void OnDisable() { if (active == this) active = null; }
}
