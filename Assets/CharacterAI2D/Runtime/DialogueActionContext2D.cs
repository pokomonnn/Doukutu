using UnityEngine;

/// <summary>必要な参照を上書き指定できます。未設定なら実行時に自動検索します。</summary>
[DisallowMultipleComponent]
public sealed class DialogueActionContext2D : MonoBehaviour
{
    [Header("STEP4 会話連携（未設定なら自動検索）")]
    public DungeonNpcShop2D shop;
    public MissionManager2D missionManager;
    public InventoryController playerInventory;
}
