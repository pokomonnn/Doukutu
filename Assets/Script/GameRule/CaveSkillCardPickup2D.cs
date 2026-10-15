using UnityEngine;

/// <summary>
/// 洞窟内のスキルカード取得オブジェクト。
/// Eキーでスキルコレクションに直接登録します。通常Inventoryには追加しません。
/// 取得後にオブジェクトを消すか残すかをInspectorから選択できます。
/// 残す場合は「取得済み」の接近案内を表示し、Eで取得済みメッセージを確認できます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class CaveSkillCardPickup2D : CavePickupInteractable2D
{
    [Header("取得させるスキルカード")]
    [SerializeField] private SkillCardData skillCard;

    [Header("取得後のオブジェクト")]
    [Tooltip("ON: スキルを取得したらオブジェクトを消します（すでに取得済みなら開始時にも非表示）。OFF: 残して『取得済み』の案内を表示します。")]
    [SerializeField] private bool destroyAfterAcquisition = true;

    [Header("Inspectorで設定する表示メッセージ")]
    [Tooltip("未取得のカードに近づいた時の案内です。前に[E]が自動で付きます。{skill}または{name}にカード名を挿入します。")]
    [SerializeField, TextArea(1, 3)]
    private string nearbyPromptMessage = "スキル習得：{skill}";

    [Tooltip("初めて習得した時の通知です。{skill}または{name}にカード名を挿入します。")]
    [SerializeField, TextArea(1, 3)]
    private string acquiredMessage = "新しいスキル獲得：{skill}";

    [Tooltip("取得後もオブジェクトを残す場合に、近づいた時に出す案内です。前に[E]が自動で付きます。")]
    [SerializeField, TextArea(1, 3)]
    private string alreadyOwnedNearbyMessage = "習得済み：{skill}";

    [Tooltip("取得済みのオブジェクトで再度Eキーを押した時の通知です。")]
    [SerializeField, TextArea(1, 3)]
    private string alreadyOwnedMessage = "このスキルは習得済みです：{skill}";

    [Tooltip("ゲームのセッション管理が見つからない場合の通知です。")]
    [SerializeField, TextArea(1, 3)]
    private string sessionMissingMessage = "スキルを取得できません：GameSessionManagerがありません";

    [Tooltip("スキルの登録に失敗した時の通知です。SkillCardDataのItemIdを確認してください。")]
    [SerializeField, TextArea(1, 3)]
    private string acquisitionFailedMessage = "スキルを習得できませんでした。カードのItemIdを確認してください";

    protected override void Update()
    {
        GameSessionManager session = GameSessionManager.Instance;

        // 「消す」モードで、ロード済みの取得カードがSceneに再配置されても
        // 再取得できないよう非表示にします。
        if (destroyAfterAcquisition && skillCard != null &&
            session != null && session.HasSkillCard(skillCard))
        {
            gameObject.SetActive(false);
            return;
        }

        base.Update();
    }

    protected override bool IsReady()
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (skillCard == null || session == null)
        {
            return false;
        }

        // 残す設定なら、取得済みカードにもEで話しかけられるようにします。
        return !destroyAfterAcquisition || !session.HasSkillCard(skillCard);
    }

    protected override string GetPromptLabel()
    {
        GameSessionManager session = GameSessionManager.Instance;
        bool alreadyOwned = skillCard != null && session != null &&
                            session.HasSkillCard(skillCard);

        return FormatMessage(alreadyOwned
            ? alreadyOwnedNearbyMessage
            : nearbyPromptMessage);
    }

    protected override void Collect(Transform player)
    {
        if (skillCard == null)
        {
            return;
        }

        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            Debug.LogWarning(
                "[CaveSkillCardPickup2D] GameSessionManagerが見つかりません。",
                this);
            ShowMessage(FormatMessage(sessionMissingMessage));
            return;
        }

        if (session.HasSkillCard(skillCard))
        {
            // 残す設定なら何度Eを押しても所持数は増えず、取得済みと案内するだけ。
            ShowMessage(FormatMessage(alreadyOwnedMessage));
            if (destroyAfterAcquisition)
            {
                FinishCollection();
            }
            return;
        }

        // InventoryController.TryAddItemは呼ばず、スキルコレクションへ直接登録。
        if (!session.UnlockSkillCard(skillCard, out bool newlyUnlocked))
        {
            ShowMessage(FormatMessage(acquisitionFailedMessage));
            return;
        }

        if (newlyUnlocked)
        {
            ShowMessage(FormatMessage(acquiredMessage));
            PlayPickupSound();
        }
        else
        {
            ShowMessage(FormatMessage(alreadyOwnedMessage));
        }

        if (destroyAfterAcquisition)
        {
            FinishCollection();
        }
        // OFFならDestroyせず次フレームから取得済み案内に自動で切り替える。
    }

    private string FormatMessage(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        string cardName = skillCard != null
            ? skillCard.DisplayName
            : "スキルカード";

        return template
            .Replace("{skill}", cardName)
            .Replace("{name}", cardName);
    }
}
