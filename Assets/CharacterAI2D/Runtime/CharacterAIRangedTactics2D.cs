using UnityEngine;

/// <summary>
/// STEP3: 遠距離型EnemyChaser2D専用の間合い調整。
/// 実移動・崖回避はChaserへ任せます。EnemyCombatMovementStyle.KeepDistanceが必要です。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterAIRangedTactics2D : MonoBehaviour
{
    [Header("射撃に適した距離")]
    [SerializeField, Min(.25f)] private float preferredMinimumDistance = 3.5f;
    [SerializeField, Min(.25f)] private float preferredMaximumDistance = 6f;
    [Tooltip("距離境界をまたぐたびの細かい往復移動を抑えます。")]
    [SerializeField, Min(0f)] private float distanceHysteresis = .35f;

    [Header("適正距離内での位置調整")]
    [SerializeField] private bool allowSmallReposition = true;
    [SerializeField, Range(0f, 1f)] private float repositionChance = .35f;
    [SerializeField, Min(.2f)] private float repositionInterval = 2.5f;
    [SerializeField, Min(.05f)] private float repositionDuration = .45f;
    [SerializeField, Range(.1f, 1f)] private float repositionSpeedMultiplier = .6f;

    private int moveMode; // -1:退く, 0:維持, 1:接近
    private float nextRepositionTime;
    private float repositionEndTime;
    private float repositionDirection;

    /// <summary>true: 戦術が有効。direction=0の場合は適正距離で停止。</summary>
    public bool TryGetMovement(float distance, float horizontalDifference,
        out float direction, out bool movingTowardTarget, out float speedMultiplier)
    {
        direction = 0f;
        movingTowardTarget = false;
        speedMultiplier = 1f;
        if (!isActiveAndEnabled) return false;
        float targetDirection = Mathf.Sign(horizontalDifference);
        if (Mathf.Abs(horizontalDifference) < .015f) return true;

        if (moveMode == -1 && distance >= preferredMinimumDistance + distanceHysteresis)
            moveMode = 0;
        else if (moveMode == 1 && distance <= preferredMaximumDistance - distanceHysteresis)
            moveMode = 0;
        else if (moveMode == 0)
        {
            if (distance < preferredMinimumDistance) moveMode = -1;
            else if (distance > preferredMaximumDistance) moveMode = 1;
        }

        if (moveMode != 0)
        {
            repositionEndTime = 0f;
            movingTowardTarget = moveMode == 1;
            direction = targetDirection * moveMode;
            return true;
        }

        if (!allowSmallReposition) return true;
        if (Time.time >= nextRepositionTime)
        {
            nextRepositionTime = Time.time + repositionInterval;
            if (Random.value <= repositionChance)
            {
                // 安全な距離範囲を保ちながら小さく位置を変える。
                bool toward = Random.value >= .5f;
                repositionDirection = targetDirection * (toward ? 1f : -1f);
                repositionEndTime = Time.time + repositionDuration;
            }
        }
        if (Time.time < repositionEndTime)
        {
            bool toward = Mathf.Sign(repositionDirection) == targetDirection;
            if ((toward && distance <= preferredMinimumDistance + .1f) ||
                (!toward && distance >= preferredMaximumDistance - .1f)) return true;
            direction = repositionDirection;
            movingTowardTarget = toward;
            speedMultiplier = repositionSpeedMultiplier;
        }
        return true;
    }

    private void OnDisable() { moveMode = 0; repositionEndTime = 0f; }
    private void OnValidate()
    {
        preferredMaximumDistance = Mathf.Max(preferredMinimumDistance + .2f, preferredMaximumDistance);
        distanceHysteresis = Mathf.Clamp(distanceHysteresis, 0f,
            (preferredMaximumDistance - preferredMinimumDistance) * .45f);
    }
}
