using UnityEngine;

/// <summary>
/// 飲み過ぎ時だけ使う、ゆっくりした大きめの画面の揺らぎ。
/// Cinemachine等がカメラを更新した後で小さな位置／Z回転を加算し、
/// 翌フレームのUpdateで前回の追加分を元へ戻してCinemachineとの干渉を軽減。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)]
public class AlcoholCameraShake2D : MonoBehaviour
{
    private float remaining;
    private float elapsed;
    private float horizontalAmplitude;
    private float verticalAmplitude;
    private float swayCyclesPerSecond;
    private float rollDegrees;
    private float phase;
    private Vector3 appliedOffset;
    private Quaternion appliedRotation = Quaternion.identity;

    public float RemainingSeconds => remaining;
    public bool IsSwaying => remaining > 0f;

    /// <summary>振動ではなく、波のようなゆっくりした横揺れにする。</summary>
    public void Sway(float seconds, float horizontal, float cyclesPerSecond, float maxRollDegrees)
    {
        remaining = Mathf.Max(0f, seconds);
        elapsed = 0f;
        horizontalAmplitude = Mathf.Max(0f, horizontal);
        verticalAmplitude = horizontalAmplitude * 0.28f;
        swayCyclesPerSecond = Mathf.Clamp(cyclesPerSecond, 0.05f, 0.8f);
        rollDegrees = Mathf.Clamp(maxRollDegrees, 0f, 8f);
    }

    // v2の呼び出しが残っていてもコンパイルエラーにならない互換API。
    public void Shake(float seconds, float strength, float speed)
    {
        Sway(seconds, Mathf.Min(strength, 0.08f), 0.23f, 0.8f);
    }

    // CinemachineBrainのLateUpdateより前のUpdateで、前回フレームのカメラ追加分を除去する。
    // LateUpdateで戻すと、Brainが設定した追従位置までずらしてしまう場合がある。
    private void Update()
    {
        UndoLastOffset();
    }

    private void LateUpdate()
    {
        if (remaining <= 0f) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        elapsed += dt;
        remaining = Mathf.Max(0f, remaining - dt);
        phase += dt * swayCyclesPerSecond * Mathf.PI * 2f;

        // 最初の1.2秒と最後の3秒はゆっくり強度を変える。
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.2f));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / 3f));
        float fade = fadeIn * fadeOut;

        // 左右約5秒で1往復。縦方向と傾きは半周期ずらして自然に。
        appliedOffset = new Vector3(
            Mathf.Sin(phase) * horizontalAmplitude * fade,
            Mathf.Sin(phase * 0.63f + 1.1f) * verticalAmplitude * fade,
            0f);
        float roll = Mathf.Sin(phase + 0.45f) * rollDegrees * fade;
        appliedRotation = Quaternion.Euler(0f, 0f, roll);
        transform.position += appliedOffset;
        transform.rotation *= appliedRotation;
    }

    private void UndoLastOffset()
    {
        // Transformの更新は逆順。Cinemachine追従後でも追加分を蓄積しない。
        transform.rotation *= Quaternion.Inverse(appliedRotation);
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
        appliedRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        UndoLastOffset();
        remaining = 0f;
    }

    private void OnDestroy()
    {
        UndoLastOffset();
    }
}
