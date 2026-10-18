using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー／銃の音通知。Player本体に付けて移動による疑似足音を自動配信可能。
/// 銃声は専用AudioSourceの再生開始を監視するか、発射コードからEmitGunshotを呼ぶ。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterAINoiseEmitter2D : MonoBehaviour
{
    [Header("足音（横移動から簡易生成）")]
    [SerializeField] private bool autoFootsteps = true;
    [SerializeField, Min(0f)] private float minimumWalkingSpeed = .3f;
    [SerializeField, Min(.1f)] private float footstepInterval = .55f;
    [SerializeField, Min(0f)] private float footstepHearingRadius = 4f;
    [Header("銃声")]
    [Tooltip("専用の銃声AudioSourceを登録。再生開始時に一度通知。短い間隔の連射は取りこぼすため、確実にするなら発射コードからEmitGunshot()を呼ぶ。")]
    [SerializeField] private List<AudioSource> gunshotAudioSources = new List<AudioSource>();
    [SerializeField, Min(0f)] private float gunshotHearingRadius = 13f;
    [Header("デバッグ")]
    [SerializeField] private bool debugLogs;
    private Rigidbody2D body;
    private CharacterHealth health;
    private bool[] previouslyPlaying;
    private float nextFootstepTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = GetComponentInParent<Rigidbody2D>();
        health = GetComponent<CharacterHealth>();
        previouslyPlaying = new bool[gunshotAudioSources.Count];
    }
    private void OnEnable()
    {
        nextFootstepTime = Time.time + footstepInterval;
        previouslyPlaying = new bool[gunshotAudioSources.Count];
        for (int i = 0; i < gunshotAudioSources.Count; i++)
            previouslyPlaying[i] = gunshotAudioSources[i] != null && gunshotAudioSources[i].isPlaying;
    }
    private void Update()
    {
        if (health != null && health.IsDead) return;
        if (autoFootsteps && body != null && Mathf.Abs(body.linearVelocity.x) >= minimumWalkingSpeed &&
            Time.time >= nextFootstepTime)
        {
            EmitFootstep();
            nextFootstepTime = Time.time + footstepInterval;
        }
        if (gunshotAudioSources.Count != previouslyPlaying.Length)
            previouslyPlaying = new bool[gunshotAudioSources.Count];
        for (int i = 0; i < gunshotAudioSources.Count; i++)
        {
            bool playing = gunshotAudioSources[i] != null && gunshotAudioSources[i].isPlaying;
            if (playing && !previouslyPlaying[i]) EmitGunshot();
            previouslyPlaying[i] = playing;
        }
    }
    [ContextMenu("AI STEP2: 足音を通知（Play中）")]
    public void EmitFootstep()
    {
        CharacterAINoise2D.Emit(transform.position, footstepHearingRadius, transform, CharacterAINoiseKind2D.Footstep);
        if (debugLogs) Debug.Log("[AI Noise] Footstep " + name, this);
    }
    [ContextMenu("AI STEP2: 銃声を通知（Play中）")]
    public void EmitGunshot()
    {
        CharacterAINoise2D.Emit(transform.position, gunshotHearingRadius, transform, CharacterAINoiseKind2D.Gunshot);
        if (debugLogs) Debug.Log("[AI Noise] Gunshot " + name, this);
    }
    private void OnValidate()
    {
        minimumWalkingSpeed = Mathf.Max(0f, minimumWalkingSpeed);
        footstepInterval = Mathf.Max(.1f, footstepInterval);
        footstepHearingRadius = Mathf.Max(0f, footstepHearingRadius);
        gunshotHearingRadius = Mathf.Max(0f, gunshotHearingRadius);
    }
}
