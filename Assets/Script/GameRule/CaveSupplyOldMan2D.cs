using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟のお爺さん用支援物資処理。
/// Playerが近づくと、このお爺さん個体につき1回だけ抽選して物資を地面へ落とします。
/// 診断ログ版：Player検索・距離判定・設定不足・ドロップ生成まで原因をConsoleで追えます。
/// </summary>
[DisallowMultipleComponent]
public class CaveSupplyOldMan2D : MonoBehaviour
{
    [Serializable]
    public class SupplyEntry
    {
        [SerializeField] private ItemData itemData;
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;
        [SerializeField, Min(1)] private int minimumAmount = 1;
        [SerializeField, Min(1)] private int maximumAmount = 1;
        public ItemData ItemData => itemData;
        public float DropChance => Mathf.Clamp01(dropChance);
        public int MinimumAmount => Mathf.Max(1, minimumAmount);
        public int MaximumAmount => Mathf.Max(MinimumAmount, maximumAmount);
        public void Validate() {
            dropChance = Mathf.Clamp01(dropChance);
            minimumAmount = Mathf.Max(1, minimumAmount);
            maximumAmount = Mathf.Max(minimumAmount, maximumAmount);
        }
    }

    [Header("Playerレイヤー範囲検知")]
    [Tooltip("このレイヤーが検知範囲に入ると物資を落とします。Playerレイヤーを指定してください。")]
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("お爺さんを中心にした検知半径です。")]
    [SerializeField, Min(0.1f)] private float triggerDistance = 3f;
    [Tooltip("検知円の中心をお爺さんのTransformからずらしたい場合に使用します。")]
    [SerializeField] private Vector2 detectionOffset = Vector2.zero;

    [Header("支援物資")]
    [SerializeField] private WorldItemPickup worldItemPickupPrefab;
    [SerializeField] private List<SupplyEntry> supplies = new List<SupplyEntry>();
    [SerializeField] private bool guaranteeAtLeastOneDrop = true;

    [Header("ドロップ位置・飛び方")]
    [SerializeField] private Vector3 localDropOffset = new Vector3(0.8f, 0.5f, 0f);
    [SerializeField] private Vector2 horizontalVelocityRange = new Vector2(-1.2f, 1.2f);
    [SerializeField] private Vector2 verticalVelocityRange = new Vector2(1.5f, 2.5f);
    [SerializeField, Min(0f)] private float positionScatter = 0.25f;

    [Header("設定")]
    [SerializeField] private bool activeOnStart = true;
    [SerializeField] private bool showDebugLogs = true;

    [Header("診断ログ")]
    [Tooltip("ON推奨。起動時に現在の設定状態をまとめてConsoleへ出します。")]
    [SerializeField] private bool logStartupDiagnosis = true;    [Tooltip("ONにすると、待機中のOverlapCircle診断を指定秒数ごとに表示します。")]
    [SerializeField] private bool logDetectionWhileWaiting = true;
    [SerializeField, Min(0.2f)] private float detectionLogInterval = 1f;
    private bool hasDroppedSupplies;
    private bool isActive;
    private float nextDetectionLogTime;

    public bool HasDroppedSupplies => hasDroppedSupplies;

    private void Awake()
    {
        isActive = activeOnStart;
        if (logStartupDiagnosis)
            PrintDiagnosis("Awake");
    }

    private void Start()
    {
        if (logStartupDiagnosis)
            PrintDiagnosis("Start");
    }

    private void Update()
    {
        if (!isActive || hasDroppedSupplies)
            return;

        Vector2 center = (Vector2)transform.position + detectionOffset;
        Collider2D hit = Physics2D.OverlapCircle(center, triggerDistance, playerLayer);

        if (logDetectionWhileWaiting && Time.unscaledTime >= nextDetectionLogTime)
        {
            nextDetectionLogTime = Time.unscaledTime + detectionLogInterval;
            Log($"待機中: PlayerLayerMask={playerLayer.value}, 検知中心={center}, 半径={triggerDistance:F2}, " +
                $"検知結果={(hit != null ? hit.name + " / Layer=" + LayerMask.LayerToName(hit.gameObject.layer) : "なし")}");
        }

        if (hit == null)
            return;

        Log($"Playerレイヤーを検知しました: Object={hit.name}, Layer={LayerMask.LayerToName(hit.gameObject.layer)}。ドロップ処理を開始します。");
        DropSuppliesOnce();
    }

    public void SetActive(bool value)
    {
        isActive = value;
        Log($"SetActive({value}) が呼ばれました。");
    }

    [ContextMenu("診断情報をConsoleへ表示")]
    public void PrintDiagnosisFromContextMenu() => PrintDiagnosis("ContextMenu");

    [ContextMenu("Drop Supplies Once")]
    public void DropSuppliesOnce()
    {
        Log("DropSuppliesOnce() が呼ばれました。");

        if (hasDroppedSupplies)
        {
            LogWarning("すでに物資をドロップ済みのため処理を中止しました。");
            return;
        }

        if (worldItemPickupPrefab == null)
        {
            LogError("World Item Pickup Prefabが未設定です。InspectorでWorldItemPickup付きPrefabを設定してください。");
            return;
        }

        List<SupplyEntry> valid = GetValidSupplies();
        if (valid.Count == 0)
        {
            LogError(
                "有効な支援物資が0件です。SuppliesにItemDataを設定し、Drop Chanceを0より大きくしてください。" +
                $" Supplies総数={(supplies == null ? 0 : supplies.Count)}");
            return;
        }

        // 設定不備で失敗した時に再試行できるよう、ここで初めてドロップ済みにする。
        hasDroppedSupplies = true;

        int droppedTypes = 0;
        foreach (SupplyEntry entry in valid)
        {
            float roll = UnityEngine.Random.value;
            Log($"抽選: Item={entry.ItemData.name}, Chance={entry.DropChance:F2}, Roll={roll:F3}");

            if (roll > entry.DropChance)
            {
                Log($"抽選外れ: {entry.ItemData.name}");
                continue;
            }

            if (DropEntry(entry))
                droppedTypes++;
        }

        if (droppedTypes == 0 && guaranteeAtLeastOneDrop)
        {
            SupplyEntry fallback = valid[UnityEngine.Random.Range(0, valid.Count)];
            Log($"全抽選が外れたため保証ドロップ: {fallback.ItemData.name}");
            if (DropEntry(fallback))
                droppedTypes = 1;
        }

        if (droppedTypes == 0)
        {
            LogWarning("ドロップされた種類数が0でした。Guarantee At Least One DropがOFF、または生成処理に失敗しています。");
        }
        else
        {
            Log($"支援物資ドロップ完了。種類数={droppedTypes}");
        }
    }

    private bool DropEntry(SupplyEntry entry)
    {
        if (entry == null || entry.ItemData == null)
        {
            LogError("DropEntryに無効なSupplyEntryが渡されました。");
            return false;
        }

        int totalAmount = UnityEngine.Random.Range(entry.MinimumAmount, entry.MaximumAmount + 1);
        int originalAmount = totalAmount;
        int maxStack = Mathf.Max(1, entry.ItemData.MaxStack);
        int stackCount = 0;

        try
        {
            while (totalAmount > 0)
            {
                int stackAmount = Mathf.Min(totalAmount, maxStack);
                totalAmount -= stackAmount;

                Vector3 basePos = transform.TransformPoint(localDropOffset);
                Vector3 pos = basePos + new Vector3(
                    UnityEngine.Random.Range(-positionScatter, positionScatter),
                    UnityEngine.Random.Range(-positionScatter, positionScatter), 0f);

                WorldItemPickup pickup = Instantiate(worldItemPickupPrefab, pos, Quaternion.identity);
                if (pickup == null)
                {
                    LogError($"Instantiateに失敗しました: {entry.ItemData.name}");
                    return false;
                }

                InventoryItem item = new InventoryItem(entry.ItemData, 0, 0, stackAmount);
                pickup.Setup(item);
                pickup.SetVelocity(new Vector2(
                    UnityEngine.Random.Range(horizontalVelocityRange.x, horizontalVelocityRange.y),
                    UnityEngine.Random.Range(verticalVelocityRange.x, verticalVelocityRange.y)));

                stackCount++;
                Log($"生成成功: Item={entry.ItemData.name}, Amount={stackAmount}, Position={pos}, Pickup={pickup.name}");
            }

            Log($"Itemドロップ完了: {entry.ItemData.name} 合計={originalAmount}, Stack数={stackCount}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Item生成中に例外が発生しました: Item={entry.ItemData.name}\n{ex}");
            return false;
        }
    }

    private List<SupplyEntry> GetValidSupplies()
    {
        var result = new List<SupplyEntry>();

        if (supplies == null)
        {
            LogWarning("Suppliesリスト自体がnullです。");
            return result;
        }

        for (int i = 0; i < supplies.Count; i++)
        {
            SupplyEntry e = supplies[i];

            if (e == null)
            {
                LogWarning($"Supplies[{i}] がnullです。");
                continue;
            }

            if (e.ItemData == null)
            {
                LogWarning($"Supplies[{i}] のItem Dataが未設定です。");
                continue;
            }

            if (e.DropChance <= 0f)
            {
                LogWarning($"Supplies[{i}] ({e.ItemData.name}) のDrop Chanceが0です。");
                continue;
            }

            result.Add(e);
        }

        return result;
    }

    private void PrintDiagnosis(string source)
    {
        int supplyCount = supplies == null ? 0 : supplies.Count;
        int validCount = GetValidSupplies().Count;

        Log(
            $"=== お爺さん物資 診断 [{source}] ===\n" +
            $"GameObject={name}\n" +
            $"ComponentEnabled={enabled}, GameObjectActive={gameObject.activeInHierarchy}\n" +
            $"ActiveOnStart={activeOnStart}, IsActive={isActive}, HasDropped={hasDroppedSupplies}\n" +
            $"TriggerDistance={triggerDistance}\n" +
            $"DetectionOffset={detectionOffset}\n" +
            $"PlayerLayerMaskValue={playerLayer.value}\n" +
            $"WorldItemPickupPrefab={(worldItemPickupPrefab != null ? worldItemPickupPrefab.name : "未設定")}\n" +
            $"Supplies={supplyCount}, ValidSupplies={validCount}\n" +
            $"GuaranteeAtLeastOneDrop={guaranteeAtLeastOneDrop}\n" +
            $"ShowDebugLogs={showDebugLogs}\n" +
            "================================");
    }

    private void Log(string m)
    {
        if (showDebugLogs)
            Debug.Log($"[CaveSupplyOldMan2D:{name}] {m}", this);
    }

    private void LogWarning(string m)
    {
        Debug.LogWarning($"[CaveSupplyOldMan2D:{name}] {m}", this);
    }

    private void LogError(string m)
    {
        Debug.LogError($"[CaveSupplyOldMan2D:{name}] {m}", this);
    }

    private void OnValidate()
    {
        triggerDistance = Mathf.Max(0.1f, triggerDistance);
        positionScatter = Mathf.Max(0f, positionScatter);
        detectionLogInterval = Mathf.Max(0.2f, detectionLogInterval);

        if (horizontalVelocityRange.x > horizontalVelocityRange.y)
            (horizontalVelocityRange.x, horizontalVelocityRange.y) =
                (horizontalVelocityRange.y, horizontalVelocityRange.x);

        if (verticalVelocityRange.x > verticalVelocityRange.y)
            (verticalVelocityRange.x, verticalVelocityRange.y) =
                (verticalVelocityRange.y, verticalVelocityRange.x);

        if (supplies == null)
            supplies = new List<SupplyEntry>();

        foreach (SupplyEntry e in supplies)
            e?.Validate();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere((Vector2)transform.position + detectionOffset, triggerDistance);
        Gizmos.DrawWireSphere(transform.TransformPoint(localDropOffset), 0.18f);
    }
#endif
}
