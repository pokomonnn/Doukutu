using UnityEngine;

/// <summary>
/// プレイヤーがこのオブジェクトの2D Trigger Collider内にいる間だけ、
/// 指定したUI Buttonを表示します。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShowButtonInTrigger2D : MonoBehaviour
{
    [Header("表示するUI")]
    [Tooltip("プレイヤーが範囲内にいる間だけ表示するButtonのGameObject")]
    [SerializeField] private GameObject targetButton;

    [Header("プレイヤー判定")]
    [Tooltip("プレイヤーに設定しているTag")]
    [SerializeField] private string playerTag = "Player";

    [Header("初期設定")]
    [Tooltip("ゲーム開始時にButtonを非表示にする")]
    [SerializeField] private bool hideOnStart = true;

    private int playerColliderCount;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                $"[{nameof(ShowButtonInTrigger2D)}] " +
                $"{gameObject.name} の Collider2D が Is Trigger になっていません。",
                this);
        }

        if (targetButton == null)
        {
            Debug.LogWarning(
                $"[{nameof(ShowButtonInTrigger2D)}] " +
                $"{gameObject.name} の Target Button が未設定です。",
                this);
        }
    }

    private void Start()
    {
        if (hideOnStart)
        {
            SetButtonVisible(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerColliderCount++;
        SetButtonVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount == 0)
        {
            SetButtonVisible(false);
        }
    }

    private void OnDisable()
    {
        playerColliderCount = 0;
        SetButtonVisible(false);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        // 子オブジェクト側にColliderがあるプレイヤーにも対応します。
        Transform rootTransform = other.transform.root;

        return other.CompareTag(playerTag)
               || (rootTransform != null && rootTransform.CompareTag(playerTag));
    }

    private void SetButtonVisible(bool isVisible)
    {
        if (targetButton != null && targetButton.activeSelf != isVisible)
        {
            targetButton.SetActive(isVisible);
        }
    }
}