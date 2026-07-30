using System.Collections.Generic;
using UnityEngine;

public class StoneThrower : MonoBehaviour
{
    [Header("石の設定")]
    [SerializeField] private GameObject stonePrefab;
    [SerializeField] private Transform rightThrowPoint;
    [SerializeField] private Transform leftThrowPoint;

    [Header("投げる設定")]
    [SerializeField] private float minThrowSpeed = 5f;
    [SerializeField] private float maxThrowSpeed = 15f;
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float throwInterval = 0.5f;
    [SerializeField] private float throwUpPower = 0.3f;

    [Header("入力設定")]
    [SerializeField] private KeyCode throwKey = KeyCode.F;

    [Header("プレイヤー")]
    [SerializeField] private PlayerMove playerMove;

    private float lastThrowTime;
    private float chargeStartTime;
    private bool isCharging;

    // ロープ接続表示中など、複数の機能からF投擲を止めるためのロックです。
    private readonly HashSet<object> throwControlLocks =
        new HashSet<object>();

    public bool IsThrowControlLocked => throwControlLocks.Count > 0;

    private void Awake()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }
    }

    private void OnDisable()
    {
        isCharging = false;
    }

    private void Update()
    {
        if (IsThrowControlLocked)
        {
            isCharging = false;
            return;
        }

        if (Input.GetKeyDown(throwKey))
        {
            StartCharge();
        }

        if (Input.GetKeyUp(throwKey))
        {
            ReleaseThrow();
        }
    }

    /// <summary>
    /// ロープ操作など、Fキーを使用する別機能から石投げを一時停止します。
    /// ownerごとに管理するため、別のロックが残っている間は再開しません。
    /// </summary>
    public void SetThrowControlLock(object owner, bool locked)
    {
        if (owner == null)
        {
            return;
        }

        bool changed = locked
            ? throwControlLocks.Add(owner)
            : throwControlLocks.Remove(owner);

        if (changed && locked)
        {
            isCharging = false;
        }
    }

    private void StartCharge()
    {
        if (IsThrowControlLocked)
        {
            return;
        }

        // 投げる間隔中ならチャージ開始しない
        if (Time.time < lastThrowTime + throwInterval)
        {
            return;
        }

        isCharging = true;
        chargeStartTime = Time.time;
    }

    private void ReleaseThrow()
    {
        if (!isCharging || IsThrowControlLocked)
        {
            isCharging = false;
            return;
        }

        isCharging = false;

        float chargeTime = Time.time - chargeStartTime;

        // 0〜1のチャージ率にする
        float chargeRate = Mathf.Clamp01(
            chargeTime / Mathf.Max(0.01f, maxChargeTime)
        );

        // チャージ率に応じて速度を決める
        float throwSpeed = Mathf.Lerp(
            minThrowSpeed,
            maxThrowSpeed,
            chargeRate
        );

        ThrowStone(throwSpeed);
    }

    private void ThrowStone(float throwSpeed)
    {
        if (stonePrefab == null || playerMove == null)
        {
            Debug.LogWarning(
                "[StoneThrower] Stone Prefab または PlayerMove が未設定です。",
                this
            );
            return;
        }

        lastThrowTime = Time.time;

        bool isFacingRight = playerMove.IsFacingRight;

        Transform selectedThrowPoint = isFacingRight
            ? rightThrowPoint
            : leftThrowPoint;

        if (selectedThrowPoint == null)
        {
            Debug.LogWarning(
                "[StoneThrower] Throw Point が未設定です。",
                this
            );
            return;
        }

        float xDirection = isFacingRight ? 1f : -1f;

        GameObject stone = Instantiate(
            stonePrefab,
            selectedThrowPoint.position,
            Quaternion.identity
        );

        IgnoreCollisionWithPlayer(stone);

        Rigidbody2D rb = stone.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogWarning(
                "[StoneThrower] Stone Prefab にRigidbody2Dがありません。",
                stone
            );
            return;
        }

        Vector2 throwDirection =
            new Vector2(xDirection, throwUpPower).normalized;

        rb.linearVelocity = throwDirection * throwSpeed;
    }

    private void IgnoreCollisionWithPlayer(GameObject stone)
    {
        Collider2D[] playerColliders =
            GetComponentsInChildren<Collider2D>();

        Collider2D[] stoneColliders =
            stone.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D playerCol in playerColliders)
        {
            foreach (Collider2D stoneCol in stoneColliders)
            {
                if (playerCol != null && stoneCol != null)
                {
                    Physics2D.IgnoreCollision(playerCol, stoneCol);
                }
            }
        }
    }

    private void OnValidate()
    {
        minThrowSpeed = Mathf.Max(0f, minThrowSpeed);
        maxThrowSpeed = Mathf.Max(minThrowSpeed, maxThrowSpeed);
        maxChargeTime = Mathf.Max(0.01f, maxChargeTime);
        throwInterval = Mathf.Max(0f, throwInterval);
    }
}
