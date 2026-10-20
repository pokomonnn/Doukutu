using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class StoryIntegrationBuilder
{
    // User-approved revision overrides the older script without rewriting the source manuscripts.
    static void ApplyFinnRevision()
    {
        source.steps=source.steps.Where(s=>s.quest!="S1S02" && s.id!="rescue").ToArray();
        var report=source.steps.First(s=>s.id=="reportFinn");
        report.title="バルドからフィン救助の報酬を受け取る";report.stage=1;
        report.requires=new[]{"S1S01_Delivered"};report.flags=new[]{"S1S01_Completed","PlayerBanditTruce"};
        foreach(var step in source.steps.Where(s=>s.quest=="S1S01"))step.questTitle="毒に倒れたフィン";
        var finn=source.steps.First(s=>s.id=="finn");finn.title="毒に倒れたフィン（救急キットで治療）";
        finn.nodes=new[]{"S1S01/FINN_FOUND"};
        source.Node("S1S01/FINN_FOUND").lines=new[]{"フィン：「毒を受けて……動けない。救急キットの解毒剤を使ってくれ……。治ったらバルドのところへ運んでほしい。」"};
        source.Node(report.nodes[0]).lines=new[]{"バルド：「フィンを治療して、ここまで連れてきてくれたか。ありがとう。約束の礼だ。あんたとは争わない。」"};
        var offer=source.offers.First(o=>o.StartsWith("S1S01/"));
        source.Node(offer).lines=new[]{"毒で動けなくなったフィンを探し、手持ちの救急キットの解毒剤で治療してください。治療後は担いでバルドのもとへ運び、報酬を受け取れます。帰還エレベーターへの搬入は不要です。"};
    }
    static void ConfigureFinnScene(GameObject root)
    {
        foreach(var point in root.GetComponentsInChildren<StoryInteractionPoint>(true)) {
            if(point.stepId!="medicine" && point.stepId!="deliverMedicine")continue;
            point.gameObject.SetActive(false);
            foreach(var label in root.GetComponentsInChildren<TMPro.TextMeshPro>())
                if(label.name=="Story label" && Vector3.Distance(label.transform.position,point.transform.position+Vector3.up*2)<.1f)label.gameObject.SetActive(false);
        }
        var finn=root.GetComponentInChildren<StoryRescueIdentity>(true);
        if(finn==null)throw new Exception("Finn instance missing");
        var health=finn.GetComponent<CharacterHealth>();Put(health,"preventDeath",true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(health);
        var symptoms=finn.GetComponent<RescuePersonSymptoms2D>();
        if(symptoms==null)symptoms=finn.gameObject.AddComponent<RescuePersonSymptoms2D>();
        Put(symptoms,"initialSymptoms",StatusConditionType.Poison);Put(symptoms,"initiallyStabilized",StatusConditionType.None);Put(symptoms,"useRandomSymptoms",false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(symptoms);
        var delivery=finn.GetComponent<StoryFinnDelivery>();
        if(delivery==null)delivery=finn.gameObject.AddComponent<StoryFinnDelivery>();
        delivery.baldo=root.transform.Find("StoryHub_Baldo");delivery.dialogueFont=Font;
        delivery.treatedDialogue=MakeDialogue("FinnTreated","治療後のフィン",
            DialogueLines(new[]{"フィン：「ありがとう、助かったよ。バルドのところへ運んでくれ。」"},"treated"));
        foreach(var kit in UnityEngine.Object.FindObjectsByType<MedicalKitInventory2D>(FindObjectsInactive.Include))
            if(kit.GetComponent<MedicalKitTreatment2D>()==null)kit.gameObject.AddComponent<MedicalKitTreatment2D>();
        foreach(var label in root.GetComponentsInChildren<TMPro.TextMeshPro>())
            if(label.name=="Story label" && Vector3.Distance(label.transform.position,finn.transform.position+Vector3.up*2)<.1f)
                label.text="フィン\n[T] 救急キットで治療 / [E] 話す";
    }
    static void RemoveRetiredMissionEntries()
    {
        foreach(var manager in UnityEngine.Object.FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include)) {
            var so=new SerializedObject(manager);var entries=so.FindProperty("missions");
            for(int i=entries.arraySize-1;i>=0;i--) {
                var mission=entries.GetArrayElementAtIndex(i).FindPropertyRelative("mission").objectReferenceValue as MissionDefinition2D;
                if(mission!=null && mission.MissionId=="story_S1S02")entries.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
