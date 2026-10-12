using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Reflection;

/// <summary>
/// 応急キットUI v2.3（NPCのTキーではInventoryのみ表示し、応急キットはボタンで開く）。
/// UIControllerはPanelRootの外、通常InventoryPanelの下のActiveなObjectへ。
/// Open/CloseはスクリプトがButton.onClickへ登録します。
/// </summary>
[DisallowMultipleComponent]
public class MedicalKitInventoryUI2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MedicalKitInventory2D medicalKit;

    [Header("インベントリ内のボタン")]
    [Tooltip("通常InventoryPanelの子にある応急キットボタン。OnClickは空にしてください。")]
    [SerializeField] private Button openMedicalKitButton;
    [Tooltip("応急キットPanel内の閉じるボタン。OnClickは空にしてください。")]
    [SerializeField] private Button closeMedicalKitButton;
    [SerializeField] private GameObject mainInventoryPanel;
    [Tooltip("開閉する応急キットPanel。UIController自身・その親は絶対に設定しないでください。")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform playerItemsRoot;
    [SerializeField] private RectTransform kitSlotsRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_FontAsset japaneseFont;

    [Header("プレイヤー/NPC治療")]
    [SerializeField] private InventoryPanelToggle inventoryPanelToggle;
    [SerializeField] private MedicalKitTreatment2D treatment;
    [Tooltip("省略可。使用対象：プレイヤー / NPC名 を表示するTMP Text")]
    [SerializeField] private TMP_Text treatmentTargetText;
    [Tooltip("固定枠クリック時の使用/取り出す/情報メニュー。未設定なら自動作成")]
    [SerializeField] private MedicalKitItemContextMenu2D contextMenu;
    [Header("NPC治療の3秒間進行ゲージ")]
    [Tooltip("未設定ならUIControllerに自動追加。NPCへの使用時だけ3秒進行します。")]
    [SerializeField] private MedicalKitNPCTreatmentProgress2D npcTreatmentProgress;


    [Header("見た目")]
    [SerializeField, Min(36f)] private float slotSize = 62f;
    [SerializeField, Min(0f)] private float slotSpacing = 4f;
    [SerializeField] private Color emptyColor = new Color(0.13f, 0.15f, 0.18f, 0.95f);
    [SerializeField] private Color itemColor = new Color(0.27f, 0.39f, 0.44f, 0.98f);

    [Header("応急キット効果音（2D UI音）")]
    [Tooltip("空欄ならMedicalKitUIControllerへAudioSourceを自動追加します。UI用なので2Dで再生します。")]
    [SerializeField] private AudioSource medicalKitAudioSource;

    [Tooltip("応急キットを開いた時の音。空欄ならResources/MedicalKitSFXから読込みます。")]
    [SerializeField] private AudioClip kitOpenSound;
    [Tooltip("応急キットを閉じた時の音。")]
    [SerializeField] private AudioClip kitCloseSound;
    [Tooltip("固定アイテムをクリックしてメニューを出した時の音。")]
    [SerializeField] private AudioClip selectSound;
    [Tooltip("通常Inventoryから応急キットへ医療用品を補充した時の音。")]
    [SerializeField] private AudioClip putSound;
    [Tooltip("応急キットから通常Inventoryへ医療用品を取り出した時の音。")]
    [SerializeField] private AudioClip takeSound;
    [Tooltip("治療成功時に鳴らす短い確認音。アイテム固有のUseSoundがある場合は既定で省略します。")]
    [SerializeField] private AudioClip treatmentSuccessSound;
    [Tooltip("補充失敗・治療できない状態などで鳴らす音。")]
    [SerializeField] private AudioClip failedSound;
    [SerializeField, Range(0f, 1f)] private float medicalKitSoundVolume = 0.75f;
    [Tooltip("OFFならConsumableItemData.UseSoundがある時、治療成功UI音は重ねません。")]
    [SerializeField] private bool playSuccessUiSoundWithItemSound = false;
    [Tooltip("ONでAudioClipやAudioSource不足をConsoleへ報告。")]
    [SerializeField] private bool logSoundDiagnostics = true;

    [Header("Tキーで通常Inventoryを開く際の音（v3.0）")]
    [Tooltip("ON: まずInventoryPanelToggleのOpenClipで再生し、それが鳴らない構成なら補助音を1回鳴らします。")]
    [SerializeField] private bool playNpcInventoryOpenSound = true;
    [Tooltip("OpenClipが未設定／Tab用AudioSourceが使えない時の補助音。空欄なら通常InventoryのOpenClip→kit_openの順で自動選択。")]
    [SerializeField] private AudioClip npcInventoryOpenFallbackSound;
    private bool warnedMissingAudioSource;
    private bool warnedMissingAudioClips;

    [Header("原因調査用ログ")]
    [Tooltip("ONで起動時、ボタンクリック時、開閉時、失敗時の詳細を出します。")]
    [SerializeField] private bool showDiagnosticLogs = true;
    [SerializeField] private bool logEveryOpenClose = true;

    // v1.5のShowUnusedCellsは廃止。常に3列×2行の縦長スロットを表示します。

    private Canvas rootCanvas;
    private InventoryController subscribedInventory;
    private MedicalKitInventory2D subscribedKit;
    private bool warnedHierarchy;
    private bool warnedMissingJapaneseGlyph;
    private RescuePersonSymptoms2D npcTarget;
    private bool npcMode;
    private bool openedFromNpc;

    // v2.3: TキーでNPC用Inventoryを開く際だけ有効化したHierarchyを、閉じる時に復元する。
    // 通常Tab Inventory側がもともとActiveだったオブジェクトは変更しない。
    private readonly List<GameObject> npcTemporarilyEnabledObjects = new List<GameObject>();
    private bool npcTemporarilyEnabledComponent;

    public bool IsTreatingNpc => npcMode;
    public RescuePersonSymptoms2D CurrentNpcTarget => npcTarget;
    public bool IsMedicalPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;


    public GameObject DiagnosticPanelRoot => panelRoot;
    public GameObject DiagnosticMainInventoryPanel => mainInventoryPanel;
    public Button DiagnosticOpenButton => openMedicalKitButton;
    public Button DiagnosticCloseButton => closeMedicalKitButton;
    public MedicalKitInventory2D DiagnosticMedicalKit => medicalKit;

    private void Awake()
    {
        FindReferences();
        SetupMedicalKitAudio();
        RepairPanelReferences("Awake");
        Log("Awake：UIControllerが起動しました。 " + GetSummary());
        ReportConfiguration("Awake");
        ApplyJapaneseFont();
        ClosePanelInternal("Awake（初期非表示）");
    }

    private void OnEnable()
    {
        FindReferences();
        SetupMedicalKitAudio();
        RepairPanelReferences("OnEnable");
        Log("OnEnable：UIControllerが有効化されました。 " + GetSummary());
        Subscribe();
        BindButtons();
        ApplyJapaneseFont();
        ClosePanelInternal("OnEnable（開き直し時の初期化）");
        RefreshButtonState();
        ReportConfiguration("OnEnable");
    }

    private void Start()
    {
        ReportConfiguration("Start");
    }

    private void OnDisable()
    {
        CancelNpcTreatment("Inventory/UIControllerが閉じられたため治療中断");
        Log("OnDisable：UIControllerが無効化されました。 " + GetSummary());
        UnbindButtons();
        Unsubscribe();
        ClosePanelInternal("OnDisable（メインInventory閉鎖時）");
        ResetNpcSessionState();
        RestoreNpcTemporaryObjects();
    }

    private void LateUpdate()
    {
        // 通常Inventoryが閉じたら、応急キットの表示とNPC対象を初期化する。
        // KitPanelを開かずにTabで閉じた場合もNPC情報を次回へ引きずらない。
        if (mainInventoryPanel == null || mainInventoryPanel.activeInHierarchy)
            return;

        if (panelRoot != null && panelRoot.activeSelf)
        {
            Log("LateUpdate：メインInventoryが非表示になったので応急キットPanelを閉じます。");
            ClosePanelInternal("メインInventoryの閉鎖");
        }
        if (npcMode || openedFromNpc)
        {
            Log("[NPC SESSION終了] Inventoryが閉じられたためNPC治療対象を解除します。");
            ResetNpcSessionState();
            RestoreNpcTemporaryObjects();
        }
    }

    private void BindButtons()
    {
        if (openMedicalKitButton != null)
        {
            openMedicalKitButton.onClick.RemoveListener(TogglePanel);
            openMedicalKitButton.onClick.AddListener(TogglePanel);
            Log("OpenButtonのonClickを登録しました: " + PathOf(openMedicalKitButton.transform));
        }
        else Warn("Open Medical Kit Buttonが未設定なので開くクリックを受け取れません。");

        if (closeMedicalKitButton != null)
        {
            closeMedicalKitButton.onClick.RemoveListener(ClosePanel);
            closeMedicalKitButton.onClick.AddListener(ClosePanel);
            Log("CloseButtonのonClickを登録しました: " + PathOf(closeMedicalKitButton.transform));
        }
        else Warn("Close Medical Kit Buttonが未設定なので閉じるクリックを受け取れません。");
    }

    private void UnbindButtons()
    {
        if (openMedicalKitButton != null)
            openMedicalKitButton.onClick.RemoveListener(TogglePanel);
        if (closeMedicalKitButton != null)
            closeMedicalKitButton.onClick.RemoveListener(ClosePanel);
    }

    /// <summary>
    /// 「応急キット」ボタンは常に有効。ItemDataの所持状態は参照しない。
    /// 旧バージョンで無効化されたButtonもここで復帰する。
    /// </summary>
    private void RefreshButtonState()
    {
        if (openMedicalKitButton == null) return;
        if (!openMedicalKitButton.interactable)
        {
            openMedicalKitButton.interactable = true;
            Log("応急キットはボタン式です。OpenButtonをInteractable=trueに戻しました。");
        }
    }

    /// <summary>
    /// 誤設定を安全に補正します。特に Main=MedicalKitPanel, PanelRoot=PlayerMedicalItemsRoot
    /// の入れ違いを検出します。Editモードではコンポーネントのメニューから実行すると保存できます。
    /// </summary>
    [ContextMenu("修正：MedicalKitPanelとMain Inventoryの参照を自動修正")]
    public void RepairPanelReferencesFromInspector()
    {
        RepairPanelReferences("Inspectorメニュー");
        ReportConfiguration("参照自動修正後");
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void RepairPanelReferences(string phase)
    {
        GameObject candidatePanel = FindActualMedicalKitPanel();
        if (!IsSafePanelRoot(panelRoot) && candidatePanel != null && IsSafePanelRoot(candidatePanel))
        {
            GameObject old = panelRoot;
            panelRoot = candidatePanel;
            Warn($"[自動修正:{phase}] Panel Rootの参照が誤っていたため修正：" +
                 $"{Describe(old)} → {Describe(panelRoot)}。" +
                 " 実行中の変更は保存されません。Inspectorでも修正してください。");
        }

        if (!IsValidMainInventoryRoot(mainInventoryPanel))
        {
            GameObject candidateMain = FindCommonInventoryRoot();
            if (IsValidMainInventoryRoot(candidateMain))
            {
                GameObject old = mainInventoryPanel;
                mainInventoryPanel = candidateMain;
                Warn($"[自動修正:{phase}] Main Inventory Panelの参照を修正：" +
                     $"{Describe(old)} → {Describe(mainInventoryPanel)}。" +
                     " Tabで非表示になる通常Inventory側のObjectをInspectorで設定してください。");
            }
        }
    }

    // 正しいPanelは、CloseButtonと両アイテムRootを含み、OpenButtonとUIControllerを含まない。
    // これによりPlayerMedicalItemsRootをPanelと誤認するのを防ぐ。
    private bool IsSafePanelRoot(GameObject candidate)
    {
        if (candidate == null || WouldDisableController(candidate)) return false;
        Transform t = candidate.transform;
        if (closeMedicalKitButton == null || !closeMedicalKitButton.transform.IsChildOf(t)) return false;
        if (playerItemsRoot == null || !playerItemsRoot.IsChildOf(t)) return false;
        if (kitSlotsRoot == null || !kitSlotsRoot.IsChildOf(t)) return false;
        if (openMedicalKitButton != null && openMedicalKitButton.transform.IsChildOf(t)) return false;
        return true;
    }

    private GameObject FindActualMedicalKitPanel()
    {
        if (closeMedicalKitButton == null) return null;
        GameObject firstSafe = null;
        // CloseButtonの親を辿り、必要な要素すべてを含む最も近いパネルを見つける。
        for (Transform p = closeMedicalKitButton.transform.parent; p != null; p = p.parent)
        {
            if (!IsSafePanelRoot(p.gameObject)) continue;
            if (p.name == "MedicalKitPanel") return p.gameObject;
            if (firstSafe == null) firstSafe = p.gameObject;
        }
        return firstSafe;
    }

    private bool IsValidMainInventoryRoot(GameObject main)
    {
        if (main == null) return false;
        if (panelRoot != null &&
            (main == panelRoot || main.transform.IsChildOf(panelRoot.transform))) return false;
        if (openMedicalKitButton != null &&
            !openMedicalKitButton.transform.IsChildOf(main.transform)) return false;
        return true;
    }

    private GameObject FindCommonInventoryRoot()
    {
        if (openMedicalKitButton == null) return null;
        // ControllerとOpenButtonに共通する最も近い親。
        // 今回のHierarchyならCanvas/Inventorysに該当します。
        for (Transform p = transform.parent; p != null; p = p.parent)
        {
            if (openMedicalKitButton.transform.IsChildOf(p) &&
                IsValidMainInventoryRoot(p.gameObject))
                return p.gameObject;
        }
        return openMedicalKitButton.transform.parent != null
            ? openMedicalKitButton.transform.parent.gameObject : null;
    }

    private void FindReferences()
    {
        if (medicalKit == null)
            medicalKit = FindAnyObjectByType<MedicalKitInventory2D>(FindObjectsInactive.Include);
        if (mainInventoryPanel == null && transform.parent != null)
        {
            mainInventoryPanel = transform.parent.gameObject;
            Log("Main Inventory Panelが未設定のため、親Objectを仮採用: " + PathOf(mainInventoryPanel.transform));
        }
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (inventoryPanelToggle == null)
            inventoryPanelToggle = FindAnyObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);
        if (treatment == null && medicalKit != null)
            treatment = medicalKit.GetComponent<MedicalKitTreatment2D>();
    }

    private void Subscribe()
    {
        if (medicalKit != subscribedKit)
        {
            if (subscribedKit != null) subscribedKit.Changed -= HandleChanged;
            subscribedKit = medicalKit;
            if (subscribedKit != null) subscribedKit.Changed += HandleChanged;
        }
        InventoryController target = medicalKit != null ? medicalKit.PlayerInventory : null;
        if (target != subscribedInventory)
        {
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged -= HandleChanged;
            subscribedInventory = target;
            if (subscribedInventory != null) subscribedInventory.OnInventoryChanged += HandleChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedKit != null) subscribedKit.Changed -= HandleChanged;
        if (subscribedInventory != null) subscribedInventory.OnInventoryChanged -= HandleChanged;
        subscribedKit = null;
        subscribedInventory = null;
    }

    /// <summary>通常Tab Inventoryのボタンから開いたときは常にプレイヤー治療。</summary>
    public void OpenForPlayer()
    {
        npcTarget = null;
        npcMode = false;
        openedFromNpc = false;
        OpenPanelInternal();
    }

    /// <summary>
    /// NPCの近くでTを押したら通常Inventoryだけを開き、応急キットPanelは閉じておく。
    /// その後「応急キット」ボタンが押された時に、保存したNPCを治療対象として開く。
    /// Tab Inventoryが閉じている場合は必要なUI階層を有効化する。
    /// </summary>
    public bool OpenForNpc(RescuePersonSymptoms2D npc)
    {
        CancelNpcTreatment("別のNPCへ治療対象を変更したため処置中断");
        if (npc == null || npc.IsDead)
        {
            Warn("[NPC OPEN失敗] 対象NPCがNULLか死亡しています。");
            return false;
        }

        FindReferences();
        RepairPanelReferences("OpenForNpc");
        if (inventoryPanelToggle == null)
            inventoryPanelToggle = FindAnyObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);

        bool mainWasActive = mainInventoryPanel != null && mainInventoryPanel.activeInHierarchy;
        bool controllerWasActive = gameObject.activeInHierarchy && enabled;
        Log("[NPC OPEN開始] NPC=" + npc.name +
            " / MainWasActive=" + mainWasActive +
            " / ControllerWasActive=" + controllerWasActive +
            " / " + GetSummary());

        // Tabで開く実物PanelがmainInventoryPanelの子でない場合もあるので、
        // MainWasActiveに関係なく開閉管理へ依頼する（OpenInventory側で多重OPEN防止）。
        // 通常のTab用OpenClipが再生される場合は補助音を重ねない。
        bool tabSoundPlayed = false;
        bool actualTabWasVisible = IsTabInventoryReallyVisible();
        if (inventoryPanelToggle != null)
        {
            GameObject tabPanel = GetTabInventoryPanel();
            bool tabPanelWasClosed = tabPanel != null && !tabPanel.activeSelf;
            bool tabPanelWasVisible = tabPanel != null && tabPanel.activeInHierarchy;
            inventoryPanelToggle.OpenInventory();
            tabSoundPlayed = tabPanelWasClosed &&
                tabPanel != null && tabPanel.activeSelf &&
                DidTabInventoryPlayOpenSound();
            Log("[NPC OPEN] InventoryPanelToggle.OpenInventory()を呼びました。" +
                " TabPanelWasVisible=" + tabPanelWasVisible +
                " / SoundPlayed=" + tabSoundPlayed);
        }

        // Tab側が別のPanelを制御している場合は親がInactiveのままになる。
        // 実際のMedicalKitUIControllerに通じる階層を一時的に有効化する。
        EnsureNpcUiHierarchyActive();

        if (!gameObject.activeInHierarchy || !enabled)
        {
            Warn("[NPC OPEN失敗] UIControllerが非アクティブです。" +
                 " Inactive=" + GetInactiveHierarchyReport(transform) +
                 " / " + GetSummary());
            RevertNpcOpenFailure(mainWasActive);
            return false;
        }

        if (mainInventoryPanel != null && !mainInventoryPanel.activeInHierarchy)
        {
            Warn("[NPC OPEN失敗] Main Inventory PanelがInactiveです。" +
                 " Inactive=" + GetInactiveHierarchyReport(mainInventoryPanel.transform) +
                 " / " + GetSummary());
            RevertNpcOpenFailure(mainWasActive);
            return false;
        }

        if (panelRoot == null || !IsSafePanelRoot(panelRoot) || medicalKit == null ||
            openMedicalKitButton == null || !openMedicalKitButton.gameObject.activeInHierarchy)
        {
            Warn("[NPC INVENTORY OPEN失敗] 応急キットボタン/PanelRoot/MedicalKitの参照を確認してください。" +
                 " / " + GetSummary());
            RevertNpcOpenFailure(mainWasActive);
            return false;
        }

        // Tキー時点ではNPCを記録するだけ。KitPanelは表示しない。
        npcTarget = npc;
        npcMode = true;
        openedFromNpc = !mainWasActive;
        ClosePanelInternal("NPCのTキー：Inventoryだけを開き、応急キットは閉じて開始");

        if (panelRoot.activeSelf)
        {
            Warn("[NPC INVENTORY OPEN失敗] PanelRootを閉じられませんでした。" + GetSummary());
            ResetNpcSessionState();
            RevertNpcOpenFailure(mainWasActive);
            return false;
        }

        RefreshButtonState();
        // 開いた時だけ1回。OpenInventoryが音を再生した時は二重再生しない。
        if (!actualTabWasVisible && playNpcInventoryOpenSound && !tabSoundPlayed)
            PlayNpcInventoryOpenFallback();
        Log("[NPC INVENTORY OPEN成功] NPC=" + npc.name +
            " / KitPanel=false（応急キットボタンを押すまで非表示） / " + GetSummary());
        return true;
    }

    // InventoryPanelToggleを変更せず、既存の音設定を参照するためだけに使用。
    private static T ReadTabField<T>(InventoryPanelToggle toggle, string name) where T : class
    {
        if (toggle == null) return null;
        FieldInfo field = typeof(InventoryPanelToggle).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? field.GetValue(toggle) as T : null;
    }

    private GameObject GetTabInventoryPanel()
    {
        return ReadTabField<GameObject>(inventoryPanelToggle, "inventoryPanel");
    }

    private bool IsTabInventoryReallyVisible()
    {
        GameObject tabPanel = GetTabInventoryPanel();
        return tabPanel != null ? tabPanel.activeInHierarchy :
               (mainInventoryPanel != null && mainInventoryPanel.activeInHierarchy);
    }

    private bool DidTabInventoryPlayOpenSound()
    {
        AudioClip clip = ReadTabField<AudioClip>(inventoryPanelToggle, "openClip");
        AudioSource source = ReadTabField<AudioSource>(inventoryPanelToggle, "audioSource");
        // OpenVolumeが0ならクリップが設定済みでも無音なので再生済み扱いしない。
        FieldInfo volumeField = typeof(InventoryPanelToggle).GetField(
            "openVolume", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        float openVolume = volumeField != null && volumeField.GetValue(inventoryPanelToggle) is float
            ? (float)volumeField.GetValue(inventoryPanelToggle) : 0.8f;
        return clip != null && source != null && source.isActiveAndEnabled &&
               source.gameObject.activeInHierarchy && !source.mute && source.volume > 0f &&
               openVolume > 0f;
    }

    private void PlayNpcInventoryOpenFallback()
    {
        SetupMedicalKitAudio();
        AudioClip clip = npcInventoryOpenFallbackSound;
        if (clip == null)
            clip = ReadTabField<AudioClip>(inventoryPanelToggle, "openClip");
        if (clip == null)
            clip = kitOpenSound; // 同梱Resources/MedicalKitSFX/kit_open

        if (clip == null)
        {
            Warn("[NPC INVENTORY SOUND] 再生用Clipがありません。" +
                 " InventoryPanelToggleのOpen ClipかMedical KitのKit Open Soundを設定してください。");
            return;
        }
        PlayMedicalKitSound(clip, "NPC INVENTORY OPEN (fallback)");
        Log("[NPC INVENTORY SOUND] TキーでInventoryを開く補助音を再生。Clip=" + clip.name);
    }

    private void EnsureNpcUiHierarchyActive()
    {
        // 最上位から順に有効化することで、Awake/OnEnableの実行順が安定する。
        ActivateInactiveAncestorsForNpc(transform);
        if (mainInventoryPanel != null)
            ActivateInactiveAncestorsForNpc(mainInventoryPanel.transform);
        // PanelRoot自身はOpenPanelInternalが有効化するので、その親だけを確認。
        if (panelRoot != null && panelRoot.transform.parent != null)
            ActivateInactiveAncestorsForNpc(panelRoot.transform.parent);

        if (!enabled)
        {
            npcTemporarilyEnabledComponent = true;
            enabled = true;
            Warn("[NPC OPEN自動補正] MedicalKitInventoryUI2Dがdisabledだったため、一時的にenabledへ変更しました。");
        }
    }

    private void ActivateInactiveAncestorsForNpc(Transform target)
    {
        if (target == null) return;
        List<Transform> chain = new List<Transform>();
        for (Transform t = target; t != null; t = t.parent)
            chain.Add(t);

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            GameObject go = chain[i].gameObject;
            if (go.activeSelf) continue;
            // Canvas、Inventorys、Controller等のうち、実際にInactiveだった物だけ記録。
            npcTemporarilyEnabledObjects.Add(go);
            Warn("[NPC OPEN自動補正] 非表示だった親を一時的に有効化：" + PathOf(go.transform));
            go.SetActive(true);
        }
    }

    private void RevertNpcOpenFailure(bool mainWasActive)
    {
        if (!mainWasActive)
        {
            if (inventoryPanelToggle != null) inventoryPanelToggle.CloseInventory();
            else if (mainInventoryPanel != null) mainInventoryPanel.SetActive(false);
        }
        RestoreNpcTemporaryObjects();
    }

    private void RestoreNpcTemporaryObjects()
    {
        if (npcTemporarilyEnabledComponent)
        {
            npcTemporarilyEnabledComponent = false;
            enabled = false;
        }

        // 子から親の順に元のInactive状態へ戻す。
        for (int i = npcTemporarilyEnabledObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = npcTemporarilyEnabledObjects[i];
            if (go == null || !go.activeSelf) continue;
            Log("[NPC CLOSE復元] 元々InactiveだったObjectを戻します：" + PathOf(go.transform));
            go.SetActive(false);
        }
        npcTemporarilyEnabledObjects.Clear();
    }

    private static string GetInactiveHierarchyReport(Transform target)
    {
        if (target == null) return "NULL";
        List<string> inactive = new List<string>();
        for (Transform t = target; t != null; t = t.parent)
            if (!t.gameObject.activeSelf)
                inactive.Add(PathOf(t));
        return inactive.Count == 0 ? "(activeSelf=falseの祖先なし)" :
            string.Join(" | ", inactive.ToArray());
    }

    [ContextMenu("診断：NPC用UI階層のActive状態をConsole表示")]
    public void DiagnoseNpcUiHierarchy()
    {
        FindReferences();
        Debug.Log("[MedicalKitUI][NPC階層診断] Controller=" +
                  GetInactiveHierarchyReport(transform) +
                  " / Main=" + (mainInventoryPanel != null
                      ? GetInactiveHierarchyReport(mainInventoryPanel.transform) : "NULL") +
                  " / Panel=" + (panelRoot != null
                      ? GetInactiveHierarchyReport(panelRoot.transform) : "NULL") +
                  " / " + GetSummary(), this);
    }

    /// <summary>明示的にプレイヤー用として開く従来API。</summary>
    public void OpenPanel()
    {
        OpenForPlayer();
    }

    /// <summary>
    /// 通常Inventoryの「応急キット」ボタンから開閉する。
    /// Tキーで開いたInventory内ではNPCモードを維持し、勝手にPlayerへ戻さない。
    /// </summary>
    public void TogglePanel()
    {
        RepairPanelReferences("TogglePanel");
        Log("[CLICK] 応急キットのOpenButtonが押されました。 " + GetSummary());
        if (panelRoot == null) { Warn("TogglePanel失敗：Panel Rootが未設定です。"); return; }
        if (panelRoot.activeSelf)
        {
            ClosePanel();
            return;
        }

        if (npcMode)
        {
            if (npcTarget == null || npcTarget.IsDead)
            {
                Warn("[NPC KIT OPEN中止] 治療対象のNPCが存在しないか死亡しています。");
                return;
            }
            Log("[NPC KIT OPEN] 対象NPC=" + npcTarget.name);
            OpenPanelInternal();
        }
        else
        {
            OpenForPlayer();
        }
    }

    private void OpenPanelInternal()
    {
        Log("[OPEN] OpenPanel開始。 " + GetSummary());
        FindReferences();
        RepairPanelReferences("OpenPanel");
        Subscribe();
        RefreshButtonState();

        if (mainInventoryPanel != null && !mainInventoryPanel.activeInHierarchy)
        {
            Warn("OpenPanel中止：Main Inventory Panelが非表示です。割り当てが誤っている可能性があります。 " + PathOf(mainInventoryPanel.transform));
            return;
        }
        if (medicalKit == null)
        {
            Warn("OpenPanel中止：Medical Kitの参照がありません。");
            SetMessage("応急キット管理が見つかりません");
            return;
        }
        if (panelRoot == null || playerItemsRoot == null || kitSlotsRoot == null)
        {
            Warn("OpenPanel中止：Panel Root / Player Items Root / Kit Slots Rootのいずれかが未設定です。");
            return;
        }
        if (!IsSafePanelRoot(panelRoot))
        {
            Warn("OpenPanel中止：Panel RootがMedicalKitPanel全体ではありません。" +
                 "CloseButton・PlayerItemsRoot・KitSlotsRootを含むオブジェクトを指定してください。");
            return;
        }

        bool wasAlreadyOpen = panelRoot.activeInHierarchy;
        panelRoot.SetActive(true);
        EnsureTreatmentAndMenu();
        string targetLabel = IsTreatingNpc ? "使用対象：" + npcTarget.name : "使用対象：プレイヤー";
        if (treatmentTargetText != null) treatmentTargetText.text = targetLabel;
        else SetMessage(targetLabel);
        // UIの描画順を前面へ（PanelがUIControllerの子の場合も対応）。
        transform.SetAsLastSibling();
        panelRoot.transform.SetAsLastSibling();
        Log("[OPEN] PanelRootを有効化しました。 " + GetSummary());
        RefreshUI();
        if (!wasAlreadyOpen && panelRoot.activeInHierarchy)
            PlayMedicalKitSound(kitOpenSound, "OPEN");
    }

    public void ClosePanel()
    {
        RepairPanelReferences("ClosePanel");
        Log("[CLICK/CLOSE] ClosePanelが呼ばれました。 " + GetSummary());
        bool wasOpen = panelRoot != null && panelRoot.activeInHierarchy;
        ClosePanelInternal("CloseButton/TogglePanelからの閉鎖");
        if (wasOpen && panelRoot != null && !panelRoot.activeSelf)
            PlayMedicalKitSound(kitCloseSound, "CLOSE");
        // CloseButtonは応急キットだけを閉じる。Inventory本体は開いたまま。
        // Tキーで選択したNPC対象も維持し、もう一度ボタンを押せば同じNPCを治療する。
        Log("[CLOSE] 応急キットのみ閉じました。NPCモード=" + npcMode +
            " / Main=" + Describe(mainInventoryPanel));
    }

    private void ResetNpcSessionState()
    {
        npcTarget = null;
        npcMode = false;
        openedFromNpc = false;
    }

    private void ClosePanelInternal(string reason)
    {
        CancelNpcTreatment("応急キットを閉じたため処置中断: " + reason);
        // 起動時の誤設定でも、子のItemsRootだけを消さないよう安全確認する。
        if (panelRoot != null && !IsSafePanelRoot(panelRoot))
        {
            Warn("[CLOSE中止] Panel Rootがパネル全体を指していません。" +
                " CloseButton・PlayerItemsRoot・KitSlotsRootをすべて含むMedicalKitPanelを指定してください。" +
                " 現在値=" + Describe(panelRoot));
            return;
        }
        if (panelRoot == null)
        {
            if (logEveryOpenClose) Log("[CLOSE] Panel Root未設定のため閉じられません。理由=" + reason);
            return;
        }
        if (WouldDisableController(panelRoot))
        {
            if (!warnedHierarchy)
            {
                warnedHierarchy = true;
                Warn("[重大] Panel RootがUIController自身、またはその親を指しています。SetActive(false)するとスクリプトも停止するので閉じません。 " + PathOf(panelRoot.transform));
            }
            return;
        }
        if (contextMenu != null) contextMenu.Hide();
        bool wasActive = panelRoot.activeSelf;
        if (wasActive) panelRoot.SetActive(false);
        if (logEveryOpenClose && wasActive)
            Log("[CLOSE] PanelRootを非表示にしました。理由=" + reason + " / Panel=" + PathOf(panelRoot.transform));
    }

    private bool WouldDisableController(GameObject target)
    {
        return target != null && (target == gameObject || transform.IsChildOf(target.transform));
    }

    public string GetSummary()
    {
        return $"Controller={name}(activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy}, enabled={enabled}) / " +
               $"Main={Describe(mainInventoryPanel)} / Panel={Describe(panelRoot)} / " +
               $"Open={DescribeButton(openMedicalKitButton)} / Close={DescribeButton(closeMedicalKitButton)} / " +
               $"Kit={(medicalKit == null ? "NULL" : medicalKit.name)} / " +
               $"Mode=Fixed3_1x2(Grid=3x2,ButtonOnly・所持判定なし) / " +
               $"PlayerItemsRoot={(playerItemsRoot == null ? "NULL" : playerItemsRoot.name)} / " +
               $"KitSlotsRoot={(kitSlotsRoot == null ? "NULL" : kitSlotsRoot.name)}";
    }

    [ContextMenu("診断：UIの参照と設定をConsoleに出す")]
    public void DiagnosticReport()
    {
        FindReferences();
        ReportConfiguration("Inspector診断メニュー");
    }

    [ContextMenu("TEST：応急キットPanelを強制的に閉じる")]
    private void DiagnosticForceClose()
    {
        ClosePanel();
    }

    [ContextMenu("TEST：応急キットPanelを開く（所持判定なし）")]
    private void DiagnosticOpen()
    {
        OpenPanel();
    }

    private void ReportConfiguration(string phase)
    {
        Debug.Log("[MedicalKitUI][診断] " + phase + " | " + GetSummary(), this);
        if (panelRoot == null) Warn("Panel RootがNULLです。MedicalKitPanelを設定してください。");
        else if (!IsSafePanelRoot(panelRoot)) Warn("【参照ミス】Panel Rootがパネル全体ではありません。PlayerMedicalItemsRootではなくMedicalKitPanelを選択してください。");
        if (!IsValidMainInventoryRoot(mainInventoryPanel))
            Warn("【参照ミス】Main Inventory Panelの参照が誤っています。通常Inventoryの親（例：Canvas/Inventorys）を選択してください。");
        if (openMedicalKitButton == null) Warn("OpenButtonがNULLです。OpenMedicalKitButtonを設定してください。");
        else if (!openMedicalKitButton.gameObject.activeInHierarchy) Warn("OpenButtonが非表示です（親の非Activeも含む）。");
        if (closeMedicalKitButton == null) Warn("CloseButtonがNULLです。MedicalKitPanel配下のCloseButtonを設定してください。");
        if (openMedicalKitButton != null && panelRoot != null && openMedicalKitButton.transform.IsChildOf(panelRoot.transform))
            Warn("OpenButtonがMedicalKitPanelの子になっています。OpenButtonはPanelの外、通常InventoryPanelの中に置いてください。");
        if (medicalKit != null && medicalKit.PlayerInventory == null)
            Warn("MedicalKitInventory2DのPlayer Inventoryが未設定です。PlayerのInventoryControllerを割り当ててください。");
        if (medicalKit == null) Warn("MedicalKitInventory2Dが見つかりません。Playerにコンポーネントがあるか確認してください（キット本体ItemDataは不要です）。");
        if (playerItemsRoot == null) Warn("Player Items Rootが未設定です。");
        if (kitSlotsRoot == null) Warn("Kit Slots Rootが未設定です。");
        if (EventSystem.current == null) Warn("EventSystemが存在しません。UIのクリックが届かない原因になります。");
        if (rootCanvas == null) Warn("Canvasが見つかりません。UIControllerの配置場所を確認してください。");
        else if (rootCanvas.GetComponent<GraphicRaycaster>() == null) Warn("Root CanvasにGraphicRaycasterがありません。UIクリック不可の原因になります。");
    }

    private static string Describe(GameObject go)
    {
        return go == null ? "NULL" : PathOf(go.transform) + $"(self={go.activeSelf},hierarchy={go.activeInHierarchy})";
    }

    private static string DescribeButton(Button button)
    {
        return button == null ? "NULL" : PathOf(button.transform) +
            $"(self={button.gameObject.activeSelf},hierarchy={button.gameObject.activeInHierarchy},enabled={button.enabled},interactable={button.interactable},raycast={ButtonRaycast(button)})";
    }

    private static bool ButtonRaycast(Button button)
    {
        Graphic g = button == null ? null : button.GetComponent<Graphic>();
        return g != null && g.raycastTarget;
    }

    private static string PathOf(Transform tr)
    {
        if (tr == null) return "NULL";
        string path = tr.name;
        while (tr.parent != null) { tr = tr.parent; path = tr.name + "/" + path; }
        return path;
    }

    private void Log(string message)
    {
        if (showDiagnosticLogs) Debug.Log("[MedicalKitUI] " + message, this);
    }

    private void Warn(string message)
    {
        Debug.LogWarning("[MedicalKitUI][警告] " + message, this);
    }

    private void HandleChanged()
    {
        Log("収納または所持アイテム変更イベントを受信しました。");
        RefreshButtonState();
        if (panelRoot == null || !panelRoot.activeSelf) return;
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (medicalKit == null || playerItemsRoot == null || kitSlotsRoot == null)
        {
            Warn("RefreshUI失敗：MedicalKit/PlayerItemsRoot/KitSlotsRootの参照を確認してください。");
            return;
        }

        Log($"RefreshUI開始：グリッド={medicalKit.Width}x{medicalKit.Height}、固定アイテム={medicalKit.FixedItemWidth}x{medicalKit.FixedItemHeight}、3枠の数量を更新します。");
        SetupLayouts();
        ClearChildren(playerItemsRoot);
        ClearChildren(kitSlotsRoot);

        InventoryController main = medicalKit.PlayerInventory;
        int count = 0;
        if (main != null && main.Grid != null)
        {
            List<InventoryItem> items = new List<InventoryItem>(main.Grid.Items);
            foreach (InventoryItem item in items)
            {
                if (item == null || item.ItemData == null || !medicalKit.IsAllowed(item.ItemData)) continue;
                InventoryItem capture = item;
                CreateTile(playerItemsRoot, capture.ItemData.Icon,
                    capture.ItemData.DisplayName + "  x" + capture.Amount,
                    () => PutItem(capture), true, itemColor, capture);
                count++;
            }
        }
        if (count == 0)
            CreateTile(playerItemsRoot, null, "補充できる医療用品がありません",
                null, true, emptyColor, null);

        // v1.6: 横3×縦2の背景セルを6枚生成し、
        // その上に横1×縦2の固定アイテムを3枚重ねる。
        // 背景セルとアイテムは別オブジェクトなのでGridLayoutGroupは使わない。
        for (int row = 0; row < medicalKit.Height; row++)
        {
            for (int column = 0; column < medicalKit.Width; column++)
            {
                CreateBackgroundCell(column, row);
            }
        }

        for (int i = 0; i < medicalKit.SlotCount; i++)
        {
            ConsumableItemData data = medicalKit.GetItemAtSlot(i);
            int amount = medicalKit.GetAmountAtSlot(i);
            CreateFixedSlotTile(i, data, amount);
        }
    }

    private void CreateBackgroundCell(int column, int row)
    {
        GameObject cell = new GameObject($"Background_Cell_{column}_{row}",
            typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(kitSlotsRoot, false);

        RectTransform rect = cell.GetComponent<RectTransform>();
        PositionAtGrid(rect, column, row, 1, 1);
        Image bg = cell.GetComponent<Image>();
        bg.color = emptyColor;
        bg.raycastTarget = false;
    }

    /// <summary>
    /// column=0,1,2 で、横1セル・縦2セルのアイテムを絶対配置します。
    /// アイテム画像と0～3の数量は1つの縦長Buttonに収まります。
    /// </summary>
    private void CreateFixedSlotTile(int column, ConsumableItemData data, int amount)
    {
        GameObject cell = new GameObject("FixedSlot_" + column,
            typeof(RectTransform), typeof(Image), typeof(Button));
        cell.transform.SetParent(kitSlotsRoot, false);
        RectTransform rect = cell.GetComponent<RectTransform>();
        PositionAtGrid(rect, column, 0, 1, 2);

        Image bg = cell.GetComponent<Image>();
        bg.color = data != null && amount > 0 ? itemColor : emptyColor;
        bg.raycastTarget = true;

        Outline border = cell.AddComponent<Outline>();
        border.effectColor = new Color(0.6f, 0.7f, 0.8f, 0.35f);
        border.effectDistance = new Vector2(1f, -1f);
        border.useGraphicAlpha = false;

        Button button = cell.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.interactable = data != null;
        if (data != null)
        {
            ConsumableItemData capturedData = data;
            button.onClick.AddListener(() => ShowFixedSlotMenu(capturedData));
        }

        // 上部：固定アイテム名
        CreateSlotLabel(cell.transform, "ItemName",
            data != null ? data.DisplayName : "未設定",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -3f),
            new Vector2(-6f, 20f), 11f, TextAlignmentOptions.Top,
            Color.white);

        // 中央：Icon。数量が0でも必ず表示。
        if (data != null && data.Icon != null)
        {
            GameObject iconGo = new GameObject("FixedIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cell.transform, false);
            RectTransform ir = iconGo.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(6f, 27f);
            ir.offsetMax = new Vector2(-6f, -24f);
            Image image = iconGo.GetComponent<Image>();
            image.sprite = data.Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // 下部：0、1、2、3の在庫数。0でもImageと枠を消しません。
        CreateSlotLabel(cell.transform, "Amount",
            amount.ToString(),
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(-5f, 2f),
            new Vector2(-10f, 26f), 20f, TextAlignmentOptions.BottomRight,
            Color.white);
    }

    private void PositionAtGrid(RectTransform rect, int column, int row,
        int spanWidth, int spanHeight)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(
            column * (slotSize + slotSpacing),
            -row * (slotSize + slotSpacing));
        rect.sizeDelta = new Vector2(
            slotSize * spanWidth + slotSpacing * (spanWidth - 1),
            slotSize * spanHeight + slotSpacing * (spanHeight - 1));
    }

    private TextMeshProUGUI CreateSlotLabel(Transform parent, string labelName,
        string content, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (japaneseFont != null) tmp.font = japaneseFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void PutItem(InventoryItem item)
    {
        if (npcTreatmentProgress != null && npcTreatmentProgress.IsTreating)
        {
            SetMessage("治療中は医療用品を補充できません");
            return;
        }
        Log("[CLICK] 所持医療用品が押されました：" + (item?.ItemData != null ? item.ItemData.DisplayName : "NULL"));
        if (medicalKit.TryPut(item, out string result))
        {
            SetMessage(result);
            PlayMedicalKitSound(putSound, "PUT");
        }
        else
        {
            Warn("収納に失敗しました：" + result);
            SetMessage(result);
            PlayMedicalKitSound(failedSound, "PUT FAILED");
        }
    }

    private void EnsureTreatmentAndMenu()
    {
        if (medicalKit != null && treatment == null)
        {
            treatment = medicalKit.GetComponent<MedicalKitTreatment2D>();
            if (treatment == null)
            {
                treatment = medicalKit.gameObject.AddComponent<MedicalKitTreatment2D>();
                Log("MedicalKitTreatment2DをPlayerへ自動追加しました。");
            }
        }
        if (contextMenu == null)
        {
            contextMenu = GetComponent<MedicalKitItemContextMenu2D>();
            if (contextMenu == null) contextMenu = gameObject.AddComponent<MedicalKitItemContextMenu2D>();
        }
        if (contextMenu != null && panelRoot != null)
            contextMenu.Configure(panelRoot.transform, japaneseFont);
        if (npcTreatmentProgress == null)
        {
            npcTreatmentProgress = GetComponent<MedicalKitNPCTreatmentProgress2D>();
            if (npcTreatmentProgress == null)
                npcTreatmentProgress = gameObject.AddComponent<MedicalKitNPCTreatmentProgress2D>();
        }
        if (npcTreatmentProgress != null)
            npcTreatmentProgress.Configure(this, panelRoot, japaneseFont);
        ApplyJapaneseFont();
    }

    private void CancelNpcTreatment(string reason)
    {
        if (npcTreatmentProgress != null && npcTreatmentProgress.IsTreating)
            npcTreatmentProgress.Cancel(reason);
    }

    private void HandleNpcTreatmentFinished(bool success, bool cancelled, string result,
        ConsumableItemData data)
    {
        SetMessage(result);
        if (cancelled)
        {
            Log("[NPC治療中断] " + result + "（医療用品は未消費）");
        }
        else if (!success)
        {
            Warn("[NPC治療失敗] " + result);
            PlayMedicalKitSound(failedSound, "NPC TREATMENT FAILED");
        }
        else
        {
            Log("[NPC治療成功] " + result);
            if (data == null || data.UseSound == null || playSuccessUiSoundWithItemSound)
                PlayMedicalKitSound(treatmentSuccessSound, "NPC TREATMENT SUCCESS");
        }
        if (panelRoot != null && panelRoot.activeInHierarchy)
            RefreshUI();
    }

    private void ShowFixedSlotMenu(ConsumableItemData data)
    {
        if (npcTreatmentProgress != null && npcTreatmentProgress.IsTreating)
        {
            SetMessage("治療中です。3秒間その場で待ってください");
            return;
        }
        if (data == null || medicalKit == null) return;
        EnsureTreatmentAndMenu();
        int count = medicalKit.GetAmount(data);
        Log($"[MENU] {data.DisplayName} x{count} / 対象={(IsTreatingNpc ? (npcTarget != null ? npcTarget.name : "NPC不明") : "Player")}");
        if (contextMenu != null)
        {
            bool canUse = count > 0;
            string cannotUseReason = "";
            if (IsTreatingNpc)
            {
                canUse = treatment != null && npcTarget != null &&
                    treatment.CanTreat(medicalKit, data, npcTarget, out cannotUseReason);
                if (!canUse && !string.IsNullOrEmpty(cannotUseReason))
                    SetMessage(cannotUseReason);
            }
            // 非対応の「使用」だけ無効化。取り出す・情報は使えるまま。
            contextMenu.Show(data, count, Input.mousePosition,
                () => UseFixedItem(data),
                () => TakeFixedItem(data),
                () => SetMessage(data.Description),
                canUse);
            PlayMedicalKitSound(selectSound, "SELECT");
        }
    }

    private void UseFixedItem(ConsumableItemData data)
    {
        if (medicalKit == null || data == null) return;
        EnsureTreatmentAndMenu();
        if (treatment == null)
        {
            SetMessage("回復処理が見つかりません");
            Warn("MedicalKitTreatment2Dの取得に失敗");
            PlayMedicalKitSound(failedSound, "USE FAILED");
            return;
        }
        if (IsTreatingNpc && (npcTarget == null || npcTarget.IsDead))
        {
            SetMessage("NPCが見つかりません");
            PlayMedicalKitSound(failedSound, "USE FAILED");
            return;
        }
        // NPCは開始時に消費しない。3秒完了して初めてTryUseで効果/消費を適用。
        if (IsTreatingNpc)
        {
            if (npcTreatmentProgress == null)
            {
                Warn("NPC治療ゲージがありません。UIControllerのProgressコンポーネントを確認してください。");
                SetMessage("NPC治療ゲージが見つかりません");
                PlayMedicalKitSound(failedSound, "NPC TREATMENT MISSING");
                return;
            }
            if (!npcTreatmentProgress.Begin(medicalKit, treatment, npcTarget,
                    data, HandleNpcTreatmentFinished, out string startMessage))
            {
                SetMessage(startMessage);
                Warn("[NPC治療開始失敗] " + startMessage);
                PlayMedicalKitSound(failedSound, "NPC TREATMENT START FAILED");
            }
            else
            {
                SetMessage(startMessage);
                Log("[NPC治療開始] " + startMessage);
            }
            return;
        }
        bool success = treatment.TryUse(medicalKit, data,
            null, out string result);
        SetMessage(result);
        if (!success)
        {
            Warn("[使用失敗] " + result);
            PlayMedicalKitSound(failedSound, "USE FAILED");
        }
        else
        {
            Log("[使用成功] " + result);
            // MedicalKitTreatment2D が ConsumableItemData.UseSound を鳴らすため、二重再生を防ぐ。
            if (data.UseSound == null || playSuccessUiSoundWithItemSound)
                PlayMedicalKitSound(treatmentSuccessSound, "USE SUCCESS");
        }
    }

    private void TakeFixedItem(ConsumableItemData data)
    {
        if (npcTreatmentProgress != null && npcTreatmentProgress.IsTreating)
        {
            SetMessage("治療中はアイテムを取り出せません");
            return;
        }
        Log("[CLICK] 固定枠が押されました：" + (data != null ? data.DisplayName : "NULL"));
        if (medicalKit.TryTake(data, out string result))
        {
            SetMessage(result);
            PlayMedicalKitSound(takeSound, "TAKE");
        }
        else
        {
            Warn("取り出しに失敗しました：" + result);
            SetMessage(result);
            PlayMedicalKitSound(failedSound, "TAKE FAILED");
        }
    }

    /// <summary>
    /// InspectorのAudioClipが未設定の場合は、ZIPに同梱したResources内のWAVを使用。
    /// 既存ゲームのUI音と混ざらないよう、AudioSourceは2Dに固定します。
    /// </summary>
    private void SetupMedicalKitAudio()
    {
        if (medicalKitAudioSource == null)
        {
            medicalKitAudioSource = GetComponent<AudioSource>();
            if (medicalKitAudioSource == null)
                medicalKitAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (medicalKitAudioSource != null)
        {
            medicalKitAudioSource.playOnAwake = false;
            medicalKitAudioSource.spatialBlend = 0f;
        }
        else if (!warnedMissingAudioSource && logSoundDiagnostics)
        {
            warnedMissingAudioSource = true;
            Warn("[SOUND] AudioSourceを取得できません。MedicalKitUIControllerにAudioSourceを付けてください。");
        }

        if (kitOpenSound == null)
            kitOpenSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_open");
        if (kitCloseSound == null)
            kitCloseSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_close");
        if (selectSound == null)
            selectSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_select");
        if (putSound == null)
            putSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_put");
        if (takeSound == null)
            takeSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_take");
        if (treatmentSuccessSound == null)
            treatmentSuccessSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_use");
        if (failedSound == null)
            failedSound = Resources.Load<AudioClip>("MedicalKitSFX/kit_failed");

        if (!warnedMissingAudioClips && logSoundDiagnostics &&
            (kitOpenSound == null || kitCloseSound == null))
        {
            warnedMissingAudioClips = true;
            Warn("[SOUND] 応急キット開閉音が未設定です。ZIPのResources/MedicalKitSFXフォルダをAssets/Resources/MedicalKitSFXに入れるか、InspectorへAudioClipを割り当ててください。");
        }
    }

    private void PlayMedicalKitSound(AudioClip clip, string eventName)
    {
        if (clip == null || medicalKitAudioSource == null)
        {
            if (logSoundDiagnostics)
                Warn("[SOUND] " + eventName + " の効果音を再生できません。" +
                    " Clip=" + (clip != null ? clip.name : "NULL") +
                    " / AudioSource=" + (medicalKitAudioSource != null ? medicalKitAudioSource.name : "NULL"));
            return;
        }
        if (!medicalKitAudioSource.isActiveAndEnabled)
        {
            if (logSoundDiagnostics)
                Warn("[SOUND] " + eventName + " のAudioSourceが非Activeです。Hierarchyを確認してください。");
            return;
        }
        medicalKitAudioSource.PlayOneShot(clip, medicalKitSoundVolume);
    }

    [ContextMenu("音テスト / 開く音")]
    private void TestKitOpenSound()
    {
        SetupMedicalKitAudio();
        PlayMedicalKitSound(kitOpenSound, "TEST OPEN");
    }

    [ContextMenu("音テスト / 閉じる音")]
    private void TestKitCloseSound()
    {
        SetupMedicalKitAudio();
        PlayMedicalKitSound(kitCloseSound, "TEST CLOSE");
    }

    [ContextMenu("音テスト / 補充音")]
    private void TestKitPutSound()
    {
        SetupMedicalKitAudio();
        PlayMedicalKitSound(putSound, "TEST PUT");
    }

    private void ApplyJapaneseFont()
    {
        if (messageText != null && japaneseFont != null && messageText.font != japaneseFont)
            messageText.font = japaneseFont;
        if (treatmentTargetText != null && japaneseFont != null && treatmentTargetText.font != japaneseFont)
            treatmentTargetText.font = japaneseFont;
        if (messageText != null && japaneseFont != null &&
            !japaneseFont.HasCharacter('応', true, false) && !warnedMissingJapaneseGlyph)
        {
            warnedMissingJapaneseGlyph = true;
            Warn("Japanese Font Assetに日本語グリフがありません。TMP Font Assetを日本語文字入りで作成するかFallbackを設定してください。");
        }
    }

    private void SetMessage(string value)
    {
        ApplyJapaneseFont();
        if (messageText != null) messageText.text = value;
        else if (!string.IsNullOrEmpty(value)) Debug.Log("[MedicalKit] " + value, this);
    }

    private void SetupLayouts()
    {
        // Player側の補充リストは従来の縦一覧を維持。
        VerticalLayoutGroup vertical = playerItemsRoot.GetComponent<VerticalLayoutGroup>();
        if (vertical == null) vertical = playerItemsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = slotSpacing;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlHeight = false;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;

        // 応急キット側：3列×2行、各品目1列×2行なのでLayoutGroupの
        // 自動配置を使用せず、各RectTransformを明示的に配置する。
        // 旧v1.5で生成されたGridLayoutGroupがある場合は干渉しないようOFF。
        GridLayoutGroup previousGrid = kitSlotsRoot.GetComponent<GridLayoutGroup>();
        if (previousGrid != null) previousGrid.enabled = false;
        HorizontalLayoutGroup horizontal = kitSlotsRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) horizontal.enabled = false;
        VerticalLayoutGroup previousVertical = kitSlotsRoot.GetComponent<VerticalLayoutGroup>();
        if (previousVertical != null) previousVertical.enabled = false;
        ContentSizeFitter fitter = kitSlotsRoot.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        kitSlotsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            medicalKit.Width * slotSize + (medicalKit.Width - 1) * slotSpacing);
        kitSlotsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            medicalKit.Height * slotSize + (medicalKit.Height - 1) * slotSpacing);
    }

    private void CreateTile(RectTransform parent, Sprite sprite, string text,
        UnityEngine.Events.UnityAction clickAction, bool listMode, Color background,
        InventoryItem hoverItem)
    {
        GameObject go = new GameObject("MedicalItemCell", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        if (listMode)
        {
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.preferredHeight = slotSize;
            element.minHeight = slotSize;
        }
        else rect.sizeDelta = new Vector2(slotSize, slotSize);
        Image back = go.GetComponent<Image>();
        back.color = background;
        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.interactable = clickAction != null;
        if (clickAction != null) button.onClick.AddListener(clickAction);

        if (sprite != null)
        {
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(go.transform, false);
            RectTransform ir = icon.GetComponent<RectTransform>();
            ir.anchorMin = listMode ? new Vector2(0f, 0f) : Vector2.zero;
            ir.anchorMax = listMode ? new Vector2(0f, 1f) : Vector2.one;
            ir.offsetMin = new Vector2(5f, 5f);
            ir.offsetMax = listMode ? new Vector2(slotSize - 5f, -5f) : new Vector2(-5f, -5f);
            Image image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        if (!string.IsNullOrEmpty(text))
        {
            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            RectTransform lr = label.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = listMode ? new Vector2(slotSize + 5f, 0f) : new Vector2(3f, 2f);
            lr.offsetMax = new Vector2(-3f, -2f);
            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            if (japaneseFont != null) tmp.font = japaneseFont;
            tmp.text = text;
            tmp.fontSize = listMode ? 17f : 12f;
            tmp.color = Color.white;
            tmp.alignment = listMode ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.BottomRight;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
        }
        if (hoverItem != null)
        {
            MedicalKitItemHover2D hover = go.AddComponent<MedicalKitItemHover2D>();
            hover.Bind(hoverItem, rootCanvas);
        }
    }

    private static void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }
}
