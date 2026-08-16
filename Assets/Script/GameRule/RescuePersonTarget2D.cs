using System;
using UnityEngine;

/// <summary>
/// 救出対象NPCに付けて使う「救出された」状態管理です。
/// CharacterHealthがある場合、死亡済みNPCは救出できません。
/// 会話ControllerやButtonから Rescue() を呼べます。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonTarget2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("救出対象NPCのHP。未設定なら同じObjectまたは親子から自動取得します。")]
    [SerializeField] private CharacterHealth characterHealth;

    [Header("救出後")]
    [Tooltip("ONなら救出後にこのNPCを非表示にします。会話演出などを残したい場合はOFFにします。")]
    [SerializeField] private bool hideAfterRescue;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsRescued => isRescued;
    public bool IsDead => characterHealth != null && characterHealth.IsDead;
    public bool CanRescue => !isRescued && !IsDead;

    public event Action<RescuePersonTarget2D> Rescued;

    private bool isRescued;

    private void Awake()
    {
        FindReferences();
    }

    /// <summary>
    /// 会話の選択肢や救出ボタンから呼びます。
    /// 救出済み、または死亡済みの場合は失敗します。
    /// </summary>
    public bool Rescue()
    {
        FindReferences();

        if (isRescued)
        {
            Log("すでに救出済みのため、Rescue()を実行しませんでした。");
            return false;
        }

        if (IsDead)
        {
            Log("死亡済みのため、Rescue()を実行できません。");
            return false;
        }

        isRescued = true;

        Log("救出されました。");

        Rescued?.Invoke(this);

        if (hideAfterRescue)
        {
            gameObject.SetActive(false);
        }

        return true;
    }

    /// <summary>
    /// セーブ復元などで救出済み状態だけ設定したい時に使えます。
    /// イベントは発火しません。
    /// </summary>
    public void RestoreRescuedState(bool rescued)
    {
        isRescued = rescued;

        if (hideAfterRescue)
        {
            gameObject.SetActive(!rescued);
        }
    }

    private void FindReferences()
    {
        if (characterHealth != null)
        {
            return;
        }

        characterHealth = GetComponent<CharacterHealth>();

        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInChildren<CharacterHealth>(true);
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[RescuePersonTarget2D] {name}: {message}", this);
    }

    private void OnValidate()
    {
        FindReferences();
    }
}
