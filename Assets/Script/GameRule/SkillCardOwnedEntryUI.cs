using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SkillCardOwnedEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text benefitText;
    [SerializeField] private TMP_Text drawbackText;
    [SerializeField] private GameObject selectedMarker;
    [SerializeField] private GameObject equippedMarker;

    public SkillCardData Card { get; private set; }

    private Action<SkillCardOwnedEntryUI> onClicked;

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
        SkillCardData card,
        bool selected,
        bool equipped,
        Action<SkillCardOwnedEntryUI> clicked)
    {
        Card = card;
        onClicked = clicked;

        if (iconImage != null)
        {
            iconImage.sprite = card != null ? card.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = card != null ? card.DisplayName : "---";
        }

        if (benefitText != null)
        {
            benefitText.text = card != null ? card.BenefitText : string.Empty;
        }

        if (drawbackText != null)
        {
            drawbackText.text = card != null ? card.DrawbackText : string.Empty;
        }

        selectedMarker?.SetActive(selected);
        equippedMarker?.SetActive(equipped);
    }

    private void HandleClicked()
    {
        onClicked?.Invoke(this);
    }
}
