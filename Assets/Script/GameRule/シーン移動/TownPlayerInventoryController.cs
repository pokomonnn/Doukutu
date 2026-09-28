using UnityEngine;

/// <summary>
/// 旧Town_Main用コンポーネントとの互換性を保つためのクラスです。
/// 実処理はPlayerTradeInventoryBridgeへ共通化されています。
///
/// 既存のTown_Mainではこのコンポーネントをそのまま残して構いません。
/// 新しく作る洞窟PlayerではPlayerTradeInventoryBridgeを直接使用してください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryController))]
[RequireComponent(typeof(EquipmentController))]
[RequireComponent(typeof(PlayerInventorySessionBridge))]
public class TownPlayerInventoryController : PlayerTradeInventoryBridge
{
}
