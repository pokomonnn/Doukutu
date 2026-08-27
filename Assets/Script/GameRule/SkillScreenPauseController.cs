using UnityEngine;

/// <summary>
/// スキル画面が表示されている間だけ、ゲーム進行とプレイヤー操作を停止します。
///
/// 使い方：
/// このコンポーネントを、表示・非表示される SkillPanel 本体へ追加してください。
/// SkillPanel.SetActive(true) で自動的に停止し、
/// SkillPanel.SetActive(false) で自動的に元の状態へ戻します。
/// </summary>
[DisallowMultipleComponent]
public class SkillScreenPauseController : MonoBehaviour
{
    [Header("停止する内容")]
    [Tooltip("スキル画面を開いている間、Time.timeScaleを0にします。")]
    [SerializeField] private bool pauseGameTime = true;

    [Tooltip("スキル画面を開いている間、PlayerMoveを無効にします。")]
    [SerializeField] private bool lockPlayerMovement = true;

    [Tooltip("スキル画面を開いている間、銃の射撃・照準・リロードを禁止します。")]
    [SerializeField] private bool lockWeaponControls = true;

    [Header("参照")]
    [Tooltip("未設定ならシーン内から自動取得します。")]
    [SerializeField] private PlayerMove playerMove;

    [Tooltip("未設定ならシーン内から自動取得します。")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [Header("追加で停止したいスクリプト（任意）")]
    [Tooltip("必要なら、StoneThrowerやロープ操作など、スキル画面中に止めたいBehaviourを設定できます。")]
    [SerializeField] private Behaviour[] behavioursToDisableWhileOpen;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private float timeScaleBeforeOpen = 1f;

    private bool playerMoveWasEnabledBeforeOpen;
    private bool hasDisabledPlayerMove;

    private bool hasLockedWeaponControls;

    private Behaviour[] disabledBehaviours;
    private bool hasAppliedPause;

    public bool IsPausingGame => hasAppliedPause;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyPause();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RestorePause();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RestorePause();
    }

    private void ApplyPause()
    {
        if (hasAppliedPause)
        {
            return;
        }

        FindReferences();

        hasAppliedPause = true;

        LockPlayerMovement();
        LockWeapon();
        DisableAdditionalBehaviours();

        if (pauseGameTime)
        {
            // すでに別システムで停止中なら0もそのまま保存します。
            // これによりスキル画面を閉じても、他のポーズを勝手に解除しません。
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
        }

        Log("スキル画面を開いたため、ゲームとプレイヤー操作を停止しました。");
    }

    private void RestorePause()
    {
        if (!hasAppliedPause)
        {
            return;
        }

        // 先にロックを解除してからTimeScaleを元へ戻します。
        RestoreAdditionalBehaviours();
        UnlockWeapon();
        UnlockPlayerMovement();

        if (pauseGameTime)
        {
            Time.timeScale = timeScaleBeforeOpen;
        }

        hasAppliedPause = false;

        Log(
            $"スキル画面を閉じたため、ゲーム状態を復元しました。" +
            $" TimeScale={Time.timeScale:0.###}"
        );
    }

    private void LockPlayerMovement()
    {
        if (!lockPlayerMovement ||
            hasDisabledPlayerMove ||
            playerMove == null)
        {
            return;
        }

        // 死亡・会話などで元から無効だった場合は、
        // スキル画面を閉じても勝手に有効化しません。
        playerMoveWasEnabledBeforeOpen = playerMove.enabled;
        hasDisabledPlayerMove = true;

        if (playerMove.enabled)
        {
            playerMove.enabled = false;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (!hasDisabledPlayerMove)
        {
            return;
        }

        if (playerMove != null &&
            playerMoveWasEnabledBeforeOpen)
        {
            playerMove.enabled = true;
        }

        playerMoveWasEnabledBeforeOpen = false;
        hasDisabledPlayerMove = false;
    }

    private void LockWeapon()
    {
        if (!lockWeaponControls ||
            hasLockedWeaponControls ||
            equipmentVisualController == null)
        {
            return;
        }

        // PlayerEquipmentVisualControllerはowner単位のロックを持っているので、
        // インベントリ・死亡・キャンプ等のロックと競合しません。
        equipmentVisualController.SetWeaponControlLock(
            this,
            true
        );

        hasLockedWeaponControls = true;
    }

    private void UnlockWeapon()
    {
        if (!hasLockedWeaponControls)
        {
            return;
        }

        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(
                this,
                false
            );
        }

        hasLockedWeaponControls = false;
    }

    private void DisableAdditionalBehaviours()
    {
        if (behavioursToDisableWhileOpen == null ||
            behavioursToDisableWhileOpen.Length == 0)
        {
            disabledBehaviours = null;
            return;
        }

        disabledBehaviours =
            new Behaviour[behavioursToDisableWhileOpen.Length];

        for (int i = 0;
             i < behavioursToDisableWhileOpen.Length;
             i++)
        {
            Behaviour behaviour =
                behavioursToDisableWhileOpen[i];

            if (behaviour == null ||
                behaviour == this ||
                behaviour == playerMove ||
                !behaviour.enabled)
            {
                continue;
            }

            behaviour.enabled = false;
            disabledBehaviours[i] = behaviour;
        }
    }

    private void RestoreAdditionalBehaviours()
    {
        if (disabledBehaviours == null)
        {
            return;
        }

        foreach (Behaviour behaviour in disabledBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledBehaviours = null;
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove =
                FindAnyObjectByType<PlayerMove>(
                    FindObjectsInactive.Include
                );
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                FindAnyObjectByType<
                    PlayerEquipmentVisualController
                >(FindObjectsInactive.Include);
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[SkillScreenPauseController] {message}",
            this
        );
    }
}
