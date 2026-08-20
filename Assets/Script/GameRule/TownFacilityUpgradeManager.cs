using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Town_Mainで施設アップグレードを実行します。
/// 必要素材の確認・消費・施設レベル更新・インベントリのセッション同期を担当します。
/// </summary>
[DisallowMultipleComponent]
public class TownFacilityUpgradeManager : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("町のプレイヤーインベントリです。未設定なら自動取得します。")]
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;

    [Tooltip("未設定ならGameSessionManager.Instanceを使用します。")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("表示文言")]
    [SerializeField] private string successFormat =
        "{0}を Lv{1} にアップグレードしました。";

    [SerializeField] private string maxLevelFormat =
        "{0}はすでに最大レベルです。";

    [SerializeField] private string missingMaterialHeader =
        "アップグレード素材が足りません。";

    [SerializeField] private string missingMaterialLineFormat =
        "{0}  {1}/{2}";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool TryUpgradeFacility(
        TownFacilityUpgradeData facilityData,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        FindReferences();

        if (facilityData == null)
        {
            resultMessage = "施設アップグレードデータが設定されていません。";
            LogWarning(resultMessage);
            return false;
        }

        if (gameSessionManager == null)
        {
            resultMessage = "GameSessionManagerが見つかりません。";
            LogWarning(resultMessage);
            return false;
        }

        InventoryController inventory = townPlayerInventory != null
            ? townPlayerInventory.InventoryController
            : null;

        if (inventory == null || inventory.Grid == null)
        {
            resultMessage = "町のプレイヤーインベントリが見つかりません。";
            LogWarning(resultMessage);
            return false;
        }

        int currentLevel = gameSessionManager.GetFacilityLevel(
            facilityData.FacilityId,
            facilityData.StartingLevel
        );

        if (currentLevel >= facilityData.MaxLevel)
        {
            resultMessage = SafeFormat(
                maxLevelFormat,
                facilityData.FacilityName
            );
            return false;
        }

        int targetLevel = currentLevel + 1;
        TownFacilityUpgradeLevel upgrade =
            facilityData.GetUpgradeLevel(targetLevel);

        if (upgrade == null)
        {
            resultMessage =
                $"{facilityData.FacilityName} Lv{targetLevel} のアップグレード設定がありません。";
            LogWarning(resultMessage);
            return false;
        }

        if (!HasRequiredItems(
                inventory,
                upgrade,
                out resultMessage))
        {
            return false;
        }

        List<ConsumedItemRecord> consumed =
            new List<ConsumedItemRecord>();

        if (!ConsumeRequiredItems(
                inventory,
                upgrade,
                consumed,
                out resultMessage))
        {
            RestoreConsumedItems(inventory, consumed);
            return false;
        }

        if (!gameSessionManager.SetFacilityLevel(
                facilityData.FacilityId,
                targetLevel,
                facilityData.StartingLevel))
        {
            RestoreConsumedItems(inventory, consumed);
            resultMessage =
                $"{facilityData.FacilityName}の施設レベル更新に失敗しました。";
            LogWarning(resultMessage);
            return false;
        }

        CapturePlayerInventory();

        resultMessage = SafeFormat(
            successFormat,
            facilityData.FacilityName,
            targetLevel
        );

        Log(
            $"アップグレード成功: {facilityData.FacilityName} / " +
            $"Lv{currentLevel}→Lv{targetLevel}"
        );

        return true;
    }

    public bool CanUpgradeFacility(
        TownFacilityUpgradeData facilityData,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        FindReferences();

        if (facilityData == null || gameSessionManager == null)
        {
            resultMessage = "施設情報を取得できません。";
            return false;
        }

        InventoryController inventory = townPlayerInventory != null
            ? townPlayerInventory.InventoryController
            : null;

        if (inventory == null || inventory.Grid == null)
        {
            resultMessage = "町のプレイヤーインベントリが見つかりません。";
            return false;
        }

        int currentLevel = gameSessionManager.GetFacilityLevel(
            facilityData.FacilityId,
            facilityData.StartingLevel
        );

        if (currentLevel >= facilityData.MaxLevel)
        {
            resultMessage = SafeFormat(
                maxLevelFormat,
                facilityData.FacilityName
            );
            return false;
        }

        TownFacilityUpgradeLevel upgrade =
            facilityData.GetUpgradeLevel(currentLevel + 1);

        if (upgrade == null)
        {
            resultMessage = "次のレベルの設定がありません。";
            return false;
        }

        return HasRequiredItems(inventory, upgrade, out resultMessage);
    }

    private bool HasRequiredItems(
        InventoryController inventory,
        TownFacilityUpgradeLevel upgrade,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (inventory == null || upgrade == null)
        {
            resultMessage = "アップグレード条件を確認できません。";
            return false;
        }

        StringBuilder missing = new StringBuilder();
        bool hasMissing = false;

        IReadOnlyList<TownFacilityUpgradeRequirement> requirements =
            upgrade.RequiredItems;

        if (requirements != null)
        {
            foreach (TownFacilityUpgradeRequirement requirement in requirements)
            {
                if (requirement == null || requirement.ItemData == null)
                {
                    continue;
                }

                int requiredAmount = requirement.Amount;
                int currentAmount = inventory.GetTotalAmount(
                    requirement.ItemData
                );

                if (currentAmount >= requiredAmount)
                {
                    continue;
                }

                hasMissing = true;

                if (missing.Length > 0)
                {
                    missing.AppendLine();
                }

                missing.Append(
                    SafeFormat(
                        missingMaterialLineFormat,
                        requirement.ItemData.DisplayName,
                        currentAmount,
                        requiredAmount
                    )
                );
            }
        }

        if (!hasMissing)
        {
            return true;
        }

        resultMessage = string.IsNullOrWhiteSpace(missingMaterialHeader)
            ? missing.ToString()
            : missingMaterialHeader + "\n" + missing;

        return false;
    }

    private bool ConsumeRequiredItems(
        InventoryController inventory,
        TownFacilityUpgradeLevel upgrade,
        List<ConsumedItemRecord> consumed,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        IReadOnlyList<TownFacilityUpgradeRequirement> requirements =
            upgrade.RequiredItems;

        if (requirements == null)
        {
            return true;
        }

        foreach (TownFacilityUpgradeRequirement requirement in requirements)
        {
            if (requirement == null || requirement.ItemData == null)
            {
                continue;
            }

            int requiredAmount = requirement.Amount;
            int removed = inventory.RemoveAmountByItemData(
                requirement.ItemData,
                requiredAmount
            );

            if (removed > 0)
            {
                consumed.Add(
                    new ConsumedItemRecord(
                        requirement.ItemData,
                        removed
                    )
                );
            }

            if (removed == requiredAmount)
            {
                continue;
            }

            resultMessage =
                $"{requirement.ItemData.DisplayName} の消費に失敗しました。";
            LogWarning(
                $"素材消費失敗: {requirement.ItemData.DisplayName} / " +
                $"必要={requiredAmount} / 消費={removed}"
            );
            return false;
        }

        return true;
    }

    private void RestoreConsumedItems(
        InventoryController inventory,
        List<ConsumedItemRecord> consumed)
    {
        if (inventory == null || consumed == null)
        {
            return;
        }

        foreach (ConsumedItemRecord record in consumed)
        {
            if (record.ItemData == null || record.Amount <= 0)
            {
                continue;
            }

            bool restored = inventory.TryAddItem(
                record.ItemData,
                record.Amount,
                out int remaining
            );

            if (!restored || remaining > 0)
            {
                LogWarning(
                    $"ロールバック時に {record.ItemData.DisplayName} を " +
                    $"{remaining}個戻せませんでした。"
                );
            }
        }
    }

    private void CapturePlayerInventory()
    {
        if (townPlayerInventory == null)
        {
            return;
        }

        PlayerInventorySessionBridge bridge =
            townPlayerInventory.SessionBridge;

        if (bridge != null)
        {
            bridge.CaptureToSession();
            return;
        }

        if (gameSessionManager != null &&
            townPlayerInventory.InventoryController != null)
        {
            gameSessionManager.CapturePlayerInventory(
                townPlayerInventory.InventoryController,
                townPlayerInventory.EquipmentController
            );
        }
    }

    private void FindReferences()
    {
        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<TownPlayerInventoryController>(
                    FindObjectsInactive.Include
                );
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private static string SafeFormat(
        string format,
        params object[] args)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return string.Empty;
        }

        try
        {
            return string.Format(format, args);
        }
        catch (System.FormatException)
        {
            return format;
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TownFacilityUpgradeManager] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[TownFacilityUpgradeManager] {message}",
            this
        );
    }

    private readonly struct ConsumedItemRecord
    {
        public ItemData ItemData { get; }
        public int Amount { get; }

        public ConsumedItemRecord(ItemData itemData, int amount)
        {
            ItemData = itemData;
            Amount = amount;
        }
    }
}
