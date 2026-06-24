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
    private bool isCharging = false;

    private void Awake()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(throwKey))
        {
            StartCharge();
        }

        if (Input.GetKeyUp(throwKey))
        {
            ReleaseThrow();
        }
    }

    private void StartCharge()
    {
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
        if (!isCharging)
        {
            return;
        }

        isCharging = false;

        float chargeTime = Time.time - chargeStartTime;

        // 0〜1のチャージ率にする
        float chargeRate = Mathf.Clamp01(chargeTime / maxChargeTime);

        // チャージ率に応じて速度を決める
        float throwSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, chargeRate);

        ThrowStone(throwSpeed);
    }

    private void ThrowStone(float throwSpeed)
    {
        lastThrowTime = Time.time;

        bool isFacingRight = playerMove.IsFacingRight;

        Transform selectedThrowPoint = isFacingRight ? rightThrowPoint : leftThrowPoint;

        float xDirection = isFacingRight ? 1f : -1f;

        GameObject stone = Instantiate(
            stonePrefab,
            selectedThrowPoint.position,
            Quaternion.identity
        );

        IgnoreCollisionWithPlayer(stone);

        Rigidbody2D rb = stone.GetComponent<Rigidbody2D>();

        Vector2 throwDirection = new Vector2(xDirection, throwUpPower).normalized;

        rb.linearVelocity = throwDirection * throwSpeed;
    }

    private void IgnoreCollisionWithPlayer(GameObject stone)
    {
        Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] stoneColliders = stone.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D playerCol in playerColliders)
        {
            foreach (Collider2D stoneCol in stoneColliders)
            {
                Physics2D.IgnoreCollision(playerCol, stoneCol);
            }
        }
    }
}