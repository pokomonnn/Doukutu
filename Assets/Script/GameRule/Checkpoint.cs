using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("チェックポイント設定")]
    [SerializeField] private int checkpointNumber = 1;

    [Tooltip("復活させたい位置。未設定なら、このチェックポイント自身の位置を使います。")]
    [SerializeField] private Transform spawnPoint;

    [Header("参照")]
    [SerializeField] private GameManager gameManager;

    [Header("判定設定")]
    [SerializeField] private string playerTag = "Player";

    private bool hasBeenTouched;

    private void Awake()
    {
        // SpawnPointを設定していなければ、チェックポイント自身の位置を使う
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        // GameManagerをInspectorで入れ忘れても、自動で探す
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void Reset()
    {
        // スクリプトを付けた時、ColliderをTriggerにする
        Collider2D checkpointCollider = GetComponent<Collider2D>();

        if (checkpointCollider != null)
        {
            checkpointCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        if (hasBeenTouched)
        {
            return;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("GameManagerが見つかりません。");
            return;
        }

        hasBeenTouched = true;

        gameManager.SetCheckpoint(checkpointNumber, spawnPoint);

        Debug.Log($"チェックポイント {checkpointNumber} に到達しました。");
    }
}