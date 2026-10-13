using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPCへの応急処置が成功した時点で、今回の探索の治療実績として記録します。
/// 治療実績（回収不要）と救出実績（エレベーター回収必須）を別々に集計します。
/// 
/// 既存の MedicalKitTreatment2D の RecordSuccessfulTreatment と、
/// ExpeditionResultCollector2D の CaptureFromElevator の接続はそのまま使用します。
/// </summary>
public static class RescueNpcTreatmentResultSession2D
{
    public sealed class Report
    {
        // NPCへの治療に成功した人数：エレベーター回収の有無とは無関係。
        public int TreatedNpcCount;
        public int BleedingTreated;
        public int PoisonTreated;
        public int FractureTreated;
        public int TreatedNotRecovered;

        // 既存のエレベーター回収実績：従来どおり回収したNPCだけ。
        public int RescuedAlive;
        public int TreatedAndRescued;
        public int UnstabilizedRescued;
        public int TreatedButDeadRecovered;

        // Resultシーンの治療報酬が二重で支払われないようにする記録。
        // このReportはExpeditionResultSession.Currentと同一探索結果の間だけ有効です。
        public bool TreatmentRewardPaid;
        public int TreatmentRewardPaidAmount;
    }

    private struct TreatmentRecord
    {
        public int SceneHandle;
        public StatusConditionType Treated;
    }

    private const StatusConditionType Treatable =
        StatusConditionType.Bleeding | StatusConditionType.Poison | StatusConditionType.Fracture;

    // 記録するのは「プレイヤーが今回、実際に処置した症状」のみ。
    // Prefabで最初からStabilizedになっている症状は含めません。
    private static readonly Dictionary<int, TreatmentRecord> treatmentByNpc =
        new Dictionary<int, TreatmentRecord>();

    private static ExpeditionResultData associatedResult;
    private static Report report;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        treatmentByNpc.Clear();
        associatedResult = null;
        report = null;
    }

    /// <summary>Resultシーンに渡った探索結果に対応する治療実績。</summary>
    public static Report Current
    {
        get
        {
            ExpeditionResultData current = ExpeditionResultSession.Current;
            return current != null && ReferenceEquals(current, associatedResult)
                ? report : null;
        }
    }

    /// <summary>
    /// 実際にNPCの応急処置が成功したときだけ呼びます。
    /// NPCごと／症状ごとに重複計上されません。未回収NPCもここで記録します。
    /// </summary>
    public static void RecordSuccessfulTreatment(
        RescuePersonSymptoms2D npc,
        StatusConditionType newlyStabilized)
    {
        if (npc == null || npc.IsDead) return;

        newlyStabilized &= Treatable;
        if (newlyStabilized == StatusConditionType.None) return;

        int npcId = npc.GetInstanceID();
        int currentSceneHandle = npc.gameObject.scene.handle;
        TreatmentRecord existing;
        if (!treatmentByNpc.TryGetValue(npcId, out existing) ||
            existing.SceneHandle != currentSceneHandle)
        {
            existing = new TreatmentRecord
            {
                SceneHandle = currentSceneHandle,
                Treated = StatusConditionType.None
            };
        }

        StatusConditionType newlyRecorded = newlyStabilized & ~existing.Treated;
        if (newlyRecorded == StatusConditionType.None) return;

        existing.Treated |= newlyRecorded;
        treatmentByNpc[npcId] = existing;

        Debug.Log("[RescueTreatment][記録] NPC=" + npc.name +
                  " / 新規処置=" + newlyRecorded +
                  " / このNPCへの累計処置=" + existing.Treated +
                  " / エレベーター回収前でも記録済み", npc);
    }

    /// <summary>
    /// 洞窟を出る際にExpeditionResultCollector2Dから呼びます。
    /// 治療実績は今回処置した全NPCから計算し、救出実績のみReceiverを参照します。
    /// </summary>
    public static void CaptureFromElevator(
        ElevatorCarryReceiver2D receiver,
        ExpeditionResultData result)
    {
        Report next = new Report();
        associatedResult = result;
        report = next;

        if (result == null)
        {
            Debug.LogWarning("[RescueTreatment][集計失敗] ExpeditionResultDataがnullです。");
            return;
        }

        // 【A】エレベーターを使わずに帰っても、治療成功記録を数える。
        foreach (KeyValuePair<int, TreatmentRecord> pair in treatmentByNpc)
        {
            StatusConditionType treated = pair.Value.Treated & Treatable;
            if (treated == StatusConditionType.None) continue;

            next.TreatedNpcCount++;
            if ((treated & StatusConditionType.Bleeding) != 0) next.BleedingTreated++;
            if ((treated & StatusConditionType.Poison) != 0) next.PoisonTreated++;
            if ((treated & StatusConditionType.Fracture) != 0) next.FractureTreated++;
        }

        // 【B】エレベーター回収実績は従来どおりReceiverを見て数える。
        // Receiverが未設定でも【A】の治療実績が失われることはありません。
        if (receiver == null)
        {
            Debug.LogWarning("[RescueTreatment][回収集計警告] Receiverがありません。" +
                             "NPC治療実績は集計済みですが、救出実績は0人として扱います。");
        }
        else
        {
            List<CarryableObject2D> cargo = receiver.GetCargoSnapshot(true);
            if (cargo != null)
            {
                var alreadyCounted = new HashSet<int>();
                foreach (CarryableObject2D carryable in cargo)
                {
                    if (carryable == null) continue;

                    RescuePersonTarget2D rescue =
                        FindOnCarryable<RescuePersonTarget2D>(carryable);
                    if (rescue == null) continue;

                    RescuePersonSymptoms2D npc =
                        FindOnCarryable<RescuePersonSymptoms2D>(carryable);
                    CharacterHealth health =
                        FindOnCarryable<CharacterHealth>(carryable);

                    int uniqueId = npc != null ? npc.GetInstanceID() : rescue.GetInstanceID();
                    if (!alreadyCounted.Add(uniqueId)) continue;

                    bool dead = health != null && health.IsDead;
                    StatusConditionType treated = StatusConditionType.None;
                    if (npc != null &&
                        treatmentByNpc.TryGetValue(npc.GetInstanceID(), out TreatmentRecord recorded) &&
                        recorded.SceneHandle == npc.gameObject.scene.handle)
                    {
                        treated = recorded.Treated & Treatable;
                    }

                    if (dead)
                    {
                        if (treated != StatusConditionType.None)
                            next.TreatedButDeadRecovered++;
                        continue;
                    }

                    next.RescuedAlive++;
                    if (treated != StatusConditionType.None) next.TreatedAndRescued++;
                    if (npc != null &&
                        ((npc.ActiveSymptoms & ~npc.StabilizedSymptoms & Treatable) != 0))
                    {
                        next.UnstabilizedRescued++;
                    }
                }
            }
        }

        next.TreatedNotRecovered = Mathf.Max(0,
            next.TreatedNpcCount - next.TreatedAndRescued - next.TreatedButDeadRecovered);

        Debug.Log("[RescueTreatment][集計完了 v3.6] " +
                  "治療したNPC=" + next.TreatedNpcCount +
                  " / 止血=" + next.BleedingTreated +
                  " / 解毒=" + next.PoisonTreated +
                  " / 骨折固定=" + next.FractureTreated +
                  " / 治療して生存救出=" + next.TreatedAndRescued +
                  " / 治療後未回収=" + next.TreatedNotRecovered +
                  " / 治療後死亡回収=" + next.TreatedButDeadRecovered +
                  " / 生存救出合計=" + next.RescuedAlive);

        // 次の洞窟出撃へ過去の治療履歴を持ち越さない。
        // Result表示に必要な値はすでにReportへコピー済みです。
        treatmentByNpc.Clear();
    }

    private static T FindOnCarryable<T>(CarryableObject2D carryable) where T : Component
    {
        T found = carryable.GetComponent<T>();
        if (found != null) return found;
        found = carryable.GetComponentInChildren<T>(true);
        return found != null ? found : carryable.GetComponentInParent<T>();
    }
}
