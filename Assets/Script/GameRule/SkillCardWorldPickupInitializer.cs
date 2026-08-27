using UnityEngine;

/// <summary>
/// 洞窟などへスキルカードを直接配置するための補助です。
/// WorldItemPickupと同じGameObjectへ付け、Skill Card Dataを指定してください。
/// 既にWorldItemPickupが別データで初期化済みの場合は上書きしません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItemPickup))]
public class SkillCardWorldPickupInitializer : MonoBehaviour
{
    [SerializeField] private SkillCardData skillCardData;
    [SerializeField] private bool initializeOnStart = true;

    private WorldItemPickup pickup;

    private void Awake()
    {
        pickup = GetComponent<WorldItemPickup>();
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializePickup();
        }
    }

    public bool InitializePickup()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldItemPickup>();
        }

        if (pickup == null || skillCardData == null)
        {
            return false;
        }

        // セーブ復元・スポナーなどが先に設定済みなら、その内容を優先する。
        if (pickup.HasValidDroppedItem)
        {
            return true;
        }

        InventoryItem item = new InventoryItem(
            skillCardData,
            0,
            0,
            1
        );

        pickup.Setup(item, false);
        return true;
    }
}
