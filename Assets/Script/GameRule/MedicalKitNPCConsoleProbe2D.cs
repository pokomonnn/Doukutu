using UnityEngine;

/// <summary>
/// NPCのT案内が出ないとき用の独立診断コンポーネントです。
/// 常時ActiveなCanvasや空GameObjectに追加してください。
/// NPCInteractionが無効・未配置でも警告を出します。
/// ゲーム処理・UI表示を変更しません。
/// </summary>
[DisallowMultipleComponent]
public sealed class MedicalKitNPCConsoleProbe2D : MonoBehaviour
{
    [SerializeField] private MedicalKitNPCInteraction2D npcInteraction;
    [SerializeField, Min(2f)] private float checkInterval = 5f;
    [Tooltip("ONならPlayer側の詳細診断も毎回出力します。ログが多くなるため通常はOFF。")]
    [SerializeField] private bool alwaysPrintFullDetails;

    private float nextCheckTime;
    private string lastWarning = string.Empty;

    private void OnEnable()
    {
        nextCheckTime = 0f;
        CheckOnce();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + Mathf.Max(2f, checkInterval);
        CheckOnce();
    }

    [ContextMenu("診断 / PlayerのT治療案内を詳細診断")]
    public void CheckAndPrintAll()
    {
        Resolve();
        if (npcInteraction == null)
        {
            Debug.LogWarning("[MedicalKitNPC][外部診断] MedicalKitNPCInteraction2DがSceneに見つかりません。Playerに付いているか確認。", this);
            return;
        }
        Debug.Log("[MedicalKitNPC][外部診断] 発見: " + GetPath(npcInteraction.transform) +
                  " / ComponentEnabled=" + npcInteraction.enabled +
                  " / ActiveInHierarchy=" + npcInteraction.gameObject.activeInHierarchy, this);
        npcInteraction.DiagnoseEverything();
    }

    private void CheckOnce()
    {
        Resolve();
        string warning = null;
        if (npcInteraction == null)
            warning = "MedicalKitNPCInteraction2DがSceneにありません。Playerへ追加し、古い同名スクリプトを重複配置しないでください。";
        else if (!npcInteraction.gameObject.activeInHierarchy)
            warning = "T治療コンポーネントのGameObjectが非Activeです: " + GetPath(npcInteraction.transform);
        else if (!npcInteraction.enabled)
            warning = "T治療コンポーネントがInspectorでOFFになっています: " + GetPath(npcInteraction.transform);
        else if (npcInteraction.NpcLayerMask.value == 0)
            warning = "Npc Layers=Nothing。NPC ColliderのLayerを選択してください。";

        if (warning != null && warning != lastWarning)
            Debug.LogWarning("[MedicalKitNPC][外部診断] " + warning, this);
        lastWarning = warning ?? string.Empty;

        if (alwaysPrintFullDetails && npcInteraction != null)
            npcInteraction.DiagnoseEverything();
    }

    private void Resolve()
    {
        if (npcInteraction == null)
            npcInteraction = FindAnyObjectByType<MedicalKitNPCInteraction2D>(FindObjectsInactive.Include);
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null) return "NULL";
        string result = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            result = transform.name + "/" + result;
        }
        return result;
    }
}
