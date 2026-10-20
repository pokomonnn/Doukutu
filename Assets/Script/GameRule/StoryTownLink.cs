using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class StoryTownLink : MonoBehaviour
{
    public TownConversationData conversation;
    public bool butler;
    Button button;
    void Awake() {button=GetComponent<Button>();button.onClick.AddListener(Open);}
    void Update()
    {
        if(butler) button.interactable=StoryCampaignRuntime.Has("MayorIntroduced") &&
            (StoryCampaignRuntime.Has("S1M01_ClueFound")||StoryCampaignRuntime.Has("S2M01_ClueFound")||StoryCampaignRuntime.Has("S3M01_ClueFound"));
    }
    void Open()
    {
        var controller=FindFirstObjectByType<TownConversationController>();
        if(controller!=null && !controller.IsOpen) controller.OpenConversation(conversation);
    }
    void OnDestroy(){if(button!=null)button.onClick.RemoveListener(Open);}
}
