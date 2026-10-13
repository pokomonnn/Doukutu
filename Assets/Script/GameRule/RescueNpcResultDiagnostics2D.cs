using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// v3.5: 救出NPCがリザルトへ届かない原因を調べる、ログ専用の診断ツール。
/// 既存の救出・治療・保存処理を一切変更しません。
/// Assets内へ配置するだけでPlay開始時に1つだけ自動生成され、シーンをまたいで追跡します。
/// </summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public sealed class RescueNpcResultDiagnostics2D : MonoBehaviour
{
    private static RescueNpcResultDiagnostics2D instance;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [Header("診断間隔（秒）")]
    [SerializeField, Range(0.15f, 3f)] private float checkInterval = 0.40f;
    [SerializeField] private bool logCargoWhenChanged = true;
    [SerializeField] private bool logTreatmentRecords = true;
    [SerializeField] private bool logUiStatusOnSceneLoad = true;

    private float nextCheck;
    private int previousCargoHash = int.MinValue;
    private ExpeditionResultData lastResult;
    private bool previousResultWasPresent;
    private int previousTreatmentRecordCount = -1;
    private string previousSceneName;
    private readonly Dictionary<int, bool> exitWasConfirming = new Dictionary<int, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnce()
    {
        if (instance != null) return;
        GameObject host = new GameObject("[診断] RescueNPC_ResultFlow_v3_5");
        host.AddComponent<RescueNpcResultDiagnostics2D>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        PlayerCarryController2D.AnyCarryableDropped += OnCarryableDropped;
    }

    private void Start()
    {
        Log("D00", "診断開始 / 現在Scene=" + SceneManager.GetActiveScene().name +
            " / このComponentはPlay開始時に自動生成されます");
        StartCoroutine(SceneSnapshotAfterDelay(SceneManager.GetActiveScene().name));
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        PlayerCarryController2D.AnyCarryableDropped -= OnCarryableDropped;
        if (instance == this) instance = null;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        ExpeditionResultData data = ExpeditionResultSession.Current;
        Log("D39", "Scene破棄直前=" + scene.name +
            " / Session=" + (data != null ? Summary(data) : "NULL(Collector未実行・Clear・別ルートの可能性)"));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log("D50", "Scene読み込み: " + scene.name + " / 前Scene=" + previousSceneName +
            " / mode=" + mode);
        previousSceneName = scene.name;
        previousCargoHash = int.MinValue;
        exitWasConfirming.Clear();
        StartCoroutine(SceneSnapshotAfterDelay(scene.name));
    }

    private IEnumerator SceneSnapshotAfterDelay(string name)
    {
        yield return null;
        // Start()内でResult UIが結果を読むまでの時間を与える
        yield return new WaitForSecondsRealtime(0.20f);
        DiagnoseAll("Scene開始:" + name);
    }

    private void OnCarryableDropped(PlayerCarryController2D player,
        CarryableObject2D target, PlayerCarryState2D fromState, bool manualDrop)
    {
        string targetName = target != null ? FullPath(target.transform) : "NULL";
        Log("D10", "荷物を下ろした / NPC候補=" + targetName +
            " / 直前の運搬状態=" + fromState + " / 手動Drop=" + manualDrop);
        if (target != null) DescribeCargo(target, "D11");
        StartCoroutine(DropSnapshotAfterDelay());
    }

    private IEnumerator DropSnapshotAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.85f);
        Log("D12", "Dropから0.85秒後（収納・自動移動の結果）");
        DiagnoseReceivers();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + Mathf.Max(0.15f, checkInterval);

        ExpeditionResultData current = ExpeditionResultSession.Current;
        if (!ReferenceEquals(current, lastResult))
        {
            if (current == null && previousResultWasPresent)
                Warn("D41", "ExpeditionResultSession.CurrentがNULLになりました！Resultへ渡す前にClearされた可能性があります");
            else if (current != null)
                Log("D40", "ExpeditionResultSessionに探索結果がセットされました / " + Summary(current));
            lastResult = current;
            previousResultWasPresent = current != null;
            if (current != null) DiagnoseTreatmentReport();
        }

        int recorded = GetTreatmentRecordCount();
        if (recorded != previousTreatmentRecordCount)
        {
            if (previousTreatmentRecordCount >= 0)
                Log("D08", "NPC治療履歴数が変化: " + previousTreatmentRecordCount + " -> " + recorded);
            previousTreatmentRecordCount = recorded;
        }

        int cargoHash = 17;
        ElevatorCarryReceiver2D[] receivers = AllReceivers();
        foreach (ElevatorCarryReceiver2D receiver in receivers)
        {
            if (receiver == null) continue;
            unchecked
            {
                cargoHash = cargoHash * 31 + receiver.GetInstanceID();
                List<CarryableObject2D> cargo = receiver.GetCargoSnapshot(true);
                cargoHash = cargoHash * 31 + cargo.Count;
                foreach (CarryableObject2D item in cargo)
                    cargoHash = cargoHash * 31 + (item != null ? item.GetInstanceID() : 0);
            }
        }
        if (cargoHash != previousCargoHash)
        {
            previousCargoHash = cargoHash;
            if (logCargoWhenChanged) DiagnoseReceivers();
        }

        CaveExitElevator2D[] exits = AllExits();
        foreach (CaveExitElevator2D exit in exits)
        {
            if (exit == null) continue;
            int id = exit.GetInstanceID();
            bool open = exit.IsConfirmationOpen;
            bool was;
            exitWasConfirming.TryGetValue(id, out was);
            if (open && !was)
            {
                Log("D30", "洞窟出口の確認パネルが開きました / " + FullPath(exit.transform));
                DiagnoseCollectorLinks();
                DiagnoseReceivers();
                int totalCargo = 0;
                foreach (ElevatorCarryReceiver2D receiver in AllReceivers())
                    if (receiver != null) totalCargo += receiver.GetCargoSnapshot(true).Count;
                if (totalCargo == 0)
                    Warn("D31", "洞窟出口の確認画面時点で全ReceiverのCargo数が0です。救出NPCが収納されているか確認してください");
            }
            exitWasConfirming[id] = open;
        }
    }

    [ContextMenu("診断 / 全経路をConsoleに表示")]
    public void DiagnoseAllFromMenu() { DiagnoseAll("Inspector手動診断"); }

    public void DiagnoseAll(string reason)
    {
        Log("D01", "====== NPC救出→リザルト 診断開始 / 理由=" + reason + " ======");
        Log("D02", "Scene=" + SceneManager.GetActiveScene().name +
            " / Time=" + Time.unscaledTime.ToString("F1") + " / Platform=" + Application.platform);
        DiagnoseCollectorLinks();
        DiagnoseReceivers();
        DiagnoseLiveNpcs();
        DiagnoseExpeditionResult();
        DiagnoseTreatmentReport();
        if (logUiStatusOnSceneLoad) DiagnoseResultUi();
        Log("D99", "====== 診断終了 ======");
    }

    private void DiagnoseCollectorLinks()
    {
        ExpeditionResultCollector2D[] collectors =
            FindObjectsByType<ExpeditionResultCollector2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CaveExitElevator2D[] exits = AllExits();
        ElevatorCarryReceiver2D[] receivers = AllReceivers();
        Log("D20", "Collector数=" + collectors.Length + " / Exit数=" + exits.Length +
            " / ElevatorReceiver数=" + receivers.Length);

        foreach (CaveExitElevator2D exit in exits)
        {
            if (exit == null) continue;
            ExpeditionResultCollector2D link = ReadField<ExpeditionResultCollector2D>(exit, "expeditionResultCollector");
            string sceneName = ReadField<string>(exit, "resultSceneName");
            bool willLoad = ReadField<bool>(exit, "loadResultSceneOnYes");
            Log("D21", "出口=" + FullPath(exit.transform) +
                " / enabled=" + exit.isActiveAndEnabled +
                " / 指定Collector=" + (link != null ? FullPath(link.transform) : "NULL(実行時自動検索の可能性)") +
                " / ResultScene=" + sceneName + " / sceneLoad=" + willLoad);
        }
        foreach (ExpeditionResultCollector2D col in collectors)
        {
            if (col == null) continue;
            ElevatorCarryReceiver2D linked = ReadField<ElevatorCarryReceiver2D>(col, "elevatorCarryReceiver");
            Log("D22", "Collector=" + FullPath(col.transform) +
                " / enabled=" + col.isActiveAndEnabled +
                " / 最終集計=" + (col.LastCollectedResult != null ? Summary(col.LastCollectedResult) : "なし") +
                " / 指定Receiver=" + (linked != null ? FullPath(linked.transform) : "NULL(実行時自動検索の可能性)"));
            if (linked == null)
                Warn("D23", "CollectorのReceiverがNULLです。複数Receiverがあると別のReceiverを自動選択するおそれがあります");
            else if (linked.GetCargoSnapshot(true).Count == 0)
            {
                foreach (ElevatorCarryReceiver2D another in receivers)
                {
                    if (another != null && another != linked && another.GetCargoSnapshot(true).Count > 0)
                        Warn("D24", "別のReceiverに荷物があります！Collectorの参照=" + FullPath(linked.transform) +
                            " / 荷物があるReceiver=" + FullPath(another.transform));
                }
            }
        }
        if (collectors.Length == 0 && exits.Length > 0)
            Warn("D25", "洞窟出口があるのにCollectorが見つかりません。CaveExitElevator2Dの集計参照を確認してください");
    }

    private void DiagnoseReceivers()
    {
        ElevatorCarryReceiver2D[] receivers = AllReceivers();
        if (receivers.Length == 0)
        {
            Warn("D13", "SceneにElevatorCarryReceiver2Dがありません（Resultシーンなら正常です）");
            return;
        }
        foreach (ElevatorCarryReceiver2D receiver in receivers)
        {
            if (receiver == null) continue;
            List<CarryableObject2D> cargo = receiver.GetCargoSnapshot(true);
            Log("D14", "Receiver=" + FullPath(receiver.transform) +
                " / enabled=" + receiver.isActiveAndEnabled +
                " / Cargo数(移動中込)=" + cargo.Count +
                " / 収納完了数=" + receiver.StoredCargoCount +
                " / 空きスロット=" + receiver.FreeSlotCount);
            foreach (CarryableObject2D item in cargo)
                DescribeCargo(item, "D15");
            if (cargo.Count == 0)
                Log("D16", "このReceiverのCargoは0件です。収納完了ログが出たReceiverと同じか確認");
        }
    }

    private void DiagnoseLiveNpcs()
    {
        RescuePersonSymptoms2D[] npcs = FindObjectsByType<RescuePersonSymptoms2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Log("D17", "Scene内の症状Component数=" + npcs.Length);
        int shown = 0;
        foreach (RescuePersonSymptoms2D npc in npcs)
        {
            if (npc == null || shown >= 15) continue;
            shown++;
            CarryableObject2D carry = npc.GetComponentInParent<CarryableObject2D>();
            if (carry == null) carry = npc.GetComponentInChildren<CarryableObject2D>(true);
            RescuePersonTarget2D target = npc.GetComponent<RescuePersonTarget2D>();
            if (target == null) target = npc.GetComponentInParent<RescuePersonTarget2D>();
            if (target == null) target = npc.GetComponentInChildren<RescuePersonTarget2D>(true);
            Log("D18", "NPC=" + FullPath(npc.transform) +
                " / 死亡=" + npc.IsDead +
                " / Active=" + npc.ActiveSymptoms + " / Stabilized=" + npc.StabilizedSymptoms +
                " / Carryable=" + (carry != null ? FullPath(carry.transform) : "NULL") +
                " / RescuePersonTarget2D=" + (target != null ? "あり" : "なし") +
                " / 記録=" + TreatmentRecordSummary(npc));
        }
    }

    private void DiagnoseExpeditionResult()
    {
        ExpeditionResultData current = ExpeditionResultSession.Current;
        Log("D42", "ExpeditionResultSession.Current=" + (current != null ? Summary(current) : "NULL"));
        if (current == null)
        {
            ExpeditionResultCollector2D[] collectors =
                FindObjectsByType<ExpeditionResultCollector2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ExpeditionResultCollector2D col in collectors)
            {
                if (col != null && col.LastCollectedResult != null)
                    Warn("D43", "Collectorには結果がありますが、SessionがNULLです / " + FullPath(col.transform));
            }
            if (SceneManager.GetActiveScene().name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0)
                Warn("D44", "Resultシーンなのに探索結果がNULLです。洞窟出口でCollectAndStoreResult()が呼ばれたか確認");
        }
    }

    private void DiagnoseTreatmentReport()
    {
        RescueNpcTreatmentResultSession2D.Report data = RescueNpcTreatmentResultSession2D.Current;
        if (data == null)
        {
            if (ExpeditionResultSession.Current != null)
                Warn("D45", "救出結果はあるのに治療ResultがNULLです。v3.4のCollector側接続がない、または紐づけ先が違います");
            else
                Log("D45", "結果集計前のため治療ResultはまだNULLです（洞窟探索中なら正常）");
        }
        else
        {
            Log("D46", "治療レポート: 生存=" + data.RescuedAlive +
                " / 治療して救出=" + data.TreatedAndRescued +
                " / 止血=" + data.BleedingTreated +
                " / 解毒=" + data.PoisonTreated +
                " / 固定=" + data.FractureTreated +
                " / 未処置=" + data.UnstabilizedRescued +
                " / 治療後死亡=" + data.TreatedButDeadRecovered);
        }
        if (!logTreatmentRecords) return;
        FieldInfo field = typeof(RescueNpcTreatmentResultSession2D).GetField("treatmentByNpc", PrivateStatic);
        object obj = field != null ? field.GetValue(null) : null;
        System.Collections.IDictionary dict = obj as System.Collections.IDictionary;
        if (dict == null)
        {
            Log("D47", "治療履歴内部辞書の読み取り不可（バージョン差の可能性）");
            return;
        }
        Log("D48", "今回治療したNPCの内部記録数=" + dict.Count);
        if (dict.Count == 0)
        {
            if (ExpeditionResultSession.Current != null)
                Warn("D49", "治療履歴が0です。治療したはずならv3.4のMedicalKitTreatment2D側への記録接続を確認してください");
            else
                Log("D49", "今の時点では治療記録0件です。治療後に再確認してください");
        }
    }

    private void DiagnoseResultUi()
    {
        ExpeditionResultUI[] resultUis = FindObjectsByType<ExpeditionResultUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RescueNpcTreatmentResultUI2D[] treatmentUis = FindObjectsByType<RescueNpcTreatmentResultUI2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Log("D60", "既存ExpeditionResultUI数=" + resultUis.Length +
            " / 治療ResultUI数=" + treatmentUis.Length);
        foreach (ExpeditionResultUI ui in resultUis)
        {
            if (ui == null) continue;
            var aliveText = ReadField<TMPro.TMP_Text>(ui, "rescuedNpcCountText");
            var deadText = ReadField<TMPro.TMP_Text>(ui, "deadNpcCountText");
            Log("D61", "既存ResultUI=" + FullPath(ui.transform) + " / enabled=" + ui.isActiveAndEnabled +
                " / 救出Text=" + (aliveText != null ? aliveText.text : "NULL") +
                " / 死亡Text=" + (deadText != null ? deadText.text : "NULL"));
            if (aliveText == null) Warn("D62", "ExpeditionResultUI の Rescued Npc Count Text が未設定です");
        }
        foreach (RescueNpcTreatmentResultUI2D ui in treatmentUis)
        {
            if (ui == null) continue;
            TMPro.TMP_Text txt = ReadField<TMPro.TMP_Text>(ui, "resultText");
            if (txt == null) txt = ui.GetComponent<TMPro.TMP_Text>();
            Log("D63", "治療ResultUI=" + FullPath(ui.transform) + " / enabled=" + ui.isActiveAndEnabled +
                " / Text=" + (txt != null ? txt.text.Replace("\n", " | ") : "NULL"));
        }
        if (resultUis.Length == 0 && SceneManager.GetActiveScene().name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0)
            Warn("D64", "Resultシーンに既存ExpeditionResultUIがありません");
        if (treatmentUis.Length == 0 && SceneManager.GetActiveScene().name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0)
            Warn("D65", "ResultシーンにRescueNpcTreatmentResultUI2Dがありません");
    }

    private void DescribeCargo(CarryableObject2D carry, string stage)
    {
        if (carry == null) { Log(stage, "Cargo=NULL"); return; }
        RescuePersonTarget2D target = FindLinked<RescuePersonTarget2D>(carry);
        RescuePersonSymptoms2D npc = FindLinked<RescuePersonSymptoms2D>(carry);
        CharacterHealth hp = FindLinked<CharacterHealth>(carry);
        ItemBoxInventory box = FindLinked<ItemBoxInventory>(carry);
        Log(stage, "荷物=" + FullPath(carry.transform) +
            " / scene=" + carry.gameObject.scene.name +
            " / isCarried=" + carry.IsCarried +
            " / RescuePersonTarget2D=" + (target != null ? "あり" : "NULL") +
            " / Symptoms=" + (npc != null ? npc.ActiveSymptoms.ToString() : "なし") +
            " / Stabilized=" + (npc != null ? npc.StabilizedSymptoms.ToString() : "なし") +
            " / Dead=" + (hp != null ? hp.IsDead.ToString() : "HP Component NULL") +
            " / ItemBox=" + (box != null ? "あり" : "なし") +
            " / TreatmentRecord=" + (npc != null ? TreatmentRecordSummary(npc) : "NPCなし"));
        if (target == null && npc != null)
            Warn("D19", "症状はあるのにRescuePersonTarget2Dがありません。既存Collectorでは救出NPCとして数えられません: " + carry.name);
    }

    private static int GetTreatmentRecordCount()
    {
        FieldInfo field = typeof(RescueNpcTreatmentResultSession2D).GetField("treatmentByNpc", PrivateStatic);
        System.Collections.IDictionary dict = (field != null ? field.GetValue(null) : null) as System.Collections.IDictionary;
        return dict != null ? dict.Count : -1;
    }

    private static string TreatmentRecordSummary(RescuePersonSymptoms2D npc)
    {
        if (npc == null) return "NPCなし";
        FieldInfo field = typeof(RescueNpcTreatmentResultSession2D).GetField("treatmentByNpc", PrivateStatic);
        System.Collections.IDictionary dict = (field != null ? field.GetValue(null) : null) as System.Collections.IDictionary;
        if (dict == null) return "辞書取得不可";
        if (!dict.Contains(npc.GetInstanceID())) return "未登録";
        object record = dict[npc.GetInstanceID()];
        if (record == null) return "NULL";
        FieldInfo treatedField = record.GetType().GetField("Treated", PrivateInstance);
        FieldInfo sceneField = record.GetType().GetField("SceneHandle", PrivateInstance);
        object treated = treatedField != null ? treatedField.GetValue(record) : null;
        object handle = sceneField != null ? sceneField.GetValue(record) : null;
        return "記録=" + treated + " / sceneHandle=" + handle;
    }

    private static T FindLinked<T>(CarryableObject2D carry) where T : Component
    {
        if (carry == null) return null;
        T found = carry.GetComponent<T>();
        if (found != null) return found;
        found = carry.GetComponentInChildren<T>(true);
        return found != null ? found : carry.GetComponentInParent<T>();
    }

    private static T ReadField<T>(object source, string fieldName)
    {
        if (source == null) return default(T);
        FieldInfo field = source.GetType().GetField(fieldName, PrivateInstance);
        if (field == null) return default(T);
        object result = field.GetValue(source);
        return result is T ? (T)result : default(T);
    }

    private static ElevatorCarryReceiver2D[] AllReceivers()
    {
        return FindObjectsByType<ElevatorCarryReceiver2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
    private static CaveExitElevator2D[] AllExits()
    {
        return FindObjectsByType<CaveExitElevator2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
    private static string FullPath(Transform transform)
    {
        if (transform == null) return "NULL";
        StringBuilder builder = new StringBuilder(transform.name);
        while (transform.parent != null)
        {
            transform = transform.parent;
            builder.Insert(0, transform.name + "/");
        }
        return builder.ToString();
    }
    private static string Summary(ExpeditionResultData data)
    {
        if (data == null) return "NULL";
        return "生存救出=" + data.RescuedNpcCount + " / 死亡回収=" + data.DeadNpcCount +
            " / ItemBox=" + data.RecoveredItemBoxCount +
            " / お宝種類=" + (data.TreasureItems != null ? data.TreasureItems.Count : 0);
    }
    private void Log(string stage, string message)
    {
        Debug.Log("[RescueFlow][" + stage + "] " + message, this);
    }
    private void Warn(string stage, string message)
    {
        Debug.LogWarning("[RescueFlow][" + stage + "][要確認] " + message, this);
    }
}
