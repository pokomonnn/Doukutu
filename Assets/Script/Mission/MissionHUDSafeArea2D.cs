using TMPro;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Measure text instead of truncating it; keep the tracker clear of the town facilities and modal panels.</summary>
[DefaultExecutionOrder(100)]
public sealed class MissionHUDSafeArea2D : MonoBehaviour
{
    public RectTransform panel;
    TMP_Text title, body, notice;
    RectTransform noticePanel;
    Canvas canvas;
    CanvasGroup visibility;
    MissionMenuUI menu;
    TownConversationController conversation;
    readonly List<Func<bool>> otherPanels = new List<Func<bool>>();
    Button[] buttons;
    string lastContent;
    Vector2 lastSize;
    Rect lastSafe;
    public bool IsObscured { get; private set; }
    void Awake()
    {
        canvas=GetComponent<Canvas>();
        visibility=GetComponent<CanvasGroup>();
        if(visibility==null)visibility=gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts=false;visibility.interactable=false;
        menu=FindObjectsByType<MissionMenuUI>(FindObjectsInactive.Include).FirstOrDefault(m=>m.gameObject.scene==gameObject.scene);
        conversation=FindFirstObjectByType<TownConversationController>();
        buttons=FindObjectsByType<Button>(FindObjectsInactive.Include);
        Watch<InventoryPanelToggle>(p=>p.IsOpen);
        Watch<ItemBoxUIController>(p=>p.IsOpen);
        Watch<TownUnifiedTradeScreen2D>(p=>p.IsOpen);
        Watch<MerchantPurchaseController>(p=>p.IsOpen);
        Watch<PawnShopUIController>(p=>p.IsOpen);
        Watch<MerchantWeaponRepairController>(p=>p.IsOpen);
        Watch<StageExpeditionPanelController>(p=>p.IsOpen);
        Watch<SkillCardPanelController>(p=>p.IsOpen);
        Watch<DungeonNpcShop2D>(p=>p.IsOpen);
        Watch<ExpeditionSupplyPanelController2D>(p=>p.IsOpen);
        Watch<ExpeditionUnifiedTradeController2D>(p=>p.IsOpen);
    }
    void Watch<T>(Func<T,bool> open) where T:MonoBehaviour
    {
        foreach(var item in FindObjectsByType<T>(FindObjectsInactive.Include))
            otherPanels.Add(()=>item!=null && item.gameObject.activeInHierarchy && open(item));
    }
    void LateUpdate()
    {
        IsObscured=(menu!=null && menu.IsOpen) || (conversation!=null && conversation.IsOpen) ||
            (DialoguePresenter2D.Active!=null && DialoguePresenter2D.Active.IsPlaying) || otherPanels.Any(open=>open());
        if(visibility!=null)visibility.alpha=IsObscured?0:1;
        RebuildLayout();
    }
    public void RebuildLayout()
    {
        if(panel==null || Screen.width<=0 || Screen.height<=0)return;
        if(canvas==null)canvas=GetComponent<Canvas>();
        // Town embeds this canvas under StoryIntegration's canvas. A nested canvas does not fill its parent automatically.
        var host=transform as RectTransform;
        if(host!=null && canvas!=null && !canvas.isRootCanvas) {
            host.anchorMin=Vector2.zero;host.anchorMax=Vector2.one;host.offsetMin=host.offsetMax=Vector2.zero;host.localScale=Vector3.one;
        }
        if(title==null) {
            foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)) {
                if(text.name=="QuestName")title=text;
                if(text.name=="Objective")body=text;
                if(text.name=="CompletedObjective")notice=text;
            }
            if(notice!=null)noticePanel=notice.transform.parent as RectTransform;
        }
        if(title==null || body==null || notice==null)return;
        var background=panel.GetComponent<Image>();
        if(background!=null)background.color=Color.clear;
        var noticeBackground=noticePanel.GetComponent<Image>();
        if(noticeBackground!=null)noticeBackground.color=Color.clear;
        var parent=panel.parent as RectTransform;
        if(parent==null)return;
        var safe=Screen.safeArea;
        string content=title.text+"\n"+body.text+"\n"+notice.text;
        if(content==lastContent && parent.rect.size==lastSize && safe==lastSafe)return;
        lastContent=content;lastSize=parent.rect.size;lastSafe=safe;
        float scale=canvas!=null?Mathf.Max(.1f,canvas.rootCanvas.scaleFactor):1;
        float width=Mathf.Min(500,parent.rect.width*safe.width/Screen.width*.44f);
        panel.anchorMin=panel.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
        panel.pivot=Vector2.one;
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
        // The town's rightmost column contains facility buttons; use the clear column immediately to its left.
        float right=gameObject.scene.name=="Town_Main" ? Mathf.Max(20,safe.width*.25f/scale) : 20;
        float top=Mathf.Max(88,72/scale);
        var minimap=GetComponent<DungeonMinimap2D>();
        if(minimap!=null)top=Mathf.Max(top,minimap.ReservedTop);
        panel.anchoredPosition=new Vector2(-right, -top);
        float titleHeight=Fit(title,width-36,Mathf.Max(26,17/scale),14);
        float bodyTop=14+titleHeight+10;
        float bodyHeight=Fit(body,width-36,Mathf.Max(24,16/scale),bodyTop);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,bodyTop+bodyHeight+16);
        float noticeHeight=Fit(notice,width-36,Mathf.Max(23,16/scale),14)+28;
        noticePanel.anchorMin=new Vector2(0,0);noticePanel.anchorMax=new Vector2(1,0);
        noticePanel.pivot=new Vector2(.5f,1);noticePanel.sizeDelta=new Vector2(0,noticeHeight);
        noticePanel.anchoredPosition=new Vector2(0,-8);
        if(minimap!=null) {
            noticePanel.SetParent(parent,false);
            float noticeWidth=Mathf.Min(560,parent.rect.width*.42f);
            noticeHeight=Fit(notice,noticeWidth-36,Mathf.Max(23,16/scale),10)+20;
            notice.alignment=TextAlignmentOptions.Center;
            noticePanel.anchorMin=noticePanel.anchorMax=new Vector2(safe.center.x/Screen.width,safe.yMax/Screen.height);
            noticePanel.pivot=new Vector2(.5f,1);
            noticePanel.sizeDelta=new Vector2(noticeWidth,noticeHeight);
            noticePanel.anchoredPosition=new Vector2(0,-safe.height*.21f/scale);
        }
        if(gameObject.scene.name=="Town_Main")AvoidTownButtons(safe,scale,width,panel.rect.height+noticeHeight+8);
    }
    void AvoidTownButtons(Rect safe,float scale,float width,float height)
    {
        if(buttons==null)return;
        var occupied=new List<Rect>();
        foreach(var button in buttons) {
            if(button==null || !button.gameObject.activeInHierarchy || button.GetComponentsInParent<CanvasGroup>().Any(g=>g.alpha<.01f))continue;
            var root=button.GetComponentInParent<Canvas>();
            if(root==null)continue;
            root=root.rootCanvas;
            var camera=root.renderMode==RenderMode.ScreenSpaceOverlay?null:root.worldCamera;
            var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
            var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            occupied.Add(Rect.MinMaxRect(min.x-8,min.y-8,max.x+8,max.y+8));
        }
        float w=width*scale,h=height*scale;
        for(float top=72;top<safe.height*.6f;top+=16)
            for(float right=safe.xMax-20;right-w>=safe.xMin+safe.width*.32f;right-=16) {
                var candidate=new Rect(right-w,safe.yMax-top-h,w,h);
                if(candidate.yMin<safe.yMin+16 || occupied.Any(r=>r.Overlaps(candidate)))continue;
                panel.anchoredPosition=new Vector2((right-safe.xMax)/scale,-top/scale);
                return;
            }
    }
    static float Fit(TMP_Text text,float width,float fontSize,float top)
    {
        TextOnlyUIStyle2D.Apply(text);
        text.enableAutoSizing=false;text.fontSize=fontSize;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
        float height=Mathf.Ceil(text.GetPreferredValues(text.text,width,0).y)+4;
        var rect=text.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(18,-top-height);rect.offsetMax=new Vector2(-18,-top);
        return height;
    }
}
