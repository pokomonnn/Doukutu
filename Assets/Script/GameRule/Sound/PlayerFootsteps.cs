using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Rigidbody2D rb;

    [Tooltip("足音専用のAudioSourceを設定")]
    [SerializeField] private AudioSource footstepAudioSource;

    [Header("歩行判定")]
    [Tooltip("通常速度時の足音間隔")]
    [SerializeField, Min(0.05f)]
    private float stepInterval = 0.35f;

    [Tooltip(
        "重量なし時のPlayerMoveのMove Speed。\n" +
        "PlayerWeightControllerで速度が変わっても、この値は通常速度のまま設定します。"
    )]
    [SerializeField, Min(0.01f)]
    private float normalMoveSpeed = 5f;

    [Tooltip("この速度未満なら足音を鳴らさない")]
    [SerializeField, Min(0f)]
    private float minimumMoveSpeed = 0.1f;

    [Header("地面にFootstepSurfaceがない場合の足音")]
    [SerializeField] private AudioClip[] defaultFirstStepClips;
    [SerializeField] private AudioClip[] defaultSecondStepClips;

    [SerializeField, Range(0f, 1f)]
    private float defaultVolume = 0.8f;

    [SerializeField, Range(0.5f, 1.5f)]
    private float defaultPitchMin = 0.95f;

    [SerializeField, Range(0.5f, 1.5f)]
    private float defaultPitchMax = 1.05f;

    // 前回の足音から歩いた距離
    private float movedDistanceSinceLastStep;

    private bool wasWalking;

    // true = 1歩目、false = 2歩目
    private bool isFirstStep = true;

    private void Awake()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource != null)
        {
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.spatialBlend = 0f;
        }
    }

    private void Update()
    {
        if (playerMove == null || rb == null)
        {
            return;
        }

        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);

        bool isWalking =
            playerMove.IsGrounded &&
            horizontalSpeed >= minimumMoveSpeed;

        if (!isWalking)
        {
            wasWalking = false;

            // 次に歩き始めた時は、すぐ1歩目を鳴らす
            movedDistanceSinceLastStep = GetStepDistance();

            return;
        }

        float stepDistance = GetStepDistance();

        if (!wasWalking)
        {
            movedDistanceSinceLastStep = stepDistance;
        }

        // 実際に進んだ距離を加算する。
        // 重量で移動速度が下がるほど、
        // 次の足音までに必要な時間も長くなる。
        movedDistanceSinceLastStep +=
            horizontalSpeed * Time.deltaTime;

        while (movedDistanceSinceLastStep >= stepDistance)
        {
            PlayFootstep();

            movedDistanceSinceLastStep -= stepDistance;

            // 次の足音は反対の足にする
            isFirstStep = !isFirstStep;
        }

        wasWalking = true;
    }

    private float GetStepDistance()
    {
        // 通常時：
        // normalMoveSpeed 5 × stepInterval 0.35
        // = 1.75ユニットごとに足音を鳴らす
        return Mathf.Max(
            0.01f,
            normalMoveSpeed * stepInterval
        );
    }

    private void PlayFootstep()
    {
        if (footstepAudioSource == null)
        {
            return;
        }

        AudioClip clip = null;
        float volume = defaultVolume;

        float pitch = Random.Range(
            defaultPitchMin,
            defaultPitchMax
        );

        Collider2D groundCollider =
            playerMove.CurrentGroundCollider;

        if (groundCollider != null)
        {
            FootstepSurface surface =
                groundCollider.GetComponentInParent<
                    FootstepSurface
                >();

            if (surface != null &&
                surface.TryGetFootstep(
                    isFirstStep,
                    out AudioClip surfaceClip,
                    out float surfaceVolume,
                    out float surfacePitch))
            {
                clip = surfaceClip;
                volume = surfaceVolume;
                pitch = surfacePitch;
            }
        }

        if (clip == null)
        {
            clip = GetRandomDefaultClip(isFirstStep);
        }

        if (clip == null)
        {
            return;
        }

        footstepAudioSource.pitch = pitch;
        footstepAudioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GetRandomDefaultClip(bool useFirstStep)
    {
        AudioClip[] clips = useFirstStep
            ? defaultFirstStepClips
            : defaultSecondStepClips;

        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validClipCount = 0;

        foreach (AudioClip clip in clips)
        {
            if (clip != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(
            0,
            validClipCount
        );

        foreach (AudioClip clip in clips)
        {
            if (clip == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return clip;
            }

            selectedIndex--;
        }

        return null;
    }

    private void OnValidate()
    {
        stepInterval = Mathf.Max(0.05f, stepInterval);
        normalMoveSpeed = Mathf.Max(0.01f, normalMoveSpeed);
        minimumMoveSpeed = Mathf.Max(0f, minimumMoveSpeed);

        defaultVolume = Mathf.Clamp01(defaultVolume);

        defaultPitchMin = Mathf.Clamp(
            defaultPitchMin,
            0.5f,
            1.5f
        );

        defaultPitchMax = Mathf.Clamp(
            defaultPitchMax,
            0.5f,
            1.5f
        );

        if (defaultPitchMax < defaultPitchMin)
        {
            defaultPitchMax = defaultPitchMin;
        }
    }
}