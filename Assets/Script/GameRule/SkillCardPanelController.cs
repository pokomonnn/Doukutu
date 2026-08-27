using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inventory内の「スキル」ボタンから開くスキルカード選択画面です。
/// ・所持カード一覧
/// ・常時7枠表示（未解放枠はLOCK）
/// ・カード選択 → 装備枠クリックで装備/交換
/// ・装備枠選択 → Unequipで解除
/// ・装備中カードのメリット/デメリット一覧
/// を管理します。
/// </summary>
[DisallowMultipleComponent]
public class SkillCardPanelController : MonoBehaviour
{
    [Header("パネル")]
    [SerializeField] private GameObject skillPanel;

    [Tooltip("スキル画面を開いた時に隠す、通常Inventoryの中身部分。Inventory全体の親ではなく、グリッド等の親を指定してください。")]
    [SerializeField] private GameObject inventoryMainContent;

    [Header("所持カード一覧")]
    [SerializeField] private Transform ownedCardsRoot;
    [SerializeField] private SkillCardOwnedEntryUI ownedCardEntryPrefab;

    [Header("装備枠：0～6の順に7個登録")]
    [SerializeField] private List<SkillCardSlotUI> slots =
        new List<SkillCardSlotUI>();

    [Header("選択カード詳細")]
    [SerializeField] private Image selectedCardIcon;
    [SerializeField] private TMP_Text selectedCardNameText;
    [SerializeField] private TMP_Text selectedCardDescriptionText;
    [SerializeField] private TMP_Text selectedBenefitText;
    [SerializeField] private TMP_Text selectedDrawbackText;

    [Header("装備中カードの合計説明")]
    [SerializeField] private TMP_Text equippedBenefitsText;
    [SerializeField] private TMP_Text equippedDrawbacksText;

    [Header("操作")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button unequipButton;

    [Header("表示文")]
    [SerializeField] private string noBenefitText = "なし";
    [SerializeField] private string noDrawbackText = "なし";
    [SerializeField] private string emptySelectionText = "カードを選択してください";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private readonly List<SkillCardOwnedEntryUI> spawnedEntries =
        new List<SkillCardOwnedEntryUI>();

    private GameSessionManager session;
    private SkillCardData selectedCard;

    // 所持カード一覧のSelectedMarkerだけを管理するためのカードです。
    // selectedCardとは分けておくことで、装備枠や背景をクリックした時に
    // 装備処理・詳細表示を壊さず、所持カード側の選択マーカーだけ消せます。
    private SkillCardData selectedOwnedMarkerCard;

    // 装備枠のSelectedMarkerだけを管理するためのIndexです。
    // selectedSlotIndexとは分離し、背景などをクリックした時に
    // 装備解除対象などの内部選択状態を壊さず、見た目のマーカーだけ消します。
    private int selectedSlotMarkerIndex = -1;

    private int selectedSlotIndex = -1;
    private bool subscribed;

    public bool IsOpen =>
        skillPanel != null && skillPanel.activeInHierarchy;

    private void Awake()
    {
        FindSession();

        closeButton?.onClick.AddListener(ClosePanel);
        unequipButton?.onClick.AddListener(UnequipSelectedSlot);

        ClosePanel();
    }

    private void OnEnable()
    {
        FindSession();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (!IsOpen || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        bool pointerOverOwnedCard = IsPointerOverOwnedCardEntry();
        bool pointerOverSlot = IsPointerOverSkillCardSlot();

        bool ownedMarkerChanged = false;
        bool slotMarkerChanged = false;

        // 所持カード以外をクリックしたら、所持カード側のSelectedMarkerを消す。
        if (!pointerOverOwnedCard && selectedOwnedMarkerCard != null)
        {
            selectedOwnedMarkerCard = null;
            ownedMarkerChanged = true;
        }

        // 装備枠以外をクリックしたら、装備枠側のSelectedMarkerを消す。
        // selectedSlotIndex自体は残すため、詳細表示や装備解除対象は壊れません。
        if (!pointerOverSlot && selectedSlotMarkerIndex >= 0)
        {
            selectedSlotMarkerIndex = -1;
            slotMarkerChanged = true;
        }

        if (ownedMarkerChanged)
        {
            RefreshOwnedCardSelectionMarkers();
        }

        if (slotMarkerChanged)
        {
            RefreshSlotSelectionMarkers();
        }
    }

    private void OnDestroy()
    {
        closeButton?.onClick.RemoveListener(ClosePanel);
        unequipButton?.onClick.RemoveListener(UnequipSelectedSlot);
        Unsubscribe();
    }

    public void OpenPanel()
    {
        FindSession();

        if (session == null)
        {
            Debug.LogWarning(
                "[SkillCardPanelController] GameSessionManagerが見つかりません。",
                this
            );
            return;
        }

        if (inventoryMainContent != null)
        {
            inventoryMainContent.SetActive(false);
        }

        if (skillPanel != null)
        {
            skillPanel.SetActive(true);
        }

        Subscribe();
        RefreshAll();
    }

    public void ClosePanel()
    {
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        if (inventoryMainContent != null)
        {
            inventoryMainContent.SetActive(true);
        }

        selectedCard = null;
        selectedOwnedMarkerCard = null;
        selectedSlotMarkerIndex = -1;
        selectedSlotIndex = -1;
    }

    public void TogglePanel()
    {
        if (IsOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void RefreshAll()
    {
        FindSession();

        if (session == null)
        {
            return;
        }

        RefreshSlots();
        RefreshOwnedCards();
        RefreshSelectedDetails();
        RefreshEquippedSummary();
        RefreshButtons();
    }

    private void RefreshSlots()
    {
        int unlocked = session.UnlockedSkillSlotCount;

        for (int i = 0; i < slots.Count; i++)
        {
            SkillCardSlotUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            bool isUnlocked = i < unlocked && i < GameSessionManager.MaxSkillSlots;
            SkillCardData card = isUnlocked
                ? session.GetEquippedSkillCard(i)
                : null;

            slot.Bind(
                i,
                isUnlocked,
                card,
                selectedSlotMarkerIndex == i,
                HandleSlotClicked
            );
        }
    }

    private void RefreshOwnedCards()
    {
        foreach (SkillCardOwnedEntryUI entry in spawnedEntries)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }

        spawnedEntries.Clear();

        if (ownedCardsRoot == null || ownedCardEntryPrefab == null)
        {
            return;
        }

        IReadOnlyList<SkillCardData> ownedCards = session.OwnedSkillCards;

        for (int i = 0; i < ownedCards.Count; i++)
        {
            SkillCardData card = ownedCards[i];
            if (card == null)
            {
                continue;
            }

            SkillCardOwnedEntryUI entry = Instantiate(
                ownedCardEntryPrefab,
                ownedCardsRoot
            );

            entry.gameObject.SetActive(true);
            entry.Bind(
                card,
                selectedOwnedMarkerCard == card,
                session.IsSkillCardEquipped(card),
                HandleOwnedCardClicked
            );

            spawnedEntries.Add(entry);
        }
    }

    private void HandleOwnedCardClicked(SkillCardOwnedEntryUI entry)
    {
        if (entry == null || entry.Card == null)
        {
            return;
        }

        selectedCard = entry.Card;
        selectedOwnedMarkerCard = entry.Card;

        // 所持カードを選んだ時は、装備枠の見た目の選択は解除する。
        // ただし既に装備中なら内部のslotIndexは保持し、解除ボタン等は従来どおり使える。
        selectedSlotMarkerIndex = -1;
        selectedSlotIndex = session.FindEquippedSkillCardSlot(selectedCard);
        RefreshAll();
    }

    private void HandleSlotClicked(SkillCardSlotUI slot)
    {
        if (slot == null || !slot.IsUnlocked || session == null)
        {
            return;
        }

        // 装備枠をクリックした時は、所持カード側の見た目の選択を解除し、
        // クリックした装備枠へSelectedMarkerを移します。
        selectedOwnedMarkerCard = null;
        selectedSlotMarkerIndex = slot.SlotIndex;

        if (selectedCard != null)
        {
            if (session.TryEquipSkillCard(
                    selectedCard,
                    slot.SlotIndex,
                    out string resultMessage))
            {
                selectedSlotIndex = slot.SlotIndex;
                Log(resultMessage);
            }
            else
            {
                Debug.LogWarning(
                    $"[SkillCardPanelController] {resultMessage}",
                    this
                );
            }

            RefreshAll();
            return;
        }

        selectedSlotIndex = slot.SlotIndex;
        selectedCard = slot.Card;
        RefreshAll();
    }

    public void UnequipSelectedSlot()
    {
        if (session == null || selectedSlotIndex < 0)
        {
            return;
        }

        if (session.UnequipSkillCard(selectedSlotIndex))
        {
            selectedSlotIndex = -1;
            selectedSlotMarkerIndex = -1;
            selectedCard = null;
            RefreshAll();
        }
    }

    private void RefreshSelectedDetails()
    {
        if (selectedCardIcon != null)
        {
            selectedCardIcon.sprite = selectedCard != null
                ? selectedCard.Icon
                : null;
            selectedCardIcon.enabled = selectedCardIcon.sprite != null;
        }

        SetText(
            selectedCardNameText,
            selectedCard != null
                ? selectedCard.DisplayName
                : emptySelectionText
        );

        SetText(
            selectedCardDescriptionText,
            selectedCard != null
                ? selectedCard.Description
                : string.Empty
        );

        SetText(
            selectedBenefitText,
            selectedCard != null &&
            !string.IsNullOrWhiteSpace(selectedCard.BenefitText)
                ? selectedCard.BenefitText
                : noBenefitText
        );

        SetText(
            selectedDrawbackText,
            selectedCard != null &&
            !string.IsNullOrWhiteSpace(selectedCard.DrawbackText)
                ? selectedCard.DrawbackText
                : noDrawbackText
        );
    }

    private void RefreshEquippedSummary()
    {
        StringBuilder benefits = new StringBuilder();
        StringBuilder drawbacks = new StringBuilder();

        int unlocked = session.UnlockedSkillSlotCount;

        for (int i = 0; i < unlocked; i++)
        {
            SkillCardData card = session.GetEquippedSkillCard(i);
            if (card == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(card.BenefitText))
            {
                if (benefits.Length > 0)
                {
                    benefits.AppendLine();
                }

                benefits.Append("• ")
                    .Append(card.DisplayName)
                    .Append("：")
                    .Append(card.BenefitText);
            }

            if (!string.IsNullOrWhiteSpace(card.DrawbackText))
            {
                if (drawbacks.Length > 0)
                {
                    drawbacks.AppendLine();
                }

                drawbacks.Append("• ")
                    .Append(card.DisplayName)
                    .Append("：")
                    .Append(card.DrawbackText);
            }
        }

        SetText(
            equippedBenefitsText,
            benefits.Length > 0 ? benefits.ToString() : noBenefitText
        );

        SetText(
            equippedDrawbacksText,
            drawbacks.Length > 0 ? drawbacks.ToString() : noDrawbackText
        );
    }

    private void RefreshButtons()
    {
        if (unequipButton != null)
        {
            unequipButton.interactable =
                selectedSlotIndex >= 0 &&
                session.GetEquippedSkillCard(selectedSlotIndex) != null;
        }
    }

    private bool IsPointerOverOwnedCardEntry()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(
            EventSystem.current
        )
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            SkillCardOwnedEntryUI entry =
                result.gameObject.GetComponentInParent<
                    SkillCardOwnedEntryUI
                >();

            if (entry != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshOwnedCardSelectionMarkers()
    {
        foreach (SkillCardOwnedEntryUI entry in spawnedEntries)
        {
            if (entry == null || entry.Card == null)
            {
                continue;
            }

            entry.Bind(
                entry.Card,
                selectedOwnedMarkerCard == entry.Card,
                session != null && session.IsSkillCardEquipped(entry.Card),
                HandleOwnedCardClicked
            );
        }
    }

    private bool IsPointerOverSkillCardSlot()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(
            EventSystem.current
        )
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            SkillCardSlotUI slot =
                result.gameObject.GetComponentInParent<SkillCardSlotUI>();

            if (slot != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshSlotSelectionMarkers()
    {
        if (session == null)
        {
            return;
        }

        int unlocked = session.UnlockedSkillSlotCount;

        for (int i = 0; i < slots.Count; i++)
        {
            SkillCardSlotUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            bool isUnlocked =
                i < unlocked &&
                i < GameSessionManager.MaxSkillSlots;

            SkillCardData card = isUnlocked
                ? session.GetEquippedSkillCard(i)
                : null;

            slot.Bind(
                i,
                isUnlocked,
                card,
                selectedSlotMarkerIndex == i,
                HandleSlotClicked
            );
        }
    }

    private void FindSession()
    {
        if (session == null)
        {
            session = GameSessionManager.Instance;
        }

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(
                FindObjectsInactive.Include
            );
        }
    }

    private void Subscribe()
    {
        if (subscribed || session == null)
        {
            return;
        }

        session.SkillSessionChanged += HandleSkillSessionChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || session == null)
        {
            return;
        }

        session.SkillSessionChanged -= HandleSkillSessionChanged;
        subscribed = false;
    }

    private void HandleSkillSessionChanged()
    {
        if (IsOpen)
        {
            RefreshAll();
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SkillCardPanelController] {message}", this);
        }
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null)
        {
            target.text = text ?? string.Empty;
        }
    }
}
