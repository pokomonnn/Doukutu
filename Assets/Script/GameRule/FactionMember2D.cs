using UnityEngine;

/// <summary>
/// Player/Enemy/NPCのルートGameObjectへ付与する派閥識別コンポーネント。
/// STEP2では攻撃AIに変更を加えないため、付けるだけで既存の攻撃挙動は変化しない。
/// </summary>
[DisallowMultipleComponent]
public sealed class FactionMember2D : MonoBehaviour
{
    [Header("所属派閥（STEP1と同じIDを使用）")]
    [SerializeField] private string factionId = string.Empty;

    [Header("モンスター派閥に所属する場合")]
    [Tooltip("ドワーフがスライムとスケルトンを区別するために使用")]
    [SerializeField] private FactionMonsterType2D monsterType = FactionMonsterType2D.None;

    [Header("将来のSTEP3向け")]
    [Tooltip("中立時でも相手から攻撃を受けたら反撃してよいか。反撃動作はSTEP3で接続します。")]
    [SerializeField] private bool retaliateWhenAttacked = true;

    public string FactionId => FactionId2D.Normalize(factionId);
    public FactionMonsterType2D MonsterType => monsterType;
    public bool IsPlayer => FactionId2D.Same(factionId, FactionId2D.Player);
    public bool RetaliateWhenAttacked => retaliateWhenAttacked;

    public void Configure(string id, FactionMonsterType2D species = FactionMonsterType2D.None)
    {
        factionId = FactionId2D.Normalize(id);
        monsterType = FactionId2D.Same(factionId, FactionId2D.Monsters) ? species : FactionMonsterType2D.None;
    }

    private void OnValidate()
    {
        factionId = FactionId2D.Normalize(factionId);
        if (!FactionId2D.Same(factionId, FactionId2D.Monsters)) monsterType = FactionMonsterType2D.None;
    }
}
