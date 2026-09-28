using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンをまたいで残したいゲームデータを管理します。
/// 所持金に加え、プレイヤーのインベントリと装備の一時データを保持します。
/// このデータはプレイ中のシーン移動用です。ゲーム終了後まで残すセーブ機能は別途追加します。
/// </summary>
[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 13;
    public const int MinSkillSlots = 3;
    public const int MaxSkillSlots = 7;

    [Header("初回起動時の所持金")]
    [Tooltip("PlayerMoneyController が開始金額を引き継がない場合に使う初期所持金です。通常は0のままでOKです。")]
    [SerializeField, Min(0)] private int defaultStartingMoney = 0;

    [Header("通常デバッグ")]
    [SerializeField] private bool showDebugLogs;

    [Header("シーン間インベントリ診断")]
    [Tooltip("オンなら、保存・復元・シーン移動に関する重要なログを常にConsoleへ表示します。")]
    [SerializeField] private bool alwaysLogSessionTransfer = true;

    [Tooltip("オンなら、保存・復元したアイテム1件ごとの詳細をConsoleへ表示します。")]
    [SerializeField] private bool logEachInventoryItem = true;

    public static GameSessionManager Instance { get; private set; }

    /// <summary>現在の所持金です。負の値にはなりません。</summary>
    public int CurrentMoney => currentMoney;

    /// <summary>これまで町へ持ち帰った死亡NPCの累計人数です。</summary>
    public int TotalDeadNpcCount => totalDeadNpcCount;

    /// <summary>これまで町へ生存状態で持ち帰った救出NPCの累計人数です。</summary>
    public int TotalRescuedNpcCount => totalRescuedNpcCount;

    /// <summary>生存状態で救出済みの特殊NPC ID一覧です。</summary>
    public IReadOnlyList<string> RescuedSpecialNpcIds =>
        rescuedSpecialNpcIds;

    /// <summary>所持金の初期化が済んでいるかどうかです。</summary>
    public bool HasInitializedMoney => hasInitializedMoney;

    /// <summary>インベントリ・装備の引き継ぎデータがあるかどうかです。</summary>
    public bool HasInventorySessionData => hasInventorySessionData;

    /// <summary>町のプレイヤー保管箱の引き継ぎデータがあるかどうかです。</summary>
    public bool HasPlayerItemBoxSessionData => hasPlayerItemBoxSessionData;

    /// <summary>所持金が変化した時に、現在の所持金を通知します。</summary>
    public event Action<int> MoneyChanged;

    /// <summary>墓場へ登録された死亡NPC累計人数が変化した時に通知します。</summary>
    public event Action<int> DeadNpcCountChanged;

    /// <summary>
    /// 生存救出NPCの累計人数、または救出済み特殊NPC一覧が変化した時に通知します。
    /// </summary>
    public event Action RescueProgressChanged;

    /// <summary>町の施設レベルが変化した時に、FacilityIdと新しいLevelを通知します。</summary>
    public event Action<string, int> FacilityLevelChanged;

    /// <summary>インベントリ・装備の引き継ぎデータを保存または復元した時に通知します。</summary>
    public event Action InventorySessionChanged;

    /// <summary>町のプレイヤー保管箱データが変化した時に通知します。</summary>
    public event Action PlayerItemBoxSessionChanged;

    /// <summary>ミッション受注・進行データが変化した時に通知します。</summary>
    public event Action MissionSessionChanged;

    /// <summary>スキルカードの取得・装備・スロット解放が変化した時に通知します。</summary>
    public event Action SkillSessionChanged;

    /// <summary>ストーリー進行状態またはStory Flagが変化した時に通知します。</summary>
    public event Action StorySessionChanged;

    /// <summary>酒場の利用回数や報酬受取状態が変化した時に通知します。</summary>
    public event Action TavernProgressChanged;

    /// <summary>シーン間で保持しているミッション数です。</summary>
    public int MissionSessionCount => missionSessionData.Count;

    /// <summary>シーン間で保持しているストーリークエスト数です。</summary>
    public int StorySessionCount => storySessionData.Count;

    /// <summary>現在ONになっているStory Flag一覧です。</summary>
    public IReadOnlyList<string> StoryFlags => storyFlags;

    /// <summary>酒場の現在進捗です。</summary>
    public TavernProgressData TavernProgress
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData;
        }
    }

    public int TavernRoundIndex
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.RoundIndex;
        }
    }

    public int TavernPersonalDrinkTotal
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.PersonalDrinkTotal;
        }
    }

    public int TavernPersonalDrinkThisRound
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.PersonalDrinkThisRound;
        }
    }

    public int TavernTreatEveryoneTotal
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.TreatEveryoneTotal;
        }
    }

    public int TavernTreatEveryoneThisRound
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.TreatEveryoneThisRound;
        }
    }

    public bool TavernPersonalDrinkRewardClaimed
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.PersonalDrinkRewardClaimed;
        }
    }

    public bool TavernTreatEveryoneRewardClaimed
    {
        get
        {
            EnsureTavernProgressData();
            return tavernProgressData.TreatEveryoneRewardClaimed;
        }
    }

    /// <summary>追跡中にしたいミッションIDです。空なら追跡なしです。</summary>
    public string TrackedMissionId => trackedMissionId;

    /// <summary>現在の出撃でResult判定対象になるミッションID一覧です。</summary>
    public IReadOnlyList<string> CurrentExpeditionMissionIds =>
        currentExpeditionMissionIds;

    /// <summary>現在出撃中のStage Idです。出撃していない時は空です。</summary>
    public string CurrentExpeditionStageId =>
        currentExpeditionStageId;

    /// <summary>現在解放されているスキルカード装備枠数です。</summary>
    public int UnlockedSkillSlotCount
    {
        get
        {
            EnsureSkillSessionData();
            return Mathf.Clamp(
                skillCardSessionData.UnlockedSlotCount,
                MinSkillSlots,
                MaxSkillSlots
            );
        }
    }

    /// <summary>永久取得済みのスキルカード一覧です。</summary>
    public IReadOnlyList<SkillCardData> OwnedSkillCards
    {
        get
        {
            EnsureSkillSessionData();
            return skillCardSessionData.OwnedCards;
        }
    }

    private int currentMoney;
    private bool hasInitializedMoney;
    private int totalDeadNpcCount;

    private int totalRescuedNpcCount;

    private readonly List<string> rescuedSpecialNpcIds =
        new List<string>();

    private PlayerInventorySessionData inventorySessionData =
        new PlayerInventorySessionData();

    private bool hasInventorySessionData;

    private PlayerItemBoxSessionData playerItemBoxSessionData =
        new PlayerItemBoxSessionData();

    private bool hasPlayerItemBoxSessionData;

    private readonly List<MissionSessionData> missionSessionData =
        new List<MissionSessionData>();

    private readonly List<StorySessionData> storySessionData =
        new List<StorySessionData>();

    private readonly List<string> storyFlags =
        new List<string>();

    private TavernProgressData tavernProgressData =
        new TavernProgressData();

    private readonly List<TownFacilitySessionData> facilitySessionData =
        new List<TownFacilitySessionData>();

    private string trackedMissionId = string.Empty;

    private readonly List<string> currentExpeditionMissionIds =
        new List<string>();

    private string currentExpeditionStageId = string.Empty;

    private SkillCardSessionData skillCardSessionData =
        new SkillCardSessionData();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LogTransfer(
            $"Awake: GameSessionManagerを作成しました。開始Scene={SceneManager.GetActiveScene().name}"
        );
    }

    private void Start()
    {
        // PlayerMoneyController が同じ開始シーンにある場合は、
        // そちらの Initial Money を優先して引き継げるように
        // Start 時点まで初期化を待ちます。
        EnsureMoneyInitialized();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ---------------------------------------------------------------------
    // 所持金
    // ---------------------------------------------------------------------

    /// <summary>
    /// 初期所持金がまだ未確定の時だけ設定します。
    /// PlayerMoneyController の旧 Starting Money を引き継ぐために使います。
    /// </summary>
    public bool TrySetInitialMoney(int amount)
    {
        if (hasInitializedMoney)
        {
            return false;
        }

        currentMoney = Mathf.Max(0, amount);
        hasInitializedMoney = true;
        NotifyMoneyChanged();

        Log($"初期所持金を {currentMoney:N0} に設定しました。");
        return true;
    }

    /// <summary>
    /// 初期化されていなければ、Default Starting Money を使って初期化します。
    /// </summary>
    public void EnsureMoneyInitialized()
    {
        if (hasInitializedMoney)
        {
            return;
        }

        TrySetInitialMoney(defaultStartingMoney);
    }

    /// <summary>指定金額を所持金へ加算します。</summary>
    public bool AddMoney(int amount)
    {
        EnsureMoneyInitialized();

        if (amount <= 0)
        {
            return false;
        }

        long nextMoney = (long)currentMoney + amount;

        SetMoneyInternal(
            nextMoney > int.MaxValue
                ? int.MaxValue
                : (int)nextMoney
        );

        Log($"所持金を {amount:N0} 増やしました。現在 {currentMoney:N0}");
        return true;
    }

    /// <summary>
    /// 指定金額を支払います。所持金が足りない場合は減らしません。
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        EnsureMoneyInitialized();

        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        if (currentMoney < amount)
        {
            Log($"所持金が不足しています。必要 {amount:N0} / 現在 {currentMoney:N0}");
            return false;
        }

        SetMoneyInternal(currentMoney - amount);

        Log($"{amount:N0} 支払いました。現在 {currentMoney:N0}");
        return true;
    }

    /// <summary>指定金額を所持しているか確認します。</summary>
    public bool CanAfford(int amount)
    {
        EnsureMoneyInitialized();
        return amount >= 0 && currentMoney >= amount;
    }

    /// <summary>所持金を直接設定します。ロード・デバッグ用です。</summary>
    public void SetMoney(int amount)
    {
        EnsureMoneyInitialized();
        SetMoneyInternal(Mathf.Max(0, amount));
    }

    // ---------------------------------------------------------------------
    // 墓場：死亡NPC累計
    // ---------------------------------------------------------------------

    /// <summary>
    /// 今回町へ持ち帰った死亡NPC数を、墓場の累計人数へ加算します。
    /// Result SceneからTown_Mainへ戻る直前に呼ぶ想定です。
    /// </summary>
    public bool AddDeadNpcCount(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        long nextCount = (long)totalDeadNpcCount + amount;
        totalDeadNpcCount = nextCount > int.MaxValue
            ? int.MaxValue
            : (int)nextCount;

        NotifyDeadNpcCountChanged();

        Log(
            $"墓場の死亡NPC数を {amount:N0} 人増やしました。" +
            $"累計 {totalDeadNpcCount:N0} 人"
        );

        return true;
    }

    /// <summary>
    /// 墓場の死亡NPC累計人数を直接設定します。ロード・デバッグ用です。
    /// </summary>
    public void SetTotalDeadNpcCount(int amount)
    {
        int safeValue = Mathf.Max(0, amount);

        if (totalDeadNpcCount == safeValue)
        {
            return;
        }

        totalDeadNpcCount = safeValue;
        NotifyDeadNpcCountChanged();
    }

    // ---------------------------------------------------------------------
    // 救出NPC：累計人数・特殊NPC
    // ---------------------------------------------------------------------

    /// <summary>
    /// 今回、生存状態で町へ持ち帰った救出NPC数を累計へ加算します。
    /// </summary>
    public bool AddRescuedNpcCount(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        long nextCount =
            (long)totalRescuedNpcCount +
            amount;

        totalRescuedNpcCount =
            nextCount > int.MaxValue
                ? int.MaxValue
                : (int)nextCount;

        NotifyRescueProgressChanged();

        Log(
            $"生存救出NPC数を {amount:N0} 人増やしました。" +
            $"累計 {totalRescuedNpcCount:N0} 人"
        );

        return true;
    }

    /// <summary>
    /// 生存救出NPCの累計人数を直接設定します。
    /// ロード・デバッグ用です。
    /// </summary>
    public void SetTotalRescuedNpcCount(int amount)
    {
        int safeValue =
            Mathf.Max(0, amount);

        if (totalRescuedNpcCount ==
            safeValue)
        {
            return;
        }

        totalRescuedNpcCount =
            safeValue;

        NotifyRescueProgressChanged();
    }

    /// <summary>
    /// 指定した特殊NPCを過去に救出済みか確認します。
    /// </summary>
    public bool HasRescuedSpecialNpc(
        string specialNpcId)
    {
        if (string.IsNullOrWhiteSpace(
                specialNpcId))
        {
            return false;
        }

        string target =
            specialNpcId.Trim();

        foreach (string id in
                 rescuedSpecialNpcIds)
        {
            if (string.Equals(
                    id,
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 特殊NPCを「救出済み」として永久登録します。
    /// 初回登録時だけtrueを返します。
    /// </summary>
    public bool RegisterRescuedSpecialNpc(
        string specialNpcId)
    {
        string normalized =
            specialNpcId?.Trim() ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                normalized) ||
            HasRescuedSpecialNpc(
                normalized))
        {
            return false;
        }

        rescuedSpecialNpcIds.Add(
            normalized
        );

        NotifyRescueProgressChanged();

        Log(
            $"特殊NPC救出登録: {normalized} / " +
            $"救出済み特殊NPC={rescuedSpecialNpcIds.Count}人"
        );

        return true;
    }

    // ---------------------------------------------------------------------
    // 町の施設アップグレード
    // ---------------------------------------------------------------------

    /// <summary>
    /// 指定FacilityIdの現在レベルを返します。
    /// まだ保存データが無い施設はdefaultLevelを返します。
    /// </summary>
    public int GetFacilityLevel(string facilityId, int defaultLevel = 1)
    {
        int safeDefault = Mathf.Max(1, defaultLevel);

        TownFacilitySessionData data =
            FindFacilitySessionData(facilityId);

        return data != null
            ? Mathf.Max(1, data.Level)
            : safeDefault;
    }

    public int GetFacilityLevel(TownFacilityUpgradeData facilityData)
    {
        if (facilityData == null)
        {
            return 1;
        }

        return GetFacilityLevel(
            facilityData.FacilityId,
            facilityData.StartingLevel
        );
    }

    /// <summary>
    /// 施設レベルを直接設定します。アップグレード・ロード用です。
    /// 同じ値の場合はtrueを返しますが、イベントは発行しません。
    /// </summary>
    public bool SetFacilityLevel(
        string facilityId,
        int level,
        int defaultLevel = 1)
    {
        string normalizedId = facilityId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            LogWarning("FacilityIdが空のため施設レベルを設定できません。");
            return false;
        }

        int safeDefault = Mathf.Max(1, defaultLevel);
        int safeLevel = Mathf.Max(safeDefault, level);

        TownFacilitySessionData data =
            FindFacilitySessionData(normalizedId);

        if (data == null)
        {
            data = new TownFacilitySessionData
            {
                FacilityId = normalizedId,
                Level = safeLevel
            };

            facilitySessionData.Add(data);
            FacilityLevelChanged?.Invoke(normalizedId, safeLevel);
            Log($"施設レベル登録: {normalizedId}=Lv{safeLevel}");
            return true;
        }

        if (data.Level == safeLevel)
        {
            return true;
        }

        data.Level = safeLevel;
        FacilityLevelChanged?.Invoke(normalizedId, safeLevel);
        Log($"施設レベル更新: {normalizedId}=Lv{safeLevel}");
        return true;
    }

    public bool SetFacilityLevel(
        TownFacilityUpgradeData facilityData,
        int level)
    {
        return facilityData != null && SetFacilityLevel(
            facilityData.FacilityId,
            level,
            facilityData.StartingLevel
        );
    }

    private TownFacilitySessionData FindFacilitySessionData(
        string facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return null;
        }

        string normalizedId = facilityId.Trim();

        foreach (TownFacilitySessionData data in facilitySessionData)
        {
            if (data != null &&
                string.Equals(
                    data.FacilityId,
                    normalizedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------
    // インベントリ・装備のシーン間引き継ぎ
    // ---------------------------------------------------------------------

    /// <summary>
    /// 現在のインベントリと装備中アイテムを、シーン移動用データとして保存します。
    /// ItemDataへの参照、マス位置、回転、個数、武器の保存残弾を保持します。
    /// </summary>
    public bool CapturePlayerInventory(
        InventoryController inventoryController,
        EquipmentController equipmentController)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (inventoryController == null || inventoryController.Grid == null)
        {
            LogTransferWarning(
                $"保存失敗: Scene={sceneName}。InventoryControllerまたはGridが見つかりません。" +
                " PlayerInventorySessionBridgeの参照を確認してください。"
            );
            return false;
        }

        InventoryGrid sourceGrid = inventoryController.Grid;

        LogTransfer(
            $"保存開始: Scene={sceneName} / Object={inventoryController.gameObject.name} / " +
            $"Grid={sourceGrid.Width}x{sourceGrid.Height} / 通常アイテム={sourceGrid.Items.Count}件 / " +
            $"EquipmentController={(equipmentController != null ? equipmentController.gameObject.name : "未設定")}"
        );

        PlayerInventorySessionData nextData =
            new PlayerInventorySessionData
            {
                GridWidth = Mathf.Max(1, sourceGrid.Width),
                GridHeight = Mathf.Max(1, sourceGrid.Height)
            };

        int skippedCount = 0;

        foreach (InventoryItem item in sourceGrid.Items)
        {
            SessionInventoryItemData itemData = CreateItemSnapshot(item);

            if (itemData != null)
            {
                nextData.InventoryItems.Add(itemData);
                LogSessionItem("保存", itemData, "通常インベントリ");
            }
            else
            {
                skippedCount++;
                LogTransferWarning(
                    "保存時に無効なInventoryItemを1件スキップしました。" +
                    " ItemData未設定またはAmountが0以下の可能性があります。"
                );
            }
        }

        if (equipmentController != null)
        {
            nextData.PrimaryWeaponItem = CreateItemSnapshot(
                equipmentController.PrimaryWeaponItem
            );

            nextData.HelmetItem = CreateItemSnapshot(
                equipmentController.HelmetItem
            );

            if (nextData.PrimaryWeaponItem != null)
            {
                LogSessionItem("保存", nextData.PrimaryWeaponItem, "装備: PrimaryWeapon");
            }

            if (nextData.HelmetItem != null)
            {
                LogSessionItem("保存", nextData.HelmetItem, "装備: Helmet");
            }
        }
        else
        {
            LogTransferWarning(
                "保存時にEquipmentControllerが未設定です。装備中アイテムは保存されません。"
            );
        }

        inventorySessionData = nextData;
        hasInventorySessionData = true;

        InventorySessionChanged?.Invoke();

        LogTransfer(
            $"保存完了: 通常={nextData.InventoryItems.Count}件 / " +
            $"武器={(nextData.PrimaryWeaponItem != null ? "あり" : "なし")} / " +
            $"ヘルメット={(nextData.HelmetItem != null ? "あり" : "なし")} / " +
            $"スキップ={skippedCount}件"
        );

        return true;
    }

    /// <summary>
    /// 保存済みのインベントリと装備を、現在シーンのControllerへ復元します。
    /// 復元先のInventoryControllerに入っていた開始アイテムは置き換えられます。
    /// </summary>
    public bool RestorePlayerInventory(
        InventoryController inventoryController,
        EquipmentController equipmentController,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        string sceneName = SceneManager.GetActiveScene().name;

        if (!hasInventorySessionData || inventorySessionData == null)
        {
            resultMessage = "引き継ぎ用のインベントリデータはまだありません。";
            LogTransferWarning(
                $"復元しません: Scene={sceneName}。{resultMessage} " +
                "移動元SceneにPlayerInventorySessionBridgeが付いているか、" +
                "SceneTransitionButtonのCapture Inventory Before Loadがオンか確認してください。"
            );
            return false;
        }

        if (inventoryController == null || inventoryController.Grid == null)
        {
            resultMessage = "復元先のInventoryControllerが見つかりません。";
            LogTransferWarning(
                $"復元失敗: Scene={sceneName}。{resultMessage} " +
                "TownPlayerInventoryにInventoryControllerとPlayerInventorySessionBridgeが付いているか確認してください。"
            );
            return false;
        }

        LogTransfer(
            $"復元開始: Scene={sceneName} / Object={inventoryController.gameObject.name} / " +
            $"保存Grid={inventorySessionData.GridWidth}x{inventorySessionData.GridHeight} / " +
            $"保存通常アイテム={inventorySessionData.InventoryItems.Count}件 / " +
            $"EquipmentController={(equipmentController != null ? equipmentController.gameObject.name : "未設定")}"
        );

        ClearCurrentInventoryAndEquipment(
            inventoryController,
            equipmentController
        );

        inventoryController.Grid.Initialize(
            Mathf.Max(1, inventorySessionData.GridWidth),
            Mathf.Max(1, inventorySessionData.GridHeight),
            true
        );

        int restoredCount = 0;
        int fallbackPlacedCount = 0;
        int failedCount = 0;

        foreach (SessionInventoryItemData savedItem in
                 inventorySessionData.InventoryItems)
        {
            RestorePlacementResult result = RestoreNormalInventoryItem(
                inventoryController,
                savedItem
            );

            switch (result)
            {
                case RestorePlacementResult.Exact:
                    restoredCount++;
                    break;

                case RestorePlacementResult.Fallback:
                    restoredCount++;
                    fallbackPlacedCount++;
                    break;

                default:
                    failedCount++;
                    break;
            }
        }

        RestoreEquipmentItem(
            inventoryController,
            equipmentController,
            EquipmentSlotType.PrimaryWeapon,
            inventorySessionData.PrimaryWeaponItem,
            ref restoredCount,
            ref fallbackPlacedCount,
            ref failedCount
        );

        RestoreEquipmentItem(
            inventoryController,
            equipmentController,
            EquipmentSlotType.Helmet,
            inventorySessionData.HelmetItem,
            ref restoredCount,
            ref fallbackPlacedCount,
            ref failedCount
        );

        InventorySessionChanged?.Invoke();

        resultMessage = failedCount == 0
            ? $"インベントリを復元しました。{restoredCount}件"
            : $"インベントリを復元しましたが、{failedCount}件を配置できませんでした。";

        if (fallbackPlacedCount > 0)
        {
            resultMessage +=
                $" {fallbackPlacedCount}件は空いている別のマスへ移動しました。";
        }

        int finalNormalCount = inventoryController.Grid.Items.Count;
        resultMessage += $" 復元先通常={finalNormalCount}件。";

        if (failedCount > 0)
        {
            LogTransferWarning(resultMessage);
        }
        else
        {
            LogTransfer(resultMessage);
        }

        return failedCount == 0;
    }

    /// <summary>
    /// シーン間引き継ぎ用のインベントリデータを消去します。
    /// デバッグや「最初から」に使います。
    /// </summary>
    public void ClearInventorySessionData()
    {
        inventorySessionData = new PlayerInventorySessionData();
        hasInventorySessionData = false;
        InventorySessionChanged?.Invoke();

        Log("インベントリ引き継ぎデータを消去しました。");
    }

    // ---------------------------------------------------------------------
    // 町のプレイヤー保管箱
    // ---------------------------------------------------------------------

    /// <summary>
    /// 町のプレイヤー保管箱の現在内容をシーン間セッションへ保存します。
    /// ItemData参照、配置、回転、個数、武器残弾・弾種・耐久・ジャム状態を保持します。
    /// </summary>
    public bool CapturePlayerItemBox(ItemBoxInventory itemBoxInventory)
    {
        if (itemBoxInventory == null || itemBoxInventory.Grid == null)
        {
            LogTransferWarning(
                "プレイヤー保管箱の保存に失敗しました。ItemBoxInventoryまたはGridがnullです。"
            );
            return false;
        }

        PlayerItemBoxSessionData nextData =
            new PlayerItemBoxSessionData
            {
                GridWidth = Mathf.Max(1, itemBoxInventory.Grid.Width),
                GridHeight = Mathf.Max(1, itemBoxInventory.Grid.Height)
            };

        int skippedCount = 0;

        foreach (InventoryItem item in itemBoxInventory.Grid.Items)
        {
            SessionInventoryItemData snapshot = CreateItemSnapshot(item);

            if (snapshot != null)
            {
                nextData.Items.Add(snapshot);
            }
            else
            {
                skippedCount++;
            }
        }

        playerItemBoxSessionData = nextData;
        hasPlayerItemBoxSessionData = true;
        PlayerItemBoxSessionChanged?.Invoke();

        LogTransfer(
            $"プレイヤー保管箱を保存しました。Grid={nextData.GridWidth}x{nextData.GridHeight} / " +
            $"Item={nextData.Items.Count}件 / Skip={skippedCount}件"
        );

        return true;
    }

    /// <summary>
    /// シーン間セッションに保存されているプレイヤー保管箱を復元します。
    /// </summary>
    public bool RestorePlayerItemBox(
        ItemBoxInventory itemBoxInventory,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!hasPlayerItemBoxSessionData || playerItemBoxSessionData == null)
        {
            resultMessage = "プレイヤー保管箱の保存データはまだありません。";
            return false;
        }

        if (itemBoxInventory == null)
        {
            resultMessage = "復元先のItemBoxInventoryが見つかりません。";
            return false;
        }

        List<InventoryItem> restoredItems = new List<InventoryItem>();
        int failedCount = 0;

        if (playerItemBoxSessionData.Items != null)
        {
            foreach (SessionInventoryItemData savedItem in
                     playerItemBoxSessionData.Items)
            {
                InventoryItem item = CreateRuntimeInventoryItem(savedItem);

                if (item != null)
                {
                    restoredItems.Add(item);
                }
                else
                {
                    failedCount++;
                }
            }
        }

        itemBoxInventory.RestoreInventoryFromSave(
            Mathf.Max(1, playerItemBoxSessionData.GridWidth),
            Mathf.Max(1, playerItemBoxSessionData.GridHeight),
            restoredItems
        );

        resultMessage =
            $"プレイヤー保管箱を復元しました。Item={restoredItems.Count}件";

        if (failedCount > 0)
        {
            resultMessage += $" / 復元失敗={failedCount}件";
        }

        LogTransfer(resultMessage);
        return failedCount == 0;
    }

    /// <summary>町のプレイヤー保管箱セッションを空にします。</summary>
    public void ClearPlayerItemBoxSessionData()
    {
        playerItemBoxSessionData = new PlayerItemBoxSessionData();
        hasPlayerItemBoxSessionData = false;
        PlayerItemBoxSessionChanged?.Invoke();
        LogTransfer("プレイヤー保管箱のセッションデータを消去しました。");
    }

    /// <summary>診断表示用のプレイヤー保管箱セッション概要です。</summary>
    public string GetPlayerItemBoxSessionSummary()
    {
        if (!hasPlayerItemBoxSessionData || playerItemBoxSessionData == null)
        {
            return "[PlayerItemBoxSession] 保存データなし";
        }

        int count = playerItemBoxSessionData.Items != null
            ? playerItemBoxSessionData.Items.Count
            : 0;

        return "[PlayerItemBoxSession] 保存データあり / " +
               $"Grid={playerItemBoxSessionData.GridWidth}x{playerItemBoxSessionData.GridHeight} / " +
               $"Item={count}件";
    }

    /// <summary>
    /// タイトル画面のニューゲーム開始時に、
    /// 所持金・インベントリ・装備・ミッションの一時状態を初期化します。
    /// 所持金は未初期化へ戻すため、開始SceneのPlayerMoneyControllerが
    /// Initial Moneyを設定できます。PlayerMoneyControllerが無い場合は、
    /// 次にEnsureMoneyInitialized()が呼ばれた時にDefault Starting Moneyを使用します。
    /// </summary>
    public void ResetForNewGame()
    {
        currentMoney = 0;
        hasInitializedMoney = false;
        totalDeadNpcCount = 0;
        totalRescuedNpcCount = 0;
        rescuedSpecialNpcIds.Clear();

        RescueProgressExpeditionSession.Clear();

        inventorySessionData = new PlayerInventorySessionData();
        hasInventorySessionData = false;

        playerItemBoxSessionData = new PlayerItemBoxSessionData();
        hasPlayerItemBoxSessionData = false;

        missionSessionData.Clear();
        storySessionData.Clear();
        storyFlags.Clear();
        tavernProgressData = new TavernProgressData();
        facilitySessionData.Clear();
        trackedMissionId = string.Empty;
        currentExpeditionMissionIds.Clear();
        currentExpeditionStageId = string.Empty;
        skillCardSessionData = new SkillCardSessionData();
        EnsureSkillSessionData();

        NotifyMoneyChanged();
        NotifyDeadNpcCountChanged();
        NotifyRescueProgressChanged();
        InventorySessionChanged?.Invoke();
        PlayerItemBoxSessionChanged?.Invoke();
        MissionSessionChanged?.Invoke();
        StorySessionChanged?.Invoke();
        TavernProgressChanged?.Invoke();
        SkillSessionChanged?.Invoke();

        LogTransfer("ニューゲーム用にGameSessionManagerを初期化しました。");
    }

    // ---------------------------------------------------------------------
    // スキルカード
    // ---------------------------------------------------------------------

    public SkillCardData GetEquippedSkillCard(int slotIndex)
    {
        EnsureSkillSessionData();

        if (slotIndex < 0 ||
            slotIndex >= UnlockedSkillSlotCount ||
            slotIndex >= skillCardSessionData.EquippedCards.Count)
        {
            return null;
        }

        return skillCardSessionData.EquippedCards[slotIndex];
    }

    public bool HasSkillCard(SkillCardData card)
    {
        if (card == null)
        {
            return false;
        }

        EnsureSkillSessionData();
        return skillCardSessionData.OwnedCards.Contains(card);
    }

    public bool IsSkillCardEquipped(SkillCardData card)
    {
        return FindEquippedSkillCardSlot(card) >= 0;
    }

    public int FindEquippedSkillCardSlot(SkillCardData card)
    {
        if (card == null)
        {
            return -1;
        }

        EnsureSkillSessionData();

        int unlocked = UnlockedSkillSlotCount;
        for (int i = 0; i < unlocked; i++)
        {
            if (skillCardSessionData.EquippedCards[i] == card)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// スキルカードを永久取得します。すでに所持している場合も成功扱いですが、
    /// wasNewlyUnlockedはfalseになります。
    /// </summary>
    public bool UnlockSkillCard(
        SkillCardData card,
        out bool wasNewlyUnlocked)
    {
        wasNewlyUnlocked = false;

        if (card == null || string.IsNullOrWhiteSpace(card.ItemId))
        {
            return false;
        }

        EnsureSkillSessionData();

        if (skillCardSessionData.OwnedCards.Contains(card))
        {
            return true;
        }

        foreach (SkillCardData owned in skillCardSessionData.OwnedCards)
        {
            if (owned != null &&
                string.Equals(
                    owned.ItemId,
                    card.ItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        skillCardSessionData.OwnedCards.Add(card);
        wasNewlyUnlocked = true;
        SkillSessionChanged?.Invoke();

        Log($"スキルカード取得: {card.DisplayName} ({card.ItemId})");
        return true;
    }

    public bool SetUnlockedSkillSlotCount(int slotCount)
    {
        EnsureSkillSessionData();

        int next = Mathf.Clamp(
            slotCount,
            MinSkillSlots,
            MaxSkillSlots
        );

        if (skillCardSessionData.UnlockedSlotCount == next)
        {
            return false;
        }

        skillCardSessionData.UnlockedSlotCount = next;
        SkillSessionChanged?.Invoke();
        Log($"スキル装備枠を {next}/{MaxSkillSlots} に変更しました。");
        return true;
    }

    public bool UnlockNextSkillSlot(int amount = 1)
    {
        if (amount <= 0 || UnlockedSkillSlotCount >= MaxSkillSlots)
        {
            return false;
        }

        return SetUnlockedSkillSlotCount(
            UnlockedSkillSlotCount + amount
        );
    }

    public bool TryEquipSkillCard(
        SkillCardData card,
        int slotIndex,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        EnsureSkillSessionData();

        if (card == null)
        {
            resultMessage = "装備するスキルカードが未設定です。";
            return false;
        }

        if (!HasSkillCard(card))
        {
            resultMessage = $"{card.DisplayName} はまだ取得していません。";
            return false;
        }

        if (slotIndex < 0 || slotIndex >= MaxSkillSlots)
        {
            resultMessage = "スキル装備枠が範囲外です。";
            return false;
        }

        if (slotIndex >= UnlockedSkillSlotCount)
        {
            resultMessage = $"スキル枠{slotIndex + 1}はまだ解放されていません。";
            return false;
        }

        int existingSlot = FindEquippedSkillCardSlot(card);
        if (existingSlot >= 0 && existingSlot != slotIndex)
        {
            skillCardSessionData.EquippedCards[existingSlot] = null;
        }

        skillCardSessionData.EquippedCards[slotIndex] = card;
        SkillSessionChanged?.Invoke();

        resultMessage =
            $"{card.DisplayName} をスキル枠{slotIndex + 1}に装備しました。";
        Log(resultMessage);
        return true;
    }

    public bool UnequipSkillCard(int slotIndex)
    {
        EnsureSkillSessionData();

        if (slotIndex < 0 ||
            slotIndex >= UnlockedSkillSlotCount ||
            slotIndex >= skillCardSessionData.EquippedCards.Count ||
            skillCardSessionData.EquippedCards[slotIndex] == null)
        {
            return false;
        }

        skillCardSessionData.EquippedCards[slotIndex] = null;
        SkillSessionChanged?.Invoke();
        return true;
    }

    public float GetSkillEffectAdditiveValue(SkillEffectType effectType)
    {
        EnsureSkillSessionData();

        float total = 0f;
        for (int i = 0; i < UnlockedSkillSlotCount; i++)
        {
            SkillCardData card = skillCardSessionData.EquippedCards[i];
            if (card != null)
            {
                total += card.GetTotalValue(effectType);
            }
        }

        return total;
    }

    private void EnsureSkillSessionData()
    {
        if (skillCardSessionData == null)
        {
            skillCardSessionData = new SkillCardSessionData();
        }

        skillCardSessionData.UnlockedSlotCount = Mathf.Clamp(
            skillCardSessionData.UnlockedSlotCount <= 0
                ? MinSkillSlots
                : skillCardSessionData.UnlockedSlotCount,
            MinSkillSlots,
            MaxSkillSlots
        );

        if (skillCardSessionData.OwnedCards == null)
        {
            skillCardSessionData.OwnedCards = new List<SkillCardData>();
        }

        if (skillCardSessionData.EquippedCards == null)
        {
            skillCardSessionData.EquippedCards = new List<SkillCardData>();
        }

        while (skillCardSessionData.EquippedCards.Count < MaxSkillSlots)
        {
            skillCardSessionData.EquippedCards.Add(null);
        }

        if (skillCardSessionData.EquippedCards.Count > MaxSkillSlots)
        {
            skillCardSessionData.EquippedCards.RemoveRange(
                MaxSkillSlots,
                skillCardSessionData.EquippedCards.Count - MaxSkillSlots
            );
        }
    }

    private void GrantMissionSkillRewards(MissionDefinition2D mission)
    {
        if (mission == null)
        {
            return;
        }

        if (mission.SkillCardRewards != null)
        {
            foreach (SkillCardData card in mission.SkillCardRewards)
            {
                if (card != null)
                {
                    UnlockSkillCard(card, out _);
                }
            }
        }

        if (mission.SkillSlotUnlockAmount > 0)
        {
            UnlockNextSkillSlot(mission.SkillSlotUnlockAmount);
        }
    }

    // ---------------------------------------------------------------------
    // ミッションのシーン間引き継ぎ
    // ---------------------------------------------------------------------

    /// <summary>
    /// 町の会話などからミッションを受注状態にします。
    /// 探索シーンへ戻った時、MissionSessionBridgeがMissionManager2Dへ反映します。
    /// </summary>
    /// <summary>
    /// 互換用。従来の呼び出しはNPC会話からの受注として扱います。
    /// </summary>
    public bool AcceptMission(
        MissionDefinition2D mission,
        bool trackAfterAccepting,
        out string resultMessage)
    {
        return AcceptMission(
            mission,
            trackAfterAccepting,
            MissionAcceptSource.Conversation,
            out resultMessage
        );
    }

    /// <summary>
    /// 受注元を記録してミッションを受注します。
    /// ConversationはNPC会話報酬、ExpeditionはResult報酬として使用します。
    /// </summary>
    public bool AcceptMission(
        MissionDefinition2D mission,
        bool trackAfterAccepting,
        MissionAcceptSource acceptSource,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (mission == null)
        {
            resultMessage = "受注するミッションが設定されていません。";
            LogMissionWarning(resultMessage);
            return false;
        }

        string missionId = GetMissionId(mission);

        if (string.IsNullOrWhiteSpace(missionId))
        {
            resultMessage =
                $"{mission.DisplayName} の Mission Id が空です。MissionDefinition2DでMission Idを設定してください。";
            LogMissionWarning(resultMessage);
            return false;
        }

        MissionSessionData data = GetOrCreateMissionSessionData(mission);

        if (data.State == MissionSessionState.Completed)
        {
            resultMessage = $"{mission.DisplayName} はすでに達成済みです。";
            LogMissionWarning(resultMessage);
            return false;
        }

        bool wasAlreadyInProgress =
            data.State == MissionSessionState.InProgress;

        if (wasAlreadyInProgress &&
            data.AcceptSource != MissionAcceptSource.Unknown &&
            acceptSource != MissionAcceptSource.Unknown &&
            data.AcceptSource != acceptSource)
        {
            resultMessage =
                $"{mission.DisplayName} はすでに別の受注元({data.AcceptSource})で受注済みです。";
            LogMissionWarning(resultMessage);
            return false;
        }

        data.Mission = mission;
        data.MissionId = missionId;
        data.DisplayName = mission.DisplayName;
        data.State = MissionSessionState.InProgress;
        data.RequiredAmount = Mathf.Max(1, mission.RequiredAmount);
        data.Progress = Mathf.Clamp(
            data.Progress,
            0,
            data.RequiredAmount
        );

        if (data.AcceptSource == MissionAcceptSource.Unknown &&
            acceptSource != MissionAcceptSource.Unknown)
        {
            data.AcceptSource = acceptSource;
        }

        if (trackAfterAccepting)
        {
            trackedMissionId = missionId;
        }

        MissionSessionChanged?.Invoke();

        resultMessage = wasAlreadyInProgress
            ? $"{mission.DisplayName} はすでに受注済みです。"
            : $"{mission.DisplayName} を受注しました。";

        resultMessage += $" 受注元={data.AcceptSource}";

        LogMission(resultMessage);
        return true;
    }

    /// <summary>
    /// 新しい出撃を開始します。前回の「今回の出撃ミッション」一覧をリセットします。
    /// </summary>
    public void BeginCurrentExpedition(string stageId)
    {
        currentExpeditionStageId = stageId?.Trim() ?? string.Empty;
        currentExpeditionMissionIds.Clear();

        // 前回出撃の「特殊NPCをエレベーターへ積んだ」一時記録を破棄します。
        RescueProgressExpeditionSession.Clear();

        // 1回出撃するごとに酒場の「1ラウンド利用回数」をリセットします。
        ResetTavernRoundUsage();

        MissionSessionChanged?.Invoke();

        LogMission(
            $"出撃開始: Stage={currentExpeditionStageId} / Result対象ミッションを初期化"
        );
    }

    /// <summary>
    /// 今回の出撃でResult判定するミッションを登録します。
    /// </summary>
    public bool RegisterCurrentExpeditionMission(
        MissionDefinition2D mission)
    {
        string missionId = GetMissionId(mission);

        if (string.IsNullOrWhiteSpace(missionId))
        {
            return false;
        }

        foreach (string registeredId in currentExpeditionMissionIds)
        {
            if (string.Equals(
                    registeredId,
                    missionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        currentExpeditionMissionIds.Add(missionId);
        MissionSessionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Resultから町へ戻る時などに、今回の出撃情報だけを破棄します。
    /// ミッション履歴そのものは残します。
    /// </summary>
    public void EndCurrentExpedition()
    {
        currentExpeditionMissionIds.Clear();
        currentExpeditionStageId = string.Empty;
        MissionSessionChanged?.Invoke();
        LogMission("今回の出撃情報を終了しました。");
    }

    /// <summary>
    /// 探索シーンのMissionManager2Dから現在の進行状態を保存します。
    /// シーン移動直前、またはMissionSessionBridgeのOnDisableから呼びます。
    /// 
    /// 重要：MissionManager2DのMissionsには「受注候補」も登録されているため、
    /// Inactive（未受注）のミッションは保存しません。
    /// 未受注まで保存すると、町で受注したデータをInactiveで上書きしてしまうためです。
    /// </summary>
    public bool CaptureMissionsFromManager(MissionManager2D missionManager)
    {
        if (missionManager == null)
        {
            LogMissionWarning(
                "ミッション保存失敗: MissionManager2Dが見つかりません。"
            );
            return false;
        }

        int capturedCount = 0;
        int skippedInactiveCount = 0;
        int skippedInvalidCount = 0;

        for (int i = 0; i < missionManager.MissionCount; i++)
        {
            MissionDefinition2D mission =
                missionManager.GetMissionDefinition(i);

            if (mission == null)
            {
                skippedInvalidCount++;
                continue;
            }

            string missionId = GetMissionId(mission);

            if (string.IsNullOrWhiteSpace(missionId))
            {
                skippedInvalidCount++;
                LogMissionWarning(
                    $"ミッション保存スキップ: {mission.name} のMission Idが空です。"
                );
                continue;
            }

            MissionProgressState2D managerState =
                missionManager.GetMissionState(i);

            // MissionManager2Dに登録されているだけの未受注ミッションは保存しない。
            // ここで保存すると、町で受注したInProgress情報をInactiveで上書きする原因になる。
            if (managerState == MissionProgressState2D.Inactive)
            {
                skippedInactiveCount++;
                LogMission(
                    $"未受注のため保存スキップ: {mission.DisplayName} / MissionId={missionId}"
                );
                continue;
            }

            MissionSessionData data = GetOrCreateMissionSessionData(mission);
            data.Mission = mission;
            data.MissionId = missionId;
            data.DisplayName = mission.DisplayName;
            data.Progress = Mathf.Max(0, missionManager.GetMissionProgress(i));
            data.RequiredAmount = Mathf.Max(
                1,
                missionManager.GetMissionRequiredAmount(i)
            );
            data.State = ConvertMissionState(managerState);

            if (missionManager.IsTrackedMission(i))
            {
                trackedMissionId = missionId;
            }

            capturedCount++;

            LogMission(
                $"保存: {data.DisplayName} / 状態={data.State} / " +
                $"進捗={data.Progress}/{data.RequiredAmount} / " +
                $"追跡={missionManager.IsTrackedMission(i)}"
            );
        }

        MissionSessionChanged?.Invoke();
        LogMission(
            $"ミッション保存完了: 受注済み={capturedCount}件 / " +
            $"未受注スキップ={skippedInactiveCount}件 / 無効スキップ={skippedInvalidCount}件"
        );
        return true;
    }

    /// <summary>
    /// 保存済みの受注ミッションを、現在シーンのMissionManager2Dへ反映します。
    /// </summary>
    public bool RestoreMissionsToManager(MissionManager2D missionManager)
    {
        if (missionManager == null)
        {
            LogMissionWarning(
                "ミッション復元失敗: MissionManager2Dが見つかりません。"
            );
            return false;
        }

        // Start All Missions On Startなどで作られたランタイム状態を消し、
        // セーブデータを正として復元します。
        missionManager.ClearAllMissionRuntimeStateForLoad();

        int restoredCount = 0;
        int missingCount = 0;

        foreach (MissionSessionData data in missionSessionData)
        {
            if (data == null ||
                string.IsNullOrWhiteSpace(data.MissionId) ||
                data.State == MissionSessionState.Inactive)
            {
                continue;
            }

            int missionIndex = FindMissionIndexById(
                missionManager,
                data.MissionId
            );

            if (missionIndex < 0)
            {
                missingCount++;
                LogMissionWarning(
                    $"ミッション復元スキップ: MissionId={data.MissionId} が、" +
                    "このシーンのMissionManager2Dに登録されていません。"
                );
                continue;
            }

            MissionDefinition2D mission =
                missionManager.GetMissionDefinition(missionIndex);

            MissionProgressState2D managerState =
                data.State == MissionSessionState.Completed
                    ? MissionProgressState2D.Completed
                    : MissionProgressState2D.InProgress;

            bool shouldTrack =
                managerState == MissionProgressState2D.InProgress &&
                string.Equals(
                    trackedMissionId,
                    data.MissionId,
                    StringComparison.OrdinalIgnoreCase
                );

            bool restored = missionManager.RestoreMissionState(
                missionIndex,
                managerState,
                data.Progress,
                shouldTrack
            );

            if (!restored)
            {
                LogMissionWarning(
                    $"ミッション復元失敗: {data.DisplayName}"
                );
                continue;
            }

            restoredCount++;
            LogMission(
                $"復元: {(mission != null ? mission.DisplayName : data.DisplayName)} / " +
                $"MissionId={data.MissionId} / 状態={data.State} / " +
                $"進捗={data.Progress}/{data.RequiredAmount} / 追跡={shouldTrack}"
            );
        }

        LogMission(
            $"ミッション復元完了: 反映={restoredCount}件 / 未登録={missingCount}件"
        );

        return restoredCount > 0 || missingCount == 0;
    }

    /// <summary>
    /// 町の報告処理などから、ミッション報酬を受け取れる状態か確認します。
    /// requireObjectiveCompletedがtrueの場合、進捗が必要数未満のミッションは報酬を受け取れません。
    /// </summary>
    public bool CanClaimMissionReward(
        MissionDefinition2D mission,
        bool requireObjectiveCompleted,
        out string resultMessage)
    {
        return CanClaimMissionReward(
            mission,
            requireObjectiveCompleted,
            MissionAcceptSource.Unknown,
            out resultMessage
        );
    }

    /// <summary>
    /// 指定した受注元でのみ報酬受取を許可します。
    /// expectedSource=Unknownなら従来どおり受注元を問いません。
    /// 古いセーブデータ由来のUnknownは、Conversation要求時のみ互換のため許可します。
    /// </summary>
    public bool CanClaimMissionReward(
        MissionDefinition2D mission,
        bool requireObjectiveCompleted,
        MissionAcceptSource expectedSource,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (mission == null)
        {
            resultMessage = "報酬確認用のミッションが設定されていません。";
            LogMissionWarning(resultMessage);
            return false;
        }

        string missionId = GetMissionId(mission);

        if (string.IsNullOrWhiteSpace(missionId))
        {
            resultMessage =
                $"{mission.DisplayName} の Mission Id が空です。MissionDefinition2DでMission Idを設定してください。";
            LogMissionWarning(resultMessage);
            return false;
        }

        MissionSessionData data = FindMissionSessionData(missionId);

        if (data == null || data.State == MissionSessionState.Inactive)
        {
            resultMessage = $"{mission.DisplayName} はまだ受注していません。";
            LogMissionWarning(resultMessage);
            return false;
        }

        if (expectedSource != MissionAcceptSource.Unknown)
        {
            bool sourceMatches = data.AcceptSource == expectedSource;

            // 旧Saveには受注元が無いため、Unknownは会話ミッションとしてのみ互換扱い。
            if (!sourceMatches &&
                data.AcceptSource == MissionAcceptSource.Unknown &&
                expectedSource == MissionAcceptSource.Conversation)
            {
                data.AcceptSource = MissionAcceptSource.Conversation;
                sourceMatches = true;
            }

            if (!sourceMatches)
            {
                resultMessage =
                    expectedSource == MissionAcceptSource.Conversation
                        ? $"{mission.DisplayName} は出撃パネルから受注したミッションのため、NPC会話では報酬を受け取れません。"
                        : $"{mission.DisplayName} はNPC会話から受注したミッションのため、リザルト画面では報酬を受け取れません。";

                LogMissionWarning(resultMessage);
                return false;
            }
        }

        if (data.RewardClaimed)
        {
            resultMessage = $"{mission.DisplayName} の報酬はすでに受け取り済みです。";
            LogMissionWarning(resultMessage);
            return false;
        }

        if (requireObjectiveCompleted && !IsMissionObjectiveComplete(data))
        {
            resultMessage =
                $"{mission.DisplayName} はまだ達成していません。進捗 {Mathf.Max(0, data.Progress)} / {Mathf.Max(1, data.RequiredAmount)}";
            LogMissionWarning(resultMessage);
            return false;
        }

        resultMessage = $"{mission.DisplayName} の報酬を受け取れます。";
        return true;
    }

    public bool MarkMissionRewardClaimed(
        MissionDefinition2D mission,
        out string resultMessage)
    {
        return MarkMissionRewardClaimed(
            mission,
            MissionAcceptSource.Unknown,
            out resultMessage
        );
    }

    /// <summary>
    /// 指定受注元のミッションだけを報酬受取済みにします。
    /// </summary>
    public bool MarkMissionRewardClaimed(
        MissionDefinition2D mission,
        MissionAcceptSource expectedSource,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (mission == null)
        {
            resultMessage = "報酬受け取り済みにするミッションが設定されていません。";
            LogMissionWarning(resultMessage);
            return false;
        }

        string missionId = GetMissionId(mission);

        if (string.IsNullOrWhiteSpace(missionId))
        {
            resultMessage =
                $"{mission.DisplayName} の Mission Id が空です。MissionDefinition2DでMission Idを設定してください。";
            LogMissionWarning(resultMessage);
            return false;
        }

        MissionSessionData data = FindMissionSessionData(missionId);

        if (data == null)
        {
            resultMessage = $"{mission.DisplayName} はまだ受注していません。";
            LogMissionWarning(resultMessage);
            return false;
        }

        if (expectedSource != MissionAcceptSource.Unknown)
        {
            bool sourceMatches = data.AcceptSource == expectedSource;

            if (!sourceMatches &&
                data.AcceptSource == MissionAcceptSource.Unknown &&
                expectedSource == MissionAcceptSource.Conversation)
            {
                data.AcceptSource = MissionAcceptSource.Conversation;
                sourceMatches = true;
            }

            if (!sourceMatches)
            {
                resultMessage =
                    $"{mission.DisplayName} の受注元が一致しないため、報酬受取済みにできません。";
                LogMissionWarning(resultMessage);
                return false;
            }
        }

        if (data.RewardClaimed)
        {
            resultMessage = $"{mission.DisplayName} の報酬はすでに受け取り済みです。";
            LogMissionWarning(resultMessage);
            return false;
        }

        data.Mission = mission;
        data.MissionId = missionId;
        data.DisplayName = mission.DisplayName;
        data.RequiredAmount = Mathf.Max(1, data.RequiredAmount);
        data.Progress = Mathf.Max(data.Progress, data.RequiredAmount);
        data.State = MissionSessionState.Completed;
        data.RewardClaimed = true;

        if (string.Equals(
                trackedMissionId,
                missionId,
                StringComparison.OrdinalIgnoreCase))
        {
            trackedMissionId = string.Empty;
        }

        GrantMissionSkillRewards(mission);
        MissionSessionChanged?.Invoke();

        resultMessage = $"{mission.DisplayName} を報酬受け取り済みにしました。";
        LogMission(resultMessage);
        return true;
    }

    /// <summary>
    /// Result Scene専用。出撃ミッションのMoney / Item / Skill報酬を1回だけ付与します。
    /// Item報酬はシーン間引き継ぎ用Inventoryへ追加されるため、Result SceneにInventory UIは不要です。
    /// </summary>
    public bool TryClaimExpeditionMissionReward(
        MissionDefinition2D mission,
        out int grantedMoney,
        out string resultMessage)
    {
        grantedMoney = 0;
        resultMessage = string.Empty;

        if (!CanClaimMissionReward(
                mission,
                true,
                MissionAcceptSource.Expedition,
                out resultMessage))
        {
            return false;
        }

        PlayerInventorySessionData nextInventory =
            CloneInventorySessionData(
                inventorySessionData ?? new PlayerInventorySessionData()
            );

        if (nextInventory.InventoryItems == null)
        {
            nextInventory.InventoryItems =
                new List<SessionInventoryItemData>();
        }

        // 出撃パネル由来のCollect Itemミッションだけ、設定がONなら
        // Resultで報酬を受け取るタイミングに必要数を回収品として消費します。
        // Clone側から先に減らすため、その後の報酬Item配置に失敗しても
        // 実際のSession Inventoryは変更されません。
        if (mission.ObjectiveType == MissionObjectiveType2D.CollectItem &&
            mission.ConsumeCollectedItemsOnResult)
        {
            if (mission.RequiredItem == null)
            {
                resultMessage =
                    $"{mission.DisplayName} のRequired Itemが設定されていないため、回収品を処理できません。";
                LogMissionWarning(resultMessage);
                return false;
            }

            if (!TryConsumeItemFromSessionInventory(
                    nextInventory,
                    mission.RequiredItem,
                    mission.RequiredAmount,
                    out int consumedAmount))
            {
                resultMessage =
                    $"回収品『{mission.RequiredItem.DisplayName}』が不足しています。" +
                    $" 必要={mission.RequiredAmount} / 消費可能={consumedAmount}。";
                LogMissionWarning(resultMessage);
                return false;
            }
        }

        if (mission.ExpeditionItemRewards != null)
        {
            foreach (MissionRewardItem reward in mission.ExpeditionItemRewards)
            {
                if (reward == null ||
                    reward.ItemData == null ||
                    reward.Amount <= 0)
                {
                    continue;
                }

                if (!TryAddRewardItemToSessionInventory(
                        nextInventory,
                        reward.ItemData,
                        reward.Amount))
                {
                    resultMessage =
                        $"報酬アイテム『{reward.ItemData.DisplayName}』を入れる空きがありません。" +
                        " インベントリを整理してから再度報酬を受け取ってください。";
                    LogMissionWarning(resultMessage);
                    return false;
                }
            }
        }

        // ここまででItem報酬をすべて配置できることを確認済みです。
        // 先にRewardClaimedを確定し、その後に失敗しないMoney / Inventory反映を行います。
        // これにより報酬の一部だけが付与される状態を避けます。
        if (!MarkMissionRewardClaimed(
                mission,
                MissionAcceptSource.Expedition,
                out string markMessage))
        {
            resultMessage = markMessage;
            return false;
        }

        inventorySessionData = nextInventory;
        hasInventorySessionData = true;
        InventorySessionChanged?.Invoke();

        int moneyReward = Mathf.Max(0, mission.ExpeditionMoneyReward);

        if (moneyReward > 0)
        {
            AddMoney(moneyReward);
            grantedMoney = moneyReward;
        }

        resultMessage =
            $"{mission.DisplayName} の出撃ミッション報酬を受け取りました。";

        return true;
    }

    public bool IsMissionRewardClaimed(string missionId)
    {
        MissionSessionData data = FindMissionSessionData(missionId);
        return data != null && data.RewardClaimed;
    }

    private static bool IsMissionObjectiveComplete(MissionSessionData data)
    {
        if (data == null)
        {
            return false;
        }

        if (data.State == MissionSessionState.Completed)
        {
            return true;
        }

        return data.Progress >= Mathf.Max(1, data.RequiredAmount);
    }

    public bool HasMissionSession(string missionId)
    {
        return FindMissionSessionData(missionId) != null;
    }

    public bool TryGetMissionSession(
        string missionId,
        out MissionSessionData missionData)
    {
        missionData = FindMissionSessionData(missionId);
        return missionData != null;
    }

    public void ClearMissionSessionData()
    {
        missionSessionData.Clear();
        trackedMissionId = string.Empty;
        currentExpeditionMissionIds.Clear();
        currentExpeditionStageId = string.Empty;
        MissionSessionChanged?.Invoke();
        LogMission("ミッション引き継ぎデータを消去しました。");
    }

    public string GetMissionSessionSummary()
    {
        if (missionSessionData.Count == 0)
        {
            return "[MissionSession] 保存データなし";
        }

        List<string> lines = new List<string>();

        foreach (MissionSessionData data in missionSessionData)
        {
            if (data == null)
            {
                continue;
            }

            lines.Add(
                $"{data.DisplayName}(ID={data.MissionId}, 状態={data.State}, " +
                $"進捗={data.Progress}/{data.RequiredAmount}, 受注元={data.AcceptSource}, " +
                $"報酬={(data.RewardClaimed ? "受取済み" : "未受取")})"
            );
        }

        return "[MissionSession] 保存データあり / 追跡=" +
               (string.IsNullOrWhiteSpace(trackedMissionId)
                   ? "なし"
                   : trackedMissionId) +
               " / " +
               string.Join(" / ", lines);
    }

    // ---------------------------------------------------------------------
    // 酒場進捗
    // ---------------------------------------------------------------------

    private void EnsureTavernProgressData()
    {
        if (tavernProgressData == null)
        {
            tavernProgressData = new TavernProgressData();
        }

        tavernProgressData.RoundIndex =
            Mathf.Max(0, tavernProgressData.RoundIndex);

        tavernProgressData.PersonalDrinkTotal =
            Mathf.Max(0, tavernProgressData.PersonalDrinkTotal);

        tavernProgressData.PersonalDrinkThisRound =
            Mathf.Max(0, tavernProgressData.PersonalDrinkThisRound);

        tavernProgressData.TreatEveryoneTotal =
            Mathf.Max(0, tavernProgressData.TreatEveryoneTotal);

        tavernProgressData.TreatEveryoneThisRound =
            Mathf.Max(0, tavernProgressData.TreatEveryoneThisRound);
    }

    public bool CanUseTavernPersonalDrink(int perRoundLimit)
    {
        EnsureTavernProgressData();

        int limit = Mathf.Max(1, perRoundLimit);
        return tavernProgressData.PersonalDrinkThisRound < limit;
    }

    public bool CanUseTavernTreatEveryone(int perRoundLimit)
    {
        EnsureTavernProgressData();

        int limit = Mathf.Max(1, perRoundLimit);
        return tavernProgressData.TreatEveryoneThisRound < limit;
    }

    /// <summary>
    /// 一人飲み1回分を記録します。
    /// 料金の支払いはTavernController側で成功した後に呼び出します。
    /// </summary>
    public bool TryRegisterTavernPersonalDrink(
        int perRoundLimit,
        out string resultMessage)
    {
        EnsureTavernProgressData();

        int limit = Mathf.Max(1, perRoundLimit);

        if (tavernProgressData.PersonalDrinkThisRound >= limit)
        {
            resultMessage =
                $"一人飲みはこのラウンドですでに上限 " +
                $"{limit} 回に達しています。";
            return false;
        }

        tavernProgressData.PersonalDrinkThisRound++;
        tavernProgressData.PersonalDrinkTotal++;

        TavernProgressChanged?.Invoke();

        resultMessage =
            $"一人飲み: 今ラウンド " +
            $"{tavernProgressData.PersonalDrinkThisRound}/{limit} / " +
            $"累計 {tavernProgressData.PersonalDrinkTotal} 回";

        Log(resultMessage);
        return true;
    }

    /// <summary>
    /// みんなにおごる1回分を記録します。
    /// 料金の支払いはTavernController側で成功した後に呼び出します。
    /// </summary>
    public bool TryRegisterTavernTreatEveryone(
        int perRoundLimit,
        out string resultMessage)
    {
        EnsureTavernProgressData();

        int limit = Mathf.Max(1, perRoundLimit);

        if (tavernProgressData.TreatEveryoneThisRound >= limit)
        {
            resultMessage =
                $"みんなにおごるはこのラウンドですでに上限 " +
                $"{limit} 回に達しています。";
            return false;
        }

        tavernProgressData.TreatEveryoneThisRound++;
        tavernProgressData.TreatEveryoneTotal++;

        TavernProgressChanged?.Invoke();

        resultMessage =
            $"みんなにおごる: 今ラウンド " +
            $"{tavernProgressData.TreatEveryoneThisRound}/{limit} / " +
            $"累計 {tavernProgressData.TreatEveryoneTotal} 回";

        Log(resultMessage);
        return true;
    }

    /// <summary>
    /// 新しい出撃ラウンド開始時に、そのラウンド内の酒場利用回数を0へ戻します。
    /// 累計回数と報酬受取状態は残ります。
    /// </summary>
    public void ResetTavernRoundUsage()
    {
        EnsureTavernProgressData();

        tavernProgressData.RoundIndex++;
        tavernProgressData.PersonalDrinkThisRound = 0;
        tavernProgressData.TreatEveryoneThisRound = 0;

        TavernProgressChanged?.Invoke();

        Log(
            $"酒場ラウンド更新: Round={tavernProgressData.RoundIndex} / " +
            "ラウンド内利用回数をリセット"
        );
    }

    public bool MarkTavernPersonalDrinkRewardClaimed()
    {
        EnsureTavernProgressData();

        if (tavernProgressData.PersonalDrinkRewardClaimed)
        {
            return false;
        }

        tavernProgressData.PersonalDrinkRewardClaimed = true;
        TavernProgressChanged?.Invoke();
        return true;
    }

    public bool MarkTavernTreatEveryoneRewardClaimed()
    {
        EnsureTavernProgressData();

        if (tavernProgressData.TreatEveryoneRewardClaimed)
        {
            return false;
        }

        tavernProgressData.TreatEveryoneRewardClaimed = true;
        TavernProgressChanged?.Invoke();
        return true;
    }

    public void ClearTavernProgress()
    {
        tavernProgressData = new TavernProgressData();
        TavernProgressChanged?.Invoke();
        Log("酒場進捗を初期化しました。");
    }

    // ---------------------------------------------------------------------
    // ストーリー進行
    // ---------------------------------------------------------------------

    /// <summary>
    /// StoryQuestDefinitionが未登録ならLocked状態で作成します。
    /// </summary>
    public StorySessionData EnsureStorySessionData(
        StoryQuestDefinition story)
    {
        if (story == null || string.IsNullOrWhiteSpace(story.StoryId))
        {
            return null;
        }

        string storyId = story.StoryId.Trim();
        StorySessionData existing = FindStorySessionData(storyId);

        if (existing != null)
        {
            existing.Story = story;
            existing.DisplayName = story.DisplayName;
            return existing;
        }

        StorySessionData created = new StorySessionData
        {
            Story = story,
            StoryId = storyId,
            DisplayName = story.DisplayName,
            State = StoryQuestState.Locked
        };

        storySessionData.Add(created);
        StorySessionChanged?.Invoke();
        Log($"ストーリー状態を登録: {created.DisplayName} / ID={storyId} / Locked");
        return created;
    }

    public bool TryGetStorySession(
        string storyId,
        out StorySessionData storyData)
    {
        storyData = FindStorySessionData(storyId);
        return storyData != null;
    }

    public StoryQuestState GetStoryState(string storyId)
    {
        StorySessionData data = FindStorySessionData(storyId);
        return data != null
            ? data.State
            : StoryQuestState.Locked;
    }

    public bool SetStoryState(
        StoryQuestDefinition story,
        StoryQuestState state)
    {
        StorySessionData data = EnsureStorySessionData(story);

        if (data == null)
        {
            return false;
        }

        if (data.State == state)
        {
            return true;
        }

        data.Story = story;
        data.DisplayName = story.DisplayName;
        data.State = state;
        StorySessionChanged?.Invoke();

        Log(
            $"ストーリー状態更新: {data.DisplayName} / " +
            $"ID={data.StoryId} / 状態={state}"
        );
        return true;
    }

    /// <summary>
    /// ScriptableObject参照を持てないロード直後などにIDだけで状態を設定します。
    /// </summary>
    public bool SetStoryStateById(
        string storyId,
        string displayName,
        StoryQuestState state)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            return false;
        }

        string normalizedId = storyId.Trim();
        StorySessionData data = FindStorySessionData(normalizedId);

        if (data == null)
        {
            data = new StorySessionData
            {
                Story = null,
                StoryId = normalizedId,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? normalizedId
                    : displayName.Trim(),
                State = state
            };
            storySessionData.Add(data);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                data.DisplayName = displayName.Trim();
            }

            data.State = state;
        }

        StorySessionChanged?.Invoke();
        return true;
    }

    public bool HasStoryFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        string target = flagId.Trim();

        foreach (string flag in storyFlags)
        {
            if (string.Equals(
                    flag,
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Story FlagをON/OFFします。実際に状態が変わった時だけtrueを返します。
    /// </summary>
    public bool SetStoryFlag(string flagId, bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        string normalized = flagId.Trim();
        int foundIndex = -1;

        for (int i = 0; i < storyFlags.Count; i++)
        {
            if (string.Equals(
                    storyFlags[i],
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                foundIndex = i;
                break;
            }
        }

        if (enabled)
        {
            if (foundIndex >= 0)
            {
                return false;
            }

            storyFlags.Add(normalized);
        }
        else
        {
            if (foundIndex < 0)
            {
                return false;
            }

            storyFlags.RemoveAt(foundIndex);
        }

        StorySessionChanged?.Invoke();
        Log($"Story Flag更新: {normalized} = {(enabled ? "ON" : "OFF")}");
        return true;
    }

    public void ClearStorySessionData()
    {
        storySessionData.Clear();
        storyFlags.Clear();
        StorySessionChanged?.Invoke();
        Log("ストーリー進行データを消去しました。");
    }

    public string GetStorySessionSummary()
    {
        List<string> lines = new List<string>();

        foreach (StorySessionData data in storySessionData)
        {
            if (data == null)
            {
                continue;
            }

            lines.Add(
                $"{data.DisplayName}(ID={data.StoryId}, 状態={data.State})"
            );
        }

        string flagText = storyFlags.Count > 0
            ? string.Join(", ", storyFlags)
            : "なし";

        return
            $"[StorySession] Stories={storySessionData.Count}件 / " +
            $"Flags={flagText} / " +
            string.Join(" / ", lines);
    }

    private StorySessionData FindStorySessionData(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            return null;
        }

        string target = storyId.Trim();

        foreach (StorySessionData data in storySessionData)
        {
            if (data != null &&
                string.Equals(
                    data.StoryId,
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        return null;
    }

    private SavedTavernProgressData CreateSavedTavernProgressData()
    {
        EnsureTavernProgressData();

        return new SavedTavernProgressData
        {
            RoundIndex = Mathf.Max(0, tavernProgressData.RoundIndex),
            PersonalDrinkTotal =
                Mathf.Max(0, tavernProgressData.PersonalDrinkTotal),
            PersonalDrinkThisRound =
                Mathf.Max(0, tavernProgressData.PersonalDrinkThisRound),
            TreatEveryoneTotal =
                Mathf.Max(0, tavernProgressData.TreatEveryoneTotal),
            TreatEveryoneThisRound =
                Mathf.Max(0, tavernProgressData.TreatEveryoneThisRound),
            PersonalDrinkRewardClaimed =
                tavernProgressData.PersonalDrinkRewardClaimed,
            TreatEveryoneRewardClaimed =
                tavernProgressData.TreatEveryoneRewardClaimed
        };
    }

    private void RestoreTavernProgressData(
        SavedTavernProgressData saved)
    {
        tavernProgressData = new TavernProgressData();

        if (saved == null)
        {
            EnsureTavernProgressData();
            return;
        }

        tavernProgressData.RoundIndex =
            Mathf.Max(0, saved.RoundIndex);

        tavernProgressData.PersonalDrinkTotal =
            Mathf.Max(0, saved.PersonalDrinkTotal);

        tavernProgressData.PersonalDrinkThisRound =
            Mathf.Max(0, saved.PersonalDrinkThisRound);

        tavernProgressData.TreatEveryoneTotal =
            Mathf.Max(0, saved.TreatEveryoneTotal);

        tavernProgressData.TreatEveryoneThisRound =
            Mathf.Max(0, saved.TreatEveryoneThisRound);

        tavernProgressData.PersonalDrinkRewardClaimed =
            saved.PersonalDrinkRewardClaimed;

        tavernProgressData.TreatEveryoneRewardClaimed =
            saved.TreatEveryoneRewardClaimed;

        EnsureTavernProgressData();
    }

    // ---------------------------------------------------------------------
    // JSON本セーブ用の書き出し・読み込み
    // ---------------------------------------------------------------------

    /// <summary>
    /// 現在GameSessionManagerが保持している状態を、JSON保存可能なデータへ変換します。
    /// 保存直前にPlayerInventorySessionBridge / MissionSessionBridgeのCaptureToSessionを
    /// 呼んでおくと、現在シーンの最新状態が入ります。
    /// </summary>
    public SaveGameData CreateSaveGameData(string savedSceneName)
    {
        EnsureMoneyInitialized();

        SaveGameData saveData = new SaveGameData
        {
            SaveVersion = CurrentSaveVersion,
            SavedAtUtc = DateTime.UtcNow.ToString("O"),
            SavedSceneName = savedSceneName ?? string.Empty,
            Money = Mathf.Max(0, currentMoney),
            TotalDeadNpcCount = Mathf.Max(0, totalDeadNpcCount),
            TotalRescuedNpcCount = Mathf.Max(0, totalRescuedNpcCount),
            TrackedMissionId = trackedMissionId ?? string.Empty
        };

        EnsureSkillSessionData();
        saveData.SkillCards = CreateSavedSkillCardData();
        saveData.Tavern = CreateSavedTavernProgressData();

        PlayerInventorySessionData sourceInventory =
            inventorySessionData ?? new PlayerInventorySessionData();

        saveData.PlayerInventory = new SavedPlayerInventoryData
        {
            GridWidth = Mathf.Max(1, sourceInventory.GridWidth),
            GridHeight = Mathf.Max(1, sourceInventory.GridHeight),
            PrimaryWeapon = CreateSavedInventoryItem(
                sourceInventory.PrimaryWeaponItem
            ),
            Helmet = CreateSavedInventoryItem(
                sourceInventory.HelmetItem
            )
        };

        if (sourceInventory.InventoryItems != null)
        {
            foreach (SessionInventoryItemData item in
                     sourceInventory.InventoryItems)
            {
                SavedInventoryItemData savedItem =
                    CreateSavedInventoryItem(item);

                if (savedItem != null)
                {
                    saveData.PlayerInventory.InventoryItems.Add(savedItem);
                }
            }
        }

        saveData.PlayerItemBox = new SavedPlayerItemBoxData
        {
            HasData = hasPlayerItemBoxSessionData,
            GridWidth = playerItemBoxSessionData != null
                ? Mathf.Max(1, playerItemBoxSessionData.GridWidth)
                : 10,
            GridHeight = playerItemBoxSessionData != null
                ? Mathf.Max(1, playerItemBoxSessionData.GridHeight)
                : 12
        };

        if (hasPlayerItemBoxSessionData &&
            playerItemBoxSessionData != null &&
            playerItemBoxSessionData.Items != null)
        {
            foreach (SessionInventoryItemData item in
                     playerItemBoxSessionData.Items)
            {
                SavedInventoryItemData savedItem =
                    CreateSavedInventoryItem(item);

                if (savedItem != null)
                {
                    saveData.PlayerItemBox.Items.Add(savedItem);
                }
            }
        }

        foreach (MissionSessionData mission in missionSessionData)
        {
            if (mission == null ||
                string.IsNullOrWhiteSpace(mission.MissionId))
            {
                continue;
            }

            saveData.Missions.Add(new SavedMissionData
            {
                MissionId = mission.MissionId.Trim(),
                DisplayName = mission.DisplayName ?? string.Empty,
                State = mission.State,
                Progress = Mathf.Max(0, mission.Progress),
                RequiredAmount = Mathf.Max(1, mission.RequiredAmount),
                RewardClaimed = mission.RewardClaimed,
                AcceptSource = mission.AcceptSource
            });
        }

        foreach (StorySessionData story in storySessionData)
        {
            if (story == null ||
                string.IsNullOrWhiteSpace(story.StoryId))
            {
                continue;
            }

            saveData.Stories.Add(new SavedStoryQuestData
            {
                StoryId = story.StoryId.Trim(),
                DisplayName = story.DisplayName ?? string.Empty,
                State = story.State
            });
        }

        foreach (string flag in storyFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                saveData.StoryFlags.Add(flag.Trim());
            }
        }

        foreach (string specialNpcId in
                 rescuedSpecialNpcIds)
        {
            if (!string.IsNullOrWhiteSpace(
                    specialNpcId))
            {
                saveData.RescuedSpecialNpcIds.Add(
                    specialNpcId.Trim()
                );
            }
        }

        foreach (TownFacilitySessionData facility in facilitySessionData)
        {
            if (facility == null ||
                string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                continue;
            }

            saveData.FacilityLevels.Add(
                new SavedTownFacilityLevelData
                {
                    FacilityId = facility.FacilityId.Trim(),
                    Level = Mathf.Max(1, facility.Level)
                }
            );
        }

        LogTransfer(
            $"本セーブ用データ作成: Money={saveData.Money:N0} / " +
            $"DeadNpcTotal={saveData.TotalDeadNpcCount:N0}人 / " +
            $"RescuedNpcTotal={saveData.TotalRescuedNpcCount:N0}人 / " +
            $"SpecialRescue={saveData.RescuedSpecialNpcIds.Count}人 / " +
            $"Inventory={saveData.PlayerInventory.InventoryItems.Count}件 / " +
            $"PrimaryWeapon={(saveData.PlayerInventory.PrimaryWeapon != null ? "あり" : "なし")} / " +
            $"Helmet={(saveData.PlayerInventory.Helmet != null ? "あり" : "なし")} / " +
            $"PlayerItemBox={(saveData.PlayerItemBox.HasData ? saveData.PlayerItemBox.Items.Count : 0)}件 / " +
            $"Missions={saveData.Missions.Count}件 / " +
            $"Stories={saveData.Stories.Count}件 / " +
            $"StoryFlags={saveData.StoryFlags.Count}件 / " +
            $"TavernSolo={saveData.Tavern.PersonalDrinkTotal}回 / " +
            $"TavernTreat={saveData.Tavern.TreatEveryoneTotal}回 / " +
            $"Facilities={saveData.FacilityLevels.Count}件 / " +
            $"SkillCards={saveData.SkillCards.OwnedCardIds.Count}枚 / " +
            $"SkillSlots={saveData.SkillCards.UnlockedSlotCount}"
        );

        return saveData;
    }

    /// <summary>
    /// JSONから読み取ったデータをGameSessionManagerへ戻します。
    /// ItemDataはItemDataDatabaseを使ってItemIdから再取得します。
    /// </summary>
    public bool ApplySaveGameData(
        SaveGameData saveData,
        ItemDataDatabase itemDataDatabase,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (saveData == null)
        {
            resultMessage = "セーブデータがnullです。";
            return false;
        }

        if (saveData.SaveVersion <= 0 ||
            saveData.SaveVersion > CurrentSaveVersion)
        {
            resultMessage =
                $"対応していないセーブバージョンです。" +
                $" 保存={saveData.SaveVersion} / 対応={CurrentSaveVersion}";
            return false;
        }

        if (itemDataDatabase == null)
        {
            resultMessage = "ItemDataDatabaseが設定されていません。";
            return false;
        }

        currentMoney = Mathf.Max(0, saveData.Money);
        hasInitializedMoney = true;
        totalDeadNpcCount = Mathf.Max(0, saveData.TotalDeadNpcCount);
        totalRescuedNpcCount =
            Mathf.Max(
                0,
                saveData.TotalRescuedNpcCount
            );

        rescuedSpecialNpcIds.Clear();

        if (saveData.RescuedSpecialNpcIds != null)
        {
            foreach (string rawSpecialNpcId in
                     saveData.RescuedSpecialNpcIds)
            {
                string specialNpcId =
                    rawSpecialNpcId?.Trim() ??
                    string.Empty;

                if (string.IsNullOrWhiteSpace(
                        specialNpcId) ||
                    HasRescuedSpecialNpc(
                        specialNpcId))
                {
                    continue;
                }

                rescuedSpecialNpcIds.Add(
                    specialNpcId
                );
            }
        }

        RescueProgressExpeditionSession.Clear();

        RestoreTavernProgressData(saveData.Tavern);

        SavedPlayerInventoryData savedInventory =
            saveData.PlayerInventory ?? new SavedPlayerInventoryData();

        PlayerInventorySessionData nextInventory =
            new PlayerInventorySessionData
            {
                GridWidth = Mathf.Max(1, savedInventory.GridWidth),
                GridHeight = Mathf.Max(1, savedInventory.GridHeight)
            };

        int restoredInventoryCount = 0;
        int missingItemCount = 0;

        if (savedInventory.InventoryItems != null)
        {
            foreach (SavedInventoryItemData savedItem in
                     savedInventory.InventoryItems)
            {
                SessionInventoryItemData sessionItem =
                    CreateSessionInventoryItem(
                        savedItem,
                        itemDataDatabase,
                        out string itemError
                    );

                if (sessionItem != null)
                {
                    nextInventory.InventoryItems.Add(sessionItem);
                    restoredInventoryCount++;
                }
                else
                {
                    missingItemCount++;
                    LogTransferWarning(
                        $"ロード時にアイテムをスキップしました: {itemError}"
                    );
                }
            }
        }

        nextInventory.PrimaryWeaponItem = CreateSessionInventoryItem(
            savedInventory.PrimaryWeapon,
            itemDataDatabase,
            out string primaryWeaponError
        );

        if (savedInventory.PrimaryWeapon != null &&
            nextInventory.PrimaryWeaponItem == null)
        {
            missingItemCount++;
            LogTransferWarning(
                $"装備武器をロードできませんでした: {primaryWeaponError}"
            );
        }

        nextInventory.HelmetItem = CreateSessionInventoryItem(
            savedInventory.Helmet,
            itemDataDatabase,
            out string helmetError
        );

        if (savedInventory.Helmet != null &&
            nextInventory.HelmetItem == null)
        {
            missingItemCount++;
            LogTransferWarning(
                $"装備ヘルメットをロードできませんでした: {helmetError}"
            );
        }

        inventorySessionData = nextInventory;
        hasInventorySessionData = true;

        playerItemBoxSessionData = new PlayerItemBoxSessionData();
        hasPlayerItemBoxSessionData = false;
        int restoredPlayerItemBoxCount = 0;

        SavedPlayerItemBoxData savedPlayerItemBox = saveData.PlayerItemBox;

        if (savedPlayerItemBox != null && savedPlayerItemBox.HasData)
        {
            playerItemBoxSessionData.GridWidth =
                Mathf.Max(1, savedPlayerItemBox.GridWidth);
            playerItemBoxSessionData.GridHeight =
                Mathf.Max(1, savedPlayerItemBox.GridHeight);

            if (savedPlayerItemBox.Items != null)
            {
                foreach (SavedInventoryItemData savedItem in
                         savedPlayerItemBox.Items)
                {
                    SessionInventoryItemData sessionItem =
                        CreateSessionInventoryItem(
                            savedItem,
                            itemDataDatabase,
                            out string itemBoxItemError
                        );

                    if (sessionItem != null)
                    {
                        playerItemBoxSessionData.Items.Add(sessionItem);
                        restoredPlayerItemBoxCount++;
                    }
                    else
                    {
                        missingItemCount++;
                        LogTransferWarning(
                            $"プレイヤー保管箱のItemをロード時にスキップしました: {itemBoxItemError}"
                        );
                    }
                }
            }

            hasPlayerItemBoxSessionData = true;
        }

        facilitySessionData.Clear();
        int restoredFacilityCount = 0;

        if (saveData.FacilityLevels != null)
        {
            foreach (SavedTownFacilityLevelData savedFacility in
                     saveData.FacilityLevels)
            {
                if (savedFacility == null ||
                    string.IsNullOrWhiteSpace(savedFacility.FacilityId))
                {
                    continue;
                }

                string facilityId = savedFacility.FacilityId.Trim();

                if (FindFacilitySessionData(facilityId) != null)
                {
                    continue;
                }

                facilitySessionData.Add(
                    new TownFacilitySessionData
                    {
                        FacilityId = facilityId,
                        Level = Mathf.Max(1, savedFacility.Level)
                    }
                );

                restoredFacilityCount++;
            }
        }

        missionSessionData.Clear();
        int restoredMissionCount = 0;
        int skippedMissionCount = 0;

        if (saveData.Missions != null)
        {
            foreach (SavedMissionData savedMission in saveData.Missions)
            {
                if (savedMission == null ||
                    string.IsNullOrWhiteSpace(savedMission.MissionId))
                {
                    skippedMissionCount++;
                    continue;
                }

                MissionSessionData mission = new MissionSessionData
                {
                    Mission = null,
                    MissionId = savedMission.MissionId.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(savedMission.DisplayName)
                        ? savedMission.MissionId.Trim()
                        : savedMission.DisplayName,
                    State = savedMission.State,
                    Progress = Mathf.Max(0, savedMission.Progress),
                    RequiredAmount = Mathf.Max(1, savedMission.RequiredAmount),
                    RewardClaimed = savedMission.RewardClaimed,
                    AcceptSource = savedMission.AcceptSource
                };

                mission.Progress = Mathf.Clamp(
                    mission.Progress,
                    0,
                    mission.RequiredAmount
                );

                // 報酬受取済みなら、状態も達成済みに統一します。
                if (mission.RewardClaimed)
                {
                    mission.State = MissionSessionState.Completed;
                    mission.Progress = mission.RequiredAmount;
                }

                missionSessionData.Add(mission);
                restoredMissionCount++;
            }
        }

        storySessionData.Clear();
        int restoredStoryCount = 0;

        if (saveData.Stories != null)
        {
            foreach (SavedStoryQuestData savedStory in saveData.Stories)
            {
                if (savedStory == null ||
                    string.IsNullOrWhiteSpace(savedStory.StoryId))
                {
                    continue;
                }

                string storyId = savedStory.StoryId.Trim();

                if (FindStorySessionData(storyId) != null)
                {
                    continue;
                }

                storySessionData.Add(new StorySessionData
                {
                    Story = null,
                    StoryId = storyId,
                    DisplayName = string.IsNullOrWhiteSpace(savedStory.DisplayName)
                        ? storyId
                        : savedStory.DisplayName,
                    State = savedStory.State
                });

                restoredStoryCount++;
            }
        }

        storyFlags.Clear();

        if (saveData.StoryFlags != null)
        {
            foreach (string savedFlag in saveData.StoryFlags)
            {
                if (string.IsNullOrWhiteSpace(savedFlag) ||
                    HasStoryFlag(savedFlag))
                {
                    continue;
                }

                storyFlags.Add(savedFlag.Trim());
            }
        }

        trackedMissionId = saveData.TrackedMissionId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(trackedMissionId))
        {
            MissionSessionData trackedData =
                FindMissionSessionData(trackedMissionId);

            if (trackedData == null ||
                trackedData.State != MissionSessionState.InProgress)
            {
                trackedMissionId = string.Empty;
            }
        }

        int restoredSkillCardCount = RestoreSkillCardSession(
            saveData.SkillCards,
            itemDataDatabase
        );

        NotifyMoneyChanged();
        NotifyDeadNpcCountChanged();
        NotifyRescueProgressChanged();
        InventorySessionChanged?.Invoke();
        PlayerItemBoxSessionChanged?.Invoke();
        MissionSessionChanged?.Invoke();
        StorySessionChanged?.Invoke();
        TavernProgressChanged?.Invoke();
        SkillSessionChanged?.Invoke();

        resultMessage =
            $"所持金={currentMoney:N0} / " +
            $"墓場の死亡NPC={totalDeadNpcCount:N0}人 / " +
            $"累計救出NPC={totalRescuedNpcCount:N0}人 / " +
            $"特殊NPC救出={rescuedSpecialNpcIds.Count}人 / " +
            $"通常アイテム={restoredInventoryCount}件 / " +
            $"保管箱={restoredPlayerItemBoxCount}件 / " +
            $"ミッション={restoredMissionCount}件 / " +
            $"ストーリー={restoredStoryCount}件 / " +
            $"StoryFlag={storyFlags.Count}件 / " +
            $"酒場一人飲み={TavernPersonalDrinkTotal}回 / " +
            $"酒場おごり={TavernTreatEveryoneTotal}回 / " +
            $"施設={restoredFacilityCount}件 / " +
            $"スキルカード={restoredSkillCardCount}枚 / " +
            $"スキル枠={UnlockedSkillSlotCount}";

        if (missingItemCount > 0)
        {
            resultMessage +=
                $" / ItemData未登録などで{missingItemCount}件スキップ";
        }

        if (skippedMissionCount > 0)
        {
            resultMessage +=
                $" / 無効ミッション{skippedMissionCount}件スキップ";
        }

        LogTransfer($"本セーブデータ読込完了: {resultMessage}");
        return true;
    }

    private SavedSkillCardData CreateSavedSkillCardData()
    {
        EnsureSkillSessionData();

        SavedSkillCardData result = new SavedSkillCardData
        {
            UnlockedSlotCount = UnlockedSkillSlotCount
        };

        foreach (SkillCardData card in skillCardSessionData.OwnedCards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.ItemId))
            {
                continue;
            }

            result.OwnedCardIds.Add(card.ItemId.Trim());
        }

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            SkillCardData card = skillCardSessionData.EquippedCards[i];
            result.EquippedCardIds.Add(
                card != null && !string.IsNullOrWhiteSpace(card.ItemId)
                    ? card.ItemId.Trim()
                    : string.Empty
            );
        }

        return result;
    }

    private int RestoreSkillCardSession(
        SavedSkillCardData saved,
        ItemDataDatabase itemDataDatabase)
    {
        skillCardSessionData = new SkillCardSessionData();
        EnsureSkillSessionData();

        if (saved == null)
        {
            return 0;
        }

        skillCardSessionData.UnlockedSlotCount = Mathf.Clamp(
            saved.UnlockedSlotCount <= 0
                ? MinSkillSlots
                : saved.UnlockedSlotCount,
            MinSkillSlots,
            MaxSkillSlots
        );

        Dictionary<string, SkillCardData> restoredById =
            new Dictionary<string, SkillCardData>(
                StringComparer.OrdinalIgnoreCase
            );

        if (saved.OwnedCardIds != null)
        {
            foreach (string rawId in saved.OwnedCardIds)
            {
                string id = rawId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) ||
                    restoredById.ContainsKey(id))
                {
                    continue;
                }

                if (itemDataDatabase.TryGetItemData(
                        id,
                        out ItemData itemData) &&
                    itemData is SkillCardData card)
                {
                    skillCardSessionData.OwnedCards.Add(card);
                    restoredById.Add(id, card);
                }
                else
                {
                    LogTransferWarning(
                        $"SkillCard ItemId={id} がItemDataDatabaseに見つからないか、SkillCardDataではありません。"
                    );
                }
            }
        }

        if (saved.EquippedCardIds != null)
        {
            int count = Mathf.Min(
                MaxSkillSlots,
                saved.EquippedCardIds.Count
            );

            for (int i = 0; i < count; i++)
            {
                if (i >= skillCardSessionData.UnlockedSlotCount)
                {
                    break;
                }

                string id = saved.EquippedCardIds[i]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!restoredById.TryGetValue(id, out SkillCardData card))
                {
                    if (itemDataDatabase.TryGetItemData(
                            id,
                            out ItemData itemData) &&
                        itemData is SkillCardData resolvedCard)
                    {
                        card = resolvedCard;
                        restoredById[id] = card;
                        skillCardSessionData.OwnedCards.Add(card);
                    }
                }

                if (card != null && FindEquippedSkillCardSlot(card) < 0)
                {
                    skillCardSessionData.EquippedCards[i] = card;
                }
            }
        }

        return skillCardSessionData.OwnedCards.Count;
    }

    private static SavedInventoryItemData CreateSavedInventoryItem(
        SessionInventoryItemData source)
    {
        if (source == null ||
            source.ItemData == null ||
            string.IsNullOrWhiteSpace(source.ItemData.ItemId) ||
            source.Amount <= 0)
        {
            return null;
        }

        return new SavedInventoryItemData
        {
            ItemId = source.ItemData.ItemId.Trim(),
            GridX = source.GridX,
            GridY = source.GridY,
            IsRotated = source.IsRotated,
            Amount = Mathf.Max(1, source.Amount),
            HasStoredMagazineAmmo = source.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, source.StoredMagazineAmmo),
            StoredMagazineAmmoItemId =
                source.StoredMagazineAmmoType != null &&
                !string.IsNullOrWhiteSpace(source.StoredMagazineAmmoType.ItemId)
                    ? source.StoredMagazineAmmoType.ItemId.Trim()
                    : string.Empty,
            HasStoredWeaponDurability =
                source.HasStoredWeaponDurability,
            StoredWeaponDurability =
                Mathf.Max(0f, source.StoredWeaponDurability),
            StoredWeaponJammed = source.StoredWeaponJammed
        };
    }

    private static SessionInventoryItemData CreateSessionInventoryItem(
        SavedInventoryItemData source,
        ItemDataDatabase itemDataDatabase,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (source == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(source.ItemId))
        {
            errorMessage = "ItemIdが空です。";
            return null;
        }

        if (itemDataDatabase == null ||
            !itemDataDatabase.TryGetItemData(
                source.ItemId.Trim(),
                out ItemData itemData) ||
            itemData == null)
        {
            errorMessage =
                $"ItemId={source.ItemId} がItemDataDatabaseに登録されていません。";
            return null;
        }

        AmmoItemData storedMagazineAmmoType = null;

        if (!string.IsNullOrWhiteSpace(source.StoredMagazineAmmoItemId) &&
            itemDataDatabase.TryGetItemData(
                source.StoredMagazineAmmoItemId.Trim(),
                out ItemData storedAmmoItemData))
        {
            storedMagazineAmmoType =
                storedAmmoItemData as AmmoItemData;
        }

        return new SessionInventoryItemData
        {
            ItemData = itemData,
            GridX = Mathf.Max(0, source.GridX),
            GridY = Mathf.Max(0, source.GridY),
            IsRotated = source.IsRotated,
            Amount = Mathf.Clamp(
                source.Amount,
                1,
                Mathf.Max(1, itemData.MaxStack)
            ),
            HasStoredMagazineAmmo = source.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, source.StoredMagazineAmmo),
            StoredMagazineAmmoType = storedMagazineAmmoType,
            HasStoredWeaponDurability =
                source.HasStoredWeaponDurability,
            StoredWeaponDurability =
                Mathf.Max(0f, source.StoredWeaponDurability),
            StoredWeaponJammed = source.StoredWeaponJammed
        };
    }

    private static PlayerInventorySessionData CloneInventorySessionData(
        PlayerInventorySessionData source)
    {
        PlayerInventorySessionData clone =
            new PlayerInventorySessionData
            {
                GridWidth = source != null
                    ? Mathf.Max(1, source.GridWidth)
                    : 7,
                GridHeight = source != null
                    ? Mathf.Max(1, source.GridHeight)
                    : 10,
                PrimaryWeaponItem = CloneSessionInventoryItem(
                    source != null ? source.PrimaryWeaponItem : null
                ),
                HelmetItem = CloneSessionInventoryItem(
                    source != null ? source.HelmetItem : null
                )
            };

        if (source != null && source.InventoryItems != null)
        {
            foreach (SessionInventoryItemData item in source.InventoryItems)
            {
                SessionInventoryItemData copy =
                    CloneSessionInventoryItem(item);

                if (copy != null)
                {
                    clone.InventoryItems.Add(copy);
                }
            }
        }

        return clone;
    }

    private static SessionInventoryItemData CloneSessionInventoryItem(
        SessionInventoryItemData source)
    {
        if (source == null)
        {
            return null;
        }

        return new SessionInventoryItemData
        {
            ItemData = source.ItemData,
            GridX = source.GridX,
            GridY = source.GridY,
            IsRotated = source.IsRotated,
            Amount = source.Amount,
            HasStoredMagazineAmmo = source.HasStoredMagazineAmmo,
            StoredMagazineAmmo = source.StoredMagazineAmmo,
            StoredMagazineAmmoType = source.StoredMagazineAmmoType,
            HasStoredWeaponDurability = source.HasStoredWeaponDurability,
            StoredWeaponDurability = source.StoredWeaponDurability,
            StoredWeaponJammed = source.StoredWeaponJammed
        };
    }

    /// <summary>
    /// Result SceneでCollect Itemミッションの回収品を、
    /// シーン間引き継ぎ用Inventoryから必要数だけ消費します。
    /// 事前に総数を確認しているため、不足時は一切変更しません。
    /// </summary>
    private static bool TryConsumeItemFromSessionInventory(
        PlayerInventorySessionData target,
        ItemData itemData,
        int amount,
        out int consumedAmount)
    {
        consumedAmount = 0;

        if (target == null ||
            itemData == null ||
            amount <= 0 ||
            target.InventoryItems == null)
        {
            return false;
        }

        int available = 0;

        foreach (SessionInventoryItemData existing in
                 target.InventoryItems)
        {
            if (existing == null ||
                existing.ItemData == null ||
                existing.Amount <= 0 ||
                !IsSameItemData(existing.ItemData, itemData))
            {
                continue;
            }

            available += Mathf.Max(0, existing.Amount);

            if (available >= amount)
            {
                break;
            }
        }

        if (available < amount)
        {
            // 呼び出し側の診断用に、実際に存在した最大数を返します。
            consumedAmount = available;
            return false;
        }

        int remaining = amount;

        // 後ろから消費するとRemoveAtしても未処理Indexがずれません。
        for (int i = target.InventoryItems.Count - 1;
             i >= 0 && remaining > 0;
             i--)
        {
            SessionInventoryItemData existing =
                target.InventoryItems[i];

            if (existing == null ||
                existing.ItemData == null ||
                existing.Amount <= 0 ||
                !IsSameItemData(existing.ItemData, itemData))
            {
                continue;
            }

            int take = Mathf.Min(existing.Amount, remaining);
            existing.Amount -= take;
            remaining -= take;
            consumedAmount += take;

            if (existing.Amount <= 0)
            {
                target.InventoryItems.RemoveAt(i);
            }
        }

        return remaining <= 0;
    }

    private static bool TryAddRewardItemToSessionInventory(
        PlayerInventorySessionData target,
        ItemData itemData,
        int amount)
    {
        if (target == null || itemData == null || amount <= 0)
        {
            return false;
        }

        if (target.InventoryItems == null)
        {
            target.InventoryItems =
                new List<SessionInventoryItemData>();
        }

        int remaining = amount;
        int maxStack = Mathf.Max(1, itemData.MaxStack);

        if (itemData.CanStack)
        {
            foreach (SessionInventoryItemData existing in target.InventoryItems)
            {
                if (existing == null ||
                    existing.ItemData == null ||
                    !IsSameItemData(existing.ItemData, itemData))
                {
                    continue;
                }

                int free = Mathf.Max(
                    0,
                    maxStack - Mathf.Max(0, existing.Amount)
                );

                if (free <= 0)
                {
                    continue;
                }

                int add = Mathf.Min(free, remaining);
                existing.Amount += add;
                remaining -= add;

                if (remaining <= 0)
                {
                    return true;
                }
            }
        }

        while (remaining > 0)
        {
            int stackAmount = itemData.CanStack
                ? Mathf.Min(maxStack, remaining)
                : 1;

            if (!TryFindFreeSessionGridPosition(
                    target,
                    itemData,
                    out int gridX,
                    out int gridY,
                    out bool rotated))
            {
                return false;
            }

            target.InventoryItems.Add(
                new SessionInventoryItemData
                {
                    ItemData = itemData,
                    GridX = gridX,
                    GridY = gridY,
                    IsRotated = rotated,
                    Amount = stackAmount
                }
            );

            remaining -= stackAmount;
        }

        return true;
    }

    private static bool TryFindFreeSessionGridPosition(
        PlayerInventorySessionData target,
        ItemData itemData,
        out int gridX,
        out int gridY,
        out bool rotated)
    {
        gridX = 0;
        gridY = 0;
        rotated = false;

        int width = Mathf.Max(1, target.GridWidth);
        int height = Mathf.Max(1, target.GridHeight);
        bool[,] occupied = new bool[width, height];

        foreach (SessionInventoryItemData item in target.InventoryItems)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            Vector2Int size =
                item.ItemData.GetSize(item.IsRotated);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int px = item.GridX + x;
                    int py = item.GridY + y;

                    if (px >= 0 && px < width &&
                        py >= 0 && py < height)
                    {
                        occupied[px, py] = true;
                    }
                }
            }
        }

        if (TryFindFreeArea(
                occupied,
                width,
                height,
                itemData.GetSize(false),
                out gridX,
                out gridY))
        {
            rotated = false;
            return true;
        }

        if (itemData.CanRotate &&
            TryFindFreeArea(
                occupied,
                width,
                height,
                itemData.GetSize(true),
                out gridX,
                out gridY))
        {
            rotated = true;
            return true;
        }

        return false;
    }

    private static bool TryFindFreeArea(
        bool[,] occupied,
        int width,
        int height,
        Vector2Int size,
        out int gridX,
        out int gridY)
    {
        gridX = 0;
        gridY = 0;

        if (size.x <= 0 || size.y <= 0 ||
            size.x > width || size.y > height)
        {
            return false;
        }

        for (int y = 0; y <= height - size.y; y++)
        {
            for (int x = 0; x <= width - size.x; x++)
            {
                bool free = true;

                for (int sy = 0; sy < size.y && free; sy++)
                {
                    for (int sx = 0; sx < size.x; sx++)
                    {
                        if (occupied[x + sx, y + sy])
                        {
                            free = false;
                            break;
                        }
                    }
                }

                if (free)
                {
                    gridX = x;
                    gridY = y;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSameItemData(
        ItemData a,
        ItemData b)
    {
        if (a == b)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        string aId = a.ItemId?.Trim() ?? string.Empty;
        string bId = b.ItemId?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(aId) &&
            string.Equals(
                aId,
                bId,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private MissionSessionData GetOrCreateMissionSessionData(
        MissionDefinition2D mission)
    {
        string missionId = GetMissionId(mission);
        MissionSessionData existing = FindMissionSessionData(missionId);

        if (existing != null)
        {
            return existing;
        }

        MissionSessionData created = new MissionSessionData
        {
            Mission = mission,
            MissionId = missionId,
            DisplayName = mission != null ? mission.DisplayName : missionId,
            RequiredAmount = mission != null ? mission.RequiredAmount : 1,
            Progress = 0,
            State = MissionSessionState.Inactive,
            AcceptSource = MissionAcceptSource.Unknown
        };

        missionSessionData.Add(created);
        return created;
    }

    private MissionSessionData FindMissionSessionData(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId))
        {
            return null;
        }

        foreach (MissionSessionData data in missionSessionData)
        {
            if (data != null &&
                string.Equals(
                    data.MissionId,
                    missionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        return null;
    }

    private static int FindMissionIndexById(
        MissionManager2D missionManager,
        string missionId)
    {
        if (missionManager == null ||
            string.IsNullOrWhiteSpace(missionId))
        {
            return -1;
        }

        for (int i = 0; i < missionManager.MissionCount; i++)
        {
            MissionDefinition2D mission =
                missionManager.GetMissionDefinition(i);

            if (mission == null)
            {
                continue;
            }

            if (string.Equals(
                    mission.MissionId,
                    missionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetMissionId(MissionDefinition2D mission)
    {
        return mission != null
            ? (mission.MissionId ?? string.Empty).Trim()
            : string.Empty;
    }

    private static MissionSessionState ConvertMissionState(
        MissionProgressState2D state)
    {
        switch (state)
        {
            case MissionProgressState2D.InProgress:
                return MissionSessionState.InProgress;

            case MissionProgressState2D.Completed:
                return MissionSessionState.Completed;

            default:
                return MissionSessionState.Inactive;
        }
    }

    private void LogMission(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.Log($"[MissionSession] {message}", this);
    }

    private void LogMissionWarning(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.LogWarning($"[MissionSession] {message}", this);
    }

    [ContextMenu("Add 100 Money")]
    private void Add100MoneyFromContextMenu()
    {
        AddMoney(100);
    }

    [ContextMenu("Reset Money To Default Starting Amount")]
    private void ResetMoneyToDefaultStartingAmount()
    {
        currentMoney = Mathf.Max(0, defaultStartingMoney);
        hasInitializedMoney = true;
        NotifyMoneyChanged();
    }

    [ContextMenu("Clear Inventory Session Data")]
    private void ClearInventorySessionDataFromContextMenu()
    {
        ClearInventorySessionData();
    }

    [ContextMenu("Log Inventory Session Report")]
    private void LogInventorySessionReportFromContextMenu()
    {
        LogTransfer(GetInventorySessionSummary());
    }

    /// <summary>
    /// 現在保存されている引き継ぎデータを、Console確認用の文字列として返します。
    /// </summary>
    public string GetInventorySessionSummary()
    {
        if (!hasInventorySessionData || inventorySessionData == null)
        {
            return "[InventorySession] 保存データなし";
        }

        return "[InventorySession] 保存データあり / " +
               $"Grid={inventorySessionData.GridWidth}x{inventorySessionData.GridHeight} / " +
               $"通常={inventorySessionData.InventoryItems.Count}件 / " +
               $"武器={(inventorySessionData.PrimaryWeaponItem != null ? DescribeItem(inventorySessionData.PrimaryWeaponItem) : "なし")} / " +
               $"ヘルメット={(inventorySessionData.HelmetItem != null ? DescribeItem(inventorySessionData.HelmetItem) : "なし")}";
    }

    private SessionInventoryItemData CreateItemSnapshot(
        InventoryItem item)
    {
        if (item == null || item.ItemData == null || item.Amount <= 0)
        {
            return null;
        }

        return new SessionInventoryItemData
        {
            ItemData = item.ItemData,
            GridX = item.GridX,
            GridY = item.GridY,
            IsRotated = item.IsRotated,
            Amount = Mathf.Clamp(item.Amount, 1, item.ItemData.MaxStack),
            HasStoredMagazineAmmo = item.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, item.StoredMagazineAmmo),
            StoredMagazineAmmoType = item.StoredMagazineAmmoType,
            HasStoredWeaponDurability =
                item.HasStoredWeaponDurability,
            StoredWeaponDurability =
                Mathf.Max(0f, item.StoredWeaponDurability),
            StoredWeaponJammed = item.StoredWeaponJammed
        };
    }

    private void ClearCurrentInventoryAndEquipment(
        InventoryController inventoryController,
        EquipmentController equipmentController)
    {
        if (equipmentController != null)
        {
            equipmentController.ClearAllEquippedItems();
        }

        List<InventoryItem> currentItems =
            new List<InventoryItem>(inventoryController.Grid.Items);

        foreach (InventoryItem item in currentItems)
        {
            inventoryController.RemoveItem(item);
        }
    }

    private RestorePlacementResult RestoreNormalInventoryItem(
        InventoryController inventoryController,
        SessionInventoryItemData savedItem)
    {
        InventoryItem item = CreateRuntimeInventoryItem(savedItem);

        if (item == null)
        {
            LogTransferWarning("通常アイテム復元失敗: 保存ItemDataがnullです。");
            return RestorePlacementResult.Failed;
        }

        if (inventoryController.TryMoveItem(
                item,
                savedItem.GridX,
                savedItem.GridY,
                savedItem.IsRotated))
        {
            LogSessionItem("復元", savedItem, "通常インベントリ: 元位置へ配置成功");
            return RestorePlacementResult.Exact;
        }

        LogTransferWarning(
            $"通常アイテムの元位置復元に失敗しました。{DescribeItem(savedItem)} / " +
            $"保存位置=({savedItem.GridX},{savedItem.GridY}) / 回転={savedItem.IsRotated}。空きマスを探します。"
        );

        if (TryPlaceAtFirstAvailableSpace(
                inventoryController,
                item,
                savedItem.IsRotated))
        {
            LogSessionItem("復元", savedItem, "通常インベントリ: 空きマスへ代替配置成功");
            return RestorePlacementResult.Fallback;
        }

        LogTransferWarning(
            $"通常アイテム復元失敗: 空きマスにも置けませんでした。{DescribeItem(savedItem)}"
        );
        return RestorePlacementResult.Failed;
    }

    private void RestoreEquipmentItem(
        InventoryController inventoryController,
        EquipmentController equipmentController,
        EquipmentSlotType slotType,
        SessionInventoryItemData savedItem,
        ref int restoredCount,
        ref int fallbackPlacedCount,
        ref int failedCount)
    {
        if (savedItem == null)
        {
            LogTransfer($"装備復元: {slotType}=保存データなし");
            return;
        }

        InventoryItem item = CreateRuntimeInventoryItem(savedItem);

        if (item == null)
        {
            failedCount++;
            LogTransferWarning($"装備復元失敗: {slotType}のItemDataがnullです。");
            return;
        }

        // equipmentController がnullの場合でも、下の失敗ログで
        // 結果を安全に参照できるよう、先に初期値を入れておきます。
        EquipmentResult equipmentResult = EquipmentResult.InvalidItem;

        bool restoredToEquipment =
            equipmentController != null &&
            equipmentController.RestoreEquippedItem(
                slotType,
                item,
                out equipmentResult);

        if (restoredToEquipment)
        {
            restoredCount++;
            LogSessionItem("復元", savedItem, $"装備: {slotType}へ配置成功");
            return;
        }

        string failure = equipmentController == null
            ? "EquipmentControllerが未設定"
            : $"RestoreEquippedItem失敗({equipmentResult})";

        LogTransferWarning(
            $"装備復元の直接配置に失敗しました。{slotType} / {DescribeItem(savedItem)} / {failure}。" +
            "通常インベントリへ退避を試みます。"
        );

        if (TryPlaceAtFirstAvailableSpace(
                inventoryController,
                item,
                savedItem.IsRotated))
        {
            restoredCount++;
            fallbackPlacedCount++;

            LogSessionItem("復元", savedItem, $"{slotType}: 通常インベントリへ退避成功");
            return;
        }

        failedCount++;
        LogTransferWarning(
            $"装備復元失敗: {slotType}を通常インベントリへも置けませんでした。{DescribeItem(savedItem)}"
        );
    }

    private InventoryItem CreateRuntimeInventoryItem(
        SessionInventoryItemData savedItem)
    {
        if (savedItem == null || savedItem.ItemData == null)
        {
            return null;
        }

        InventoryItem item = new InventoryItem(
            savedItem.ItemData,
            savedItem.GridX,
            savedItem.GridY,
            Mathf.Clamp(
                savedItem.Amount,
                1,
                savedItem.ItemData.MaxStack
            )
        );

        if (savedItem.IsRotated && item.CanRotate)
        {
            item.TryRotate();
        }

        if (savedItem.HasStoredMagazineAmmo)
        {
            item.SetStoredMagazineAmmoState(
                savedItem.StoredMagazineAmmo,
                savedItem.StoredMagazineAmmoType
            );
        }

        if (savedItem.HasStoredWeaponDurability)
        {
            item.SetStoredWeaponDurability(
                savedItem.StoredWeaponDurability
            );
        }
        else
        {
            item.EnsureWeaponDurabilityInitialized();
        }

        item.SetStoredWeaponJammed(savedItem.StoredWeaponJammed);

        return item;
    }

    private bool TryPlaceAtFirstAvailableSpace(
        InventoryController inventoryController,
        InventoryItem item,
        bool preferredRotation)
    {
        if (inventoryController == null ||
            inventoryController.Grid == null ||
            item == null)
        {
            return false;
        }

        if (TryPlaceWithRotation(
                inventoryController,
                item,
                preferredRotation))
        {
            return true;
        }

        if (item.CanRotate &&
            TryPlaceWithRotation(
                inventoryController,
                item,
                !preferredRotation))
        {
            return true;
        }

        return false;
    }

    private bool TryPlaceWithRotation(
        InventoryController inventoryController,
        InventoryItem item,
        bool isRotated)
    {
        InventoryGrid grid = inventoryController.Grid;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.CanPlaceItem(item, x, y, isRotated))
                {
                    continue;
                }

                return inventoryController.TryMoveItem(
                    item,
                    x,
                    y,
                    isRotated
                );
            }
        }

        return false;
    }

    private void LogSessionItem(
        string phase,
        SessionInventoryItemData item,
        string location)
    {
        if (!logEachInventoryItem || item == null)
        {
            return;
        }

        LogTransfer($"{phase}アイテム: {location} / {DescribeItem(item)}");
    }

    private static string DescribeItem(SessionInventoryItemData item)
    {
        if (item == null)
        {
            return "Item=null";
        }

        string itemName = item.ItemData != null
            ? item.ItemData.DisplayName
            : "ItemData=null";

        string itemId = item.ItemData != null
            ? item.ItemData.ItemId
            : "null";

        string magazine = item.HasStoredMagazineAmmo
            ? $" / 残弾={item.StoredMagazineAmmo}" +
              (item.StoredMagazineAmmoType != null
                  ? $" / 装填弾={item.StoredMagazineAmmoType.DisplayName}"
                  : string.Empty)
            : string.Empty;

        string durability = item.HasStoredWeaponDurability &&
                            item.ItemData is WeaponItemData weaponData
            ? $" / 耐久={Mathf.Clamp01(item.StoredWeaponDurability / weaponData.MaxDurability) * 100f:0.#}%"
            : string.Empty;

        string jam = item.StoredWeaponJammed
            ? " / ジャム中"
            : string.Empty;

        return $"{itemName}(ID={itemId}, 数={item.Amount}, 座標={item.GridX},{item.GridY}, 回転={item.IsRotated}{magazine}{durability}{jam})";
    }

    private void SetMoneyInternal(int value)
    {
        int safeValue = Mathf.Max(0, value);

        if (currentMoney == safeValue)
        {
            return;
        }

        currentMoney = safeValue;
        NotifyMoneyChanged();
    }

    private void NotifyMoneyChanged()
    {
        MoneyChanged?.Invoke(currentMoney);
    }

    private void NotifyDeadNpcCountChanged()
    {
        DeadNpcCountChanged?.Invoke(totalDeadNpcCount);
    }

    private void NotifyRescueProgressChanged()
    {
        RescueProgressChanged?.Invoke();
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[GameSessionManager] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[GameSessionManager] {message}", this);
    }

    private void LogTransfer(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.Log($"[InventorySession] {message}", this);
    }

    private void LogTransferWarning(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.LogWarning($"[InventorySession] {message}", this);
    }

    private void OnValidate()
    {
        defaultStartingMoney = Mathf.Max(0, defaultStartingMoney);
    }

    private enum RestorePlacementResult
    {
        Failed,
        Exact,
        Fallback
    }
}

/// <summary>
/// シーン間で保持するインベントリ1件分のデータです。
/// ItemDataはScriptableObject参照なので、同一プレイ中は安全に復元できます。
/// </summary>
[Serializable]
public class SessionInventoryItemData
{
    public ItemData ItemData;
    public int GridX;
    public int GridY;
    public bool IsRotated;
    public int Amount;
    public bool HasStoredMagazineAmmo;
    public int StoredMagazineAmmo;
    public AmmoItemData StoredMagazineAmmoType;
    public bool HasStoredWeaponDurability;
    public float StoredWeaponDurability;
    public bool StoredWeaponJammed;
}

/// <summary>
/// プレイヤーの通常インベントリと装備枠をまとめた、シーン間引き継ぎデータです。
/// </summary>
[Serializable]
public class PlayerInventorySessionData
{
    public int GridWidth = 7;
    public int GridHeight = 10;
    public List<SessionInventoryItemData> InventoryItems =
        new List<SessionInventoryItemData>();
    public SessionInventoryItemData PrimaryWeaponItem;
    public SessionInventoryItemData HelmetItem;
}

/// <summary>
/// 町のプレイヤー保管箱1個分をシーン間で保持するデータです。
/// </summary>
[Serializable]
public class PlayerItemBoxSessionData
{
    public int GridWidth = 10;
    public int GridHeight = 12;
    public List<SessionInventoryItemData> Items =
        new List<SessionInventoryItemData>();
}

[Serializable]
public class SkillCardSessionData
{
    public int UnlockedSlotCount = GameSessionManager.MinSkillSlots;
    public List<SkillCardData> OwnedCards = new List<SkillCardData>();
    public List<SkillCardData> EquippedCards = new List<SkillCardData>();
}

public enum MissionSessionState
{
    Inactive,
    InProgress,
    Completed
}

/// <summary>
/// ミッションをどこから受注したかを表します。
/// </summary>
public enum MissionAcceptSource
{
    Unknown = 0,
    Conversation = 1,
    Expedition = 2
}

/// <summary>
/// シーン間で保持するミッション1件分の状態です。
/// MissionDefinition2Dへの参照も残しますが、探索シーンではMissionIdを優先してMissionManager2D内の登録を探します。
/// </summary>
[Serializable]
public class MissionSessionData
{
    public MissionDefinition2D Mission;
    public string MissionId;
    public string DisplayName;
    public MissionSessionState State;
    public int Progress;
    public int RequiredAmount = 1;
    public bool RewardClaimed;
    public MissionAcceptSource AcceptSource = MissionAcceptSource.Unknown;
}

/// <summary>
/// シーン間で保持する町施設1件分のレベルです。
/// </summary>
[Serializable]
public class TownFacilitySessionData
{
    public string FacilityId;
    public int Level = 1;
}
