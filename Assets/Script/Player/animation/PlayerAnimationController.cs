using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerMove playerMove;

    [Header("Animator Parameter")]
    [Tooltip("Animatorで作成したBool Parameter名")]
    [SerializeField] private string runBoolName = "Run";

    [Header("走行判定")]
    [Tooltip("横速度がこの値を超えたらRunに切り替えます")]
    [SerializeField, Min(0f)]
    private float runSpeedThreshold = 0.05f;

    [Tooltip("オンなら地面にいる時だけRunを再生します")]
    [SerializeField]
    private bool requireGrounded = true;

    private int runBoolHash;

    private void Awake()
    {
        FindReferences();

        runBoolHash = Animator.StringToHash(runBoolName);
    }

    private void Update()
    {
        if (animator == null || playerRigidbody == null)
        {
            return;
        }

        float horizontalSpeed =
            Mathf.Abs(playerRigidbody.linearVelocity.x);

        bool isMoving =
            horizontalSpeed > runSpeedThreshold;

        bool canRun =
            !requireGrounded ||
            playerMove == null ||
            playerMove.IsGrounded;

        animator.SetBool(
            runBoolHash,
            isMoving && canRun
        );
    }

    private void OnDisable()
    {
        if (animator != null &&
            !string.IsNullOrWhiteSpace(runBoolName))
        {
            animator.SetBool(runBoolHash, false);
        }
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(true);
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }
    }

    private void OnValidate()
    {
        runSpeedThreshold =
            Mathf.Max(0f, runSpeedThreshold);
    }
}