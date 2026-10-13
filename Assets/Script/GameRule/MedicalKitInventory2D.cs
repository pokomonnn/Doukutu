using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 応急キットの固定3枠版。アイテム本体は不要で、Inventory画面のボタンから開けます。
/// 3種類のConsumableItemDataを固定し、各枠に最大3個まで保持します。
/// 応急キット表示は横3マス×縦2マス。各アイテムは横1×縦2マスです。
/// 数量0になっても枠とイラストはそのまま残ります。
/// 現在のランタイム数量はMedicalKitSessionStore2DとSaveManagerに連携して保存します。
/// </summary>
[DisallowMultipleComponent]
public class MedicalKitInventory2D : MonoBehaviour
{
    [Serializable]
    public class FixedSlot
    {
        [Tooltip("ここに固定表示する医療用品のConsumableItemDataを設定します")]
        public ConsumableItemData itemData;

        [Tooltip("現在の収納数。0でも枠とアイコンは表示されます")]
        [Min(0)] public int amount;
    }

    [Header("参照")]
    [SerializeField] private InventoryController playerInventory;

    [Header("固定の3枠（0=解毒剤、1=副木、2=包帯など）")]
    [SerializeField] private FixedSlot[] fixedSlots =
    {
        new FixedSlot(), new FixedSlot(), new FixedSlot()
    };

    [Header("各固定枠の最大収納数")]
    [SerializeField, Min(1)] private int maxAmountPerSlot = 3;

    [Header("NPC治療：固定枠ごとの対応症状（各1種類）")]
    [Tooltip("固定枠0の用途。通常は解毒剤＝Poison。プレイヤー自身の治療には影響しません。")]
    [SerializeField] private StatusConditionType npcSlot0Treats = StatusConditionType.Poison;
    [Tooltip("固定枠1の用途。通常は副木＝Fracture。")]
    [SerializeField] private StatusConditionType npcSlot1Treats = StatusConditionType.Fracture;
    [Tooltip("固定枠2の用途。通常は包帯＝Bleeding。")]
    [SerializeField] private StatusConditionType npcSlot2Treats = StatusConditionType.Bleeding;

    [Header("ログ")]
    [SerializeField] private bool showDebugLogs = true;

    // 旧版の保存済みデータを参照するため残しておきます。
    [SerializeField, HideInInspector] private List<ConsumableItemData> allowedItems = new List<ConsumableItemData>();
    [SerializeField, HideInInspector] private InventoryGrid kitGrid = new InventoryGrid();

    public event Action Changed;

    public int Width => 3;
    public int Height => 2;
    // 応急キット内ではすべての固定品目が横1マス×縦2マスを占有。
    // 通常InventoryのItemData.Width/Heightは変更しません。
    public int FixedItemWidth => 1;
    public int FixedItemHeight => 2;
    public int SlotCount => 3;
    public int MaxAmountPerSlot => Mathf.Max(1, maxAmountPerSlot);
    public InventoryController PlayerInventory => playerInventory;

    // 旧APIを壊さないため保持。固定枠はこのGridではなくFixedSlotsに保存します。
    public InventoryGrid Grid => kitGrid;

    public float ContentsWeightKg
    {
        get
        {
            float total = 0f;
            EnsureSlots();
            for (int i = 0; i < SlotCount; i++)
            {
                FixedSlot slot = fixedSlots[i];
                if (slot.itemData != null)
                    total += Mathf.Max(0f, slot.itemData.Weight) * Mathf.Max(0, slot.amount);
            }
            // 古いキット内アイテムが移行できず残っている場合も重量を数えます。
            if (kitGrid != null)
                foreach (InventoryItem item in kitGrid.Items)
                    if (item != null && item.ItemData != null)
                        total += Mathf.Max(0f, item.ItemData.Weight) * Mathf.Max(0, item.Amount);
            return total;
        }
    }

    private void Awake()
    {
        FindInventory();
        EnsureSlots();
        if (playerInventory == null)
            Debug.LogWarning("[MedicalKit][警告] Player Inventoryが未設定です。PlayerのInventoryControllerを指定してください。", this);
        ValidateItemSlots();
    }

    private void Start()
    {
        // シーン移動後は一時保存した数量が正。Prefabの初期数量で上書きしない。
        // 新規ゲームのみ、古いKitGridから移行して初期数量を確定します。
        if (!MedicalKitSessionStore2D.HasData)
            MigrateLegacyItems();

        MedicalKitSessionStore2D.Bind(this);
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        MedicalKitSessionStore2D.Unbind(this);
    }

    private void OnValidate()
    {
        maxAmountPerSlot = Mathf.Max(1, maxAmountPerSlot);
        EnsureSlots();
        for (int i = 0; i < SlotCount; i++)
            fixedSlots[i].amount = Mathf.Clamp(fixedSlots[i].amount, 0, maxAmountPerSlot);
    }

    private void FindInventory()
    {
        if (playerInventory != null) return;
        playerInventory = GetComponent<InventoryController>();
        if (playerInventory == null) playerInventory = GetComponentInParent<InventoryController>();
    }

    private void EnsureSlots()
    {
        if (fixedSlots == null || fixedSlots.Length != 3)
        {
            FixedSlot[] old = fixedSlots;
            fixedSlots = new FixedSlot[3];
            for (int i = 0; i < 3; i++)
                fixedSlots[i] = old != null && i < old.Length && old[i] != null ? old[i] : new FixedSlot();
        }
        for (int i = 0; i < 3; i++)
            if (fixedSlots[i] == null) fixedSlots[i] = new FixedSlot();

        // v1.3 の Allowed Items の登録を初回のみ引き継ぐ。
        bool allEmpty = true;
        for (int i = 0; i < 3; i++)
            if (fixedSlots[i].itemData != null) allEmpty = false;
        if (allEmpty && allowedItems != null)
        {
            int dst = 0;
            foreach (ConsumableItemData data in allowedItems)
            {
                if (data == null || dst >= 3) continue;
                bool duplicate = false;
                for (int j = 0; j < dst; j++)
                    if (fixedSlots[j].itemData == data) duplicate = true;
                if (duplicate) continue;
                fixedSlots[dst++].itemData = data;
            }
        }
    }

    private void ValidateItemSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (fixedSlots[i].itemData == null)
                Debug.LogWarning($"[MedicalKit][設定] 固定枠{i + 1}のItemDataが未設定です。Inspectorで解毒剤・副木・包帯などを割り当ててください。", this);
            for (int j = 0; j < i; j++)
                if (fixedSlots[i].itemData != null && fixedSlots[i].itemData == fixedSlots[j].itemData)
                    Debug.LogWarning($"[MedicalKit][設定] 固定枠{i + 1}と{j + 1}に同じItemDataが設定されています。重複しないようにしてください。", this);
        }
    }

    private int FindSlot(ItemData data)
    {
        if (data == null) return -1;
        EnsureSlots();
        for (int i = 0; i < SlotCount; i++)
            if (fixedSlots[i].itemData == data) return i;
        return -1;
    }

    /// <summary>旧バージョンとの互換性用。所持品としての応急キットは不要です。</summary>
    public bool HasMedicalKit() => true;

    public bool IsAllowed(ItemData data) => FindSlot(data) >= 0;

    public ConsumableItemData GetItemAtSlot(int slotIndex)
    {
        EnsureSlots();
        return slotIndex >= 0 && slotIndex < SlotCount ? fixedSlots[slotIndex].itemData : null;
    }

    public int GetAmountAtSlot(int slotIndex)
    {
        EnsureSlots();
        return slotIndex >= 0 && slotIndex < SlotCount ? fixedSlots[slotIndex].amount : 0;
    }

    /// <summary>
    /// 治療できる状態。ConsumableItemDataのCured Conditionsを優先。
    /// 未設定のときのみ固定枠の用途（0=毒、1=骨折、2=出血）を補います。
    /// 既存のInspectorデータを失わず副木による骨折回復を可能にするための互換措置です。
    /// </summary>
    public StatusConditionType GetTreatmentConditions(ConsumableItemData data)
    {
        if (data == null) return StatusConditionType.None;
        if (data.CuredConditions != StatusConditionType.None)
            return data.CuredConditions;
        int index = FindSlot(data);
        switch (index)
        {
            case 0: return StatusConditionType.Poison;
            case 1: return StatusConditionType.Fracture;
            case 2: return StatusConditionType.Bleeding;
            default: return StatusConditionType.None;
        }
    }

    /// <summary>
    /// NPC専用：固定スロットに設定された1種類の症状のみ処置できる。
    /// ItemDataのCuredConditionsやHealAmountとは独立させ、別症状への誤使用を防止。
    /// None / 複数ビットの設定は安全のため使用不可にする。
    /// プレイヤー治療は従来のGetTreatmentConditionsを使う。
    /// </summary>
    public StatusConditionType GetNpcTreatmentCondition(ConsumableItemData data)
    {
        switch (FindSlot(data))
        {
            case 0: return SingleNpcSymptom(npcSlot0Treats);
            case 1: return SingleNpcSymptom(npcSlot1Treats);
            case 2: return SingleNpcSymptom(npcSlot2Treats);
            default: return StatusConditionType.None;
        }
    }

    private static StatusConditionType SingleNpcSymptom(StatusConditionType symptom)
    {
        switch (symptom)
        {
            case StatusConditionType.Bleeding:
            case StatusConditionType.Poison:
            case StatusConditionType.Fracture:
                return symptom;
            default:
                return StatusConditionType.None;
        }
    }

    public int GetAmount(ItemData data)
    {
        int index = FindSlot(data);
        return index < 0 ? 0 : Mathf.Max(0, fixedSlots[index].amount);
    }

    /// <summary>
    /// 通常Inventoryのスタックから空き分だけ収納（例えば2/3なら1個だけ移す）。
    /// 通常Inventory側の残数は残り、キットの数量だけ増えます。
    /// </summary>
    public bool TryPut(InventoryItem item, out string message)
    {
        message = string.Empty;
        FindInventory();
        if (playerInventory == null || playerInventory.Grid == null ||
            item == null || item.ItemData == null || !playerInventory.Grid.ContainsItem(item))
        { message = "通常インベントリにそのアイテムがありません"; return false; }

        int index = FindSlot(item.ItemData);
        if (index < 0)
        { message = "このアイテムの固定枠はありません"; return false; }

        int free = MaxAmountPerSlot - fixedSlots[index].amount;
        if (free <= 0)
        { message = "この固定枠は満杯です（最大" + MaxAmountPerSlot + "個）"; return false; }

        int request = Mathf.Min(free, item.Amount);
        int removed = playerInventory.RemoveItemAmount(item, request);
        if (removed <= 0)
        { message = "通常インベントリから移動できませんでした"; return false; }

        fixedSlots[index].amount += removed;
        message = $"{fixedSlots[index].itemData.DisplayName}を{removed}個補充（{fixedSlots[index].amount}/{MaxAmountPerSlot}）";
        Log("[収納] " + message);
        Changed?.Invoke();
        return true;
    }

    /// <summary>固定枠から通常Inventoryへ1個だけ取り出します。0枠は残ります。</summary>
    public bool TryTake(ItemData data, out string message)
    {
        message = string.Empty;
        FindInventory();
        int index = FindSlot(data);
        if (index < 0) { message = "固定枠がありません"; return false; }
        if (fixedSlots[index].amount <= 0)
        { message = "在庫が0個です"; return false; }
        if (playerInventory == null)
        { message = "通常インベントリがありません"; return false; }

        // 先にInventoryへの追加が成功したことを確認してからキットの数量を減らす。
        playerInventory.TryAddItem(data, 1, out int remaining);
        if (remaining != 0)
        { message = "通常インベントリに空きがありません"; return false; }

        fixedSlots[index].amount--;
        message = $"{data.DisplayName}を1個取り出しました（残り{fixedSlots[index].amount}）";
        Log("[取出] " + message);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 旧UI互換の取り出しAPI。旧Grid内のInventoryItemを持つ旧UI用。
    /// 固定枠版では新しいTryTake(ItemData)を使用してください。
    /// </summary>
    public bool TryTake(InventoryItem item, out string message)
    {
        return TryTake(item != null ? item.ItemData : null, out message);
    }

    /// <summary>
    /// ⑨ NPC応急処置などから呼び出す消費API。消費してもアイコンは消えず、0を表示します。
    /// </summary>
    public bool TryConsume(ItemData data, int amount = 1)
    {
        int index = FindSlot(data);
        if (index < 0 || amount <= 0 || fixedSlots[index].amount < amount) return false;
        fixedSlots[index].amount -= amount;
        Log($"[使用] {data.DisplayName} x{amount} / 残り{fixedSlots[index].amount}");
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// セッション/セーブから固定3枠の数量を一括復元します。
    /// 途中状態をUIに通知せず、3枠の反映後にChangedを1回通知します。
    /// ItemDataの設定やNPCの対応症状は変更しません。
    /// </summary>
    public void RestoreSavedAmounts(int slot0, int slot1, int slot2)
    {
        EnsureSlots();
        int capacity = MaxAmountPerSlot;
        fixedSlots[0].amount = Mathf.Clamp(slot0, 0, capacity);
        fixedSlots[1].amount = Mathf.Clamp(slot1, 0, capacity);
        fixedSlots[2].amount = Mathf.Clamp(slot2, 0, capacity);
        Changed?.Invoke();
    }

    /// <summary>セーブ／ロードを接続するときに利用できる数量設定API。</summary>
    public bool TrySetAmount(ItemData data, int amount)
    {
        int index = FindSlot(data);
        if (index < 0 || amount < 0 || amount > MaxAmountPerSlot) return false;
        fixedSlots[index].amount = amount;
        Changed?.Invoke();
        return true;
    }

    // v1.3 の古いKitGridに残っている実体を、固定枠へできるだけ移し、余剰は通常Inventoryへ戻す。
    // 通常Inventoryに空きがない余剰は旧Gridに残し、消滅させない。
    [ContextMenu("移行：旧キット収納データを固定枠へ移動")]
    public void MigrateLegacyItems()
    {
        FindInventory();
        if (kitGrid == null || kitGrid.Items == null || kitGrid.Items.Count == 0) return;

        kitGrid.RebuildSlotMap();
        List<InventoryItem> snapshot = new List<InventoryItem>(kitGrid.Items);
        foreach (InventoryItem old in snapshot)
        {
            if (old == null || old.ItemData == null || !kitGrid.ContainsItem(old)) continue;
            int index = FindSlot(old.ItemData);
            if (index >= 0)
            {
                int moved = Mathf.Min(Mathf.Max(0, MaxAmountPerSlot - fixedSlots[index].amount), old.Amount);
                if (moved > 0)
                {
                    kitGrid.RemoveAmount(old, moved);
                    fixedSlots[index].amount += moved;
                    Log($"[旧データ移行] {old.ItemData.DisplayName} x{moved} を固定枠へ移行");
                }
            }
            if (old.Amount > 0 && playerInventory != null)
            {
                playerInventory.TryAddItem(old.ItemData, old.Amount, out int remaining);
                int movedToPlayer = old.Amount - remaining;
                if (movedToPlayer > 0) kitGrid.RemoveAmount(old, movedToPlayer);
            }
        }
        if (kitGrid.Items.Count > 0)
            Debug.LogWarning("[MedicalKit][旧データ移行] メインInventoryが満杯などの理由で旧収納データが残っています。" +
                "アイテム消失を防ぐため旧Gridに保持しています。空きを作って『移行』を再実行してください。", this);
        Changed?.Invoke();
    }


    [ContextMenu("TEST：固定3枠を各3個にする（Play中のみ）")]
    private void TestFillAllSlots()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[MedicalKit][TEST] Playモード中に実行してください。", this);
            return;
        }
        EnsureSlots();
        for (int i = 0; i < SlotCount; i++)
            fixedSlots[i].amount = fixedSlots[i].itemData != null ? MaxAmountPerSlot : 0;
        Log("[TEST] 3枠を満タンに設定しました。UIを確認してください。");
        Changed?.Invoke();
    }

    [ContextMenu("TEST：3番目（包帯）の枠を1個使う（Play中のみ）")]
    private void TestConsumeThirdSlot()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[MedicalKit][TEST] Playモード中に実行してください。", this);
            return;
        }
        ConsumableItemData data = GetItemAtSlot(2);
        if (data == null || !TryConsume(data, 1))
            Debug.LogWarning("[MedicalKit][TEST] 3番目の固定枠が未設定、または在庫0です。", this);
    }

    [ContextMenu("診断：応急キット固定枠をConsoleに表示")]
    private void DiagnosticReport()
    {
        FindInventory();
        EnsureSlots();
        Debug.Log($"[MedicalKit][診断] Mode=Fixed3_1x2 / Grid=3x2 / PlayerInventory={(playerInventory != null ? playerInventory.name : "NULL")} / Capacity={MaxAmountPerSlot} / LegacyStacks={(kitGrid != null ? kitGrid.Items.Count : 0)}", this);
        for (int i = 0; i < SlotCount; i++)
            Debug.Log($"[MedicalKit][診断] 固定枠{i + 1}：{(fixedSlots[i].itemData != null ? fixedSlots[i].itemData.DisplayName : "未設定")} / {fixedSlots[i].amount}/{MaxAmountPerSlot}", this);
    }

    private void Log(string text)
    {
        if (showDebugLogs) Debug.Log("[MedicalKit] " + text, this);
    }
}
