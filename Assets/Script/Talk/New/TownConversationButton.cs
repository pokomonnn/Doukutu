using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の建物やNPCに重ねたButtonから、統一会話を開きます。
/// 商人会話ではMerchantStockInventoryも一緒に記録します。
/// Unity 6000.4以降のFindObjectsByType APIに対応しています。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TownConversationButton : MonoBehaviour
{
    [Header("会話")]
    [SerializeField] private TownConversationController conversationController;
    [SerializeField] private TownConversationData conversationData;

    [Header("商人の実在庫")]
    [Tooltip("Merchant会話の時だけ設定します。この商人の商品を入れたItemBoxInventoryと同じObjectのMerchantStockInventoryです。")]
    [SerializeField] private MerchantStockInventory merchantStockInventory;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private Button targetButton;

    private void Awake()
    {
        FindReferences();

        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OpenConversation);
            targetButton.onClick.AddListener(OpenConversation);
        }
    }

    private void OnDestroy()
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OpenConversation);
        }
    }

    public void OpenConversation()
    {
        FindReferences();

        if (conversationController == null)
        {
            LogWarning("TownConversationControllerが見つかりません。");
            return;
        }

        if (conversationData == null)
        {
            LogWarning("Conversation Dataが未設定です。");
            return;
        }

        if (conversationData.ConversationType ==
            TownConversationType.Merchant)
        {
            MerchantStockInventory resolvedStock =
                ResolveMerchantStock();

            MerchantShopConversationContext.SetMerchant(
                conversationData,
                resolvedStock
            );

            if (resolvedStock == null)
            {
                LogWarning(
                    "Merchant会話ですがMerchant Stock Inventoryが見つかりません。" +
                    "このButtonへ商人のMerchantStockInventoryを設定してください。"
                );
            }
            else if (conversationData.MerchantShopData != null &&
                     resolvedStock.ShopData !=
                     conversationData.MerchantShopData)
            {
                LogWarning(
                    "Conversation DataのMerchant Shop Dataと、" +
                    "Merchant Stock InventoryのShop Dataが一致していません。"
                );
            }
        }
        else
        {
            MerchantShopConversationContext.Clear();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownConversationButton] 会話開始要求: " +
                $"Data={conversationData.name} / " +
                $"Resident={conversationData.ResidentName} / " +
                $"Type={conversationData.ConversationType} / " +
                $"MerchantStock=" +
                $"{(MerchantShopConversationContext.CurrentStock != null ? MerchantShopConversationContext.CurrentStock.name : "未設定")}",
                this
            );
        }

        conversationController.OpenConversation(conversationData);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownConversationButton] 会話開始結果: " +
                $"IsOpen={conversationController.IsOpen} / " +
                $"CurrentBlock={conversationController.CurrentBlockId}",
                this
            );
        }
    }

    private MerchantStockInventory ResolveMerchantStock()
    {
        if (merchantStockInventory != null)
        {
            return merchantStockInventory;
        }

        merchantStockInventory =
            GetComponentInParent<MerchantStockInventory>();

        if (merchantStockInventory == null)
        {
            merchantStockInventory =
                GetComponentInChildren<MerchantStockInventory>(true);
        }

        if (merchantStockInventory != null)
        {
            return merchantStockInventory;
        }

        MerchantShopData targetShop =
            conversationData != null
                ? conversationData.MerchantShopData
                : null;

        if (targetShop == null)
        {
            return null;
        }

        // Unity 6000.4以降ではFindObjectsSortMode指定版が非推奨。
        MerchantStockInventory[] candidates =
            FindObjectsByType<MerchantStockInventory>(
                FindObjectsInactive.Include
            );

        foreach (MerchantStockInventory candidate in candidates)
        {
            if (candidate != null &&
                candidate.ShopData == targetShop)
            {
                merchantStockInventory = candidate;
                return candidate;
            }
        }

        return null;
    }

    private void FindReferences()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (conversationController == null)
        {
            conversationController =
                FindAnyObjectByType<TownConversationController>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[TownConversationButton: {name}] {message}",
            this
        );
    }
}
