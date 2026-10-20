using Unity.Cinemachine;
using UnityEngine;

/// <summary>洞窟の会話中だけ既存カメラの画角を変更。追従や会話進行は変更しない。</summary>
[DisallowMultipleComponent]
public sealed class CaveConversationZoom2D : MonoBehaviour
{
    [SerializeField, Range(0.4f, 1f)] private float sizeRatio = 0.67f;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.35f;
    private Camera output;
    private CinemachineCamera virtualCamera;
    private TownConversationController townDialogue;
    private DialoguePresenter2D dialogue;
    private Transform speaker;
    private float originalSize;
    private bool ownsSize;

    public static void Begin(TownConversationController controller, Transform npc)
    {
        if (controller == null || !controller.IsOpen) return;
        var zoom = Resolve();
        if (zoom == null) return;
        zoom.townDialogue = controller;
        zoom.dialogue = null;
        zoom.speaker = npc;
        zoom.Capture();
    }

    public static void Begin(DialoguePresenter2D presenter, Transform npc)
    {
        // 町の会話には適用しない。洞窟の帰還設備をシーン判定に使う。
        var exit = FindFirstObjectByType<CaveExitElevator2D>();
        if (npc == null || exit == null || exit.gameObject.scene != npc.gameObject.scene) return;
        var zoom = Resolve();
        if (zoom == null) return;
        zoom.dialogue = presenter;
        zoom.townDialogue = null;
        zoom.speaker = npc;
        zoom.Capture();
    }

    private static CaveConversationZoom2D Resolve()
    {
        var camera = Camera.main;
        if (camera == null || !camera.orthographic) return null;
        var camp = FindFirstObjectByType<CampModeController>();
        if (camp != null && (camp.IsCamping || camp.IsBusy)) return null;
        var zoom = camera.GetComponent<CaveConversationZoom2D>();
        if (zoom == null) zoom = camera.gameObject.AddComponent<CaveConversationZoom2D>();
        return zoom.enabled ? zoom : null;
    }

    private void Capture()
    {
        // 戻り途中で次の会話が始まっても、ズーム後のサイズを基準にしない。
        if (ownsSize) return;
        output = GetComponent<Camera>();
        var brain = output.GetComponent<CinemachineBrain>();
        virtualCamera = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
        // Brain使用時に不明なカメラへ書き込むと競合するため、切替中などは見送る。
        if (brain != null && brain.enabled && virtualCamera == null) return;
        originalSize = ReadSize();
        ownsSize = true;
    }

    private float ReadSize() => virtualCamera != null
        ? virtualCamera.Lens.OrthographicSize : output.orthographicSize;

    private void WriteSize(float size)
    {
        if (virtualCamera != null)
        {
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = size;
            virtualCamera.Lens = lens;
        }
        else if (output != null) output.orthographicSize = size;
    }

    private void Update()
    {
        if (!ownsSize) return;
        bool talking = speaker != null && speaker.gameObject.activeInHierarchy &&
            ((townDialogue != null && townDialogue.isActiveAndEnabled && townDialogue.IsOpen) ||
             (dialogue != null && dialogue.isActiveAndEnabled && dialogue.IsPlaying));
        float target = talking ? originalSize * sizeRatio : originalSize;
        float size = Mathf.MoveTowards(ReadSize(), target,
            originalSize * (1f - sizeRatio) * Time.unscaledDeltaTime / Mathf.Max(0.05f, transitionSeconds));
        WriteSize(size);
        if (!talking && Mathf.Approximately(size, originalSize)) Restore();
    }

    private void Restore()
    {
        if (ownsSize) WriteSize(originalSize);
        ownsSize = false;
        speaker = null;
        townDialogue = null;
        dialogue = null;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
