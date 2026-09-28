using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の酒場パネルを管理します。
/// ・一人飲み：初期値 50G / 1ラウンド3回 / 累計30回でスキル取得
/// ・みんなにおごる：初期値 1000G / 1ラウンド1回 / 累計10回でスキル取得
/// ・ラウンド内回数はGameSessionManager.BeginCurrentExpedition()でリセットされます。
/// </summary>
[DisallowMultipleComponent]
public class TavernController : MonoBehaviour
{
    [Header("基本参照")]
    [SerializeField, InspectorName("酒場パネル")]
    private GameObject panelRoot;

    [SerializeField, InspectorName("開くボタン")]
    private Button openButton;

    [SerializeField, InspectorName("閉じるボタン")]
    private Button closeButton;

    [SerializeField, InspectorName("開始時はパネルを閉じる")]
    private bool closePanelOnStart = true;

    [Header("一人飲み")]
    [SerializeField, Min(0), InspectorName("料金")]
    private int personalDrinkPrice = 50;

    [SerializeField, Min(1), InspectorName("1ラウンド上限回数")]
    private int personalDrinkPerRoundLimit = 3;

    [SerializeField, Min(1), InspectorName("スキル獲得までの累計回数")]
    private int personalDrinkRewardRequiredCount = 30;

    [SerializeField, InspectorName("獲得スキル")]
    private SkillCardData personalDrinkRewardSkill;

    [SerializeField, InspectorName("一人飲みボタン")]
    private Button personalDrinkButton;

    [SerializeField, InspectorName("一人飲み進捗テキスト")]
    private TMP_Text personalDrinkProgressText;

    [SerializeField, InspectorName("一人飲みボタンテキスト")]
    private TMP_Text personalDrinkButtonText;

    [Header("みんなにおごる")]
    [SerializeField, Min(0), InspectorName("料金")]
    private int treatEveryonePrice = 1000;

    [SerializeField, Min(1), InspectorName("1ラウンド上限回数")]
    private int treatEveryonePerRoundLimit = 1;

    [SerializeField, Min(1), InspectorName("スキル獲得までの累計回数")]
    private int treatEveryoneRewardRequiredCount = 10;

    [SerializeField, InspectorName("獲得スキル")]
    private SkillCardData treatEveryoneRewardSkill;

    [SerializeField, InspectorName("みんなにおごるボタン")]
    private Button treatEveryoneButton;

    [SerializeField, InspectorName("みんなにおごる進捗テキスト")]
    private TMP_Text treatEveryoneProgressText;

    [SerializeField, InspectorName("みんなにおごるボタンテキスト")]
    private TMP_Text treatEveryoneButtonText;

    [Header("共通UI")]
    [SerializeField, InspectorName("所持金テキスト")]
    private TMP_Text moneyText;

    [SerializeField, InspectorName("メッセージテキスト")]
    private TMP_Text messageText;

    [SerializeField, InspectorName("ラウンドテキスト")]
    private TMP_Text roundText;

    [Header("サウンド（任意）")]
    [SerializeField, InspectorName("Audio Source")]
    private AudioSource audioSource;

    [SerializeField, InspectorName("一人飲みSE")]
    private AudioClip personalDrinkSound;

    [SerializeField, InspectorName("みんなにおごるSE")]
    private AudioClip treatEveryoneSound;

    [SerializeField, InspectorName("スキル獲得SE")]
    private AudioClip skillUnlockSound;

    [SerializeField, InspectorName("失敗SE")]
    private AudioClip errorSound;

    [SerializeField, Range(0f, 1f), InspectorName("SE音量")]
    private float soundVolume = 1f;

    [Header("デバッグ")]
    [SerializeField, InspectorName("デバッグログを表示")]
    private bool showDebugLogs;

    private GameSessionManager session;
    private bool listenersRegistered;

    private void Awake()
    {
        FindSession();
        RegisterListeners();

        if (closePanelOnStart && panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        TryGrantPendingRewards(false);
        RefreshUI();
    }

    private void OnEnable()
    {
        FindSession();
        SubscribeSessionEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeSessionEvents();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
        UnsubscribeSessionEvents();
    }

    public void OpenTavern()
    {
        FindSession();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        TryGrantPendingRewards(false);
        RefreshUI();
        SetMessage(string.Empty);
    }

    public void CloseTavern()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void DrinkAlone()
    {
        FindSession();

        if (session == null)
        {
            Fail("GameSessionManagerが見つかりません。");
            return;
        }

        if (!session.CanUseTavernPersonalDrink(personalDrinkPerRoundLimit))
        {
            Fail(
                $"一人飲みは1ラウンド{personalDrinkPerRoundLimit}回までです。"
            );
            return;
        }

        if (session.CurrentMoney < personalDrinkPrice)
        {
            Fail(
                $"所持金が足りません。必要 {personalDrinkPrice:N0}G / " +
                $"所持 {session.CurrentMoney:N0}G"
            );
            return;
        }

        if (!session.TrySpendMoney(personalDrinkPrice))
        {
            Fail("料金の支払いに失敗しました。");
            return;
        }

        if (!session.TryRegisterTavernPersonalDrink(
                personalDrinkPerRoundLimit,
                out string resultMessage))
        {
            // 念のため、回数登録に失敗した場合は支払ったお金を返します。
            session.AddMoney(personalDrinkPrice);
            Fail(resultMessage);
            return;
        }

        PlaySound(personalDrinkSound);

        string rewardMessage = TryGrantPendingRewards(true);
        SetMessage(
            string.IsNullOrWhiteSpace(rewardMessage)
                ? $"一人で飲みました。 -{personalDrinkPrice:N0}G"
                : rewardMessage
        );

        Log(resultMessage);
        RefreshUI();
    }

    public void TreatEveryone()
    {
        FindSession();

        if (session == null)
        {
            Fail("GameSessionManagerが見つかりません。");
            return;
        }

        if (!session.CanUseTavernTreatEveryone(treatEveryonePerRoundLimit))
        {
            Fail(
                $"みんなにおごるのは1ラウンド{treatEveryonePerRoundLimit}回までです。"
            );
            return;
        }

        if (session.CurrentMoney < treatEveryonePrice)
        {
            Fail(
                $"所持金が足りません。必要 {treatEveryonePrice:N0}G / " +
                $"所持 {session.CurrentMoney:N0}G"
            );
            return;
        }

        if (!session.TrySpendMoney(treatEveryonePrice))
        {
            Fail("料金の支払いに失敗しました。");
            return;
        }

        if (!session.TryRegisterTavernTreatEveryone(
                treatEveryonePerRoundLimit,
                out string resultMessage))
        {
            session.AddMoney(treatEveryonePrice);
            Fail(resultMessage);
            return;
        }

        PlaySound(treatEveryoneSound);

        string rewardMessage = TryGrantPendingRewards(true);
        SetMessage(
            string.IsNullOrWhiteSpace(rewardMessage)
                ? $"みんなにおごりました！ -{treatEveryonePrice:N0}G"
                : rewardMessage
        );

        Log(resultMessage);
        RefreshUI();
    }

    /// <summary>
    /// 条件達成済みなのにスキル未受取の状態を補完します。
    /// Inspectorで後から報酬スキルを設定した場合にも対応できます。
    /// </summary>
    public string TryGrantPendingRewards(bool playRewardSound)
    {
        FindSession();

        if (session == null)
        {
            return string.Empty;
        }

        string unlockedMessage = string.Empty;
        bool unlockedAny = false;

        if (!session.TavernPersonalDrinkRewardClaimed &&
            session.TavernPersonalDrinkTotal >=
                Mathf.Max(1, personalDrinkRewardRequiredCount) &&
            personalDrinkRewardSkill != null)
        {
            if (session.UnlockSkillCard(
                    personalDrinkRewardSkill,
                    out bool wasNewlyUnlocked))
            {
                session.MarkTavernPersonalDrinkRewardClaimed();
                unlockedAny = true;

                unlockedMessage = wasNewlyUnlocked
                    ? $"スキル「{personalDrinkRewardSkill.DisplayName}」を獲得しました！"
                    : $"一人飲みの報酬条件を達成しました。";
            }
        }

        if (!session.TavernTreatEveryoneRewardClaimed &&
            session.TavernTreatEveryoneTotal >=
                Mathf.Max(1, treatEveryoneRewardRequiredCount) &&
            treatEveryoneRewardSkill != null)
        {
            if (session.UnlockSkillCard(
                    treatEveryoneRewardSkill,
                    out bool wasNewlyUnlocked))
            {
                session.MarkTavernTreatEveryoneRewardClaimed();
                unlockedAny = true;

                string message = wasNewlyUnlocked
                    ? $"スキル「{treatEveryoneRewardSkill.DisplayName}」を獲得しました！"
                    : $"みんなにおごる報酬条件を達成しました。";

                unlockedMessage = string.IsNullOrWhiteSpace(unlockedMessage)
                    ? message
                    : unlockedMessage + "\n" + message;
            }
        }

        if (unlockedAny && playRewardSound)
        {
            PlaySound(skillUnlockSound);
        }

        return unlockedMessage;
    }

    public void RefreshUI()
    {
        FindSession();

        int currentMoney = session != null
            ? session.CurrentMoney
            : 0;

        if (moneyText != null)
        {
            moneyText.text = $"所持金: {currentMoney:N0} G";
        }

        if (personalDrinkButtonText != null)
        {
            personalDrinkButtonText.text =
                $"一人で飲む  {personalDrinkPrice:N0}G";
        }

        if (treatEveryoneButtonText != null)
        {
            treatEveryoneButtonText.text =
                $"みんなにおごる  {treatEveryonePrice:N0}G";
        }

        if (roundText != null)
        {
            int round = session != null
                ? session.TavernRoundIndex + 1
                : 1;

            roundText.text = $"ラウンド {round}";
        }

        RefreshPersonalDrinkUI();
        RefreshTreatEveryoneUI();
        RefreshButtons();
    }

    private void RefreshPersonalDrinkUI()
    {
        if (personalDrinkProgressText == null)
        {
            return;
        }

        int total = session != null
            ? session.TavernPersonalDrinkTotal
            : 0;

        int currentRound = session != null
            ? session.TavernPersonalDrinkThisRound
            : 0;

        int required = Mathf.Max(1, personalDrinkRewardRequiredCount);
        int shownTotal = Mathf.Min(total, required);

        string rewardState;

        if (session != null && session.TavernPersonalDrinkRewardClaimed)
        {
            rewardState = "報酬スキル取得済み";
        }
        else if (total >= required && personalDrinkRewardSkill == null)
        {
            rewardState = "報酬条件達成 / 獲得スキル未設定";
        }
        else
        {
            rewardState = $"報酬まであと {Mathf.Max(0, required - total)} 回";
        }

        personalDrinkProgressText.text =
            $"今ラウンド {currentRound}/{personalDrinkPerRoundLimit}\n" +
            $"累計 {shownTotal}/{required}\n" +
            rewardState;
    }

    private void RefreshTreatEveryoneUI()
    {
        if (treatEveryoneProgressText == null)
        {
            return;
        }

        int total = session != null
            ? session.TavernTreatEveryoneTotal
            : 0;

        int currentRound = session != null
            ? session.TavernTreatEveryoneThisRound
            : 0;

        int required = Mathf.Max(1, treatEveryoneRewardRequiredCount);
        int shownTotal = Mathf.Min(total, required);

        string rewardState;

        if (session != null && session.TavernTreatEveryoneRewardClaimed)
        {
            rewardState = "報酬スキル取得済み";
        }
        else if (total >= required && treatEveryoneRewardSkill == null)
        {
            rewardState = "報酬条件達成 / 獲得スキル未設定";
        }
        else
        {
            rewardState = $"報酬まであと {Mathf.Max(0, required - total)} 回";
        }

        treatEveryoneProgressText.text =
            $"今ラウンド {currentRound}/{treatEveryonePerRoundLimit}\n" +
            $"累計 {shownTotal}/{required}\n" +
            rewardState;
    }

    private void RefreshButtons()
    {
        bool hasSession = session != null;

        if (personalDrinkButton != null)
        {
            personalDrinkButton.interactable =
                hasSession &&
                session.CanUseTavernPersonalDrink(personalDrinkPerRoundLimit) &&
                session.CurrentMoney >= personalDrinkPrice;
        }

        if (treatEveryoneButton != null)
        {
            treatEveryoneButton.interactable =
                hasSession &&
                session.CanUseTavernTreatEveryone(treatEveryonePerRoundLimit) &&
                session.CurrentMoney >= treatEveryonePrice;
        }
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenTavern);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseTavern);
        }

        if (personalDrinkButton != null)
        {
            personalDrinkButton.onClick.AddListener(DrinkAlone);
        }

        if (treatEveryoneButton != null)
        {
            treatEveryoneButton.onClick.AddListener(TreatEveryone);
        }

        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenTavern);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTavern);
        }

        if (personalDrinkButton != null)
        {
            personalDrinkButton.onClick.RemoveListener(DrinkAlone);
        }

        if (treatEveryoneButton != null)
        {
            treatEveryoneButton.onClick.RemoveListener(TreatEveryone);
        }

        listenersRegistered = false;
    }

    private void SubscribeSessionEvents()
    {
        if (session == null)
        {
            return;
        }

        session.MoneyChanged -= HandleMoneyChanged;
        session.MoneyChanged += HandleMoneyChanged;

        session.TavernProgressChanged -= HandleTavernProgressChanged;
        session.TavernProgressChanged += HandleTavernProgressChanged;
    }

    private void UnsubscribeSessionEvents()
    {
        if (session == null)
        {
            return;
        }

        session.MoneyChanged -= HandleMoneyChanged;
        session.TavernProgressChanged -= HandleTavernProgressChanged;
    }

    private void HandleMoneyChanged(int _)
    {
        RefreshUI();
    }

    private void HandleTavernProgressChanged()
    {
        RefreshUI();
    }

    private void FindSession()
    {
        if (session == null)
        {
            session = GameSessionManager.Instance;
        }

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(
                FindObjectsInactive.Include
            );
        }
    }

    private void Fail(string message)
    {
        SetMessage(message);
        PlaySound(errorSound);
        Log(message);
        RefreshUI();
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(soundVolume));
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[TavernController] {message}", this);
    }

    private void OnValidate()
    {
        personalDrinkPrice = Mathf.Max(0, personalDrinkPrice);
        personalDrinkPerRoundLimit = Mathf.Max(1, personalDrinkPerRoundLimit);
        personalDrinkRewardRequiredCount =
            Mathf.Max(1, personalDrinkRewardRequiredCount);

        treatEveryonePrice = Mathf.Max(0, treatEveryonePrice);
        treatEveryonePerRoundLimit = Mathf.Max(1, treatEveryonePerRoundLimit);
        treatEveryoneRewardRequiredCount =
            Mathf.Max(1, treatEveryoneRewardRequiredCount);

        soundVolume = Mathf.Clamp01(soundVolume);
    }
}
