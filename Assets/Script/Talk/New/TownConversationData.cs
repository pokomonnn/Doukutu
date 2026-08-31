using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常住人・ミッション住人・商人の会話を、1つの形式で管理するデータです。
/// 数値のNode番号ではなく、分かりやすいBlock IDで会話を接続します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownConversation",
    menuName = "Town/Conversation/Unified Conversation Data"
)]
public class TownConversationData : ScriptableObject
{
    [Header("会話の種類")]
    [SerializeField]
    private TownConversationType conversationType =
        TownConversationType.NormalResident;

    [Header("NPCの基本情報")]
    [Tooltip("会話履歴の管理に使う重複しないIDです。通常住人では必ず設定してください。")]
    [SerializeField] private string residentId = "resident_id";

    [SerializeField] private string residentName = "住人";

    [Tooltip("各ページで個別指定しない場合に表示する立ち絵です。")]
    [SerializeField] private Sprite defaultPortrait;

    [Header("通常住人：開始ブロック")]
    [Tooltip("初めて会話する時に開始するBlock IDです。")]
    [SerializeField] private string firstConversationBlockId = "first";

    [Tooltip("2回目以降に開始するBlock IDです。")]
    [SerializeField] private string repeatConversationBlockId = "repeat";

    [Header("ミッション住人：対象ミッション")]
    [SerializeField] private MissionDefinition2D mission;

    [SerializeField] private bool trackMissionAfterAccept = true;

    [Header("ミッション住人：状態別の開始ブロック")]
    [Tooltip("このミッションをまだ受注していない時に、最初に表示するBlock IDです。")]
    [SerializeField] private string missionNotAcceptedBlockId = "mission_offer";

    [Tooltip("ミッションを受注した直後に表示するBlock IDです。受注選択肢の移動先が空欄の場合にも使用されます。")]
    [SerializeField] private string missionAcceptedJustNowBlockId = "mission_accepted";

    [Tooltip("ミッション受注後、まだ目的を達成していない時に会話を開始するBlock IDです。納品ミッションでは、通常ここから納品確認用Blockへ進めます。")]
    [SerializeField] private string missionInProgressBlockId = "mission_progress";

    [Tooltip("ミッションの目的を達成済みで、まだ報酬を受け取っていない時に会話を開始するBlock IDです。")]
    [SerializeField] private string missionReadyToReportBlockId = "mission_report";

    [Tooltip("ミッション報酬を受け取り済みの時に会話を開始するBlock IDです。")]
    [SerializeField] private string missionRewardClaimedBlockId = "mission_finished";

    [Header("ミッション報酬")]
    [Tooltip("オンなら、必要進捗に到達している時だけ報酬を渡します。")]
    [SerializeField] private bool requireObjectiveCompleted = true;

    [SerializeField, Min(0)] private int moneyReward;

    [SerializeField]
    private List<TownConversationRewardItem> itemRewards =
        new List<TownConversationRewardItem>();

    [Header("商人：開始ブロック")]
    [SerializeField] private string merchantStartBlockId = "merchant";

    [Header("商人：販売商品")]
    [Tooltip("この商人が販売する商品データです。OpenPawnShopの選択肢から購入・売却画面を開きます。")]
    [SerializeField] private MerchantShopData merchantShopData;

    [Header("共通ボタン表示")]
    [SerializeField] private string nextButtonText = "次へ";
    [SerializeField] private string closeButtonText = "閉じる";

    [Header("会話ブロック")]
    [Tooltip("Block IDで会話を接続します。1つのBlockには複数の文章と、最後に表示する選択肢を設定できます。")]
    [SerializeField]
    private List<TownConversationBlock> blocks =
        new List<TownConversationBlock>();

    public TownConversationType ConversationType => conversationType;

    public string ResidentId => string.IsNullOrWhiteSpace(residentId)
        ? name
        : residentId;

    public string ResidentName => string.IsNullOrWhiteSpace(residentName)
        ? name
        : residentName;

    public Sprite DefaultPortrait => defaultPortrait;

    public string FirstConversationBlockId => firstConversationBlockId;
    public string RepeatConversationBlockId => repeatConversationBlockId;

    public MissionDefinition2D Mission => mission;
    public bool TrackMissionAfterAccept => trackMissionAfterAccept;
    public bool RequireObjectiveCompleted => requireObjectiveCompleted;
    public int MoneyReward => Mathf.Max(0, moneyReward);
    public IReadOnlyList<TownConversationRewardItem> ItemRewards => itemRewards;

    public string MissionNotAcceptedBlockId => missionNotAcceptedBlockId;
    public string MissionAcceptedJustNowBlockId => missionAcceptedJustNowBlockId;
    public string MissionInProgressBlockId => missionInProgressBlockId;
    public string MissionReadyToReportBlockId => missionReadyToReportBlockId;
    public string MissionRewardClaimedBlockId => missionRewardClaimedBlockId;
    public string MerchantStartBlockId => merchantStartBlockId;
    public MerchantShopData MerchantShopData => merchantShopData;

    public string NextButtonText => string.IsNullOrWhiteSpace(nextButtonText)
        ? "次へ"
        : nextButtonText;

    public string CloseButtonText => string.IsNullOrWhiteSpace(closeButtonText)
        ? "閉じる"
        : closeButtonText;

    public int BlockCount => blocks != null ? blocks.Count : 0;

    public TownConversationBlock GetBlock(string blockId)
    {
        if (blocks == null || string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        string normalizedId = blockId.Trim();

        foreach (TownConversationBlock block in blocks)
        {
            if (block != null &&
                string.Equals(
                    block.BlockId,
                    normalizedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return block;
            }
        }

        return null;
    }

    public bool HasBlock(string blockId)
    {
        return GetBlock(blockId) != null;
    }

    public string GetFirstConfiguredBlockId()
    {
        if (blocks == null)
        {
            return string.Empty;
        }

        foreach (TownConversationBlock block in blocks)
        {
            if (block != null && !string.IsNullOrWhiteSpace(block.BlockId))
            {
                return block.BlockId;
            }
        }

        return string.Empty;
    }

    private void OnValidate()
    {
        residentId = Normalize(residentId);
        residentName = Normalize(residentName);

        firstConversationBlockId = Normalize(firstConversationBlockId);
        repeatConversationBlockId = Normalize(repeatConversationBlockId);

        missionNotAcceptedBlockId = Normalize(missionNotAcceptedBlockId);
        missionAcceptedJustNowBlockId = Normalize(missionAcceptedJustNowBlockId);
        missionInProgressBlockId = Normalize(missionInProgressBlockId);
        missionReadyToReportBlockId = Normalize(missionReadyToReportBlockId);
        missionRewardClaimedBlockId = Normalize(missionRewardClaimedBlockId);
        merchantStartBlockId = Normalize(merchantStartBlockId);

        nextButtonText = Normalize(nextButtonText);
        closeButtonText = Normalize(closeButtonText);
        moneyReward = Mathf.Max(0, moneyReward);

        if (blocks == null)
        {
            blocks = new List<TownConversationBlock>();
        }

        HashSet<string> usedIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (TownConversationBlock block in blocks)
        {
            if (block == null)
            {
                continue;
            }

            block.Validate();

            if (string.IsNullOrWhiteSpace(block.BlockId))
            {
                continue;
            }

            if (!usedIds.Add(block.BlockId))
            {
                Debug.LogWarning(
                    $"[TownConversationData] {name}: Block ID『{block.BlockId}』が重複しています。",
                    this
                );
            }
        }
    }

    private static string Normalize(string value)
    {
        return value?.Trim() ?? string.Empty;
    }
}

public enum TownConversationType
{
    NormalResident,
    MissionResident,
    Merchant
}

[Serializable]
public class TownConversationBlock
{
    [Header("管理用ID")]
    [Tooltip("例：first、mission_offer、ask_about_town。重複しない文字列にします。")]
    [SerializeField] private string blockId = "block";

    [Header("洞窟会話の表示設定")]
    [Tooltip("オン：重要会話。洞窟ではプレイヤーを停止し、会話パネルのクリックで次へ進みます。オフ：通常会話。プレイヤーは動けて、自動で次へ進みます。町ではこの設定は使いません。")]
    [SerializeField] private bool importantConversation;

    [Tooltip("オンにすると、このBlockの文章が表示された時に、おびえたような揺れ演出を行います。洞窟用です。")]
    [SerializeField] private bool fearShake;

    [Tooltip("重要会話がオフの時、1ページを読み終えてから次のページ・Blockへ自動で進むまでの秒数です。")]
    [SerializeField, Min(0f)] private float autoAdvanceDelay = 2.5f;

    [Tooltip("重要会話がオフで、このBlockが会話全体の最後の場合だけ使います。最後の文章を何秒表示してから閉じるかを指定します。")]
    [SerializeField, Min(0f)] private float finalBlockDisplayDuration = 3f;

    [Header("納品ミッション：Block終了時の自動判定")]
    [Tooltip("オンにすると、このBlockの最後から次へ進む瞬間に、現在のMission Residentの納品ミッションを自動判定します。必要アイテムがそろっていれば消費してSuccess Blockへ、足りなければFailure Blockへ進みます。")]
    [SerializeField] private bool checkMissionDeliveryOnBlockEnd;

    [Tooltip("納品成功時に進むBlock IDです。必要数をすべて所持している場合、アイテムを消費してミッション達成後にこのBlockへ進みます。")]
    [SerializeField] private string deliverySuccessBlockId;

    [Tooltip("納品失敗時に進むBlock IDです。必要アイテムを持っていない、または必要数に足りない場合に進みます。アイテムは消費しません。")]
    [SerializeField] private string deliveryFailureBlockId;

    [Header("このブロックで順番に表示する文章")]
    [SerializeField]
    private List<TownConversationPage> pages =
        new List<TownConversationPage>();

    [Header("文章の最後に表示する選択肢")]
    [SerializeField]
    private List<TownConversationChoice> choices =
        new List<TownConversationChoice>();

    [Header("選択肢がない場合の次のブロック")]
    [Tooltip("空欄なら、このブロックの最後で会話を終了します。")]
    [SerializeField] private string nextBlockId;

    public string BlockId => blockId?.Trim() ?? string.Empty;
    public string NextBlockId => nextBlockId?.Trim() ?? string.Empty;
    public bool ImportantConversation => importantConversation;
    public bool FearShake => fearShake;
    public float AutoAdvanceDelay => Mathf.Max(0f, autoAdvanceDelay);
    public float FinalBlockDisplayDuration => Mathf.Max(0f, finalBlockDisplayDuration);
    public bool CheckMissionDeliveryOnBlockEnd => checkMissionDeliveryOnBlockEnd;
    public string DeliverySuccessBlockId => deliverySuccessBlockId?.Trim() ?? string.Empty;
    public string DeliveryFailureBlockId => deliveryFailureBlockId?.Trim() ?? string.Empty;
    public int PageCount => pages != null ? pages.Count : 0;
    public int ChoiceCount => choices != null ? choices.Count : 0;

    public TownConversationPage GetPage(int index)
    {
        if (pages == null || index < 0 || index >= pages.Count)
        {
            return null;
        }

        return pages[index];
    }

    public TownConversationChoice GetChoice(int index)
    {
        if (choices == null || index < 0 || index >= choices.Count)
        {
            return null;
        }

        return choices[index];
    }

    public void Validate()
    {
        blockId = blockId?.Trim() ?? string.Empty;
        nextBlockId = nextBlockId?.Trim() ?? string.Empty;
        deliverySuccessBlockId = deliverySuccessBlockId?.Trim() ?? string.Empty;
        deliveryFailureBlockId = deliveryFailureBlockId?.Trim() ?? string.Empty;
        autoAdvanceDelay = Mathf.Max(0f, autoAdvanceDelay);
        finalBlockDisplayDuration = Mathf.Max(0f, finalBlockDisplayDuration);

        if (pages == null)
        {
            pages = new List<TownConversationPage>();
        }

        if (choices == null)
        {
            choices = new List<TownConversationChoice>();
        }

        foreach (TownConversationChoice choice in choices)
        {
            choice?.Validate();
        }
    }
}

[Serializable]
public class TownConversationPage
{
    [Header("話者（空欄ならNPCの基本情報を使用）")]
    [SerializeField] private string speakerNameOverride;
    [SerializeField] private Sprite portraitOverride;

    [Header("文章")]
    [SerializeField, TextArea(3, 8)]
    private string message = "会話内容";

    public string SpeakerNameOverride => speakerNameOverride?.Trim() ?? string.Empty;
    public Sprite PortraitOverride => portraitOverride;
    public string Message => message ?? string.Empty;
}

public enum TownConversationChoiceAction
{
    GoToBlock,
    AcceptMission,
    ClaimMissionReward,
    OpenPawnShop,
    CloseDialogue,
    UpgradeFacility,

    // 既存Actionの数値を変えないため、必ず末尾へ追加する。
    OpenWeaponRepair
}

[Serializable]
public class TownConversationChoice
{
    [Header("表示")]
    [SerializeField] private string choiceText = "選択肢";

    [Header("実行内容")]
    [SerializeField]
    private TownConversationChoiceAction action =
        TownConversationChoiceAction.GoToBlock;

    [Tooltip("Go To Blockの移動先です。受注・報酬成功後の移動先としても使用できます。施設アップグレード成功時の移動先にも使用します。空欄なら会話を終了します。")]
    [SerializeField] private string nextBlockId;

    [Header("施設アップグレード（Action=Upgrade Facility時）")]
    [Tooltip("この選択肢でアップグレードする施設データです。")]
    [SerializeField] private TownFacilityUpgradeData facilityUpgradeData;

    [Tooltip("必要素材不足・最大Lvなど、アップグレードに失敗した時に進むBlock IDです。空欄なら現在の選択肢を再度操作できます。")]
    [SerializeField] private string upgradeFailureBlockId;

    [Header("この選択肢だけ音を変える場合")]
    [SerializeField] private AudioClip selectionSoundOverride;

    public string ChoiceText => string.IsNullOrWhiteSpace(choiceText)
        ? "選択肢"
        : choiceText;

    public TownConversationChoiceAction Action => action;
    public string NextBlockId => nextBlockId?.Trim() ?? string.Empty;
    public TownFacilityUpgradeData FacilityUpgradeData => facilityUpgradeData;
    public string UpgradeFailureBlockId =>
        upgradeFailureBlockId?.Trim() ?? string.Empty;
    public AudioClip SelectionSoundOverride => selectionSoundOverride;

    public void Validate()
    {
        choiceText = choiceText?.Trim() ?? string.Empty;
        nextBlockId = nextBlockId?.Trim() ?? string.Empty;
        upgradeFailureBlockId =
            upgradeFailureBlockId?.Trim() ?? string.Empty;
    }
}

[Serializable]
public class TownConversationRewardItem
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);
}
