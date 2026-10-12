using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MedicalKitUIが非Activeでも確認できる独立した診断用（v1.3 ボタン式対応）。
/// InventoryCanvas（またはInventoryPanel）のActiveなGameObjectへ取り付けてください。
/// 問題解決後は取り外してOKです。
/// </summary>
[DisallowMultipleComponent]
public class MedicalKitUIRuntimeDebugger2D : MonoBehaviour
{
    [Header("診断対象（未設定なら名前・Componentから検索）")]
    [SerializeField] private MedicalKitInventoryUI2D uiController;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject medicalKitPanel;
    [SerializeField] private GameObject mainInventoryPanel;

    [Header("出力")]
    [SerializeField] private bool autoFindMissingReferences = true;
    [SerializeField] private bool logPointerEvents = true;
    [SerializeField] private bool inspectPointerRaycast = true;
    [SerializeField, Range(0.1f, 2f)] private float raycastCheckInterval = 0.3f;

    private float nextCheck;
    private string lastPointerStatus = string.Empty;
    private MedicalKitUIButtonProbe2D openProbe;
    private MedicalKitUIButtonProbe2D closeProbe;

    private void Awake()
    {
        FindReferences();
        AttachProbes();
        Report("Awake");
    }

    private void OnEnable()
    {
        FindReferences();
        AttachProbes();
        SubscribeClicks();
        Report("OnEnable");
    }

    private void Start()
    {
        FindReferences();
        Report("Start：起動後の設定確認");
    }

    private void OnDisable()
    {
        UnsubscribeClicks();
        Info("DebuggerがOnDisableになりました。診断ログが止まる場合は、常時ActiveのCanvasへ移動してください。");
    }

    private void LateUpdate()
    {
        if (!inspectPointerRaycast || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + raycastCheckInterval;
        FindReferences();
        AttachProbes();
        if (mainInventoryPanel != null && !mainInventoryPanel.activeInHierarchy) return;
        CheckRaycast(openButton, "OpenButton");
        CheckRaycast(closeButton, "CloseButton");
    }

    private void FindReferences()
    {
        if (!autoFindMissingReferences) return;
        if (uiController == null)
            uiController = FindAnyObjectByType<MedicalKitInventoryUI2D>(FindObjectsInactive.Include);
        if (uiController != null)
        {
            if (openButton == null) openButton = uiController.DiagnosticOpenButton;
            if (closeButton == null) closeButton = uiController.DiagnosticCloseButton;
            if (medicalKitPanel == null) medicalKitPanel = uiController.DiagnosticPanelRoot;
            if (mainInventoryPanel == null) mainInventoryPanel = uiController.DiagnosticMainInventoryPanel;
        }
        if (openButton == null) openButton = FindNamedButton("OpenMedicalKitButton");
        if (closeButton == null) closeButton = FindNamedButton("CloseButton");
        if (medicalKitPanel == null) medicalKitPanel = FindNamedObject("MedicalKitPanel");
        if (mainInventoryPanel == null) mainInventoryPanel = FindNamedObject("InventoryPanel");
    }

    private Button FindNamedButton(string objectName)
    {
        GameObject go = FindNamedObject(objectName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private GameObject FindNamedObject(string objectName)
    {
        Transform[] all = transform.root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in all)
            if (candidate.name == objectName) return candidate.gameObject;
        return null;
    }

    private void AttachProbes()
    {
        if (openButton != null)
        {
            openProbe = openButton.GetComponent<MedicalKitUIButtonProbe2D>();
            if (openProbe == null) openProbe = openButton.gameObject.AddComponent<MedicalKitUIButtonProbe2D>();
            openProbe.Configure(this, "OpenButton");
        }
        if (closeButton != null)
        {
            closeProbe = closeButton.GetComponent<MedicalKitUIButtonProbe2D>();
            if (closeProbe == null) closeProbe = closeButton.gameObject.AddComponent<MedicalKitUIButtonProbe2D>();
            closeProbe.Configure(this, "CloseButton");
        }
    }

    private void SubscribeClicks()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenButtonClicked);
            openButton.onClick.AddListener(OpenButtonClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseButtonClicked);
            closeButton.onClick.AddListener(CloseButtonClicked);
        }
    }

    private void UnsubscribeClicks()
    {
        if (openButton != null) openButton.onClick.RemoveListener(OpenButtonClicked);
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseButtonClicked);
    }

    private void OpenButtonClicked()
    {
        Info("[BUTTON OnClick到達] OpenButtonのButton.onClickイベントが発火しました。UIController側で[CLICK]ログが無ければUIControllerが無効、またはボタン参照が違います。");
    }

    private void CloseButtonClicked()
    {
        Info("[BUTTON OnClick到達] CloseButtonのButton.onClickイベントが発火しました。UIController側で[CLICK/CLOSE]ログが無ければUIControllerが無効、またはボタン参照が違います。");
    }

    public void PointerEvent(string buttonName, string phase, PointerEventData e)
    {
        if (!logPointerEvents) return;
        Info($"[POINTER {phase}] {buttonName} / pointer={e.position} / EventSystem={(EventSystem.current != null ? "OK" : "NULL")}");
    }

    private void CheckRaycast(Button button, string label)
    {
        if (button == null || !button.gameObject.activeInHierarchy) return;
        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null) return;
        Vector2 mouse;
        if (Mouse.current == null) return;
        mouse = Mouse.current.position.ReadValue();
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mouse, cam)) return;

        if (EventSystem.current == null)
        {
            ReportPointerState(label + ": EventSystemが無いためクリック不可");
            return;
        }
        PointerEventData data = new PointerEventData(EventSystem.current) { position = mouse };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        string result;
        if (hits.Count == 0) result = label + ": UI Raycastが0件。CanvasのGraphicRaycasterとButtonのImage/Raycast Targetを確認。";
        else
        {
            Transform top = hits[0].gameObject.transform;
            bool reachesButton = top == button.transform || top.IsChildOf(button.transform);
            result = reachesButton
                ? label + ": Raycast OK / top=" + PathOf(top) + " / interactable=" + button.interactable
                : label + ": 【遮蔽あり】ボタンの手前に " + PathOf(top) + " が存在。ImageのRaycast Targetを確認 / interactable=" + button.interactable;
        }
        ReportPointerState(result);
    }

    private void ReportPointerState(string result)
    {
        if (result == lastPointerStatus) return;
        lastPointerStatus = result;
        Info("[マウス位置診断] " + result);
    }

    [ContextMenu("診断：応急キットUIの接続状況をConsoleに出す")]
    public void ReportFromInspector()
    {
        FindReferences();
        AttachProbes();
        Report("Inspectorメニュー");
    }

    private void Report(string phase)
    {
        Info("[総合診断] " + phase +
            " / Debugger=" + PathOf(transform) +
            " / Controller=" + DescribeController() +
            " / Main=" + Describe(mainInventoryPanel) +
            " / Panel=" + Describe(medicalKitPanel) +
            " / OpenButton=" + DescribeButton(openButton) +
            " / CloseButton=" + DescribeButton(closeButton));

        if (uiController == null) Warn("UIControllerが見つかりません。MedicalKitInventoryUI2Dを付けたObjectを参照に設定してください。");
        else
        {
            if (!uiController.gameObject.activeInHierarchy || !uiController.enabled)
                Warn("【原因候補】UIControllerが動作していません。UIControllerのObjectと全ての親がActiveか、ComponentがEnabledか確認してください。");
            if (medicalKitPanel != null && uiController.transform.IsChildOf(medicalKitPanel.transform))
                Warn("【原因候補】UIControllerがMedicalKitPanelの中にあります。UIControllerをPanelの外へ移動してください。");
            if (uiController.DiagnosticPanelRoot != medicalKitPanel)
                Warn("【原因候補】DebuggerのPanelとUIControllerのPanel Root参照が一致していません。別のPanelを閉じている可能性があります。");
            if (uiController.DiagnosticOpenButton != openButton)
                Warn("【原因候補】DebuggerのOpenButtonとUIControllerのOpen Button参照が違います。");
            if (uiController.DiagnosticCloseButton != closeButton)
                Warn("【原因候補】DebuggerのCloseButtonとUIControllerのClose Button参照が違います。");
        }
        if (EventSystem.current == null) Warn("EventSystemがありません。UIクリックを処理できません。");
        if (openButton != null && !openButton.interactable)
            Warn("OpenButtonがInteractable=falseです。ボタン式では常に有効です。UIControllerのOnEnableと他スクリプトのButton制御を確認してください。");
        if (openButton != null && openButton.GetComponent<Graphic>() == null)
            Warn("OpenButtonにGraphic(Imageなど)がありません。Raycastできない可能性があります。");
        if (medicalKitPanel != null && medicalKitPanel.activeSelf &&
            (uiController == null || !uiController.isActiveAndEnabled))
            Warn("Panelは表示中なのにUIControllerが無効です。パネルが開いたままになる原因候補です。");
    }

    private string DescribeController()
    {
        if (uiController == null) return "NULL";
        return PathOf(uiController.transform) +
            $"(active={uiController.gameObject.activeInHierarchy},enabled={uiController.enabled})";
    }

    private static string Describe(GameObject obj)
    {
        return obj == null ? "NULL" : PathOf(obj.transform) +
            $"(self={obj.activeSelf},hierarchy={obj.activeInHierarchy})";
    }

    private static string DescribeButton(Button button)
    {
        if (button == null) return "NULL";
        return PathOf(button.transform) +
            $"(active={button.gameObject.activeInHierarchy},enabled={button.enabled},interactable={button.interactable},raycastTarget={(button.GetComponent<Graphic>() != null && button.GetComponent<Graphic>().raycastTarget)})";
    }

    private static string PathOf(Transform t)
    {
        if (t == null) return "NULL";
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    private void Info(string msg)
    {
        Debug.Log("[MedicalKitUI Debugger] " + msg, this);
    }

    private void Warn(string msg)
    {
        Debug.LogWarning("[MedicalKitUI Debugger][警告] " + msg, this);
    }
}

/// <summary>診断専用のPointerイベント記録。Debuggerがボタンへ自動追加します。</summary>
[DisallowMultipleComponent]
public class MedicalKitUIButtonProbe2D : MonoBehaviour,
    IPointerEnterHandler, IPointerDownHandler, IPointerClickHandler, IPointerExitHandler
{
    private MedicalKitUIRuntimeDebugger2D debugger;
    private string label;

    public void Configure(MedicalKitUIRuntimeDebugger2D owner, string buttonLabel)
    {
        debugger = owner;
        label = buttonLabel;
    }

    public void OnPointerEnter(PointerEventData data) => Relay("Enter", data);
    public void OnPointerDown(PointerEventData data) => Relay("Down", data);
    public void OnPointerClick(PointerEventData data) => Relay("Click", data);
    public void OnPointerExit(PointerEventData data) => Relay("Exit", data);

    private void Relay(string phase, PointerEventData e)
    {
        if (debugger != null && debugger.isActiveAndEnabled)
            debugger.PointerEvent(label, phase, e);
    }
}
