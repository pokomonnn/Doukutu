using System;
using UnityEngine;

public enum DialogueActionKind2D
{
    OpenShop = 0,
    StartMission = 1,
    DeliverMissionItems = 2,
    GiveItemToPlayer = 3,
    TakeItemFromPlayer = 4,
}

[Serializable]
public sealed class DialogueAction2D
{
    [Tooltip("会話ノードの『選択時イベントID』と一致させます。必ず固有のIDにしてください。")]
    public string eventId = "action_new";
    public DialogueActionKind2D kind = DialogueActionKind2D.OpenShop;
    [Tooltip("ミッションはMissionManager2Dのリスト内の0始まりIndexを指定")]
    [Min(0)] public int missionIndex;
    public ItemData item;
    [Min(1)] public int amount = 1;
    [Tooltip("同じNPCから同じ報酬を何度も獲得しないため、Play中は成功後の再実行を禁止。セーブ永続化は未対応")]
    public bool oncePerRuntime = true;
}
