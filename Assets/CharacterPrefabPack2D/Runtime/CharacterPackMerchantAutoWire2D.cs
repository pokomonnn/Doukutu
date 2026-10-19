using System.Reflection;
using UnityEngine;

/// <summary>
/// PrefabからScene参照を保持できないため、実行時に共通売買画面だけを接続する。
/// DungeonNpcShop2D v1.3.5 のprivate serialize fieldを安全に探す互換ブリッジ。
/// 既存ショップの計算・保存・在庫処理は変更しない。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DungeonNpcShop2D))]
public sealed class CharacterPackMerchantAutoWire2D : MonoBehaviour
{
    [Tooltip("Scene上の売買UIを直接設定できる場合はここを利用します。PrefabからはScene参照不可なので通常は空欄でOK")]
    [SerializeField] private TownUnifiedTradeScreen2D sharedScreenOverride;
    [SerializeField] private bool printWarnings = true;

    private void Awake() { Connect(); }

    // 1体ごとのDungeonNpcShop2Dが全員同時にEを処理しないよう、
    // 商人Prefabでは通常のEを無効にし、最寄り1体だけが処理する。
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (DialoguePresenter2D.Active != null && DialoguePresenter2D.Active.IsPlaying) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        CharacterPackMerchantAutoWire2D[] all = Object.FindObjectsByType<CharacterPackMerchantAutoWire2D>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        CharacterPackMerchantAutoWire2D nearest = null;
        float best = 2.5f * 2.5f;
        foreach (CharacterPackMerchantAutoWire2D m in all)
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            float d=(m.transform.position-player.transform.position).sqrMagnitude;
            if(d < best) { best=d;nearest=m; }
        }
        if(nearest != this) return;
        DungeonNpcShop2D shop = GetComponent<DungeonNpcShop2D>();
        if(shop == null || shop.IsOpen) return;
        if(sharedScreenOverride == null)
        {
            // 最初のPrefab配置時に売買Canvasが後から初期化された場合にも対応。
            Connect();
        }
        if (!shop.TryOpenShop(out string reason) && printWarnings)
            Debug.LogWarning("[CharacterPrefabPack 商人] 取引失敗: " + reason, this);
    }


    [ContextMenu("共通売買UIの参照を再取得")]
    public void Connect()
    {
        DungeonNpcShop2D shop = GetComponent<DungeonNpcShop2D>();
        MerchantStockInventory stock = GetComponentInChildren<MerchantStockInventory>(true);
        if (shop == null || stock == null) { Warn("MerchantStockInventoryがありません"); return; }
        TownUnifiedTradeScreen2D screen = sharedScreenOverride;
        if (screen == null)
        {
            TownUnifiedTradeScreen2D[] screens = Object.FindObjectsByType<TownUnifiedTradeScreen2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TownUnifiedTradeScreen2D candidate in screens)
                if (candidate != null && candidate.gameObject.scene.IsValid() &&
                    candidate.gameObject.scene == gameObject.scene)
                { screen = candidate; break; }
        }
        if (screen == null) { Warn("シーンにTownUnifiedTradeScreen2Dが見つかりません。STEP4の共通売買PrefabをCanvasに配置してください"); return; }
        bool ok = Put(shop, "useSharedTownTradeUI", true) &&
                  Put(shop, "sharedTownTradeScreen", screen) &&
                  Put(shop, "sharedMerchantStock", stock);
        if (!ok) Warn("DungeonNpcShop2Dのフィールドが想定と異なります。STEP4 v1.3.5を確認してください");
    }
    private static bool Put(DungeonNpcShop2D shop, string name, object val)
    {
        var field = typeof(DungeonNpcShop2D).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || !field.FieldType.IsInstanceOfType(val)) return false;
        field.SetValue(shop, val);
        return true;
    }
    private void Warn(string message)
    { if (printWarnings) Debug.LogWarning("[CharacterPrefabPack 商人] "+name+": "+message,this); }
}
