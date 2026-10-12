using UnityEngine;

/// <summary>
/// チュートリアル用エレベーター入室判定。
/// BoxCollider2Dを自動で要求し、Trigger内へPlayerが入るとManagerへ通知します。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CaveTutorialElevatorZone2D : MonoBehaviour
{
    [SerializeField] private CaveTutorialManager2D manager;
    [SerializeField] private PlayerMove player;

    private void Reset()
    {
        BoxCollider2D zone = GetComponent<BoxCollider2D>();

        if (zone != null)
        {
            zone.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<CaveTutorialManager2D>();
        }

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerMove>();
        }

        BoxCollider2D zone = GetComponent<BoxCollider2D>();

        if (zone != null && !zone.isTrigger)
        {
            Debug.LogWarning(
                "Tutorial Elevator Zone の BoxCollider2D は Is Trigger をONにしてください。",
                this
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CheckPlayer(other);
    }

    private void CheckPlayer(Collider2D other)
    {
        if (manager == null ||
            player == null ||
            other == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerMove>() != player)
        {
            return;
        }

        manager.NotifyElevatorEntered();
    }
}
