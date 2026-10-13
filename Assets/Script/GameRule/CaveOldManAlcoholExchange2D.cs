using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// 洞窟の素材おじさん：地面に落ちた AlcoholItemData を1本回収して、
/// 登録した ItemData を WorldItemPickup として地面に落とす。
/// プレイヤーの所持アイテムは触らず、WorldItemPickup のみを対象にする。
///
/// 必要な既存型: AlcoholItemData, WorldItemPickup, InventoryItem, ItemData。
/// CaveSupplyOldMan2D と同じ GameObject に追加する。
/// </summary>
[DisallowMultipleComponent]
public class CaveOldManAlcoholExchange2D : MonoBehaviour
{
    [Serializable]
    public class RewardEntry
    {
        [Tooltip("お酒1本と交換で地面に出すアイテム")]
        public ItemData itemData;

        [Min(1)]
        public int amount = 1;

        [Tooltip("抽選の重み。10は5の2倍選ばれやすい。0なら抽選対象外")]
        [Min(0f)]
        public float weight = 1f;
    }

    [Header("旧自動配布を停止")]
    [Tooltip("旧 CaveSupplyOldMan2D など、近づくだけで配るコンポーネント。空欄でも子階層から CaveSupplyOldMan2D を探して無効化する")]
    [SerializeField] private Behaviour oldAutomaticSupplyComponent;

    [SerializeField] private bool disableOldAutomaticSupply = true;

    [Header("お酒の回収")]
    [Tooltip("素材おじさんから地面の酒までの距離（単位: ワールド）")]
    [SerializeField, Min(0.1f)] private float acceptRadius = 2f;

    [Tooltip("落ちてから回収するまでの猶予。誤判定防止用")]
    [SerializeField, Min(0f)] private float settleDelaySeconds = 0.45f;

    [Tooltip("酒を1本受け取った後、次の交換ができるまでの時間")]
    [SerializeField, Min(0f)] private float exchangeCooldownSeconds = 1.25f;

    [Tooltip("地面の酒を探索する間隔")]
    [SerializeField, Range(0.05f, 2f)] private float scanIntervalSeconds = 0.2f;

    [Header("交換する素材")]
    [Tooltip("既存の DroppedItem プレハブ。WorldItemPickup が付いていること")]
    [SerializeField] private WorldItemPickup droppedItemPrefab;

    [SerializeField] private List<RewardEntry> rewards = new List<RewardEntry>();

    [Header("素材を落とす位置・動き")]
    [SerializeField] private Vector2 rewardDropOffset = new Vector2(1f, 0.6f);

    [SerializeField] private Vector2 rewardThrowVelocity = new Vector2(1.4f, 2.5f);

    [Header("吹き出しとお返しまでの時間")]
    [Tooltip("酒を受け取ったセリフの後、素材を地面へ出すまで待つ時間")]
    [SerializeField, Min(0f)] private float rewardDelaySeconds = 1.5f;

    [Tooltip("空欄の場合は同じGameObjectの吹き出しを探し、無ければ自動追加する")]
    [SerializeField] private CaveOldManSpeechBubble2D speechBubble;

    [SerializeField] private bool addSpeechBubbleAutomatically = true;

    [Header("演出（任意）")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip acceptAlcoholSound;
    [SerializeField] private AudioClip rewardDropSound;
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private string thankYouTrigger = "";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    // 複数のおじさんが同じ酒を同時に交換することを防ぐ。
    private static readonly HashSet<int> ClaimedItems = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClaimsOnNewPlay()
    {
        ClaimedItems.Clear();
    }

    private readonly Dictionary<int, float> firstSeenTime = new Dictionary<int, float>();
    private float nextScanTime;
    private float nextExchangeTime;
    private bool warnedMissingReward;
    private Coroutine exchangeRoutine;
    private WorldItemPickup reservedPickup;
    private int reservedPickupId;

    private void Awake()
    {
        if (disableOldAutomaticSupply)
        {
            DisableOldComponent();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (speechBubble == null)
            speechBubble = GetComponent<CaveOldManSpeechBubble2D>();
        if (speechBubble == null && addSpeechBubbleAutomatically)
            speechBubble = gameObject.AddComponent<CaveOldManSpeechBubble2D>();
    }

    private void OnEnable()
    {
        nextScanTime = Time.time + 0.1f;
    }

    private void OnDisable()
    {
        if (exchangeRoutine != null)
        {
            StopCoroutine(exchangeRoutine);
            exchangeRoutine = null;
        }
        ReleaseReservation();
        firstSeenTime.Clear();
    }

    private void DisableOldComponent()
    {
        Behaviour legacy = oldAutomaticSupplyComponent;
        if (legacy == null)
        {
            MonoBehaviour[] components = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null &&
                    components[i] != this &&
                    components[i].GetType().Name == "CaveSupplyOldMan2D")
                {
                    legacy = components[i];
                    break;
                }
            }
        }

        if (legacy != null && legacy != this && legacy.enabled)
        {
            legacy.enabled = false;
            Debug.Log("[CaveOldManExchange][旧配布停止] " + legacy.GetType().Name +
                      " を無効にしました。お酒のドロップ交換に切り替えます。", this);
        }
        else if (legacy == null)
        {
            Debug.LogWarning("[CaveOldManExchange][確認] 旧 CaveSupplyOldMan2D が見つかりません。" +
                             "別のGameObjectにある場合は Old Automatic Supply Component へ設定してね。", this);
        }
    }

    private void Update()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanIntervalSeconds;

        if (!HasValidRewards()) return;
        if (reservedPickup != null || exchangeRoutine != null) return;
        if (Time.time < nextExchangeTime) return;

        // WorldItemPickup はプレイヤーのドロップ処理で使われている既存コンポーネント。
        // Collider や Layer の設定によらず機能するよう、地面アイテムの位置を確認する。
        WorldItemPickup[] groundItems = FindObjectsByType<WorldItemPickup>(FindObjectsSortMode.None);
        WorldItemPickup closest = null;
        float shortestSq = acceptRadius * acceptRadius;
        HashSet<int> presentIds = new HashSet<int>();

        for (int i = 0; i < groundItems.Length; i++)
        {
            WorldItemPickup pickup = groundItems[i];
            if (pickup == null || !pickup.isActiveAndEnabled || !pickup.HasValidDroppedItem) continue;

            InventoryItem inventoryItem = pickup.DroppedItem;
            if (inventoryItem == null || inventoryItem.Amount <= 0 ||
                !(inventoryItem.ItemData is AlcoholItemData)) continue;

            int id = pickup.GetInstanceID();
            presentIds.Add(id);

            float distanceSq = ((Vector2)(pickup.transform.position - transform.position)).sqrMagnitude;
            if (distanceSq > acceptRadius * acceptRadius) continue;

            if (!firstSeenTime.ContainsKey(id)) firstSeenTime[id] = Time.time;
            if (Time.time - firstSeenTime[id] < settleDelaySeconds) continue;

            if (ClaimedItems.Contains(id)) continue;

            if (distanceSq <= shortestSq)
            {
                shortestSq = distanceSq;
                closest = pickup;
            }
        }

        // 古い参照を蓄積しない。
        if (firstSeenTime.Count > presentIds.Count)
        {
            List<int> missing = new List<int>();
            foreach (int id in firstSeenTime.Keys)
            {
                if (!presentIds.Contains(id)) missing.Add(id);
            }
            foreach (int id in missing) firstSeenTime.Remove(id);
        }

        if (closest != null) Exchange(closest);
    }

    private bool HasValidRewards()
    {
        if (droppedItemPrefab == null)
        {
            WarnOnce("Dropped Item Prefab に WorldItemPickup 付きのプレハブを割り当ててね。");
            return false;
        }

        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardEntry entry = rewards[i];
                if (entry != null && entry.itemData != null && entry.weight > 0f && entry.amount > 0 &&
                    !(entry.itemData is AlcoholItemData))
                {
                    warnedMissingReward = false;
                    return true;
                }
            }
        }

        WarnOnce("Rewards が未設定です。ItemData と Weight > 0 を設定してね（お酒以外）。");
        return false;
    }

    private void WarnOnce(string message)
    {
        if (warnedMissingReward) return;
        warnedMissingReward = true;
        Debug.LogWarning("[CaveOldManExchange][設定不足] " + message, this);
    }

    private RewardEntry ChooseReward()
    {
        float total = 0f;
        foreach (RewardEntry r in rewards)
        {
            if (r != null && r.itemData != null && r.weight > 0f && r.amount > 0 &&
                !(r.itemData is AlcoholItemData))
                total += r.weight;
        }
        if (total <= 0f) return null;
        float roll = UnityEngine.Random.Range(0f, total);
        foreach (RewardEntry r in rewards)
        {
            if (r == null || r.itemData == null || r.weight <= 0f || r.amount <= 0 ||
                r.itemData is AlcoholItemData) continue;
            roll -= r.weight;
            if (roll <= 0f) return r;
        }
        for (int i = rewards.Count - 1; i >= 0; i--)
        {
            RewardEntry r = rewards[i];
            if (r != null && r.itemData != null && r.weight > 0f && r.amount > 0 &&
                !(r.itemData is AlcoholItemData)) return r;
        }
        return null;
    }

    /// <summary>
    /// お酒を一時予約し、吹き出しの後で報酬を生成する。
    /// 成功するまでお酒の個数は減らさない。途中で無効化されたら予約を解除する。
    /// </summary>
    private void Exchange(WorldItemPickup alcoholOnGround)
    {
        if (reservedPickup != null || exchangeRoutine != null) return;
        if (alcoholOnGround == null || !alcoholOnGround.HasValidDroppedItem) return;
        InventoryItem offered = alcoholOnGround.DroppedItem;
        if (offered == null || offered.Amount <= 0 || !(offered.ItemData is AlcoholItemData)) return;

        RewardEntry chosen = ChooseReward();
        if (chosen == null) return;

        int id = alcoholOnGround.GetInstanceID();
        if (!ClaimedItems.Add(id)) return;

        reservedPickup = alcoholOnGround;
        reservedPickupId = id;
        // WorldItemPickupの近距離候補検索は、無効化されたComponentも
        // 呼び出す場合があるので、演出中はGameObjectごと一時非表示にする。
        // 成功時に1個だけ減らし、残りがあれば元へ戻す。
        alcoholOnGround.gameObject.SetActive(false);

        Log("[交換予約] 酒=" + offered.ItemData.DisplayName +
            " / お返し=" + chosen.itemData.DisplayName +
            " / 待ち=" + rewardDelaySeconds.ToString("F2") + "秒");
        if (speechBubble != null)
            speechBubble.SayAccept(offered.ItemData.DisplayName);
        PlaySound(acceptAlcoholSound);
        if (npcAnimator != null && !string.IsNullOrEmpty(thankYouTrigger))
            npcAnimator.SetTrigger(thankYouTrigger);

        exchangeRoutine = StartCoroutine(ExchangeAfterDelay(chosen, id));
    }

    private IEnumerator ExchangeAfterDelay(RewardEntry chosen, int id)
    {
        if (rewardDelaySeconds > 0f)
            yield return new WaitForSeconds(rewardDelaySeconds);
        else
            yield return null;

        bool success = TryFinishExchange(chosen, id);
        if (!success && speechBubble != null)
            speechBubble.SayExchangeFailed();

        ReleaseReservation();
        exchangeRoutine = null;
        nextExchangeTime = Time.time + exchangeCooldownSeconds;
    }

    private bool TryFinishExchange(RewardEntry chosen, int id)
    {
        WorldItemPickup alcoholOnGround = reservedPickup;
        if (alcoholOnGround == null || !alcoholOnGround.HasValidDroppedItem)
        {
            Log("[交換中止] 酒がシーンから無くなりました。");
            return false;
        }

        InventoryItem offered = alcoholOnGround.DroppedItem;
        if (offered == null || offered.Amount <= 0 || !(offered.ItemData is AlcoholItemData))
            return false;
        if (chosen == null || chosen.itemData == null || droppedItemPrefab == null)
            return false;

        int amountToGive = Mathf.Clamp(chosen.amount, 1, Mathf.Max(1, chosen.itemData.MaxStack));
        WorldItemPickup rewardPickup = null;
        try
        {
            Vector3 rewardPosition = transform.position + (Vector3)rewardDropOffset;
            rewardPickup = Instantiate(droppedItemPrefab, rewardPosition, Quaternion.identity);
            if (rewardPickup == null)
                return false;
            rewardPickup.Setup(new InventoryItem(chosen.itemData, 0, 0, amountToGive), false);
            rewardPickup.SetVelocity(rewardThrowVelocity);
        }
        catch (Exception e)
        {
            if (rewardPickup != null) Destroy(rewardPickup.gameObject);
            Debug.LogException(e, this);
            return false;
        }

        int consumed = offered.RemoveAmount(1);
        if (consumed != 1)
        {
            Destroy(rewardPickup.gameObject);
            Debug.LogWarning("[CaveOldManExchange][失敗] 酒を消費できなかったため素材生成を取り消しました。", this);
            return false;
        }

        string alcoholName = offered.ItemData.DisplayName;
        int remainingAmount = offered.Amount;
        if (offered.IsEmpty())
        {
            alcoholOnGround.enabled = false;
            Destroy(alcoholOnGround.gameObject);
            firstSeenTime.Remove(id);
        }
        else
        {
            alcoholOnGround.Setup(offered, false);
            firstSeenTime[id] = Time.time;
        }

        if (speechBubble != null)
            speechBubble.SayReward(chosen.itemData.DisplayName);
        PlaySound(rewardDropSound);
        Log("[交換成功] お酒=" + alcoholName + " ×1 -> " +
            chosen.itemData.DisplayName + " ×" + amountToGive +
            " / 酒スタック残=" + remainingAmount);
        return true;
    }

    private void ReleaseReservation()
    {
        // OnDisableや報酬生成失敗でも酒を返し、再びEキーで拾えるようにする。
        if (reservedPickup != null && !reservedPickup.gameObject.activeSelf)
        {
            if (reservedPickup.HasValidDroppedItem &&
                reservedPickup.DroppedItem != null && reservedPickup.DroppedItem.Amount > 0)
                reservedPickup.gameObject.SetActive(true);
        }
        if (reservedPickupId != 0)
            ClaimedItems.Remove(reservedPickupId);
        reservedPickupId = 0;
        reservedPickup = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log("[CaveOldManExchange] " + name + " " + message, this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.95f, 0.75f, 0.12f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, acceptRadius));
    }
}
