using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 同じシーン内のドアワープ。
/// プレイヤーがTrigger内で指定キーを押すと、
/// 1秒フェードアウト → 瞬間移動 → 1秒フェードインします。
///
/// 重要：移動・射撃等を制御するBehaviourを「ワープ中に停止するスクリプト」に
/// 登録してください（Rigidbody2Dの停止だけではTransform直書きの移動を止められません）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DoorTeleport2D : MonoBehaviour
{
    [Header("ワープ先（同じシーン内）")]
    [Tooltip("ワープ先の位置に置いた空のGameObject（Transform）を指定します。")]
    [SerializeField] private Transform destinationPoint;

    [Header("操作")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("例：『E ドアに入る』のTextMeshProオブジェクト。未指定でも動作します。")]
    [SerializeField] private GameObject interactPrompt;

    [Header("画面フェード")]
    [Tooltip("画面を黒くする時間（秒）。既定：1秒")]
    [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1f;
    [Tooltip("黒画面から戻す時間（秒）。既定：1秒")]
    [SerializeField, Min(0.05f)] private float fadeInSeconds = 1f;

    [Header("ワープ中に停止するスクリプト（重要）")]
    [Tooltip("プレイヤーの移動スクリプト、射撃スクリプト、ジャンプ操作等を登録。\n" +
             "実行前に有効だったものだけ、ワープ終了後に再び有効になります。")]
    [SerializeField] private Behaviour[] disableDuringTeleport;

    [Header("ワープサウンド")]
    [Tooltip("Eキーを押してドアに入るときに鳴る音。未指定なら鳴りません。")]
    [SerializeField] private AudioClip doorEnterSound;

    [Tooltip("画面が完全に黒くなり、ワープする瞬間に鳴る音。未指定なら鳴りません。")]
    [SerializeField] private AudioClip teleportSound;

    [Tooltip("ワープ後、フェードインが始まるときに鳴る音。未指定なら鳴りません。")]
    [SerializeField] private AudioClip arrivalSound;

    [Tooltip("効果音の音量。0 = 無音、1 = 最大音量。")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

    [Tooltip("サウンド再生用のAudioSource。未設定ならこのドアから取得し、なければ自動追加します。2D再生に設定します。")]
    [SerializeField] private AudioSource soundSource;

    [Header("運搬NPC")]
    [Tooltip("CarryableObject2D.IsCarried が true のNPCをプレイヤーと一緒に移動。")]
    [SerializeField] private bool teleportCarriedNpcs = true;

    private static bool s_IsTeleporting;
    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private readonly List<Behaviour> actuallyDisabled = new List<Behaviour>();

    private Transform playerInRange;
    private Rigidbody2D lockedPlayerBody;
    private RigidbodyConstraints2D originalConstraints;
    private GameObject fadeRoot;
    private CanvasGroup fadeGroup;
    private Coroutine teleportRoutine;
    private bool ownsTeleport;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedState()
    {
        s_IsTeleporting = false;
    }

    private void Awake()
    {
        HidePrompt();
        PrepareAudioSource();
        Collider2D trigger = GetComponent<Collider2D>();
        if (!trigger.isTrigger)
        {
            Debug.LogWarning($"[{nameof(DoorTeleport2D)}] {name}: Collider2Dの Is Trigger をONにしてください。", this);
        }
    }

    private void Update()
    {
        bool canUse = !s_IsTeleporting && playerInRange != null &&
                      playerCollidersInRange.Count > 0 && destinationPoint != null;

        if (interactPrompt != null && interactPrompt.activeSelf != canUse)
        {
            interactPrompt.SetActive(canUse);
        }

        if (canUse && IsInteractPressed())
        {
            StartTeleport();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryAddPlayerCollider(other);
    private void OnTriggerStay2D(Collider2D other) => TryAddPlayerCollider(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        if (playerCollidersInRange.Remove(other) && playerCollidersInRange.Count == 0)
        {
            playerInRange = null;
            HidePrompt();
        }
    }

    private void TryAddPlayerCollider(Collider2D other)
    {
        if (other == null) return;

        // 子オブジェクトのCollider2Dにも対応（Playerタグは親に設定可能）。
        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag))
            {
                // 運搬NPCがPlayerの子になっていても、そのNPCのColliderでは
                // ドアを使用できないようにする。
                Rigidbody2D playerBody = t.GetComponent<Rigidbody2D>();
                if (playerBody != null && other.attachedRigidbody != null &&
                    other.attachedRigidbody != playerBody)
                {
                    return;
                }

                playerInRange = t;
                playerCollidersInRange.Add(other);
                return;
            }
            t = t.parent;
        }
    }

    private static bool IsInteractPressedFallback(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        // E/F/T/W 等のキーを想定（InputSystemのKey名と一致するキー）。
        return System.Enum.TryParse(key.ToString(), out Key parsed) &&
               parsed != Key.None && Keyboard.current[parsed].wasPressedThisFrame;
#else
        return Input.GetKeyDown(key);
#endif
    }

    private bool IsInteractPressed() => IsInteractPressedFallback(interactKey);

    private void StartTeleport()
    {
        if (s_IsTeleporting || destinationPoint == null || playerInRange == null) return;

        s_IsTeleporting = true;
        ownsTeleport = true;
        HidePrompt();
        PlayTeleportSound(doorEnterSound);
        teleportRoutine = StartCoroutine(TeleportSequence(playerInRange));
    }

    private IEnumerator TeleportSequence(Transform player)
    {
        LockPlayer(player);
        CreateFadeOverlay();

        // 先に画面を完全な黒にする。
        yield return Fade(0f, 1f, fadeOutSeconds);

        // 真っ暗なときだけ音を鳴らして位置を変える。
        PlayTeleportSound(teleportSound);
        if (player != null && destinationPoint != null)
        {
            MovePlayerAndCarriedNpcs(player, destinationPoint.position);
        }

        // 新しい位置でカメラなどのLateUpdateが1回走るのを待つ。
        yield return null;

        // 到着音を鳴らし、移動後に画面を明るく戻す。
        PlayTeleportSound(arrivalSound);
        yield return Fade(1f, 0f, fadeInSeconds);

        FinishTeleport();
    }

    private void LockPlayer(Transform player)
    {
        actuallyDisabled.Clear();
        if (disableDuringTeleport != null)
        {
            foreach (Behaviour behaviour in disableDuringTeleport)
            {
                if (behaviour == null || behaviour == this || !behaviour.enabled) continue;
                behaviour.enabled = false;
                actuallyDisabled.Add(behaviour);
            }
        }

        lockedPlayerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (lockedPlayerBody != null)
        {
            originalConstraints = lockedPlayerBody.constraints;
            lockedPlayerBody.linearVelocity = Vector2.zero;
            lockedPlayerBody.angularVelocity = 0f;
            lockedPlayerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void UnlockPlayer()
    {
        if (lockedPlayerBody != null)
        {
            lockedPlayerBody.linearVelocity = Vector2.zero;
            lockedPlayerBody.angularVelocity = 0f;
            lockedPlayerBody.constraints = originalConstraints;
        }
        lockedPlayerBody = null;

        foreach (Behaviour behaviour in actuallyDisabled)
        {
            if (behaviour != null) behaviour.enabled = true;
        }
        actuallyDisabled.Clear();
    }

    private void MovePlayerAndCarriedNpcs(Transform player, Vector3 target)
    {
        Vector3 oldPosition = player.position;
        target.z = oldPosition.z; // 2D用：Sorting等で使うZを維持
        Vector3 delta = target - oldPosition;

        // ワープ後に落下判定をリセットするNPCを記録する。
        List<RescuePersonFallDamage2D> fallsToReset = new List<RescuePersonFallDamage2D>();

        // 運搬中NPCのうちプレイヤーの子でないものは、同じ移動量を加算する。
        // 子になっているNPCはプレイヤー移動に自動で追随するので二重移動させない。
        if (teleportCarriedNpcs)
        {
            CarryableObject2D[] allCarriedObjects =
                FindObjectsByType<CarryableObject2D>(FindObjectsSortMode.None);

            foreach (CarryableObject2D carried in allCarriedObjects)
            {
                if (carried == null || !carried.IsCarried) continue;

                if (!carried.transform.IsChildOf(player))
                {
                    Rigidbody2D npcBody = carried.GetComponent<Rigidbody2D>();
                    if (npcBody == null) npcBody = carried.GetComponentInParent<Rigidbody2D>();

                    if (npcBody != null && npcBody.transform != player &&
                        !npcBody.transform.IsChildOf(player))
                    {
                        npcBody.position += (Vector2)delta;
                        npcBody.linearVelocity = Vector2.zero;
                        npcBody.angularVelocity = 0f;
                    }
                    else
                    {
                        carried.transform.position += delta;
                    }
                }

                RescuePersonFallDamage2D fall =
                    carried.GetComponent<RescuePersonFallDamage2D>();
                if (fall == null) fall = carried.GetComponentInChildren<RescuePersonFallDamage2D>(true);
                if (fall != null) fallsToReset.Add(fall);
            }
        }

        player.position = target;
        if (lockedPlayerBody != null && lockedPlayerBody.transform == player)
        {
            lockedPlayerBody.position = new Vector2(target.x, target.y);
            lockedPlayerBody.linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();

        // Cinemachineがプレイヤーの子（CameraTarget等）を追跡するケースにも対応。
        foreach (Transform childTarget in player.GetComponentsInChildren<Transform>(true))
        {
            NotifyCinemachineAboutWarp(childTarget, delta);
        }

        // 移動後の座標で落下履歴をリセット（座標同期前にはリセットしない）。
        foreach (RescuePersonFallDamage2D fall in fallsToReset)
        {
            if (fall != null) fall.ResetFallTracking();
        }
        foreach (RescuePersonFallDamage2D fall in
                 player.GetComponentsInChildren<RescuePersonFallDamage2D>(true))
        {
            if (fall != null && !fallsToReset.Contains(fall)) fall.ResetFallTracking();
        }
    }

    /// <summary>
    /// Cinemachine 2/3 使用時、ワープ後にカメラが旧位置からゆっくり移動するのを抑制。
    /// パッケージ未導入でもコンパイルできるようにReflectionで通知します。
    /// </summary>
    private static void NotifyCinemachineAboutWarp(Transform target, Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0001f) return;

        // Cinemachine 3 の一括通知APIがあれば優先。
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type core = assembly.GetType("Unity.Cinemachine.CinemachineCore");
            if (core == null) continue;

            MethodInfo method = core.GetMethod("OnTargetObjectWarped",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Transform), typeof(Vector3) }, null);
            if (method == null) break;

            try
            {
                method.Invoke(null, new object[] { target, delta });
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Cinemachineへのワープ通知に失敗: " + ex.Message);
                break;
            }
        }

        // Cinemachine 2 等へのフォールバック：仮想カメラに個別通知。
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour == null) continue;
            System.Type type = behaviour.GetType();
            bool isVirtualCamera = false;
            for (System.Type baseType = type; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.Name == "CinemachineVirtualCameraBase")
                {
                    isVirtualCamera = true;
                    break;
                }
            }
            if (!isVirtualCamera) continue;

            MethodInfo method = type.GetMethod("OnTargetObjectWarped",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Transform), typeof(Vector3) }, null);
            if (method == null) continue;
            try
            {
                method.Invoke(behaviour, new object[] { target, delta });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Cinemachineへのワープ通知に失敗: " + ex.Message);
            }
        }
    }

    private void PrepareAudioSource()
    {
        if (soundSource == null)
        {
            soundSource = GetComponent<AudioSource>();
        }
        if (soundSource == null)
        {
            soundSource = gameObject.AddComponent<AudioSource>();
        }

        // 音がワープ後のプレイヤー位置に影響されないように2Dで再生する。
        soundSource.playOnAwake = false;
        soundSource.spatialBlend = 0f;
    }

    private void PlayTeleportSound(AudioClip clip)
    {
        if (clip == null || soundVolume <= 0f) return;
        if (soundSource == null) PrepareAudioSource();
        if (soundSource != null && soundSource.isActiveAndEnabled)
        {
            // 同時再生に対応し、各クリップの長さで途切れないようにする。
            soundSource.PlayOneShot(clip, soundVolume);
        }
    }

    private void CreateFadeOverlay()
    {
        fadeRoot = new GameObject("DoorTeleport_FadeCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        Canvas canvas = fadeRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        fadeGroup = fadeRoot.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = true;
        fadeGroup.interactable = false;

        GameObject blackPanel = new GameObject("BlackPanel",
            typeof(RectTransform), typeof(Image));
        blackPanel.transform.SetParent(fadeRoot.transform, false);

        RectTransform rect = blackPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = blackPanel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        if (fadeGroup == null) yield break;
        float elapsed = 0f;
        fadeGroup.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(startAlpha, endAlpha,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fadeGroup.alpha = endAlpha;
    }

    private void FinishTeleport()
    {
        UnlockPlayer();
        DestroyFadeOverlay();
        teleportRoutine = null;
        ownsTeleport = false;
        s_IsTeleporting = false;
    }

    private void DestroyFadeOverlay()
    {
        if (fadeRoot != null) Destroy(fadeRoot);
        fadeRoot = null;
        fadeGroup = null;
    }

    private void HidePrompt()
    {
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnDisable()
    {
        HidePrompt();
        playerCollidersInRange.Clear();
        playerInRange = null;

        // シーンの再読み込み／ドア削除中に演出が中断した場合も、入力停止を残さない。
        if (ownsTeleport)
        {
            if (teleportRoutine != null) StopCoroutine(teleportRoutine);
            teleportRoutine = null;
            UnlockPlayer();
            DestroyFadeOverlay();
            ownsTeleport = false;
            s_IsTeleporting = false;
        }
    }

    private void OnValidate()
    {
        fadeOutSeconds = Mathf.Max(0.05f, fadeOutSeconds);
        fadeInSeconds = Mathf.Max(0.05f, fadeInSeconds);
    }
}
