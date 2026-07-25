using System.Text;
using UnityEngine;

/// <summary>
/// Playerの通常移動・ほふくアニメーションを制御します。
/// 実際にAnimator Controllerが設定されているAnimatorを優先して探し、
/// ほふく状態、移動判定、Animator Parameter、現在Stateを診断ログへ出します。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerProneController proneController;

    [Header("Animator Parameter")]
    [Tooltip("通常移動用のBool Parameter名")]
    [SerializeField] private string runBoolName = "Run";

    [Tooltip("ほふく状態用のBool Parameter名")]
    [SerializeField] private string proneBoolName = "Prone";

    [Tooltip("ほふく移動用のBool Parameter名")]
    [SerializeField] private string proneMoveBoolName = "ProneMove";

    [Header("移動判定")]
    [Tooltip("横速度がこの値を超えたら移動中として扱います")]
    [SerializeField, Min(0f)]
    private float runSpeedThreshold = 0.05f;

    [Tooltip("オンなら地面にいる時だけRun／ProneMoveを再生します")]
    [SerializeField] private bool requireGrounded = true;

    [Header("Animator探索")]
    [Tooltip("現在参照しているAnimatorにControllerが無い場合、Player階層内からController付きAnimatorを探します")]
    [SerializeField] private bool preferAnimatorWithController = true;

    [Header("診断ログ")]
    [Tooltip("ほふくAnimationの診断ログをConsoleへ表示します")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("Prone、ProneMove、Runなどの値が変化した時にログを表示します")]
    [SerializeField] private bool logStateChanges = true;

    [Tooltip("一定間隔で現在のAnimator状態を表示します。原因調査中だけオンがおすすめです")]
    [SerializeField] private bool logPeriodicAnimatorState = true;

    [SerializeField, Min(0.1f)]
    private float periodicLogInterval = 1f;

    private int runBoolHash;
    private int proneBoolHash;
    private int proneMoveBoolHash;

    private bool hasRunParameter;
    private bool hasProneParameter;
    private bool hasProneMoveParameter;

    private bool isSubscribedToProne;
    private bool hasLoggedMissingReferences;

    private bool hasPreviousRuntimeState;
    private bool previousIsProne;
    private bool previousIsMoving;
    private bool previousCanPlayMovement;
    private bool previousRunValue;
    private bool previousProneValue;
    private bool previousProneMoveValue;

    private float nextPeriodicLogTime;

    private void Awake()
    {
        FindReferences();
        CacheAnimatorParameters();
        SubscribeToProneController();
        LogFullDiagnostics("Awake");
    }

    private void OnEnable()
    {
        FindReferences();
        CacheAnimatorParameters();
        SubscribeToProneController();
        LogFullDiagnostics("OnEnable");
    }

    private void Start()
    {
        FindReferences();
        CacheAnimatorParameters();
        LogFullDiagnostics("Start");
    }

    private void Update()
    {
        FindReferences();

        if (animator == null || playerRigidbody == null)
        {
            if (!hasLoggedMissingReferences)
            {
                hasLoggedMissingReferences = true;
                LogWarning(
                    "Animationを更新できません。" +
                    $" Animator={(animator != null ? "OK" : "未取得")}," +
                    $" Rigidbody2D={(playerRigidbody != null ? "OK" : "未取得")}"
                );
            }

            return;
        }

        hasLoggedMissingReferences = false;

        float horizontalSpeed =
            Mathf.Abs(playerRigidbody.linearVelocity.x);

        bool isMoving =
            horizontalSpeed > runSpeedThreshold;

        bool grounded =
            playerMove == null || playerMove.IsGrounded;

        bool canPlayMovement =
            !requireGrounded || grounded;

        bool isProne =
            proneController != null &&
            proneController.IsProne;

        bool runValue =
            !isProne && isMoving && canPlayMovement;

        bool proneValue = isProne;

        bool proneMoveValue =
            isProne && isMoving && canPlayMovement;

        if (hasRunParameter)
        {
            animator.SetBool(runBoolHash, runValue);
        }

        if (hasProneParameter)
        {
            animator.SetBool(proneBoolHash, proneValue);
        }

        if (hasProneMoveParameter)
        {
            animator.SetBool(proneMoveBoolHash, proneMoveValue);
        }

        if (logStateChanges &&
            (!hasPreviousRuntimeState ||
             previousIsProne != isProne ||
             previousIsMoving != isMoving ||
             previousCanPlayMovement != canPlayMovement ||
             previousRunValue != runValue ||
             previousProneValue != proneValue ||
             previousProneMoveValue != proneMoveValue))
        {
            Log(
                "Animation入力更新: " +
                $"IsProne={isProne}, " +
                $"IsMoving={isMoving}, " +
                $"HorizontalSpeed={horizontalSpeed:0.###}, " +
                $"Grounded={grounded}, " +
                $"CanPlayMovement={canPlayMovement}, " +
                $"Run={FormatParameterValue(hasRunParameter, runValue)}, " +
                $"Prone={FormatParameterValue(hasProneParameter, proneValue)}, " +
                $"ProneMove={FormatParameterValue(hasProneMoveParameter, proneMoveValue)}"
            );
        }

        previousIsProne = isProne;
        previousIsMoving = isMoving;
        previousCanPlayMovement = canPlayMovement;
        previousRunValue = runValue;
        previousProneValue = proneValue;
        previousProneMoveValue = proneMoveValue;
        hasPreviousRuntimeState = true;

        if (showDebugLogs &&
            logPeriodicAnimatorState &&
            Time.unscaledTime >= nextPeriodicLogTime)
        {
            nextPeriodicLogTime =
                Time.unscaledTime + Mathf.Max(0.1f, periodicLogInterval);

            LogCurrentAnimatorState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromProneController();
        ResetAnimatorParameters();
    }

    private void OnDestroy()
    {
        UnsubscribeFromProneController();
    }

    private void ResetAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        if (hasRunParameter)
        {
            animator.SetBool(runBoolHash, false);
        }

        if (hasProneParameter)
        {
            animator.SetBool(proneBoolHash, false);
        }

        if (hasProneMoveParameter)
        {
            animator.SetBool(proneMoveBoolHash, false);
        }
    }

    private void CacheAnimatorParameters()
    {
        runBoolHash = Animator.StringToHash(
            runBoolName ?? string.Empty
        );

        proneBoolHash = Animator.StringToHash(
            proneBoolName ?? string.Empty
        );

        proneMoveBoolHash = Animator.StringToHash(
            proneMoveBoolName ?? string.Empty
        );

        hasRunParameter = HasBoolParameter(
            runBoolName,
            runBoolHash,
            out string runReason
        );

        hasProneParameter = HasBoolParameter(
            proneBoolName,
            proneBoolHash,
            out string proneReason
        );

        hasProneMoveParameter = HasBoolParameter(
            proneMoveBoolName,
            proneMoveBoolHash,
            out string proneMoveReason
        );

        if (!showDebugLogs)
        {
            return;
        }

        if (!hasRunParameter)
        {
            LogWarning($"Animator Parameter『{runBoolName}』を使用できません。{runReason}");
        }

        if (!hasProneParameter)
        {
            LogWarning($"Animator Parameter『{proneBoolName}』を使用できません。{proneReason}");
        }

        if (!hasProneMoveParameter)
        {
            LogWarning($"Animator Parameter『{proneMoveBoolName}』を使用できません。{proneMoveReason}");
        }
    }

    private bool HasBoolParameter(
        string parameterName,
        int parameterHash,
        out string reason)
    {
        reason = string.Empty;

        if (animator == null)
        {
            reason = "Animator参照がありません。";
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            reason =
                $"参照中のAnimator『{GetTransformPath(animator.transform)}』に " +
                "Runtime Animator Controllerが設定されていません。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            reason = "Parameter名が空です。";
            return false;
        }

        foreach (AnimatorControllerParameter parameter
                 in animator.parameters)
        {
            if (parameter.nameHash != parameterHash)
            {
                continue;
            }

            if (parameter.type !=
                AnimatorControllerParameterType.Bool)
            {
                reason =
                    $"同名Parameterはありますが型が{parameter.type}です。Boolにしてください。";
                return false;
            }

            reason = "OK";
            return true;
        }

        reason = "Animator Controller内に同名Parameterがありません。";
        return false;
    }

    private void HandleProneStateChanged(bool isProne)
    {
        Log(
            $"PlayerProneControllerから状態変更通知を受信しました: IsProne={isProne}"
        );

        if (animator == null)
        {
            LogWarning("状態変更通知を受けましたがAnimator参照がありません。");
            return;
        }

        if (!hasProneParameter || !hasProneMoveParameter)
        {
            CacheAnimatorParameters();
        }
    }

    private void SubscribeToProneController()
    {
        if (isSubscribedToProne || proneController == null)
        {
            return;
        }

        proneController.ProneStateChanged +=
            HandleProneStateChanged;

        isSubscribedToProne = true;
    }

    private void UnsubscribeFromProneController()
    {
        if (!isSubscribedToProne || proneController == null)
        {
            return;
        }

        proneController.ProneStateChanged -=
            HandleProneStateChanged;

        isSubscribedToProne = false;
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponentInParent<PlayerMove>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponentInChildren<PlayerMove>(true);
        }

        if (proneController == null)
        {
            proneController = GetComponent<PlayerProneController>();
        }

        if (proneController == null)
        {
            proneController =
                GetComponentInParent<PlayerProneController>();
        }

        if (proneController == null)
        {
            proneController =
                GetComponentInChildren<PlayerProneController>(true);
        }

        if (playerRigidbody == null && playerMove != null)
        {
            playerRigidbody =
                playerMove.GetComponent<Rigidbody2D>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponentInParent<Rigidbody2D>();
        }

        FindBestAnimator();
        SubscribeToProneController();
    }

    private void FindBestAnimator()
    {
        if (animator != null &&
            (!preferAnimatorWithController ||
             animator.runtimeAnimatorController != null))
        {
            return;
        }

        Animator previousAnimator = animator;
        Transform searchRoot = playerMove != null
            ? playerMove.transform
            : transform.root;

        Animator[] candidates =
            searchRoot.GetComponentsInChildren<Animator>(true);

        Animator firstCandidate = null;
        Animator controllerCandidate = null;

        foreach (Animator candidate in candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            if (firstCandidate == null)
            {
                firstCandidate = candidate;
            }

            if (candidate.runtimeAnimatorController != null)
            {
                controllerCandidate = candidate;
                break;
            }
        }

        animator = controllerCandidate != null
            ? controllerCandidate
            : firstCandidate;

        if (showDebugLogs &&
            previousAnimator != animator)
        {
            string previousPath = previousAnimator != null
                ? GetTransformPath(previousAnimator.transform)
                : "未設定";

            string currentPath = animator != null
                ? GetTransformPath(animator.transform)
                : "未取得";

            Log(
                $"Animator参照を選択しました: {previousPath} → {currentPath}"
            );
        }
    }

    [ContextMenu("Log Prone Animation Diagnostics")]
    public void LogProneAnimationDiagnostics()
    {
        FindReferences();
        CacheAnimatorParameters();
        LogFullDiagnostics("手動診断");
        LogCurrentAnimatorState();
    }

    private void LogFullDiagnostics(string phase)
    {
        if (!showDebugLogs)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"========== ほふくAnimation診断: {phase} ==========");
        builder.AppendLine(
            $"Controller Object={GetTransformPath(transform)} / " +
            $"enabled={enabled} / active={gameObject.activeInHierarchy}"
        );

        if (animator == null)
        {
            builder.AppendLine("Animator=未取得");
        }
        else
        {
            string controllerName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "未設定";

            builder.AppendLine(
                $"Animator={GetTransformPath(animator.transform)} / " +
                $"enabled={animator.enabled} / active={animator.gameObject.activeInHierarchy} / " +
                $"Controller={controllerName} / speed={animator.speed:0.###} / " +
                $"layerCount={animator.layerCount} / cullingMode={animator.cullingMode}"
            );
        }

        builder.AppendLine(
            $"Rigidbody2D={(playerRigidbody != null ? GetTransformPath(playerRigidbody.transform) : "未取得")}"
        );

        builder.AppendLine(
            $"PlayerMove={(playerMove != null ? GetTransformPath(playerMove.transform) : "未取得")} / " +
            $"Grounded={(playerMove != null ? playerMove.IsGrounded.ToString() : "不明")}"
        );

        builder.AppendLine(
            $"PlayerProneController={(proneController != null ? GetTransformPath(proneController.transform) : "未取得")} / " +
            $"IsProne={(proneController != null ? proneController.IsProne.ToString() : "不明")}"
        );

        builder.AppendLine(
            $"Parameters: {runBoolName}={hasRunParameter}, " +
            $"{proneBoolName}={hasProneParameter}, " +
            $"{proneMoveBoolName}={hasProneMoveParameter}"
        );

        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            builder.Append("Animator内の全Parameter: ");

            if (animator.parameters.Length == 0)
            {
                builder.Append("なし");
            }
            else
            {
                for (int i = 0; i < animator.parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter =
                        animator.parameters[i];

                    if (i > 0)
                    {
                        builder.Append(" / ");
                    }

                    builder.Append(
                        $"{parameter.name}({parameter.type})"
                    );
                }
            }

            builder.AppendLine();
        }

        Debug.Log(builder.ToString(), this);
    }

    private void LogCurrentAnimatorState()
    {
        if (!showDebugLogs || animator == null)
        {
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            LogWarning(
                "現在参照しているAnimatorにはRuntime Animator Controllerがありません。"
            );
            return;
        }

        if (animator.layerCount <= 0)
        {
            LogWarning("AnimatorのLayerがありません。");
            return;
        }

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(0);

        bool inTransition = animator.IsInTransition(0);

        string parameterValues =
            $"Run={GetAnimatorBoolText(hasRunParameter, runBoolHash)}, " +
            $"Prone={GetAnimatorBoolText(hasProneParameter, proneBoolHash)}, " +
            $"ProneMove={GetAnimatorBoolText(hasProneMoveParameter, proneMoveBoolHash)}";

        Log(
            "Animator現在状態: " +
            $"fullPathHash={state.fullPathHash}, " +
            $"shortNameHash={state.shortNameHash}, " +
            $"normalizedTime={state.normalizedTime:0.###}, " +
            $"inTransition={inTransition}, " +
            parameterValues
        );
    }

    private string GetAnimatorBoolText(
        bool hasParameter,
        int parameterHash)
    {
        if (!hasParameter || animator == null)
        {
            return "Parameterなし";
        }

        return animator.GetBool(parameterHash).ToString();
    }

    private static string FormatParameterValue(
        bool hasParameter,
        bool value)
    {
        return hasParameter
            ? value.ToString()
            : "Parameterなし";
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void Log(string message)
    {
        if (!showDebugLogs || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log(
            $"[PlayerAnimationController] {message}",
            this
        );
    }

    private void LogWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning(
            $"[PlayerAnimationController] {message}",
            this
        );
    }

    private void OnValidate()
    {
        runSpeedThreshold =
            Mathf.Max(0f, runSpeedThreshold);

        periodicLogInterval =
            Mathf.Max(0.1f, periodicLogInterval);
    }
}
