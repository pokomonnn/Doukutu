using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SkillCardSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text slotNumberText;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TMP_Text lockedText;
    [SerializeField] private GameObject selectedMarker;

    public int SlotIndex { get; private set; }
    public SkillCardData Card { get; private set; }
    public bool IsUnlocked { get; private set; }

    private Action<SkillCardSlotUI> onClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button?.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        button?.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(
        int slotIndex,
        bool unlocked,
        SkillCardData card,
        bool selected,
        Action<SkillCardSlotUI> clicked)
    {
        SlotIndex = slotIndex;
        IsUnlocked = unlocked;
        Card = unlocked ? card : null;
        onClicked = clicked;

        if (slotNumberText != null)
        {
            slotNumberText.text = (slotIndex + 1).ToString();
        }

        if (lockedText != null)
        {
            lockedText.text = "LOCK";
        }

        lockedOverlay?.SetActive(!unlocked);
        selectedMarker?.SetActive(selected && unlocked);

        if (button != null)
        {
            button.interactable = unlocked;
        }

        if (iconImage != null)
        {
            iconImage.sprite = Card != null ? Card.Icon : null;
            iconImage.enabled = Card != null && iconImage.sprite != null;
        }

        if (cardNameText != null)
        {
            cardNameText.text = Card != null ? Card.DisplayName : string.Empty;
        }
    }

    private void HandleClicked()
    {
        onClicked?.Invoke(this);
    }
}
