using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>UI events stop at choice buttons, so choosing never also advances a node.</summary>
public sealed class DialogueClickAdvance2D : MonoBehaviour, IPointerClickHandler
{
    public DialoguePresenter2D presenter;
    public TownConversationController town;
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        presenter?.Next();
        if (town != null && !town.UsesExternalNavigation) town.AdvanceConversation();
    }
}
