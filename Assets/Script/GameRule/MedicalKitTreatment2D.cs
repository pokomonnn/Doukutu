using UnityEngine;

/// <summary>
/// 固定枠の在庫から、プレイヤーまたは救出NPCへ回復効果を適用します。
/// 所持InventoryのTryUseItemは呼びません（固定枠は通常Inventory.Grid内ではないため）。
/// 治療効果が実際に発生したときだけ消費します。
/// </summary>
[DisallowMultipleComponent]
public sealed class MedicalKitTreatment2D : MonoBehaviour
{
    [Header("Playerの参照（空なら自動検索）")]
    [Tooltip("プレイヤーのTransform。救急キットがManager/InventoryManager配下にある場合は指定推奨。未設定ならPlayerタグ等から自動検索します。")]
    [SerializeField] private Transform playerRootOverride;
    [SerializeField] private CharacterHealth playerHealth;
    [SerializeField] private PlayerStatusConditionController playerConditions;
    [SerializeField] private PlayerConsumableUseController useAnimation;
    [SerializeField] private AudioSource useAudioSource;
    [Header("診断")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake() { Resolve(); }

    /// <summary>
    /// MedicalKitInventory2DはInventoryManagerに置かれることがあるため、
    /// このObjectの子だけではなくシーン上の実際のPlayerを探します。
    /// NPC治療は引き続き引数npcを対象にし、この参照は使いません。
    /// </summary>
    private void Resolve()
    {
        if (useAudioSource == null)
            useAudioSource = GetComponent<AudioSource>();

        Transform playerRoot = FindPlayerRoot();
        if (playerRoot == null)
            return;

        // Inspectorで設定された参照は維持。未設定・破棄時だけ補完します。
        if (playerHealth == null)
            playerHealth = FindComponentOnPlayer<CharacterHealth>(playerRoot);
        if (playerConditions == null)
            playerConditions = FindComponentOnPlayer<PlayerStatusConditionController>(playerRoot);
        if (useAnimation == null)
            useAnimation = FindComponentOnPlayer<PlayerConsumableUseController>(playerRoot);
    }

    private Transform FindPlayerRoot()
    {
        // Player Root Overrideを指定した場合は最優先。
        if (playerRootOverride != null)
            return playerRootOverride;

        // シリアライズ済みのプレイヤー参照を優先。
        if (playerConditions != null)
            return playerConditions.transform;
        if (playerHealth != null)
            return playerHealth.transform;
        if (useAnimation != null)
            return useAnimation.transform;

        // Kit自身をPlayerに付けている既存構成も引き続き使用可能。
        PlayerStatusConditionController localConditions =
            GetComponent<PlayerStatusConditionController>() ??
            GetComponentInParent<PlayerStatusConditionController>();
        if (localConditions != null)
        {
            playerConditions = localConditions;
            return localConditions.transform;
        }

        // Manager上のKitからでも、PlayerタグのGameObjectを優先して特定。
        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                return taggedPlayer.transform;
        }
        catch (UnityException)
        {
            // Playerタグ未登録のプロジェクトでも例外で使用処理を止めない。
        }

        // TagなしでもPlayer固有コンポーネントから特定可能。
        PlayerStatusConditionController sceneConditions =
            FindAnyObjectByType<PlayerStatusConditionController>();
        if (sceneConditions != null)
        {
            playerConditions = sceneConditions;
            return sceneConditions.transform;
        }

        PlayerConsumableUseController sceneUseAnimation =
            FindAnyObjectByType<PlayerConsumableUseController>();
        if (sceneUseAnimation != null)
        {
            useAnimation = sceneUseAnimation;
            return sceneUseAnimation.transform;
        }

        return null;
    }

    private static T FindComponentOnPlayer<T>(Transform root) where T : Component
    {
        if (root == null) return null;
        T target = root.GetComponent<T>();
        if (target == null) target = root.GetComponentInParent<T>();
        if (target == null) target = root.GetComponentInChildren<T>(true);
        return target;
    }

    [ContextMenu("診断：プレイヤーの治療先を確認")]
    private void DiagnosePlayerTreatmentTarget()
    {
        Resolve();
        Debug.Log(
            "[MedicalKitTreatment][参照診断] " +
            "PlayerRoot=" + (FindPlayerRoot() != null ? FindPlayerRoot().name : "なし") +
            " / Health=" + (playerHealth != null ? playerHealth.name : "なし") +
            " / Conditions=" + (playerConditions != null ? playerConditions.name : "なし") +
            " / UseAnimation=" + (useAnimation != null ? useAnimation.name : "なし"),
            this);
    }

    /// <summary>
    /// 使用前の判定。副作用なし／アイテム消費なし。
    /// NPCの3秒処置開始時に使用し、完了時のTryUseでも同条件を再確認。
    /// </summary>
    public bool CanTreat(MedicalKitInventory2D kit, ConsumableItemData data,
        RescuePersonSymptoms2D npc, out string message)
    {
        Resolve();
        if (kit == null || data == null || !kit.IsAllowed(data))
        {
            message = "このアイテムは応急キットに登録されていません";
            return false;
        }
        if (kit.GetAmount(data) <= 0)
        {
            message = data.DisplayName + " の在庫が0個です";
            return false;
        }
        if (npc != null && npc.IsDead)
        {
            message = "死亡したNPCには使用できません";
            return false;
        }
        CharacterHealth health = npc != null ? npc.Health : playerHealth;
        if (health == null || health.IsDead)
        {
            message = npc == null
                ? (health == null
                    ? "プレイヤーのHealthが見つかりません。MedicalKitTreatment2DのPlayer Root OverrideにPlayerを設定してください"
                    : "死亡中は使用できません")
                : "治療対象のHealthが未設定、または死亡しています";
            return false;
        }
        // NPCは医療用品ごとに決めた症状だけを処置できる。
        // HPが減っているだけでは、無関係な包帯/解毒剤/副木を使用できない。
        StatusConditionType cures = npc != null
            ? kit.GetNpcTreatmentCondition(data)
            : kit.GetTreatmentConditions(data);
        if (npc != null && cures == StatusConditionType.None)
        {
            message = "この医療用品にはNPC治療用の対応症状が設定されていません。MedicalKitInventory2Dの固定枠を確認してください";
            return false;
        }
        bool canCure = npc != null
            ? ((npc.ActiveSymptoms & cures & ~npc.StabilizedSymptoms) != 0)
            : (playerConditions != null && (playerConditions.ActiveConditions & cures) != 0);
        // 一致する症状の処置に成功する場合のみNPCのHP回復も許可する。
        bool canHeal = data.HealAmount > 0 && health.CurrentHealth < health.MaxHealth &&
            (npc == null || canCure);
        if (!canCure && !canHeal)
        {
            message = npc != null
                ? "このNPCには「" + data.DisplayName + "」を使用できません。対応する未処置の症状がありません（出血＝包帯／毒＝解毒剤／骨折＝副木）"
                : "今はこのアイテムで回復できる状態ではありません";
            return false;
        }
        message = "処置可能";
        return true;
    }

    /// <summary>
    /// npc == null ならプレイヤー治療。
    /// NPC: RescuePersonSymptoms2D.StabilizeSymptoms（洞窟での応急処置）。
    /// Player: PlayerStatusConditionController.CureConditions（骨折など完治）。
    /// </summary>
    public bool TryUse(MedicalKitInventory2D kit, ConsumableItemData data,
        RescuePersonSymptoms2D npc, out string message)
    {
        Resolve();
        // 処置中に症状が別の方法で改善された場合も含め、消費前に再検証。
        if (!CanTreat(kit, data, npc, out message)) return false;
        if (kit == null || data == null || !kit.IsAllowed(data))
        {
            message = "このアイテムは応急キットに登録されていません";
            return false;
        }
        if (kit.GetAmount(data) <= 0)
        {
            message = data.DisplayName + " の在庫が0個です";
            return false;
        }
        if (npc != null && npc.IsDead)
        {
            message = "死亡したNPCには使用できません";
            return false;
        }

        CharacterHealth targetHealth = npc != null ? npc.Health : playerHealth;
        if (npc == null && (targetHealth == null || targetHealth.IsDead))
        {
            message = "プレイヤーのHealthが未設定か、使用できない状態です";
            Debug.LogWarning("[MedicalKitTreatment] CharacterHealthが見つかりません。", this);
            return false;
        }
        StatusConditionType cures = npc != null
            ? kit.GetNpcTreatmentCondition(data)
            : kit.GetTreatmentConditions(data);
        bool canCure = false;
        if (cures != StatusConditionType.None)
        {
            if (npc != null)
            {
                // NPCは処置済みフラグを立てる。すでに処置済みの症状には消費しない。
                canCure = ((npc.ActiveSymptoms & cures & ~npc.StabilizedSymptoms) != 0);
            }
            else if (playerConditions != null)
            {
                canCure = ((playerConditions.ActiveConditions & cures) != 0);
            }
        }
        bool canHeal = data.HealAmount > 0 && targetHealth != null &&
            !targetHealth.IsDead && targetHealth.CurrentHealth < targetHealth.MaxHealth &&
            (npc == null || canCure);

        if (!canCure && !canHeal)
        {
            message = npc != null
                ? "このNPCには処置が必要な症状がありません（または処置済みです）"
                : "今はこのアイテムで回復できる状態ではありません";
            if (cures != StatusConditionType.None && npc == null && playerConditions == null)
            {
                message = "PlayerStatusConditionControllerがPlayerにありません";
                Debug.LogWarning("[MedicalKitTreatment] PlayerStatusConditionControllerが見つかりません。", this);
            }
            return false;
        }

        // NPC治療結果：今回新たに処置した症状だけを記録する。
        StatusConditionType treatmentBeforeFlags = npc != null
            ? npc.StabilizedSymptoms : StatusConditionType.None;

        // 使えることが確認できたときだけ効果を発生させる。
        bool applied = false;
        if (canCure)
            applied |= npc != null ? npc.StabilizeSymptoms(cures) : playerConditions.CureConditions(cures);
        if (canHeal)
        {
            int before = targetHealth.CurrentHealth;
            targetHealth.Heal(data.HealAmount);
            applied |= targetHealth.CurrentHealth > before;
        }
        if (!applied)
        {
            message = "使用しましたが回復効果が発生しませんでした（在庫は消費していません）";
            return false;
        }

        if (data.ConsumeOnUse && !kit.TryConsume(data, 1))
        {
            // 単一フレームで在庫が変わらない通常処理では到達しません。
            message = "治療効果は発生しましたが在庫更新に失敗しました。Consoleを確認してください";
            Debug.LogError("[MedicalKitTreatment] 使用後の消費に失敗。", this);
            return false;
        }

        // NPC治療結果：処置完了時だけ集計（途中キャンセル・失敗は対象外）。
        if (npc != null)
        {
            StatusConditionType newlyStabilized =
                npc.StabilizedSymptoms & ~treatmentBeforeFlags;
            RescueNpcTreatmentResultSession2D.RecordSuccessfulTreatment(npc, newlyStabilized);

            // NPCの3秒処置完了後、実際に治療が成功した場合だけ1回通知。
            GameEventLogManager2D.NpcTreated(data.DisplayName);
        }

        if (useAnimation != null) useAnimation.BeginConsumableUse(data);
        if (data.UseSound != null)
        {
            if (useAudioSource != null) useAudioSource.PlayOneShot(data.UseSound);
            else AudioSource.PlayClipAtPoint(data.UseSound, transform.position);
        }

        string target = npc != null ? "NPC「" + npc.name + "」" : "プレイヤー";
        string result = npc != null && canCure ? "（症状は応急処置済み）" : "";
        message = target + "に" + data.DisplayName + "を使用しました" + result +
            "。残り " + kit.GetAmount(data) + " / " + kit.MaxAmountPerSlot;
        if (showDebugLogs) Debug.Log("[MedicalKitTreatment] " + message, this);
        return true;
    }
}
