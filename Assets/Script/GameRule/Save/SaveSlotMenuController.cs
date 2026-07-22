using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SaveSlotMenuMode
{
    Save,
    Load
}

/// <summary>
/// 最大20個の手動セーブ枠を一覧表示します。
/// Saveモードでは空枠へ新規保存、使用済み枠へ確認後の上書きができます。
/// Loadモードでは手動セーブと、任意でオートセーブを読み込めます。
/// </summary>
[DisallowMultipleComponent]
public class SaveSlotMenuController : MonoBehaviour
{
    private enum PendingAction
    {
        None,
        Overwrite,
        Delete
    }

    [Header("動作モード")]
    [SerializeField] private SaveSlotMenuMode mode = SaveSlotMenuMode.Save;

    [Tooltip("Loadモードの一覧先頭へオートセーブを表示します。手動保存先には使用しません。")]
    [SerializeField] private bool showAutoSaveInLoadMode = true;

    [Header("参照")]
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private SaveSlotEntryUI slotEntryPrefab;

    [Header("操作ボタン")]
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionButtonText;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button closeButton;

    [Header("選択中スロットの詳細")]
    [SerializeField] private TMP_Text selectedSlotText;
    [SerializeField] private TMP_Text selectedSceneText;
    [SerializeField] private TMP_Text selectedDateText;
    [SerializeField] private TMP_Text selectedMoneyText;
    [SerializeField] private TMP_Text selectedInventoryText;
    [SerializeField] private TMP_Text selectedMissionText;
    [SerializeField] private TMP_Text statusText;

    [Header("確認パネル")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text confirmationMessageText;
    [SerializeField] private Button confirmationYesButton;
    [SerializeField] private Button confirmationNoButton;

    [Header("表示文")]
    [SerializeField] private string newSaveButtonText = "新規セーブ";
    [SerializeField] private string overwriteButtonText = "上書き";
    [SerializeField] private string loadButtonText = "ロード";
    [SerializeField] private string noSelectionText = "セーブ枠を選択してください";
    [SerializeField] private string overwriteConfirmationFormat =
        "{0}へ上書きしますか？\n以前のデータは元に戻せません。";
    [SerializeField] private string deleteConfirmationFormat =
        "{0}を削除しますか？\n削除したデータは元に戻せません。";

    [Header("動作")]
    [SerializeField] private bool wireButtonsAutomatically = true;
    [SerializeField] private bool closeAfterSuccessfulSave = false;
    [SerializeField] private bool showDebugLogs = true;

    private readonly List<SaveSlotEntryUI> entries =
        new List<SaveSlotEntryUI>();

    private SaveSlotEntryUI selectedEntry;
    private SaveSlotInfo selectedInfo;
    private PendingAction pendingAction;
    private bool operationInProgress;

    public SaveSlotMenuMode Mode => mode;
    public SaveSlotInfo SelectedInfo => selectedInfo;

    private void Awake()
    {
        FindReferences();

        if (wireButtonsAutomatically)
        {
            primaryActionButton?.onClick.AddListener(ExecutePrimaryAction);
            deleteButton?.onClick.AddListener(RequestDelete);
            closeButton?.onClick.AddListener(CloseMenu);
            confirmationYesButton?.onClick.AddListener(ConfirmPendingAction);
            confirmationNoButton?.onClick.AddListener(CancelConfirmation);
        }

        SetActive(confirmationPanel, false);
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            RefreshSlots();
        }
    }

    private void OnDestroy()
    {
        if (!wireButtonsAutomatically)
        {
            return;
        }

        primaryActionButton?.onClick.RemoveListener(ExecutePrimaryAction);
        deleteButton?.onClick.RemoveListener(RequestDelete);
        closeButton?.onClick.RemoveListener(CloseMenu);
        confirmationYesButton?.onClick.RemoveListener(ConfirmPendingAction);
        confirmationNoButton?.onClick.RemoveListener(CancelConfirmation);
    }

    public void OpenSaveMenu()
    {
        Open(SaveSlotMenuMode.Save);
    }

    public void OpenLoadMenu()
    {
        Open(SaveSlotMenuMode.Load);
    }

    public void Open(SaveSlotMenuMode requestedMode)
    {
        mode = requestedMode;

        if (!gameObject.activeSelf)
        {
            // modeを設定してから有効化することで、OnEnableの1回だけで一覧を作ります。
            gameObject.SetActive(true);
            return;
        }

        RefreshSlots();
    }

    public void CloseMenu()
    {
        if (operationInProgress)
        {
            return;
        }

        CancelConfirmation();
        gameObject.SetActive(false);
    }

    public void SetMode(SaveSlotMenuMode requestedMode)
    {
        mode = requestedMode;
        if (gameObject.activeInHierarchy)
        {
            RefreshSlots();
        }
    }

    public void RefreshSlots()
    {
        FindReferences();
        operationInProgress = false;
        pendingAction = PendingAction.None;
        SetActive(confirmationPanel, false);

        int previousSlot = selectedInfo?.SlotNumber ?? -1;
        bool previousWasAuto = selectedInfo?.IsAutoSave ?? false;

        ClearEntries();
        selectedEntry = null;
        selectedInfo = null;

        if (saveManager == null)
        {
            SetStatus("SaveManagerが見つかりません");
            RefreshActionButtons();
            return;
        }

        if (slotContainer == null || slotEntryPrefab == null)
        {
            SetStatus("Slot ContainerまたはSlot Entry Prefabが設定されていません");
            RefreshActionButtons();
            return;
        }

        if (mode == SaveSlotMenuMode.Load && showAutoSaveInLoadMode)
        {
            saveManager.TryReadAutoSaveInfo(
                out SaveSlotInfo autoInfo,
                out _
            );

            // オートセーブがまだ無い間は、空欄を一覧へ出しません。
            if (autoInfo != null && autoInfo.HasSaveData)
            {
                AddEntry(autoInfo);
            }
        }

        for (int slot = 1; slot <= SaveManager.MaxManualSlots; slot++)
        {
            saveManager.TryReadSlotInfo(
                slot,
                out SaveSlotInfo info,
                out _
            );
            AddEntry(info);
        }

        SaveSlotEntryUI preferred = FindEntry(previousSlot, previousWasAuto);

        if (preferred == null && mode == SaveSlotMenuMode.Save)
        {
            int preferredSlot = saveManager.HasCurrentManualSlot
                ? saveManager.CurrentManualSlotNumber
                : FindFirstEmptySlotNumber();
            preferred = FindEntry(preferredSlot, false);
        }

        if (preferred == null && mode == SaveSlotMenuMode.Load)
        {
            preferred = FindNewestLoadableEntry();
        }

        if (preferred == null && entries.Count > 0)
        {
            preferred = entries[0];
        }

        if (preferred != null)
        {
            SelectEntry(preferred);
        }
        else
        {
            ClearDetails();
            SetStatus(noSelectionText);
            RefreshActionButtons();
        }
    }

    public void ExecutePrimaryAction()
    {
        if (operationInProgress || selectedInfo == null)
        {
            return;
        }

        if (mode == SaveSlotMenuMode.Save)
        {
            if (selectedInfo.IsAutoSave)
            {
                SetStatus("オートセーブ枠へ手動保存はできません");
                return;
            }

            if (selectedInfo.HasSaveData)
            {
                ShowConfirmation(
                    PendingAction.Overwrite,
                    string.Format(
                        overwriteConfirmationFormat,
                        selectedInfo.SlotLabel
                    )
                );
                return;
            }

            SaveSelectedSlot();
            return;
        }

        LoadSelectedSlot();
    }

    public void RequestDelete()
    {
        if (operationInProgress ||
            selectedInfo == null ||
            !selectedInfo.HasSaveData)
        {
            return;
        }

        ShowConfirmation(
            PendingAction.Delete,
            string.Format(
                deleteConfirmationFormat,
                selectedInfo.SlotLabel
            )
        );
    }

    public void ConfirmPendingAction()
    {
        PendingAction action = pendingAction;
        CancelConfirmation();

        switch (action)
        {
            case PendingAction.Overwrite:
                SaveSelectedSlot();
                break;

            case PendingAction.Delete:
                DeleteSelectedSlot();
                break;
        }
    }

    public void CancelConfirmation()
    {
        pendingAction = PendingAction.None;
        SetActive(confirmationPanel, false);
        RefreshActionButtons();
    }

    private void SaveSelectedSlot()
    {
        if (selectedInfo == null || selectedInfo.IsAutoSave)
        {
            return;
        }

        operationInProgress = true;
        RefreshActionButtons();
        SetStatus($"{selectedInfo.SlotLabel}へ保存しています…");

        int slotNumber = selectedInfo.SlotNumber;
        bool success = saveManager.SaveSlot(slotNumber);

        operationInProgress = false;
        SetStatus(saveManager.LastOperationMessage);

        if (!success)
        {
            RefreshActionButtons();
            return;
        }

        string successMessage = saveManager.LastOperationMessage;

        if (closeAfterSuccessfulSave)
        {
            gameObject.SetActive(false);
            return;
        }

        RefreshSlots();
        SelectBySlot(slotNumber, false);
        SetStatus(successMessage);
    }

    private void LoadSelectedSlot()
    {
        if (selectedInfo == null || !selectedInfo.CanLoad)
        {
            SetStatus("このセーブデータはロードできません");
            return;
        }

        operationInProgress = true;
        RefreshActionButtons();
        SetStatus($"{selectedInfo.SlotLabel}を読み込んでいます…");

        bool success = selectedInfo.IsAutoSave
            ? saveManager.LoadAutoGame()
            : saveManager.LoadSlot(selectedInfo.SlotNumber);

        if (!success)
        {
            operationInProgress = false;
            SetStatus(saveManager.LastOperationMessage);
            RefreshActionButtons();
        }
    }

    private void DeleteSelectedSlot()
    {
        if (selectedInfo == null || !selectedInfo.HasSaveData)
        {
            return;
        }

        operationInProgress = true;
        RefreshActionButtons();

        int slotNumber = selectedInfo.SlotNumber;
        bool isAutoSave = selectedInfo.IsAutoSave;
        bool success = isAutoSave
            ? saveManager.DeleteAutoSave()
            : saveManager.DeleteSlot(slotNumber);

        operationInProgress = false;
        string resultMessage = saveManager.LastOperationMessage;

        RefreshSlots();
        if (!isAutoSave)
        {
            SelectBySlot(slotNumber, false);
        }
        SetStatus(resultMessage);
    }

    private void AddEntry(SaveSlotInfo info)
    {
        SaveSlotEntryUI entry = Instantiate(slotEntryPrefab, slotContainer);
        entry.gameObject.SetActive(true);
        entry.Bind(info, SelectEntry);
        entry.SetSelected(false);
        entries.Add(entry);
    }

    private void SelectEntry(SaveSlotEntryUI entry)
    {
        if (entry == null)
        {
            return;
        }

        selectedEntry?.SetSelected(false);
        selectedEntry = entry;
        selectedInfo = entry.SlotInfo;
        selectedEntry.SetSelected(true);

        ApplyDetails(selectedInfo);
        SetStatus(string.Empty);
        RefreshActionButtons();
    }

    private void SelectBySlot(int slotNumber, bool isAutoSave)
    {
        SaveSlotEntryUI entry = FindEntry(slotNumber, isAutoSave);
        if (entry != null)
        {
            SelectEntry(entry);
        }
    }

    private SaveSlotEntryUI FindEntry(int slotNumber, bool isAutoSave)
    {
        foreach (SaveSlotEntryUI entry in entries)
        {
            SaveSlotInfo info = entry != null ? entry.SlotInfo : null;
            if (info != null &&
                info.SlotNumber == slotNumber &&
                info.IsAutoSave == isAutoSave)
            {
                return entry;
            }
        }

        return null;
    }

    private SaveSlotEntryUI FindNewestLoadableEntry()
    {
        SaveSlotEntryUI newest = null;
        DateTime newestTime = DateTime.MinValue;

        foreach (SaveSlotEntryUI entry in entries)
        {
            SaveSlotInfo info = entry != null ? entry.SlotInfo : null;
            if (info == null || !info.CanLoad)
            {
                continue;
            }

            if (newest == null || info.FileModifiedUtc > newestTime)
            {
                newest = entry;
                newestTime = info.FileModifiedUtc;
            }
        }

        return newest;
    }

    private int FindFirstEmptySlotNumber()
    {
        foreach (SaveSlotEntryUI entry in entries)
        {
            SaveSlotInfo info = entry != null ? entry.SlotInfo : null;
            if (info != null && !info.IsAutoSave && !info.HasSaveData)
            {
                return info.SlotNumber;
            }
        }

        return 1;
    }

    private void ApplyDetails(SaveSlotInfo info)
    {
        if (info == null)
        {
            ClearDetails();
            return;
        }

        SetText(selectedSlotText, info.SlotLabel);

        if (!info.HasSaveData)
        {
            SetText(selectedSceneText, "空きスロット");
            SetText(selectedDateText, "ここへ新規保存できます");
            SetText(selectedMoneyText, string.Empty);
            SetText(selectedInventoryText, string.Empty);
            SetText(selectedMissionText, string.Empty);
            return;
        }

        SetText(
            selectedSceneText,
            $"場所：{(string.IsNullOrWhiteSpace(info.SavedSceneName) ? "不明" : info.SavedSceneName)}"
        );
        SetText(selectedDateText, FormatSavedDate(info.SavedAtUtc));
        SetText(selectedMoneyText, $"所持金：{info.Money:N0}");
        SetText(selectedInventoryText, $"所持アイテム：{info.InventoryItemCount}");
        SetText(selectedMissionText, $"ミッション：{info.MissionCount}");
    }

    private void ClearDetails()
    {
        SetText(selectedSlotText, string.Empty);
        SetText(selectedSceneText, string.Empty);
        SetText(selectedDateText, string.Empty);
        SetText(selectedMoneyText, string.Empty);
        SetText(selectedInventoryText, string.Empty);
        SetText(selectedMissionText, string.Empty);
    }

    private void RefreshActionButtons()
    {
        bool hasSelection = selectedInfo != null;
        bool primaryAvailable = hasSelection && !operationInProgress;

        if (mode == SaveSlotMenuMode.Load)
        {
            primaryAvailable &= selectedInfo != null && selectedInfo.CanLoad;
            SetText(primaryActionButtonText, loadButtonText);
        }
        else
        {
            primaryAvailable &= selectedInfo != null && !selectedInfo.IsAutoSave;
            SetText(
                primaryActionButtonText,
                selectedInfo != null && selectedInfo.HasSaveData
                    ? overwriteButtonText
                    : newSaveButtonText
            );
        }

        if (primaryActionButton != null)
        {
            primaryActionButton.interactable = primaryAvailable;
        }

        if (deleteButton != null)
        {
            deleteButton.interactable =
                hasSelection &&
                selectedInfo.HasSaveData &&
                !operationInProgress;
        }

        if (closeButton != null)
        {
            closeButton.interactable = !operationInProgress;
        }
    }

    private void ShowConfirmation(PendingAction action, string message)
    {
        pendingAction = action;
        SetText(confirmationMessageText, message);
        SetActive(confirmationPanel, true);
        RefreshActionButtons();
    }

    private void ClearEntries()
    {
        foreach (SaveSlotEntryUI entry in entries)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }

        entries.Clear();
    }

    private void FindReferences()
    {
        if (saveManager == null)
        {
            saveManager = SaveManager.Instance;
        }

        if (saveManager == null)
        {
            saveManager = FindAnyObjectByType<SaveManager>(
                FindObjectsInactive.Include
            );
        }
    }

    private void SetStatus(string message)
    {
        SetText(statusText, message ?? string.Empty);
        if (showDebugLogs && !string.IsNullOrWhiteSpace(message))
        {
            Debug.Log($"[SaveSlotMenuController] {message}", this);
        }
    }

    private static string FormatSavedDate(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
        {
            return "保存日時：不明";
        }

        if (DateTime.TryParse(
                utcText,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return $"保存日時：{parsed.ToLocalTime():yyyy/MM/dd HH:mm}";
        }

        return $"保存日時：{utcText}";
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
