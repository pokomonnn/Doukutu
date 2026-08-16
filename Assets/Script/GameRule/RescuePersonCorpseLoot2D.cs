using System.Collections;
using UnityEngine;

/// <summary>
/// 救出対象NPCが死亡した時に、死体をItemBoxとして漁れるようにします。
///
/// 推奨構成:
/// RescueNPC (CharacterHealth / RescuePersonTarget2D / このスクリプト)
/// └ CorpseLoot
///    ├ Collider2D (Is Trigger = ON)
///    ├ ItemBoxInventory
///    ├ ItemBoxInteractable
///    ├ AudioSource
///    └ ItemBoxRandomLootInitializer
///
/// 生存中はCorpseLootの操作を無効化し、死亡した瞬間にLootを抽選して
/// ItemBoxInteractableを有効化します。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonCorpseLoot2D : MonoBehaviour
{
    [Header("死亡判定")]
    [Tooltip("NPCのCharacterHealth。未設定なら同じObject/親子から自動取得します。")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("救出状態管理。未設定なら同じObject/親子から自動取得します。")]
    [SerializeField] private RescuePersonTarget2D rescueTarget;

    [Header("死体ItemBox")]
    [Tooltip("死亡後に有効化するItemBoxInteractable。通常は子のCorpseLootに付けます。")]
    [SerializeField] private ItemBoxInteractable corpseItemBoxInteractable;

    [Tooltip("死体のItemBoxInventory。通常は子のCorpseLootに付けます。")]
    [SerializeField] private ItemBoxInventory corpseInventory;

    [Tooltip("死体を漁る範囲のTrigger Collider。生存中は無効、死亡後に有効化します。")]
    [SerializeField] private Collider2D corpseLootTrigger;

    [Header("死体のランダム所持品")]
    [Tooltip("ItemBoxLootTableから死体の中身を抽選するInitializerです。")]
    [SerializeField] private ItemBoxRandomLootInitializer lootInitializer;

    [Tooltip("このNPC専用のLootTable。未設定ならInitializer側に設定済みのLootTableを使います。")]
    [SerializeField] private ItemBoxLootTable corpseLootTable;

    [Tooltip("死亡した時に初めてLootを抽選します。通常はON推奨です。")]
    [SerializeField] private bool rollLootOnDeath = true;

    [Header("死亡時の見た目・機能")]
    [Tooltip("死亡時にOFFにするBehaviour。会話用スクリプトなどを指定できます。")]
    [SerializeField] private Behaviour[] behavioursToDisableOnDeath;

    [Tooltip("死亡時に非表示にするObject。会話アイコンなどを指定できます。")]
    [SerializeField] private GameObject[] objectsToDisableOnDeath;

    [Tooltip("死亡時に表示するObject。死体Spriteなどを指定できます。")]
    [SerializeField] private GameObject[] objectsToEnableOnDeath;

    [Tooltip("Animatorがある場合、死亡時にこのTriggerを再生します。空欄なら使用しません。")]
    [SerializeField] private Animator animator;

    [SerializeField] private string deathTriggerName = "Death";

    [Header("死亡時の色変更")]
    [Tooltip("ONなら死亡時にNPCのSpriteRendererの色を変更します。")]
    [SerializeField] private bool changeSpriteColorOnDeath = true;

    [Tooltip("色を変更するSpriteRenderer。空欄ならこのNPC配下から自動取得します。CorpseLoot配下を除外したい場合は手動指定してください。")]
    [SerializeField] private SpriteRenderer[] spriteRenderersToTint;

    [Tooltip("死亡時の色です。灰色や暗い赤などがおすすめです。")]
    [SerializeField] private Color deathColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Tooltip("死亡色へ変化する時間です。0なら即座に変更します。")]
    [SerializeField, Min(0f)] private float deathColorFadeDuration = 0.25f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsCorpseLootEnabled => corpseLootEnabled;

    private bool isSubscribed;
    private bool corpseLootEnabled;
    private Color[] aliveSpriteColors;
    private Coroutine deathColorCoroutine;

    private void Awake()
    {
        FindReferences();
        CacheAliveSpriteColors();
        PrepareAliveState();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeHealthEvents();

        if (characterHealth != null && characterHealth.IsDead)
        {
            ApplyDeathState();
        }
        else
        {
            PrepareAliveState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeHealthEvents();
    }

    private void SubscribeHealthEvents()
    {
        if (isSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.Died += HandleDied;
        characterHealth.HealthChanged += HandleHealthChanged;
        isSubscribed = true;
    }

    private void UnsubscribeHealthEvents()
    {
        if (!isSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.Died -= HandleDied;
        characterHealth.HealthChanged -= HandleHealthChanged;
        isSubscribed = false;
    }

    private void HandleDied()
    {
        ApplyDeathState();
    }

    /// <summary>
    /// RestoreHealth(0, false)のようにDiedイベントを発火しない死亡復元にも対応します。
    /// </summary>
    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (currentHealth <= 0)
        {
            ApplyDeathState();
        }
    }

    private void PrepareAliveState()
    {
        if (corpseLootEnabled)
        {
            return;
        }

        // Startで勝手にLoot抽選されないように抑止する。
        if (lootInitializer != null)
        {
            lootInitializer.SetManagedBySpawnManager(true);

            if (corpseLootTable != null)
            {
                lootInitializer.SetLootTable(corpseLootTable);
            }
        }

        RestoreAliveSpriteColors();
        SetCorpseInteractionEnabled(false);
    }

    [ContextMenu("Apply Death State Now")]
    public void ApplyDeathState()
    {
        if (corpseLootEnabled)
        {
            return;
        }

        FindReferences();
        corpseLootEnabled = true;

        if (rescueTarget != null && rescueTarget.IsRescued)
        {
            Log("すでに救出済みですが死亡状態が適用されました。");
        }

        if (animator != null && !string.IsNullOrWhiteSpace(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
        }

        DisableDeathBehaviours();
        ApplyDeathObjects();
        ApplyDeathColor();

        if (lootInitializer != null)
        {
            lootInitializer.SetManagedBySpawnManager(true);

            if (corpseLootTable != null)
            {
                lootInitializer.SetLootTable(corpseLootTable);
            }

            if (rollLootOnDeath)
            {
                bool rolled = lootInitializer.RollLoot(null, false);
                Log(rolled
                    ? "死亡時Loot抽選を完了しました。"
                    : "死亡時Loot抽選を実行できませんでした。LootTable設定を確認してください。");
            }
        }
        else if (rollLootOnDeath)
        {
            Debug.LogWarning(
                $"[RescuePersonCorpseLoot2D] {name}: " +
                "Roll Loot On DeathがONですが、ItemBoxRandomLootInitializerが見つかりません。",
                this
            );
        }

        SetCorpseInteractionEnabled(true);
        Log("死亡しました。死体のItemBox操作を有効化しました。");
    }

    private void SetCorpseInteractionEnabled(bool enabledState)
    {
        if (corpseLootTrigger != null)
        {
            corpseLootTrigger.enabled = enabledState;
        }

        if (corpseItemBoxInteractable != null)
        {
            corpseItemBoxInteractable.enabled = enabledState;
        }
    }

    private void DisableDeathBehaviours()
    {
        if (behavioursToDisableOnDeath == null)
        {
            return;
        }

        foreach (Behaviour behaviour in behavioursToDisableOnDeath)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private void ApplyDeathObjects()
    {
        if (objectsToDisableOnDeath != null)
        {
            foreach (GameObject target in objectsToDisableOnDeath)
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        if (objectsToEnableOnDeath != null)
        {
            foreach (GameObject target in objectsToEnableOnDeath)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }
        }
    }

    private void CacheAliveSpriteColors()
    {
        EnsureSpriteRenderers();

        if (spriteRenderersToTint == null)
        {
            aliveSpriteColors = null;
            return;
        }

        aliveSpriteColors = new Color[spriteRenderersToTint.Length];

        for (int i = 0; i < spriteRenderersToTint.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderersToTint[i];
            aliveSpriteColors[i] = renderer != null
                ? renderer.color
                : Color.white;
        }
    }

    private void EnsureSpriteRenderers()
    {
        if (spriteRenderersToTint != null && spriteRenderersToTint.Length > 0)
        {
            return;
        }

        spriteRenderersToTint = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void RestoreAliveSpriteColors()
    {
        if (!changeSpriteColorOnDeath)
        {
            return;
        }

        EnsureSpriteRenderers();

        if (aliveSpriteColors == null ||
            aliveSpriteColors.Length != spriteRenderersToTint.Length)
        {
            CacheAliveSpriteColors();
            return;
        }

        if (deathColorCoroutine != null)
        {
            StopCoroutine(deathColorCoroutine);
            deathColorCoroutine = null;
        }

        for (int i = 0; i < spriteRenderersToTint.Length; i++)
        {
            if (spriteRenderersToTint[i] != null)
            {
                spriteRenderersToTint[i].color = aliveSpriteColors[i];
            }
        }
    }

    private void ApplyDeathColor()
    {
        if (!changeSpriteColorOnDeath)
        {
            return;
        }

        EnsureSpriteRenderers();

        if (spriteRenderersToTint == null || spriteRenderersToTint.Length == 0)
        {
            Log("死亡色を変更するSpriteRendererが見つかりませんでした。");
            return;
        }

        if (aliveSpriteColors == null ||
            aliveSpriteColors.Length != spriteRenderersToTint.Length)
        {
            CacheAliveSpriteColors();
        }

        if (deathColorCoroutine != null)
        {
            StopCoroutine(deathColorCoroutine);
            deathColorCoroutine = null;
        }

        if (deathColorFadeDuration <= 0f || !isActiveAndEnabled)
        {
            SetSpriteColorImmediate(deathColor);
            return;
        }

        deathColorCoroutine = StartCoroutine(FadeToDeathColor());
    }

    private IEnumerator FadeToDeathColor()
    {
        EnsureSpriteRenderers();

        Color[] startColors = new Color[spriteRenderersToTint.Length];

        for (int i = 0; i < spriteRenderersToTint.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderersToTint[i];
            startColors[i] = renderer != null
                ? renderer.color
                : Color.white;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, deathColorFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < spriteRenderersToTint.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderersToTint[i];

                if (renderer != null)
                {
                    renderer.color = Color.Lerp(startColors[i], deathColor, t);
                }
            }

            yield return null;
        }

        SetSpriteColorImmediate(deathColor);
        deathColorCoroutine = null;
    }

    private void SetSpriteColorImmediate(Color color)
    {
        if (spriteRenderersToTint == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in spriteRenderersToTint)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInChildren<CharacterHealth>(true);
        }

        if (rescueTarget == null)
        {
            rescueTarget = GetComponent<RescuePersonTarget2D>();
        }

        if (rescueTarget == null)
        {
            rescueTarget = GetComponentInParent<RescuePersonTarget2D>();
        }

        if (rescueTarget == null)
        {
            rescueTarget = GetComponentInChildren<RescuePersonTarget2D>(true);
        }

        if (corpseItemBoxInteractable == null)
        {
            corpseItemBoxInteractable =
                GetComponentInChildren<ItemBoxInteractable>(true);
        }

        if (corpseInventory == null)
        {
            corpseInventory =
                GetComponentInChildren<ItemBoxInventory>(true);
        }

        if (lootInitializer == null)
        {
            lootInitializer =
                GetComponentInChildren<ItemBoxRandomLootInitializer>(true);
        }

        if (corpseLootTrigger == null && corpseItemBoxInteractable != null)
        {
            corpseLootTrigger =
                corpseItemBoxInteractable.GetComponent<Collider2D>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[RescuePersonCorpseLoot2D] {name}: {message}", this);
    }

    private void OnValidate()
    {
        deathColorFadeDuration = Mathf.Max(0f, deathColorFadeDuration);
        FindReferences();
    }
}
