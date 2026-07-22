using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameSessionManagerの状態をJSONへ保存・ロードします。
/// 第1段階の保存対象：
/// ・所持金
/// ・通常インベントリ
/// ・装備中の武器／ヘルメット
/// ・武器の保存残弾
/// ・ミッション進行度／報酬受取状態／追跡中ミッション
/// 第2段階の保存対象：
/// ・HP・食料・水分・SAN・状態異常・松明残量
/// 第3段階の保存対象：現在Scene・チェックポイント・地面アイテム・アイテム箱・会話履歴
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("ゲーム内で使用する全ItemDataを登録したDatabaseです。ロード時にItemIdから復元します。")]
    [SerializeField] private ItemDataDatabase itemDataDatabase;

    [Tooltip("未設定ならGameSessionManager.Instanceを使用します。")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("セーブスロット")]
    public const int MaxManualSlots = 20;
    public const string AutoSaveFileName = "autosave.json";

    [SerializeField, Range(1, MaxManualSlots)]
    private int defaultSlotNumber = 1;

    [Tooltip("trueなら読みやすい整形済みJSONで保存します。")]
    [SerializeField] private bool prettyPrintJson = true;

    [Header("保存前・ロード後の同期")]
    [Tooltip("保存直前に、現在シーンのインベントリをGameSessionManagerへ取り込みます。")]
    [SerializeField] private bool captureInventoryBeforeSave = true;

    [Tooltip("保存直前に、現在シーンのミッション状態をGameSessionManagerへ取り込みます。")]
    [SerializeField] private bool captureMissionsBeforeSave = true;

    [Tooltip("ロード後、現在シーンのインベントリへ即座に反映します。")]
    [SerializeField] private bool restoreInventoryAfterLoad = true;

    [Tooltip("ロード後、現在シーンのMissionManager2Dへ即座に反映します。")]
    [SerializeField] private bool restoreMissionsAfterLoad = true;

    [Tooltip("保存直前に、現在シーンのPlayerStatusSaveBridgeからHP等を取り込みます。")]
    [SerializeField] private bool capturePlayerStatusBeforeSave = true;

    [Tooltip("ロード後、現在シーンのPlayerStatusSaveBridgeへ即座に反映します。")]
    [SerializeField] private bool restorePlayerStatusAfterLoad = true;

    [Header("第3段階：シーン・ワールド状態")]
    [SerializeField] private bool captureWorldStateBeforeSave = true;
    [SerializeField] private bool restoreWorldStateAfterLoad = true;
    [SerializeField] private bool loadSavedSceneAfterLoad = true;
    [SerializeField] private bool saveConversationHistory = true;
    [SerializeField] private bool saveCheckpoint = true;

    [Header("動作")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("DontDestroyOnLoadを使う時、SaveManagerが子Objectなら自動的にルートへ移動します。")]
    [SerializeField] private bool detachFromParentForPersistence = true;

    [Tooltip("シーン読込後にSaveManagerの生存状況をConsoleへ表示します。")]
    [SerializeField] private bool logSceneLoaded = true;

    [SerializeField] private bool showDebugLogs = true;

    public static SaveManager Instance { get; private set; }

    public string LastOperationMessage { get; private set; } = string.Empty;
    public int DefaultSlotNumber => Mathf.Clamp(defaultSlotNumber, 1, MaxManualSlots);
    public int CurrentManualSlotNumber { get; private set; }
    public bool HasCurrentManualSlot =>
        CurrentManualSlotNumber >= 1 &&
        CurrentManualSlotNumber <= MaxManualSlots;
    public bool LastLoadedWasAutoSave { get; private set; }
    public ItemDataDatabase ItemDataDatabase => itemDataDatabase;

    public event Action<bool, string> OperationFinished;

    private bool isDuplicateForwarder;
    private bool applySessionAfterSceneLoad;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // SceneごとにSaveManagerを置いてしまった場合でも、
            // Buttonが破棄済みオブジェクトを参照しないよう、この個体は
            // 既存Instanceへの転送役としてScene内に残します。
            isDuplicateForwarder = true;

            if (Instance.itemDataDatabase == null && itemDataDatabase != null)
            {
                Instance.itemDataDatabase = itemDataDatabase;
            }

            if (showDebugLogs)
            {
                Debug.LogWarning(
                    "[SaveManager] SaveManagerが複数あります。" +
                    "このObjectへのButton操作は既存のSaveManagerへ転送します。" +
                    "基本的には最初のSceneに1個だけ配置してください。",
                    this
                );
            }

            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            if (transform.parent != null)
            {
                if (detachFromParentForPersistence)
                {
                    Transform oldParent = transform.parent;
                    transform.SetParent(null, true);

                    if (showDebugLogs)
                    {
                        Debug.LogWarning(
                            $"[SaveManager] DontDestroyOnLoadを確実にするため、" +
                            $"SaveManagerを親Object『{oldParent.name}』から外してルートへ移動しました。",
                            this
                        );
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[SaveManager] SaveManagerが子Objectのため、DontDestroyOnLoadが正常に働かない可能性があります。" +
                        "Hierarchy直下のルートObjectへ移動してください。",
                        this
                    );
                }
            }

            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;

        FindReferences();
        Log(
            $"初期化完了 / Slot={DefaultSlotNumber} / " +
            $"Path={GetSavePath(DefaultSlotNumber)} / " +
            $"GameSessionManager={(gameSessionManager != null ? "OK" : "未検出")} / " +
            $"ItemDataDatabase={(itemDataDatabase != null ? "OK" : "未設定")}"
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this)
        {
            return;
        }

        FindReferences();

        if (showDebugLogs && logSceneLoaded)
        {
            Debug.Log(
                $"[SaveManager] シーン読込後も生存しています：" +
                $"Scene={scene.name} / Object={name} / " +
                $"GameSessionManager={(gameSessionManager != null ? "OK" : "未検出")} / " +
                $"ManualSaveCount={CountManualSaveFiles()} / " +
                $"AutoSaveExists={HasAutoSaveData()}",
                this
            );
        }


        if (applySessionAfterSceneLoad)
        {
            applySessionAfterSceneLoad = false;
            ApplyLoadedSessionToCurrentScene();
            Log($"保存Scene『{scene.name}』への移動後、ロード内容を反映しました。");
        }
    }

    /// <summary>InspectorのButtonから呼ぶ既定スロット保存です。</summary>
    public void SaveGame()
    {
        if (ForwardToPrimaryIfDuplicate(manager => manager.SaveGame()))
        {
            return;
        }

        Log("SaveGame()が呼ばれました。");
        SaveSlot(DefaultSlotNumber);
    }

    /// <summary>InspectorのButtonから呼ぶ既定スロットロードです。</summary>
    public void LoadGame()
    {
        if (ForwardToPrimaryIfDuplicate(manager => manager.LoadGame()))
        {
            return;
        }

        Log("LoadGame()が呼ばれました。");
        LoadSlot(DefaultSlotNumber);
    }

    /// <summary>InspectorのButtonから呼ぶ既定スロット削除です。</summary>
    public void DeleteSave()
    {
        if (ForwardToPrimaryIfDuplicate(manager => manager.DeleteSave()))
        {
            return;
        }

        Log("DeleteSave()が呼ばれました。");
        DeleteSlot(DefaultSlotNumber);
    }

    public bool SaveSlot(int slotNumber)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.SaveSlot(slotNumber);
        }

        if (!IsValidManualSlot(slotNumber))
        {
            return Finish(
                false,
                $"セーブ失敗：手動セーブ枠は1～{MaxManualSlots}です。指定={slotNumber}"
            );
        }

        bool success = SaveToPath(
            GetSavePath(slotNumber),
            slotNumber,
            false
        );

        if (success)
        {
            CurrentManualSlotNumber = slotNumber;
            LastLoadedWasAutoSave = false;
        }

        return success;
    }

    /// <summary>
    /// 将来の自動セーブ用です。手動20枠とは別のautosave.jsonへ保存します。
    /// 現時点ではタイマー等は含まず、任意のタイミングで呼び出します。
    /// </summary>
    public bool SaveAutoGame()
    {
        if (Instance != null && Instance != this)
        {
            return Instance.SaveAutoGame();
        }

        return SaveToPath(GetAutoSavePath(), 0, true);
    }

    public bool SaveCurrentManualSlot()
    {
        if (!HasCurrentManualSlot)
        {
            return Finish(false, "上書き先の手動セーブ枠が選択されていません。");
        }

        return SaveSlot(CurrentManualSlotNumber);
    }

    public bool LoadSlot(int slotNumber)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.LoadSlot(slotNumber);
        }

        if (!IsValidManualSlot(slotNumber))
        {
            return Finish(
                false,
                $"ロード失敗：手動セーブ枠は1～{MaxManualSlots}です。指定={slotNumber}"
            );
        }

        bool success = LoadFromPath(
            GetSavePath(slotNumber),
            slotNumber,
            false
        );

        if (success)
        {
            CurrentManualSlotNumber = slotNumber;
            LastLoadedWasAutoSave = false;
        }

        return success;
    }

    public bool LoadAutoGame()
    {
        if (Instance != null && Instance != this)
        {
            return Instance.LoadAutoGame();
        }

        bool success = LoadFromPath(GetAutoSavePath(), 0, true);
        if (success)
        {
            CurrentManualSlotNumber = 0;
            LastLoadedWasAutoSave = true;
        }

        return success;
    }

    public bool LoadMostRecentSave(bool includeAutoSave = true)
    {
        if (!TryReadMostRecentSaveInfo(
                includeAutoSave,
                out SaveSlotInfo info,
                out string message))
        {
            return Finish(false, message);
        }

        return info.IsAutoSave
            ? LoadAutoGame()
            : LoadSlot(info.SlotNumber);
    }

    private bool SaveToPath(
        string savePath,
        int slotNumber,
        bool isAutoSave)
    {
        FindReferences();

        if (gameSessionManager == null)
        {
            return Finish(false, "セーブ失敗：GameSessionManagerが見つかりません。");
        }

        if (itemDataDatabase == null)
        {
            Log("注意：ItemDataDatabaseは未設定ですが、セーブ処理は続行します。ロード前には設定してください。");
        }

        string slotLabel = isAutoSave ? "オートセーブ" : $"スロット{slotNumber}";
        Log($"セーブ開始：{slotLabel} / Path={savePath}");

        CaptureCurrentSceneToSession();

        SaveGameData saveData = gameSessionManager.CreateSaveGameData(
            SceneManager.GetActiveScene().name
        );

        if (saveData == null)
        {
            return Finish(false, "セーブ失敗：保存データを作成できませんでした。");
        }

        saveData.PlayerStatus =
            PlayerStatusSessionStore.CreateSnapshot() ??
            new SavedPlayerStatusData();

        saveData.SaveVersion = GameSessionManager.CurrentSaveVersion;
        saveData.WorldState = WorldStateSessionStore.CreateSnapshot();
        saveData.Checkpoint = saveCheckpoint
            ? GameManager.CreateCheckpointSaveData()
            : new SavedCheckpointData();
        saveData.ConversationHistory = saveConversationHistory
            ? CaptureConversationHistory()
            : new List<SavedConversationHistoryData>();

        string tempPath = savePath + ".tmp";

        try
        {
            string directoryPath = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string json = JsonUtility.ToJson(saveData, prettyPrintJson);
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            File.Move(tempPath, savePath);

            if (!File.Exists(savePath))
            {
                return Finish(false, $"セーブ失敗：書き込み後にファイルを確認できません。\n{savePath}");
            }

            FileInfo fileInfo = new FileInfo(savePath);
            if (fileInfo.Length <= 0)
            {
                return Finish(false, $"セーブ失敗：作成されたファイルが空です。\n{savePath}");
            }

            string verifyJson = File.ReadAllText(savePath, Encoding.UTF8);
            SaveGameData verifyData = JsonUtility.FromJson<SaveGameData>(verifyJson);
            if (verifyData == null)
            {
                return Finish(false, $"セーブ失敗：保存後のJSON検証に失敗しました。\n{savePath}");
            }
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile(tempPath);
            Debug.LogException(exception, this);
            return Finish(false, $"セーブ失敗：{exception.Message}");
        }

        return Finish(
            true,
            $"{slotLabel}へ保存しました。" +
            $" ファイルサイズ={new FileInfo(savePath).Length:N0} bytes\n{savePath}"
        );
    }

    private bool LoadFromPath(
        string savePath,
        int slotNumber,
        bool isAutoSave)
    {
        FindReferences();

        if (gameSessionManager == null)
        {
            return Finish(false, "ロード失敗：GameSessionManagerが見つかりません。");
        }

        if (itemDataDatabase == null)
        {
            return Finish(false, "ロード失敗：ItemDataDatabaseがSaveManagerに設定されていません。");
        }

        string slotLabel = isAutoSave ? "オートセーブ" : $"スロット{slotNumber}";
        Log($"ロード開始：{slotLabel} / Path={savePath}");

        if (!File.Exists(savePath))
        {
            return Finish(false, $"ロード失敗：{slotLabel}にセーブデータがありません。\n確認先：{savePath}");
        }

        SaveGameData saveData;

        try
        {
            string json = File.ReadAllText(savePath, Encoding.UTF8);
            saveData = JsonUtility.FromJson<SaveGameData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return Finish(false, $"ロード失敗：{exception.Message}");
        }

        if (saveData == null)
        {
            return Finish(false, "ロード失敗：セーブデータを読み取れませんでした。");
        }

        if (!gameSessionManager.ApplySaveGameData(
                saveData,
                itemDataDatabase,
                out string applyMessage))
        {
            return Finish(false, $"ロード失敗：{applyMessage}");
        }

        if (saveData.PlayerStatus != null &&
            saveData.PlayerStatus.HasAnyData)
        {
            PlayerStatusSessionStore.SetData(saveData.PlayerStatus);
            Log("ロードしたプレイヤー状態をセッションへ登録しました。");
        }
        else
        {
            Log("このセーブデータには第2段階のプレイヤー状態がありません。現在のHP等は変更しません。");
        }

        WorldStateSessionStore.SetData(saveData.WorldState);
        ApplyConversationHistory(saveData.ConversationHistory);
        GameManager.ApplyCheckpointSaveData(saveData.Checkpoint);

        string savedSceneName = saveData.SavedSceneName?.Trim() ?? string.Empty;
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (loadSavedSceneAfterLoad &&
            !string.IsNullOrWhiteSpace(savedSceneName) &&
            !string.Equals(savedSceneName, currentSceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (Application.CanStreamedLevelBeLoaded(savedSceneName))
            {
                applySessionAfterSceneLoad = true;
                SceneManager.LoadScene(savedSceneName, LoadSceneMode.Single);
                return Finish(true, $"{slotLabel}をロードし、Scene『{savedSceneName}』へ移動します。\n{applyMessage}");
            }

            Log($"保存Scene『{savedSceneName}』を読み込めないため、現在Sceneへデータを反映します。");
        }

        ApplyLoadedSessionToCurrentScene();

        return Finish(true, $"{slotLabel}をロードしました。\n{applyMessage}");
    }

    // ---------------------------------------------------------------------
    // タイトル画面
    // ---------------------------------------------------------------------

    /// <summary>
    /// 既定スロットの概要を読み取ります。
    /// </summary>
    public bool TryReadDefaultSlotInfo(
        out SaveSlotInfo slotInfo,
        out string resultMessage)
    {
        return TryReadSlotInfo(DefaultSlotNumber, out slotInfo, out resultMessage);
    }

    public bool TryReadSlotInfo(
        int slotNumber,
        out SaveSlotInfo slotInfo,
        out string resultMessage)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.TryReadSlotInfo(
                slotNumber,
                out slotInfo,
                out resultMessage
            );
        }

        if (!IsValidManualSlot(slotNumber))
        {
            slotInfo = new SaveSlotInfo
            {
                SlotNumber = slotNumber,
                HasSaveData = false,
                ReadError = $"手動セーブ枠は1～{MaxManualSlots}です。"
            };
            resultMessage = slotInfo.ReadError;
            return false;
        }

        return TryReadSaveInfo(
            GetSavePath(slotNumber),
            slotNumber,
            false,
            out slotInfo,
            out resultMessage
        );
    }

    public bool TryReadAutoSaveInfo(
        out SaveSlotInfo slotInfo,
        out string resultMessage)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.TryReadAutoSaveInfo(out slotInfo, out resultMessage);
        }

        return TryReadSaveInfo(
            GetAutoSavePath(),
            0,
            true,
            out slotInfo,
            out resultMessage
        );
    }

    public List<SaveSlotInfo> ReadAllManualSlotInfos()
    {
        var result = new List<SaveSlotInfo>(MaxManualSlots);

        for (int slot = 1; slot <= MaxManualSlots; slot++)
        {
            TryReadSlotInfo(slot, out SaveSlotInfo info, out _);
            result.Add(info);
        }

        return result;
    }

    public bool TryReadMostRecentSaveInfo(
        bool includeAutoSave,
        out SaveSlotInfo slotInfo,
        out string resultMessage)
    {
        slotInfo = null;
        DateTime newestTime = DateTime.MinValue;

        for (int slot = 1; slot <= MaxManualSlots; slot++)
        {
            TryReadSlotInfo(slot, out SaveSlotInfo info, out _);
            if (info == null || !info.HasSaveData || !info.IsCompatible)
            {
                continue;
            }

            DateTime time = info.FileModifiedUtc;
            if (slotInfo == null || time > newestTime)
            {
                slotInfo = info;
                newestTime = time;
            }
        }

        if (includeAutoSave)
        {
            TryReadAutoSaveInfo(out SaveSlotInfo autoInfo, out _);
            if (autoInfo != null &&
                autoInfo.HasSaveData &&
                autoInfo.IsCompatible &&
                (slotInfo == null || autoInfo.FileModifiedUtc > newestTime))
            {
                slotInfo = autoInfo;
                newestTime = autoInfo.FileModifiedUtc;
            }
        }

        if (slotInfo == null)
        {
            resultMessage = "読み込めるセーブデータがありません。";
            return false;
        }

        resultMessage = slotInfo.IsAutoSave
            ? "最新のオートセーブを取得しました。"
            : $"最新の手動セーブ（スロット{slotInfo.SlotNumber}）を取得しました。";
        return true;
    }

    public bool TryFindFirstEmptyManualSlot(out int slotNumber)
    {
        for (int slot = 1; slot <= MaxManualSlots; slot++)
        {
            if (!HasSaveData(slot))
            {
                slotNumber = slot;
                return true;
            }
        }

        slotNumber = 0;
        return false;
    }

    public int CountManualSaveFiles()
    {
        int count = 0;
        for (int slot = 1; slot <= MaxManualSlots; slot++)
        {
            if (HasSaveData(slot))
            {
                count++;
            }
        }

        return count;
    }

    public bool HasAnyCompatibleSaveData(bool includeAutoSave = true)
    {
        return TryReadMostRecentSaveInfo(includeAutoSave, out _, out _);
    }

    private bool TryReadSaveInfo(
        string savePath,
        int slotNumber,
        bool isAutoSave,
        out SaveSlotInfo slotInfo,
        out string resultMessage)
    {
        slotInfo = new SaveSlotInfo
        {
            SlotNumber = slotNumber,
            IsAutoSave = isAutoSave,
            SavePath = savePath,
            HasSaveData = File.Exists(savePath),
            DisplayName = isAutoSave
                ? "オートセーブ"
                : $"セーブ {slotNumber:00}"
        };

        if (!slotInfo.HasSaveData)
        {
            resultMessage = isAutoSave
                ? "オートセーブデータがありません。"
                : $"スロット{slotNumber}にセーブデータがありません。";
            return false;
        }

        try
        {
            string json = File.ReadAllText(savePath, Encoding.UTF8);
            SaveGameData saveData = JsonUtility.FromJson<SaveGameData>(json);

            if (saveData == null)
            {
                resultMessage = "セーブデータを読み取れませんでした。";
                slotInfo.ReadError = resultMessage;
                return false;
            }

            slotInfo.SaveVersion = saveData.SaveVersion;
            slotInfo.IsCompatible =
                saveData.SaveVersion > 0 &&
                saveData.SaveVersion <= GameSessionManager.CurrentSaveVersion;
            slotInfo.SavedSceneName =
                saveData.SavedSceneName?.Trim() ?? string.Empty;
            slotInfo.SavedAtUtc =
                saveData.SavedAtUtc?.Trim() ?? string.Empty;
            slotInfo.Money = Mathf.Max(0, saveData.Money);
            slotInfo.MissionCount =
                saveData.Missions != null ? saveData.Missions.Count : 0;

            SavedPlayerInventoryData inventory = saveData.PlayerInventory;
            slotInfo.InventoryItemCount =
                inventory?.InventoryItems != null
                    ? inventory.InventoryItems.Count
                    : 0;
            slotInfo.PrimaryWeaponItemId =
                inventory?.PrimaryWeapon?.ItemId?.Trim() ?? string.Empty;
            slotInfo.HasPrimaryWeapon =
                !string.IsNullOrWhiteSpace(slotInfo.PrimaryWeaponItemId);

            FileInfo fileInfo = new FileInfo(savePath);
            slotInfo.FileSizeBytes = fileInfo.Exists
                ? Math.Max(0L, fileInfo.Length)
                : 0L;
            slotInfo.FileModifiedUtc = fileInfo.Exists
                ? fileInfo.LastWriteTimeUtc
                : DateTime.MinValue;

            if (!slotInfo.IsCompatible)
            {
                resultMessage =
                    $"対応していないセーブバージョンです。" +
                    $" 保存={slotInfo.SaveVersion} / " +
                    $"対応={GameSessionManager.CurrentSaveVersion}";
                slotInfo.ReadError = resultMessage;
                return false;
            }

            resultMessage = isAutoSave
                ? "オートセーブの概要を読み取りました。"
                : $"スロット{slotNumber}の概要を読み取りました。";
            return true;
        }
        catch (Exception exception)
        {
            resultMessage = $"セーブ概要の読み取り失敗：{exception.Message}";
            slotInfo.ReadError = resultMessage;
            return false;
        }
    }

    /// <summary>
    /// タイトル画面からニューゲームを開始します。
    /// メモリ上の全セッションを消去し、必要なら現在のセーブファイルも削除して、
    /// 指定Sceneへ移動します。
    /// </summary>
    public bool StartNewGame(
        string startSceneName,
        bool deleteExistingSave = false)
    {
        return StartNewGame(
            startSceneName,
            DefaultSlotNumber,
            deleteExistingSave
        );
    }

    public bool StartNewGame(
        string startSceneName,
        int slotNumber,
        bool deleteExistingSave)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.StartNewGame(
                startSceneName,
                slotNumber,
                deleteExistingSave
            );
        }

        string normalizedSceneName = startSceneName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return Finish(false, "ニューゲーム失敗：開始Scene名が空です。");
        }

        if (!Application.CanStreamedLevelBeLoaded(normalizedSceneName))
        {
            return Finish(
                false,
                $"ニューゲーム失敗：Scene『{normalizedSceneName}』を読み込めません。" +
                " Build ProfilesのScene Listへ追加してください。"
            );
        }

        if (!IsValidManualSlot(slotNumber))
        {
            slotNumber = DefaultSlotNumber;
        }

        if (deleteExistingSave)
        {
            string savePath = GetSavePath(slotNumber);
            try
            {
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    Log($"ニューゲーム開始前にスロット{slotNumber}を削除しました。");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return Finish(
                    false,
                    $"ニューゲーム失敗：セーブデータを削除できません。{exception.Message}"
                );
            }
        }

        ResetRuntimeSessionForNewGame();
        CurrentManualSlotNumber = 0;
        LastLoadedWasAutoSave = false;
        applySessionAfterSceneLoad = false;

        Finish(
            true,
            $"ニューゲームを開始します。Scene={normalizedSceneName}"
        );

        SceneManager.LoadScene(normalizedSceneName, LoadSceneMode.Single);
        return true;
    }

    /// <summary>
    /// セーブファイルには触れず、現在のプレイ中データだけを初期化します。
    /// </summary>
    public void ResetRuntimeSessionForNewGame()
    {
        if (Instance != null && Instance != this)
        {
            Instance.ResetRuntimeSessionForNewGame();
            return;
        }

        FindReferences();

        if (gameSessionManager != null)
        {
            gameSessionManager.ResetForNewGame();
        }

        PlayerStatusSessionStore.Clear();
        WorldStateSessionStore.Clear();
        TorchController.ClearStoredSessionValue();

        if (TownConversationHistoryManager.Instance != null)
        {
            TownConversationHistoryManager.Instance.ClearAllHistory();
        }

        GameManager.ApplyCheckpointSaveData(
            new SavedCheckpointData()
        );

        CurrentManualSlotNumber = 0;
        LastLoadedWasAutoSave = false;
        applySessionAfterSceneLoad = false;
        Log("ニューゲーム用に全ランタイムセッションを初期化しました。");
    }

    public bool DeleteSlot(int slotNumber)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.DeleteSlot(slotNumber);
        }

        if (!IsValidManualSlot(slotNumber))
        {
            return Finish(false, $"削除失敗：手動セーブ枠は1～{MaxManualSlots}です。");
        }

        bool success = DeletePath(
            GetSavePath(slotNumber),
            $"スロット{slotNumber}"
        );

        if (success && CurrentManualSlotNumber == slotNumber)
        {
            CurrentManualSlotNumber = 0;
        }

        return success;
    }

    public bool DeleteAutoSave()
    {
        if (Instance != null && Instance != this)
        {
            return Instance.DeleteAutoSave();
        }

        return DeletePath(GetAutoSavePath(), "オートセーブ");
    }

    private bool DeletePath(string savePath, string label)
    {
        if (!File.Exists(savePath))
        {
            return Finish(false, $"{label}に削除するセーブデータがありません。");
        }

        try
        {
            File.Delete(savePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return Finish(false, $"セーブデータ削除失敗：{exception.Message}");
        }

        return Finish(true, $"{label}のセーブデータを削除しました。");
    }

    public bool HasSaveData()
    {
        return HasSaveData(DefaultSlotNumber);
    }

    public bool HasSaveData(int slotNumber)
    {
        return IsValidManualSlot(slotNumber) &&
               File.Exists(GetSavePath(slotNumber));
    }

    public bool HasAutoSaveData()
    {
        return File.Exists(GetAutoSavePath());
    }

    public bool HasAnySaveData(bool includeAutoSave = true)
    {
        if (CountManualSaveFiles() > 0)
        {
            return true;
        }

        return includeAutoSave && HasAutoSaveData();
    }

    public string GetSavePath(int slotNumber)
    {
        int normalized = Mathf.Clamp(slotNumber, 1, MaxManualSlots);
        return Path.Combine(
            GetSaveFolderPath(),
            $"save_slot_{normalized}.json"
        );
    }

    public string GetAutoSavePath()
    {
        return Path.Combine(GetSaveFolderPath(), AutoSaveFileName);
    }

    public string GetSaveFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, "Saves");
    }

    private static bool IsValidManualSlot(int slotNumber)
    {
        return slotNumber >= 1 && slotNumber <= MaxManualSlots;
    }

    [ContextMenu("Save Default Slot")]
    private void SaveDefaultSlotFromContextMenu()
    {
        SaveGame();
    }

    [ContextMenu("Load Default Slot")]
    private void LoadDefaultSlotFromContextMenu()
    {
        LoadGame();
    }

    [ContextMenu("Delete Default Slot")]
    private void DeleteDefaultSlotFromContextMenu()
    {
        DeleteSave();
    }

    [ContextMenu("Log Default Save Path")]
    private void LogDefaultSavePath()
    {
        Debug.Log(GetSavePath(DefaultSlotNumber), this);
    }

    [ContextMenu("Run Save Diagnostics")]
    public void LogSaveDiagnostics()
    {
        FindReferences();

        string path = GetSavePath(DefaultSlotNumber);
        bool exists = File.Exists(path);
        long size = exists ? new FileInfo(path).Length : 0L;

        Debug.Log(
            "[SaveManager Diagnostics]\n" +
            $"Object={name}\n" +
            $"IsPrimary={(Instance == this)}\n" +
            $"IsDuplicateForwarder={isDuplicateForwarder}\n" +
            $"DefaultSlot={DefaultSlotNumber}\n" +
            $"CurrentManualSlot={CurrentManualSlotNumber}\n" +
            $"ManualSaveCount={CountManualSaveFiles()} / {MaxManualSlots}\n" +
            $"AutoSaveExists={HasAutoSaveData()}\n" +
            $"GameSessionManager={(gameSessionManager != null ? "OK" : "未検出")}\n" +
            $"ItemDataDatabase={(itemDataDatabase != null ? "OK" : "未設定（保存可・ロード不可）")}\n" +
            $"PlayerStatusSession={(PlayerStatusSessionStore.HasData ? "保存値あり" : "未取得")}\n" +
            $"SaveFileExists={exists}\n" +
            $"SaveFileSize={size:N0} bytes\n" +
            $"SavePath={path}",
            this
        );
    }

    [ContextMenu("Open Save Folder")]
    public void OpenSaveFolder()
    {
        string folder = GetSaveFolderPath();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        Application.OpenURL("file:///" + folder.Replace("\\", "/"));
    }

    private void CaptureCurrentSceneToSession()
    {
        if (captureInventoryBeforeSave)
        {
            PlayerInventorySessionBridge inventoryBridge =
                FindBestComponentInActiveScene<PlayerInventorySessionBridge>();

            if (inventoryBridge != null)
            {
                inventoryBridge.CaptureToSession();
            }
            else
            {
                Log("保存前同期：現在シーンにPlayerInventorySessionBridgeがないため、GameSessionManagerの既存データを使用します。");
            }
        }

        if (captureMissionsBeforeSave)
        {
            MissionSessionBridge missionBridge =
                FindBestComponentInActiveScene<MissionSessionBridge>();

            if (missionBridge != null)
            {
                missionBridge.CaptureToSession();
            }
            else
            {
                Log("保存前同期：現在シーンにMissionSessionBridgeがないため、GameSessionManagerの既存データを使用します。");
            }
        }

        if (capturePlayerStatusBeforeSave)
        {
            PlayerStatusSaveBridge statusBridge =
                FindBestComponentInActiveScene<PlayerStatusSaveBridge>();

            if (statusBridge != null)
            {
                statusBridge.CaptureToSession();
            }
            else if (PlayerStatusSessionStore.HasData)
            {
                Log("保存前同期：現在シーンにPlayerStatusSaveBridgeがないため、探索シーンで保持したプレイヤー状態を使用します。");
            }
            else
            {
                Log("保存前同期：PlayerStatusSaveBridgeも保持済みプレイヤー状態もありません。HP等は今回のセーブ対象に含まれません。");
            }
        }


        if (captureWorldStateBeforeSave)
        {
            WorldStateSaveBridge worldBridge =
                FindBestComponentInActiveScene<WorldStateSaveBridge>();

            if (worldBridge != null)
            {
                worldBridge.CaptureToSession();
            }
            else
            {
                Log("保存前同期：現在シーンにWorldStateSaveBridgeがないため、保持済みワールド状態を使用します。");
            }
        }
    }

    private void ApplyLoadedSessionToCurrentScene()
    {
        if (restoreInventoryAfterLoad)
        {
            PlayerInventorySessionBridge inventoryBridge =
                FindBestComponentInActiveScene<PlayerInventorySessionBridge>();

            if (inventoryBridge != null)
            {
                inventoryBridge.ReloadFromSession();
            }
            else
            {
                Log("ロード後反映：現在シーンにPlayerInventorySessionBridgeがありません。");
            }
        }

        if (restoreMissionsAfterLoad)
        {
            MissionSessionBridge missionBridge =
                FindBestComponentInActiveScene<MissionSessionBridge>();

            if (missionBridge != null)
            {
                missionBridge.ReloadFromSession();
            }
            else
            {
                Log("ロード後反映：現在シーンにMissionSessionBridgeがありません。");
            }
        }

        if (restorePlayerStatusAfterLoad)
        {
            PlayerStatusSaveBridge statusBridge =
                FindBestComponentInActiveScene<PlayerStatusSaveBridge>();

            if (statusBridge != null)
            {
                statusBridge.ReloadFromSession();
            }
            else if (PlayerStatusSessionStore.HasData)
            {
                Log("ロード後反映：現在シーンにPlayerStatusSaveBridgeがありません。次にBridge付きPlayerが生成された時に復元します。");
            }
        }


        if (restoreWorldStateAfterLoad)
        {
            WorldStateSaveBridge worldBridge =
                FindBestComponentInActiveScene<WorldStateSaveBridge>();

            if (worldBridge != null)
            {
                worldBridge.ReloadFromSession();
            }
            else if (WorldStateSessionStore.HasAnyData)
            {
                Log("ロード後反映：現在シーンにWorldStateSaveBridgeがありません。");
            }
        }
    }

    private System.Collections.Generic.List<SavedConversationHistoryData> CaptureConversationHistory()
    {
        var result = new System.Collections.Generic.List<SavedConversationHistoryData>();
        TownConversationHistoryManager manager = TownConversationHistoryManager.GetOrCreate();
        if (manager == null || manager.Entries == null) return result;

        foreach (TownConversationHistoryEntry entry in manager.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResidentId)) continue;
            result.Add(new SavedConversationHistoryData
            {
                ResidentId = entry.ResidentId,
                CompletedConversationCount = entry.CompletedConversationCount
            });
        }

        return result;
    }

    private void ApplyConversationHistory(System.Collections.Generic.List<SavedConversationHistoryData> entries)
    {
        if (!saveConversationHistory || entries == null) return;
        TownConversationHistoryManager manager = TownConversationHistoryManager.GetOrCreate();
        if (manager == null) return;

        manager.ClearAllHistory();
        foreach (SavedConversationHistoryData entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResidentId)) continue;
            manager.SetCompletedConversationCount(entry.ResidentId, entry.CompletedConversationCount);
        }
    }

    private T FindBestComponentInActiveScene<T>() where T : Behaviour
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
        Scene activeScene = SceneManager.GetActiveScene();
        T firstInScene = null;

        foreach (T component in components)
        {
            if (component == null || component.gameObject.scene != activeScene)
            {
                continue;
            }

            if (component.gameObject.activeInHierarchy && component.enabled)
            {
                return component;
            }

            if (firstInScene == null)
            {
                firstInScene = component;
            }
        }

        return firstInScene;
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }

        if (itemDataDatabase == null)
        {
            ItemDataDatabase[] loadedDatabases =
                Resources.FindObjectsOfTypeAll<ItemDataDatabase>();

            if (loadedDatabases != null && loadedDatabases.Length == 1)
            {
                itemDataDatabase = loadedDatabases[0];
                Log($"ItemDataDatabaseを自動取得しました：{itemDataDatabase.name}");
            }
        }
    }

    private bool ForwardToPrimaryIfDuplicate(Action<SaveManager> action)
    {
        if (Instance == null || Instance == this)
        {
            return false;
        }

        action?.Invoke(Instance);
        return true;
    }

    private bool Finish(bool success, string message)
    {
        LastOperationMessage = message ?? string.Empty;

        if (success)
        {
            Log(LastOperationMessage);
        }
        else
        {
            Debug.LogWarning($"[SaveManager] {LastOperationMessage}", this);
        }

        OperationFinished?.Invoke(success, LastOperationMessage);
        return success;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] {message}", this);
        }
    }

    private static void TryDeleteTemporaryFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // 一時ファイル削除失敗は、元の例外を優先します。
        }
    }

    private void OnValidate()
    {
        defaultSlotNumber = Mathf.Clamp(
            defaultSlotNumber,
            1,
            MaxManualSlots
        );
    }
}
