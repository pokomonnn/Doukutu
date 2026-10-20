using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DungeonUIVisualChecks
{
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Fields).GetValue(obj);
    static void Assert(bool ok,string message){if(!ok)throw new Exception("DUNGEON UI: "+message);}
    static Rect Bounds(RectTransform rect)=>(Rect)typeof(QuestHUDVisualTests).GetMethod("ScreenRect",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{rect});
    static void Capture(string name)
    {
        var image=(Texture2D)typeof(QuestHUDVisualTests).GetMethod("CaptureGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        File.WriteAllBytes("Logs/QuestHUD/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
    public static IEnumerator Check(Vector2Int size)
    {
        var presenter=DialoguePresenter2D.Active;
        Assert(presenter!=null,"presenter missing");
        var actor=UnityEngine.Object.FindFirstObjectByType<DialogueActor2D>();
        var click=Get<CanvasGroup>(presenter,"panel").GetComponent<DialogueClickAdvance2D>();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        var pages=ScriptableObject.CreateInstance<DialogueData2D>();pages.EditorInitialize("click_test","クリック確認");
        pages.Nodes[0].body="文字送り中のクリックは全文表示。次のクリックで会話を終了します。";
        Assert(presenter.Play(pages,actor),"click test start");
        UnityEngine.EventSystems.ExecuteEvents.Execute(click.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Assert(presenter.IsPlaying && Get<TMP_Text>(presenter,"bodyText").maxVisibleCharacters==int.MaxValue,"click did not reveal");
        Assert(!Get<Button>(presenter,"nextButton").gameObject.activeSelf,"next button still visible");
        UnityEngine.EventSystems.ExecuteEvents.Execute(click.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Assert(!presenter.IsPlaying,"click did not advance/end");
        UnityEngine.Object.Destroy(pages);
        var data=Resources.Load<DialogueData2D>("StoryCampaign/Dialogue_Hub_1_Baldo");
        Assert(presenter.Play(data,actor),"Baldo dialogue failed");
        presenter.Next(); // Reveal without selecting or firing actions.
        UnityEngine.EventSystems.ExecuteEvents.Execute(click.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Assert(presenter.IsPlaying,"background click skipped choices");
        for(int i=0;i<4;i++)yield return null;
        Validate(presenter,size);
        var suppressed=DialogueBattleBarkBubble2D.Show(actor.transform,"会話中は表示しない",null,Vector3.up,3,100,5,Color.white,Color.black);
        Assert(suppressed==null,"ambient bark overlaps dialogue");
        Capture("dialogue-"+size.x+"x"+size.y);
        presenter.EndDialogue();
        yield return null;
        // A long body plus a list longer than the viewport exercises wrapping and scrolling.
        var stress=ScriptableObject.CreateInstance<DialogueData2D>();stress.EditorInitialize("ui_test","UI test");
        var node=stress.Nodes[0];node.speakerName="バルド";
        node.body="旧居住区で動けなくなったフィンを探し、手持ちの救急キットの解毒剤で治療してくれ。治療が終わったらフィンを担いで私のところへ連れてきてほしい。町に戻る前に報告してくれれば報酬を渡せる。";
        for(int i=0;i<24;i++)node.choices.Add(new DialogueChoice2D{label=(i+1)+"：フィンの救出と報告について詳しく聞く"});
        Assert(presenter.Play(stress,actor),"stress dialogue failed");presenter.Next();
        for(int i=0;i<4;i++)yield return null;
        Validate(presenter,size);
        var scroll=presenter.GetComponentInChildren<ScrollRect>(true);
        Capture("dialogue-long-before-"+size.x+"x"+size.y);
        Assert(scroll!=null && scroll.vertical && scroll.content.rect.height>scroll.viewport.rect.height,
            "long choices do not scroll: "+scroll.name+" vertical="+scroll.vertical+" content="+scroll.content.rect+" viewport="+scroll.viewport.rect);
        scroll.verticalNormalizedPosition=0;
        for(int i=0;i<2;i++)yield return null;
        Capture("dialogue-long-"+size.x+"x"+size.y);
        var buttons=Get<Transform>(presenter,"choicesRoot").GetComponentsInChildren<Button>();
        buttons.Last().onClick.Invoke();
        Assert(!presenter.IsPlaying,"choice failed to close dialogue");
        UnityEngine.Object.Destroy(stress);
        yield return null;
        var font=Get<TMP_Text>(presenter,"bodyText").font;
        var visibleSpeaker=UnityEngine.Object.FindFirstObjectByType<PlayerMove>().transform;
        var first=DialogueBattleBarkBubble2D.Show(visibleSpeaker,"前のセリフ",font,Vector3.up*2,4,100,5,Color.white,Color.black,speechPriority:100);
        var second=DialogueBattleBarkBubble2D.Show(visibleSpeaker,"ありがとう、助かったよ",font,Vector3.up*2,4,100,5,Color.white,Color.black,0,speechPriority:100);
        Assert(first!=null && second!=null && !first.enabled,"barks not coordinated");
        yield return null;
        Assert(second.GetComponentsInChildren<Image>().All(i=>!i.enabled),"speech background remains");
        var text=second.GetComponentInChildren<TMP_Text>();text.ForceMeshUpdate();
        Assert(!text.isTextOverflowing,"speech text clipped");
        Capture("speech-"+size.x+"x"+size.y);
        NpcSpeechCoordinator2D.Silence();
        for(int i=0;i<3;i++)yield return null;
    }
    static void Validate(DialoguePresenter2D presenter,Vector2Int size)
    {
        var name=Get<TMP_Text>(presenter,"speakerText");var body=Get<TMP_Text>(presenter,"bodyText");
        var panel=Get<CanvasGroup>(presenter,"panel");
        Assert(panel.GetComponent<Image>().color.a==0,"dialogue background remains");
        foreach(var text in new[]{name,body}) {
            text.ForceMeshUpdate();Assert(!text.isTextOverflowing,"text overflow "+text.name);
            var bounds=Bounds(text.rectTransform);
            Assert(bounds.xMin>=0 && bounds.yMin>=0 && bounds.xMax<=size.x+1 && bounds.yMax<=size.y+1,"dialogue outside screen");
        }
        Assert(!Bounds(name.rectTransform).Overlaps(Bounds(body.rectTransform)),"speaker overlaps body");
        var viewport=presenter.GetComponentInChildren<ScrollRect>(true).viewport;
        Assert(!Bounds(viewport).Overlaps(Bounds(body.rectTransform)),"choices overlap body");
        Assert(!Bounds(viewport).Overlaps(Bounds(name.rectTransform)),"choices overlap name");
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)) {
            // Choices outside the viewport are clipped by RectMask2D, not visible collisions.
            if(!button.isActiveAndEnabled || button.transform.IsChildOf(panel.transform) || button.GetComponentInParent<Canvas>()==null ||
                button.GetComponentsInParent<Canvas>().Any(c=>!c.enabled) ||
                button.GetComponentsInParent<CanvasGroup>().Any(g=>g.alpha<.01f))continue;
            var visible=Bounds((RectTransform)button.transform);
            foreach(var mask in button.GetComponentsInParent<RectMask2D>()) {
                var clip=Bounds(mask.rectTransform);
                visible=Rect.MinMaxRect(Mathf.Max(visible.xMin,clip.xMin),Mathf.Max(visible.yMin,clip.yMin),
                    Mathf.Min(visible.xMax,clip.xMax),Mathf.Min(visible.yMax,clip.yMax));
            }
            if(visible.width<=0 || visible.height<=0)continue;
            Assert(!visible.Overlaps(Bounds(body.rectTransform)),
                "body overlaps existing button "+button.name+" scene="+button.gameObject.scene.name+
                " canvas="+button.GetComponentInParent<Canvas>().name+" bounds="+visible);
        }
    }
}
