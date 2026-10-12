using UnityEngine;

/// <summary>
/// UnityEventからチュートリアルを進めたい場合の中継用です。
/// 通常はCaveTutorialIntegration2Dが自動連携するので、必要な時だけ使用してください。
/// </summary>
public sealed class CaveTutorialEventRelay2D : MonoBehaviour
{
    [Tooltip("NPCのルートObject。空欄の場合はこのGameObjectを使用します。")]
    [SerializeField] private GameObject adventurer;

    private GameObject Npc =>
        adventurer != null ? adventurer : gameObject;

    public void JumpSucceeded()
        => CaveTutorialManager2D.Instance?.NotifyJumped();

    public void WallClimbSucceeded()
        => CaveTutorialManager2D.Instance?.NotifyWallClimbed();

    public void ItemBoxOpened()
        => CaveTutorialManager2D.Instance?.NotifyItemBoxOpened();

    // 旧設定互換。
    public void LootSucceeded()
        => CaveTutorialManager2D.Instance?.NotifyItemBoxOpened();

    public void ItemCollected()
        => CaveTutorialManager2D.Instance?.NotifyItemCollected();

    public void WallDescendSucceeded()
        => CaveTutorialManager2D.Instance?.NotifyWallDescended();

    public void ProneEntered()
        => CaveTutorialManager2D.Instance?.NotifyProneEntered();

    public void InventoryOpened()
        => CaveTutorialManager2D.Instance?.NotifyInventoryOpened();

    public void WeaponEquipped()
        => CaveTutorialManager2D.Instance?.NotifyWeaponEquipped();

    public void ReloadCompleted()
        => CaveTutorialManager2D.Instance?.NotifyReloadCompleted();

    public void EnemyHit()
        => CaveTutorialManager2D.Instance?.NotifyEnemyHit();

    public void AdventurerPickedUp()
        => CaveTutorialManager2D.Instance?.NotifyAdventurerPickedUp(Npc);

    public void AdventurerShouldered()
        => CaveTutorialManager2D.Instance?.NotifyAdventurerShouldered(Npc);

    public void AdventurerDropped()
        => CaveTutorialManager2D.Instance?.NotifyAdventurerDropped(Npc);

    public void PlayerEnteredElevator()
        => CaveTutorialManager2D.Instance?.NotifyElevatorEntered();

    public void Fractured()
        => CaveTutorialManager2D.Instance?.SetFractured(true);

    public void FractureHealed()
        => CaveTutorialManager2D.Instance?.SetFractured(false);

    public void NaegiUsedSuccessfully()
        => CaveTutorialManager2D.Instance?.NotifyNaegiUsed();

    public void TorchLevelChanged(float level01)
        => CaveTutorialManager2D.Instance?.SetTorchLevel01(level01);

    public void TorchBrightenedSuccessfully()
        => CaveTutorialManager2D.Instance?.NotifyTorchBrightened();
}
