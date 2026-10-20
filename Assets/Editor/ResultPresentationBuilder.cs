#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Result の表示だけを再構成。集計・報酬・帰還処理は既存コンポーネントを使用。</summary>
public static class ResultPresentationBuilder
{
    static TMP_FontAsset font;
    static readonly Color Ink=new Color(.88f,.90f,.88f), Gold=new Color(.86f,.73f,.46f);
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 inset)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=inset;r.offsetMax=-inset;return r;
    }
    static void Background(RectTransform r,Color color){var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;}
    static TMP_Text Text(string name,Transform parent,string text,int size,Vector2 min,Vector2 max,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
    {
        var r=Rect(name,parent,min,max,new Vector2(16,2));var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.text=text;t.fontSize=size;t.color=Ink;t.alignment=align;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    static RectTransform Card(string name,Transform root,Vector2 min,Vector2 max,string heading)
    {
        var r=Rect(name,root,min,max,new Vector2(8,8));Background(r,new Color(.095f,.125f,.14f));
        var title=Text(name+" Heading",r,heading,22,new Vector2(0,.82f),Vector2.one);title.color=Gold;return r;
    }
    static RectTransform Scroll(Transform parent,Vector2 min,Vector2 max)
    {
        var viewport=Rect("Scrollable details",parent,min,max,new Vector2(16,12));
        var image=viewport.gameObject.AddComponent<Image>();image.color=new Color(0,0,0,.001f);
        viewport.gameObject.AddComponent<RectMask2D>();var scroll=viewport.gameObject.AddComponent<ScrollRect>();
        var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one,Vector2.zero);content.pivot=new Vector2(.5f,1);
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.spacing=12;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
        return content;
    }
    public static void RefineSpacing()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Result.unity");
        var root=GameObject.Find("Expedition Report");
        foreach(var text in root.GetComponentsInChildren<TMP_Text>()) {
            if(text.GetComponentInParent<Button>()!=null || text.transform.parent.GetComponent<VerticalLayoutGroup>()!=null)continue;
            var r=text.rectTransform;r.offsetMin=new Vector2(r.offsetMin.x,2);r.offsetMax=new Vector2(r.offsetMax.x,-2);
        }
        var data=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>());
        data.FindProperty("missionCompletedMark").stringValue="達成：";data.FindProperty("missionFailedMark").stringValue="・";data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
    }
    public static void Apply()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Result.unity");
        var ui=UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>();var so=new SerializedObject(ui);
        var oldTotal=(TMP_Text)so.FindProperty("totalRewardText").objectReferenceValue;
        var canvas=oldTotal.canvas; font=oldTotal.font;
        if(canvas.transform.Find("Expedition Report")!=null)throw new InvalidOperationException("Result layout already installed; edit the existing layout instead.");
        var oldChildren=canvas.transform.Cast<Transform>().ToArray();
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var root=Rect("Expedition Report",canvas.transform,Vector2.zero,Vector2.one,Vector2.zero);Background(root,new Color(.035f,.055f,.065f));
        Text("Title",root,"探索結果",38,new Vector2(.035f,.88f),new Vector2(.6f,.98f));
        Text("Subtitle",root,"今回の探索記録",18,new Vector2(.65f,.9f),new Vector2(.965f,.97f),TextAlignmentOptions.MidlineRight).color=Gold;
        var stats=Rect("01 Exploration summary",root,new Vector2(.035f,.72f),new Vector2(.965f,.88f),new Vector2(8,8));Background(stats,new Color(.12f,.16f,.17f));
        string[] fields={"rescuedNpcCountText","deadNpcCountText","recoveredItemBoxCountText"};string[] labels={"救出した NPC","死亡した NPC","回収したアイテムボックス"};
        for(int i=0;i<3;i++){
            Text("Label "+i,stats,labels[i],20,new Vector2(i/3f,.5f),new Vector2((i+1)/3f,1));
            so.FindProperty(fields[i]).objectReferenceValue=Text(fields[i],stats,"0",30,new Vector2(i/3f,0),new Vector2((i+1)/3f,.6f));
        }
        var missions=Card("02 Missions",root,new Vector2(.035f,.39f),new Vector2(.57f,.72f),"出撃ミッション");
        var list=Text("Mission results",Scroll(missions,Vector2.zero,new Vector2(1,.80f)),"出撃ミッションなし",21,Vector2.zero,Vector2.one);so.FindProperty("expeditionMissionListText").objectReferenceValue=list;
        var treasure=Card("03 Treasure",root,new Vector2(.035f,.22f),new Vector2(.57f,.39f),"");
        Text("Treasure heading",treasure,"回収したお宝",20,new Vector2(0,.55f),Vector2.one).color=Gold;
        so.FindProperty("treasureListText").objectReferenceValue=Text("Treasure results",Scroll(treasure,Vector2.zero,new Vector2(1,.60f)),"お宝なし",20,Vector2.zero,Vector2.one);
        var medical=Card("04 Medical",root,new Vector2(.57f,.22f),new Vector2(.965f,.72f),"救護・治療の記録");
        var treatment=UnityEngine.Object.FindFirstObjectByType<RescueNpcTreatmentResultUI2D>();
        var treatmentText=treatment.GetComponent<TMP_Text>();treatment.transform.SetParent(Scroll(medical,Vector2.zero,new Vector2(1,.82f)),false);
        treatmentText.font=font;treatmentText.fontSize=20;treatmentText.enableAutoSizing=false;treatmentText.color=Ink;treatmentText.alignment=TextAlignmentOptions.TopLeft;treatmentText.textWrappingMode=TextWrappingModes.Normal;treatmentText.raycastTarget=false;
        Text("Scroll hint",root,"詳細が長い場合は、各欄の上でスクロールできます",14,new Vector2(.04f,.18f),new Vector2(.8f,.22f)).color=new Color(.55f,.63f,.64f);
        var rewards=Rect("05 Rewards",root,new Vector2(.035f,.025f),new Vector2(.73f,.18f),new Vector2(8,8));Background(rewards,new Color(.14f,.17f,.17f));
        string[] rewardFields={"recoveredMoneyText","rescueRewardText","expeditionMissionRewardText","totalRewardText"};string[] rewardLabels={"回収した現金","救出報酬","ミッション報酬","探索合計 ※治療報酬は別途"};
        for(int i=0;i<4;i++){
            float left=i*.23f,right=i==3?1:(i+1)*.23f;
            Text("Reward label "+i,rewards,rewardLabels[i],i==3?14:16,new Vector2(left,.52f),new Vector2(right,1)).color=Gold;
            so.FindProperty(rewardFields[i]).objectReferenceValue=Text(rewardFields[i],rewards,"0 G",26,new Vector2(left,0),new Vector2(right,.6f));
        }
        var button=(Button)so.FindProperty("returnTownButton").objectReferenceValue;button.transform.SetParent(root,false);
        var br=(RectTransform)button.transform;br.anchorMin=new Vector2(.76f,.055f);br.anchorMax=new Vector2(.955f,.15f);br.offsetMin=br.offsetMax=Vector2.zero;
        button.GetComponent<Image>().color=Gold;
        var bt=button.GetComponentInChildren<TMP_Text>();bt.fontSize=26;bt.color=new Color(.06f,.09f,.1f);bt.alignment=TextAlignmentOptions.Center;bt.rectTransform.anchorMin=Vector2.zero;bt.rectTransform.anchorMax=Vector2.one;bt.rectTransform.offsetMin=bt.rectTransform.offsetMax=Vector2.zero;
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var child in oldChildren)child.gameObject.SetActive(false);
        var sequence=ui.GetComponent<ExpeditionResultSequenceAnimator2D>();var seq=new SerializedObject(sequence);
        var rows=seq.FindProperty("customRows");var groups=new[]{stats,missions,treasure,medical,rewards};rows.arraySize=groups.Length;
        for(int i=0;i<groups.Length;i++){var row=rows.GetArrayElementAtIndex(i);row.FindPropertyRelative("target").objectReferenceValue=groups[i];row.FindPropertyRelative("useRewardSound").boolValue=i==4;}
        seq.FindProperty("enterSeconds").floatValue=.3f;seq.FindProperty("gapSeconds").floatValue=.15f;seq.FindProperty("slideDownDistance").floatValue=12;seq.FindProperty("revealReturnTownButtonLast").boolValue=true;seq.ApplyModifiedPropertiesWithoutUndo();
        var data=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>());
        data.FindProperty("missionCompletedMark").stringValue="達成：";data.FindProperty("missionFailedMark").stringValue="・";data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);Debug.Log("RESULT LAYOUT INSTALLED");
    }
}
#endif


