using UnityEngine;

/// <summary>Prefabパック由来の種族識別。派閥IDとは別で、スライム／スケルトンの立ち話混線を防ぐ。</summary>
[DisallowMultipleComponent]
public sealed class CharacterPackRaceIdentity2D : MonoBehaviour
{
    [SerializeField] private string raceCode;
    [SerializeField] private bool isMonster;
    public string RaceCode => raceCode;
    public bool IsMonster => isMonster;
#if UNITY_EDITOR
    public void EditorConfigure(string code, bool monster) { raceCode=code;isMonster=monster; }
#endif
}
