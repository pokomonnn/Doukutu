using System;
using UnityEngine.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出撃準備画面専用。3種類の医療品を各ボタン1回=1個で直接購入。
/// 通常Inventoryには入れず、MedicalKitSessionStore2Dの固定3枠の数量を更新します。
/// Town_MainにMedicalKitInventory2Dが無い構成でもセッションへ安全に補充可能です。
/// </summary>
[DisallowMultipleComponent]
public class MedicalKitSupplyShop2D : MonoBehaviour
{
    [Serializable]
    public class SupplySlot
    {
        [Tooltip("医療キットの固定スロットと同じ番号。0=解毒剤,1=副木,2=包帯を想定。")]
        public int slotIndex;
        [Tooltip("町にMedicalKitInventory2Dが無い場合はこのItemDataを必ず指定してください。")]
        public ConsumableItemData itemData;
        [Min(0)] public int priceG = 100;
        public Button buyOneButton;
        public Image itemIcon;
        public TMP_Text itemNameText;
        public TMP_Text priceText;
        public TMP_Text stockText;
    }

    [Header("応急キット3枠")]
    [SerializeField] private MedicalKitInventory2D medicalKit;
    [SerializeField] private GameSessionManager gameSessionManager;
    [Tooltip("固定枠の3商品。Slot Index=0,1,2。各Buy One Buttonに購入ボタンを設定。")]
    [SerializeField] private SupplySlot[] supplies =
    {
        new SupplySlot { slotIndex = 0, priceG = 150 },
        new SupplySlot { slotIndex = 1, priceG = 150 },
        new SupplySlot { slotIndex = 2, priceG = 100 }
    };

    [Header("町に医療キット本体を置かない場合")]
    [Tooltip("Town_MainにMedicalKitInventory2Dが無いときに使う上限。洞窟側と同じ値にしてください。")]
    [SerializeField, Min(1)] private int fallbackMaxPerSlot = 3;

    [Header("表示")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool disableBuyButtonWhenFull = true;

    [Header("効果音")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip failedSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool subscribed;
    private UnityAction[] clickActions;

    private void Awake()
    {
        RegisterButtonListeners();
    }

    private void OnEnable()
    {
        FindReferences();
        RegisterButtonListeners();
        Subscribe();
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RemoveButtonListeners();
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
            gameSessionManager = GameSessionManager.Instance;
        if (gameSessionManager == null)
            gameSessionManager = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        if (medicalKit == null)
            medicalKit = FindAnyObjectByType<MedicalKitInventory2D>(FindObjectsInactive.Include);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (gameSessionManager != null) gameSessionManager.MoneyChanged += OnMoneyChanged;
        if (medicalKit != null) medicalKit.Changed += OnKitChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (gameSessionManager != null) gameSessionManager.MoneyChanged -= OnMoneyChanged;
        if (medicalKit != null) medicalKit.Changed -= OnKitChanged;
        subscribed = false;
    }

    private void OnMoneyChanged(int amount) => RefreshUI();
    private void OnKitChanged() => RefreshUI();

    private void RegisterButtonListeners()
    {
        RemoveButtonListeners();
        if (supplies == null) return;
        clickActions = new UnityAction[supplies.Length];
        for (int i = 0; i < supplies.Length; i++)
        {
            if (supplies[i] == null || supplies[i].buyOneButton == null) continue;
            int capturedIndex = i;
            UnityAction action = () => BuyOne(capturedIndex);
            clickActions[i] = action;
            supplies[i].buyOneButton.onClick.AddListener(action);
        }
    }

    private void RemoveButtonListeners()
    {
        if (clickActions == null || supplies == null) return;
        int length = Mathf.Min(clickActions.Length, supplies.Length);
        for (int i = 0; i < length; i++)
        {
            if (supplies[i] != null && supplies[i].buyOneButton != null && clickActions[i] != null)
                supplies[i].buyOneButton.onClick.RemoveListener(clickActions[i]);
        }
        clickActions = null;
    }

    public void BuySlot0() => BuyOne(0);
    public void BuySlot1() => BuyOne(1);
    public void BuySlot2() => BuyOne(2);

    /// <summary>0..2はsupplies配列の番号。各項目のslotIndexもチェックします。</summary>
    public bool BuyOne(int buttonIndex)
    {
        FindReferences();
        if (supplies == null || buttonIndex < 0 || buttonIndex >= supplies.Length ||
            supplies[buttonIndex] == null)
            return Fail("購入アイテムが設定されていません。");

        SupplySlot item = supplies[buttonIndex];
        int slot = item.slotIndex;
        if (slot < 0 || slot > 2)
            return Fail("Slot Indexは0〜2に設定してください。");
        ConsumableItemData data = ResolveItem(item);
        if (data == null)
            return Fail("医療品のItemDataが未設定です。固定枠またはSupply Slotに指定してください。");
        if (gameSessionManager == null)
            return Fail("GameSessionManagerが見つかりません。");

        SavedMedicalKitData snapshot = MedicalKitSessionStore2D.CreateSnapshot();
        int before = ReadSlot(snapshot, slot);
        int capacity = medicalKit != null ? medicalKit.MaxAmountPerSlot : Mathf.Max(1, fallbackMaxPerSlot);
        if (before >= capacity) return Fail(data.DisplayName + "は満杯です（" + capacity + "個まで）。");

        int price = Mathf.Max(0, item.priceG);
        if (!gameSessionManager.CanAfford(price))
            return Fail("所持金が足りません。必要：" + price.ToString("N0") + "G");
        if (!gameSessionManager.TrySpendMoney(price))
            return Fail("支払いができませんでした。");

        // 保存済み数量を1増やし、存在するキットにも即時反映。
        // SessionStoreはChangedを購読しているので、別シーンへ移動しても数が保持されます。
        try
        {
            if (snapshot == null) snapshot = new SavedMedicalKitData();
            snapshot.HasData = true;
            WriteSlot(snapshot, slot, before + 1);
            MedicalKitSessionStore2D.RestoreSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            gameSessionManager.AddMoney(price);
            Debug.LogException(exception, this);
            return Fail("補充に失敗したため、代金を返金しました。");
        }

        if (statusText != null) statusText.text = data.DisplayName + "を1個購入しました（" + (before + 1) + "/" + capacity + "）";
        Play(successSound);
        RefreshUI();
        return true;
    }

    public void RefreshUI()
    {
        FindReferences();
        SavedMedicalKitData snapshot = MedicalKitSessionStore2D.CreateSnapshot();
        if (moneyText != null)
            moneyText.text = "所持金：" + (gameSessionManager != null ? gameSessionManager.CurrentMoney.ToString("N0") : "--") + "G";
        if (supplies == null) return;
        int capacity = medicalKit != null ? medicalKit.MaxAmountPerSlot : Mathf.Max(1, fallbackMaxPerSlot);
        for (int i = 0; i < supplies.Length; i++)
        {
            SupplySlot entry = supplies[i];
            if (entry == null) continue;
            ConsumableItemData data = ResolveItem(entry);
            bool valid = entry.slotIndex >= 0 && entry.slotIndex <= 2 && data != null;
            int amount = valid ? ReadSlot(snapshot, entry.slotIndex) : 0;
            if (entry.itemNameText != null)
                entry.itemNameText.text = data != null ? data.DisplayName : "医療品未設定";
            if (entry.priceText != null) entry.priceText.text = Mathf.Max(0, entry.priceG).ToString("N0") + "G / 個";
            if (entry.stockText != null) entry.stockText.text = "所持 " + amount + " / " + capacity;
            if (entry.itemIcon != null)
            {
                entry.itemIcon.sprite = data != null ? data.Icon : null;
                entry.itemIcon.enabled = entry.itemIcon.sprite != null;
                entry.itemIcon.preserveAspect = true;
            }
            if (entry.buyOneButton != null)
                entry.buyOneButton.interactable = valid && (!disableBuyButtonWhenFull || amount < capacity);
        }
    }

    private ConsumableItemData ResolveItem(SupplySlot entry)
    {
        if (entry == null || entry.slotIndex < 0 || entry.slotIndex > 2) return null;
        ConsumableItemData inKit = medicalKit != null ? medicalKit.GetItemAtSlot(entry.slotIndex) : null;
        if (inKit != null)
        {
            if (entry.itemData != null && entry.itemData != inKit)
            {
                Debug.LogWarning("[MedicalKitSupplyShop] 医療キットのSlot " + entry.slotIndex + " と購入設定のItemDataが異なります。キット側を優先します。", this);
            }
            return inKit;
        }
        return entry.itemData;
    }

    private static int ReadSlot(SavedMedicalKitData data, int slot)
    {
        if (data == null) return 0;
        switch (slot)
        {
            case 0: return Mathf.Max(0, data.Slot0Amount);
            case 1: return Mathf.Max(0, data.Slot1Amount);
            case 2: return Mathf.Max(0, data.Slot2Amount);
            default: return 0;
        }
    }
    private static void WriteSlot(SavedMedicalKitData data, int slot, int amount)
    {
        switch (slot)
        {
            case 0: data.Slot0Amount = amount; break;
            case 1: data.Slot1Amount = amount; break;
            case 2: data.Slot2Amount = amount; break;
        }
    }
    private bool Fail(string reason)
    {
        if (statusText != null) statusText.text = reason;
        Play(failedSound);
        Debug.LogWarning("[MedicalKitSupplyShop] " + reason, this);
        RefreshUI();
        return false;
    }
    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(soundVolume));
    }
}
