using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーがこのオブジェクトのTrigger範囲に入っている間、
/// 指定したSpriteRendererの外周だけを白く表示します。
///
/// 既存のドア・ItemBox・NPC・エレベーター処理には手を加えません。
/// 既存Colliderを変更しないため、検知用の子Object（Is Trigger ON）に
/// このスクリプトを追加してください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class InteractableOutline2D : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("白く縁取りしたいオブジェクトの親。通常はドア・箱・NPC・エレベーターの見た目のRootを指定します。")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("指定があれば、このSpriteRendererだけを縁取ります。空ならVisual Rootの子を自動検索します。")]
    [SerializeField] private SpriteRenderer[] targetSprites;

    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    [Header("白い縁取り")]
    [Tooltip("InteractableWhiteSilhouette2D.shaderで作成したMaterialを指定してください。")]
    [SerializeField] private Material whiteSilhouetteMaterial;

    [Tooltip("縁取りの太さ（Unityのワールド単位）。0.025～0.06あたりを目安に調整します。")]
    [SerializeField, Min(0.001f)] private float outlineThickness = 0.035f;

    [Tooltip("白い縁取りをゆっくり明滅させます。")]
    [SerializeField] private bool pulse = true;

    [SerializeField, Min(0f)] private float pulseSpeed = 2f;

    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.7f;

    private static readonly Vector2[] Directions =
    {
        Vector2.right, Vector2.left, Vector2.up, Vector2.down,
        new Vector2(0.70710678f, 0.70710678f),
        new Vector2(-0.70710678f, 0.70710678f),
        new Vector2(0.70710678f, -0.70710678f),
        new Vector2(-0.70710678f, -0.70710678f)
    };

    private sealed class OutlinePart
    {
        public SpriteRenderer Source;
        public SpriteRenderer[] Copies;
    }

    private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
    private readonly HashSet<Collider2D> touchingPlayerColliders = new HashSet<Collider2D>();

    private Material createdMaterial;
    private bool isHighlighted;
    private bool dialogueDriven;
    public bool IsHighlighted => isHighlighted;

    // Dialogue already owns distance/hostility/carry checks; no extra physics trigger is needed.
    public static InteractableOutline2D CreateForDialogue(Transform root)
    {
        var existing = root.GetComponentInChildren<InteractableOutline2D>();
        if (existing != null) return existing;
        var child = new GameObject("Dialogue InteractableOutline2D");
        child.SetActive(false);
        child.transform.SetParent(root, false);
        var collider = child.AddComponent<BoxCollider2D>();
        collider.isTrigger = true; collider.enabled = false;
        var outline = child.AddComponent<InteractableOutline2D>();
        outline.visualRoot = root;
        outline.dialogueDriven = true;
        child.SetActive(true);
        return outline;
    }
    public void SetDialogueHighlight(bool value) { SetHighlighted(value); }

    private void Awake()
    {
        Collider2D area = GetComponent<Collider2D>();
        if (area != null && !area.isTrigger)
        {
            Debug.LogWarning(
                "[InteractableOutline2D] 検知用Collider2DのIs TriggerをONにしてください。" +
                " NPCの物理用ColliderをTriggerに変更せず、子オブジェクトに別のTriggerを作ってください。",
                this);
        }

        Material material = whiteSilhouetteMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Custom/InteractableWhiteSilhouette2D");
            if (shader != null)
            {
                createdMaterial = new Material(shader);
                createdMaterial.name = "Runtime_InteractableWhiteSilhouette";
                if (dialogueDriven) createdMaterial.SetFloat("_AlphaThreshold", .1f);
                material = createdMaterial;
            }
            else
            {
                Debug.LogError(
                    "[InteractableOutline2D] 白い縁取り用Shaderが見つかりません。" +
                    " shaderファイルをインポートし、そのShaderで作ったMaterialを設定してください。",
                    this);
                return;
            }
        }

        if (visualRoot == null)
        {
            visualRoot = transform.parent != null ? transform.parent : transform;
        }

        SpriteRenderer[] sources =
            targetSprites != null && targetSprites.Length > 0
                ? targetSprites
                : visualRoot.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer source in sources)
        {
            if (source == null)
                continue;

            OutlinePart part = new OutlinePart
            {
                Source = source,
                Copies = new SpriteRenderer[Directions.Length]
            };

            for (int i = 0; i < Directions.Length; i++)
            {
                GameObject copyObject = new GameObject("[White Outline] " + i);
                copyObject.transform.SetParent(source.transform, false);
                copyObject.transform.localRotation = Quaternion.identity;
                copyObject.transform.localScale = Vector3.one;

                SpriteRenderer copy = copyObject.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = material;
                copy.enabled = false;
                part.Copies[i] = copy;
            }

            outlineParts.Add(part);
        }

        if (outlineParts.Count == 0)
        {
            Debug.LogWarning(
                "[InteractableOutline2D] 表示対象のSpriteRendererがありません。" +
                " Visual Root または Target Spritesを設定してください。",
                this);
        }
    }

    private void OnEnable()
    {
        touchingPlayerColliders.Clear();
        SetHighlighted(false);
    }

    private void OnDisable()
    {
        touchingPlayerColliders.Clear();
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        if (createdMaterial != null)
        {
            Destroy(createdMaterial);
            createdMaterial = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayerCollider(other);
    }

    // 起動直後や、プレイヤーが既に範囲内にいる状態にも対応。
    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterPlayerCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (touchingPlayerColliders.Remove(other) &&
            touchingPlayerColliders.Count == 0)
        {
            SetHighlighted(false);
        }
    }

    private void RegisterPlayerCollider(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (touchingPlayerColliders.Add(other))
        {
            SetHighlighted(true);
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null || string.IsNullOrEmpty(playerTag))
            return false;

        // 子Colliderであっても親Playerのタグを認識。
        for (Transform current = other.transform;
             current != null;
             current = current.parent)
        {
            if (!current.CompareTag(playerTag))
                continue;

            // 背負ったNPCがPlayerの子になっているケースを除外。
            Rigidbody2D playerBody = current.GetComponent<Rigidbody2D>();
            if (playerBody != null &&
                other.attachedRigidbody != null &&
                other.attachedRigidbody != playerBody)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted)
            return;

        isHighlighted = highlighted;
        foreach (OutlinePart part in outlineParts)
        {
            foreach (SpriteRenderer copy in part.Copies)
            {
                if (copy != null)
                    copy.enabled = highlighted && part.Source != null && part.Source.enabled;
            }
        }
    }

    private void LateUpdate()
    {
        if (!isHighlighted)
            return;

        float alpha = pulse
            ? Mathf.Lerp(minimumAlpha, 1f,
                (Mathf.Sin(Time.time * Mathf.PI * 2f * pulseSpeed) + 1f) * 0.5f)
            : 1f;

        foreach (OutlinePart part in outlineParts)
        {
            SpriteRenderer source = part.Source;
            if (source == null)
                continue;

            for (int i = 0; i < part.Copies.Length; i++)
            {
                SpriteRenderer copy = part.Copies[i];
                if (copy == null)
                    continue;

                copy.enabled = source.enabled && source.sprite != null;
                if (!copy.enabled)
                    continue;

                // AnimatorによるSprite切替や反転にも追従。
                copy.sprite = source.sprite;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.drawMode = source.drawMode;
                if (source.drawMode != SpriteDrawMode.Simple)
                    copy.size = source.size;

                copy.spriteSortPoint = source.spriteSortPoint;
                copy.maskInteraction = source.maskInteraction;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder - 1;
                copy.color = new Color(1f, 1f, 1f, alpha);

                // スケールや回転の影響を受けても世界座標で一定幅の縁取り。
                Vector3 worldOffset = (Vector3)(Directions[i] * outlineThickness);
                copy.transform.localPosition =
                    source.transform.InverseTransformPoint(
                        source.transform.position + worldOffset);
            }
        }
    }

    private void OnValidate()
    {
        outlineThickness = Mathf.Max(0.001f, outlineThickness);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        minimumAlpha = Mathf.Clamp01(minimumAlpha);
    }
}
