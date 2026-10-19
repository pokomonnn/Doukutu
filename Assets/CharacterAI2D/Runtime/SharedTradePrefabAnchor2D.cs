using UnityEngine;

/// <summary>
/// 町の売買UIから生成した、町/洞窟で共用するPrefabのルート識別子。
/// Prefab内のManager/Windowは元の町のUIと同一構造です。
/// </summary>
[DisallowMultipleComponent]
public sealed class SharedTradePrefabAnchor2D : MonoBehaviour
{
    [SerializeField] private TownUnifiedTradeScreen2D screen;
    [SerializeField] private ExpeditionUnifiedTradeController2D controller;

    public TownUnifiedTradeScreen2D Screen => screen;
    public ExpeditionUnifiedTradeController2D Controller => controller;

    public void Configure(TownUnifiedTradeScreen2D targetScreen,
        ExpeditionUnifiedTradeController2D targetController)
    {
        screen = targetScreen;
        controller = targetController;
    }
}
