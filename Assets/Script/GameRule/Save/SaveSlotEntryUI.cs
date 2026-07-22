using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// セーブスロット一覧の1行分です。
/// SaveSlotMenuControllerから実行時に内容を設定します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SaveSlotEntryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text slotNameText;
    [SerializeField] private TMP_Text sceneText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject selectedMarker;

    [Header("表示文")]
    [SerializeField] private string emptyText = "新規セーブ";
    [SerializeField] private string incompatibleText = "読み込み非対応";
    [SerializeField] private string sceneFormat = "場所：{0}";
    [SerializeField] private string moneyFormat = "所持金：{0:N0}";

    public SaveSlotInfo SlotInfo { get; private set; }

    private Action<SaveSlotEntryUI> clicked;

    private void Awake()
    {
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        selectButton?.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        selectButton?.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(
        SaveSlotInfo slotInfo,
        Action<SaveSlotEntryUI> onClicked)
    {
        SlotInfo = slotInfo;
        clicked = onClicked;

        if (slotInfo == null)
        {
            SetText(slotNameText, "不明なスロット");
            SetText(sceneText, string.Empty);
            SetText(dateText, string.Empty);
            SetText(moneyText, string.Empty);
            SetText(stateText, "読み取り失敗");
            return;
        }

        SetText(
            slotNameText,
            string.IsNullOrWhiteSpace(slotInfo.DisplayName)
                ? slotInfo.SlotLabel
                : slotInfo.DisplayName
        );

        if (!slotInfo.HasSaveData)
        {
            SetText(sceneText, string.Empty);
            SetText(dateText, string.Empty);
            SetText(moneyText, string.Empty);
            SetText(stateText, emptyText);
            return;
        }

        SetText(
            sceneText,
            string.Format(
                sceneFormat,
                string.IsNullOrWhiteSpace(slotInfo.SavedSceneName)
                    ? "不明"
                    : slotInfo.SavedSceneName
            )
        );
        SetText(dateText, FormatSavedDate(slotInfo.SavedAtUtc));
        SetText(moneyText, string.Format(moneyFormat, slotInfo.Money));
        SetText(
            stateText,
            slotInfo.IsCompatible
                ? string.Empty
                : incompatibleText
        );
    }

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }
    }

    private void HandleClicked()
    {
        clicked?.Invoke(this);
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
}
