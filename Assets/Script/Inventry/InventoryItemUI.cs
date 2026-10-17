using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryItemUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const string QuickSellDiagnosticVersion =
        "SellClickFix_v3_2026-09-01";

    [Header("見た目")]
    [SerializeField]
    private Color backgroundColor =
        new Color(0.18f, 0.30f, 0.38f, 0.92f);

    [SerializeField, Min(0f)] private float iconPadding = 5f;
    [SerializeField] private bool showStackAmount = true;

    [Header("商人取引中：売却不可Itemの見た目")]
    [Tooltip(
        "商人との取引中、現在の商人へ売却できないPlayer Inventory内のItemをグレー表示します。"
    )]
    [SerializeField] private bool grayUnsellableItemsDuringMerchantTrade = true;

    [Tooltip("売却不可Itemの背景色です。")]
    [SerializeField]
    private Color unsellableBackgroundColor =
        new Color(0.16f, 0.16f, 0.16f, 0.92f);

    [Tooltip("売却不可ItemのIcon色です。")]
    [SerializeField]
    private Color unsellableIconColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    [Tooltip("売却不可Itemの数量Text色です。")]
    [SerializeField]
    private Color unsellableAmountTextColor =
        new Color(0.62f, 0.62f, 0.62f, 1f);

    [Header("商人の商品選択中の見た目")]
    [Tooltip(
        "商人Inventoryで購入対象として選択したItemの背景色です。"
    )]
    [SerializeField]
    private Color purchaseSelectedBackgroundColor =
        new Color(0.18f, 0.55f, 0.28f, 0.96f);

    [Tooltip("購入対象として選択したItemのIcon色です。")]
    [SerializeField]
    private Color purchaseSelectedIconColor = Color.white;

    [Tooltip("購入対象として選択したItemの数量Text色です。")]
    [SerializeField]
    private Color purchaseSelectedAmountTextColor = Color.white;

    [Header("武器修理：選択中の見た目")]
    [Tooltip("武器修理画面で修理対象として選択した武器の背景色です。")]
    [SerializeField]
    private Color repairSelectedBackgroundColor =
        new Color(0.18f, 0.55f, 0.28f, 0.96f);

    [Tooltip("武器修理で選択中の武器Icon色です。")]
    [SerializeField]
    private Color repairSelectedIconColor = Color.white;

    [Tooltip("武器修理で選択中の数量Text色です。")]
    [SerializeField]
    private Color repairSelectedAmountTextColor = Color.white;

    [Header("武器修理中：武器以外の見た目")]
    [Tooltip("武器修理画面を開いている間、武器以外のPlayer Inventory Itemを灰色にします。")]
    [SerializeField] private bool grayNonWeaponItemsDuringRepair = true;

    [SerializeField]
    private Color repairUnavailableBackgroundColor =
        new Color(0.16f, 0.16f, 0.16f, 0.92f);

    [SerializeField]
    private Color repairUnavailableIconColor =
        new Color(0.42f, 0.42f, 0.42f, 1f);

    [SerializeField]
    private Color repairUnavailableAmountTextColor =
        new Color(0.62f, 0.62f, 0.62f, 1f);

    [Header("ドラッグ中の見た目")]
    [SerializeField, Range(0.1f, 1f)] private float dragAlpha = 0.75f;

    [Header("入力設定")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("クリック売却診断")]
    [Tooltip("左クリックがInventoryItemUIまで届いているか、PawnShopの売却タブ状態をConsoleへ表示します。")]
    [SerializeField] private bool showQuickSellDiagnostics = true;

    [Tooltip("InventorySoundPlayerの取得先と再生要求を詳しくログへ出します。原因特定後はOFFにできます。") ]
    [SerializeField] private bool showSoundDiagnostics = true;

    // このGrid専用のスキン設定。通常Inventoryや倉庫には適用しない。
    private bool useExpeditionMedievalTheme;
    private Sprite medievalItemSprite;
    private Sprite medievalSelectedItemSprite;
    private Color medievalItemTint = Color.white;
    private Color medievalSelectedTint = Color.white;
    private Color medievalAmountTint = Color.white;

    public void ConfigureExpeditionMedievalTheme(
        bool enabled, Sprite itemSprite, Sprite selectedSprite,
        Color itemTint, Color selectedTint, Color amountTint)
    {
        useExpeditionMedievalTheme = enabled;
        medievalItemSprite = itemSprite;
        medievalSelectedItemSprite = selectedSprite;
        medievalItemTint = itemTint;
        medievalSelectedTint = selectedTint;
        medievalAmountTint = amountTint;
    }

    private InventoryItem inventoryItem;
    private InventoryGridUI gridUI;
    private InventorySoundPlayer soundPlayer;

    [Header("インベントリ外ドロップ")]
    [Tooltip("プレイヤーInventoryのアイテムを、すべてのInventory Gridの外で離した時に地面へ捨てます。")]
    [SerializeField] private bool dropItemWhenReleasedOutsideInventory = true;

    [Tooltip("Playerに付いているPlayerItemDropper。未設定なら自動検索します。")]
    [SerializeField] private PlayerItemDropper playerItemDropper;

    private RectTransform itemRect;
    private Image backgroundImage;
    private Image iconImage;
    private Text amountText;
    private CanvasGroup canvasGroup;

    // 形状の占有マスだけ背景を描くためのセル。
    private RectTransform customShapeRoot;
    private readonly List<Image> customBackgroundCells = new List<Image>();

    private Canvas rootCanvas;
    private Transform originalParent;

    // 掴んだ場所がアイテム内の何マス目か
    private Vector2Int dragCellOffset;

    // マウス位置とアイテム左上のズレ
    private Vector2 dragPointerOffset;

    private bool isDragging;

    // ドラッグ中だけ使用する「仮の向き」
    // ドロップ成功時だけ、本当のInventoryItemへ反映する
    private bool dragIsRotated;
    // IrregularShapesV2: R increments 0 -> 1 -> 2 -> 3 -> 0.
    private int dragRotationSteps;
    [SerializeField, Range(0.06f, 0.5f)] private float rotationAnimationSeconds = 0.15f;
    private Coroutine rotationAnimation;
    private float visualIconAngle;

    private static Sprite defaultSprite;

    // Scene内の売却カートは全ItemUIで共通なのでStatic Cacheします。
    // Unity Objectが破棄された場合は == null になるため再検索できます。
    private static SellCartInventory cachedSellCart;

    // 商人変更／Shop Close時に見た目を自動更新するための前回状態。
    private MerchantStockInventory lastMerchantForSellVisual;
    private bool lastUnsellableVisualState;
    private bool hasSellVisualState;

    // 商人の商品購入選択状態の前回値。
    // 選択／解除された時だけ見た目を再適用します。
    private bool lastPurchaseSelectedVisualState;
    private bool hasPurchaseSelectedVisualState;

    // 武器修理選択状態の前回値。
    private bool lastRepairSelectedVisualState;
    private bool lastRepairModeVisualState;
    private bool hasRepairSelectedVisualState;

    public InventoryItem Item => inventoryItem;

    // v3.3: 装備スロットの事前判定で、ドラッグ中のアイテム情報を参照します。
    public static InventoryItemUI ActiveDragUI { get; private set; }
    public InventoryGridUI DragSourceGrid => isDragging ? gridUI : null;
    public int DragRotationSteps => dragRotationSteps;

    private void Update()
    {
        // 商人ShopのOpen / Closeや商人変更時に、
        // Player Inventoryのグレー表示を自動更新します。
        RefreshMerchantSellVisualIfNeeded();

        // 商人Inventoryの商品が購入選択／解除された時に、
        // 選択色を自動更新します。
        RefreshMerchantPurchaseSelectionVisualIfNeeded();

        // 武器修理画面でPlayer Inventoryの武器が
        // 選択／解除された時に選択色を更新します。
        RefreshWeaponRepairSelectionVisualIfNeeded();

        if (!isDragging)
        {
            return;
        }

        if (Input.GetKeyDown(rotateKey))
        {
            TryRotateDuringDrag();
        }
    }

    public void Setup(InventoryItem item, InventoryGridUI ownerGridUI)
    {
        inventoryItem = item;
        gridUI = ownerGridUI;

        hasSellVisualState = false;
        lastMerchantForSellVisual = null;
        lastUnsellableVisualState = false;

        hasPurchaseSelectedVisualState = false;
        lastPurchaseSelectedVisualState = false;

        hasRepairSelectedVisualState = false;
        lastRepairSelectedVisualState = false;
        lastRepairModeVisualState = false;

        FindSoundPlayer();
        EnsureVisuals();

        // ドラッグ中に更新イベントが来ても
        // 位置を元の場所へ戻さない
        if (!isDragging)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.ContainsItem(inventoryItem))
        {
            InventoryItemTooltipUI.HideFor(inventoryItem);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);

        itemRect.anchoredPosition = gridUI.GetCellPosition(
            inventoryItem.GridX,
            inventoryItem.GridY
        );

        ApplyVisuals(inventoryItem.RotationSteps);

        gameObject.name = $"ItemUI_{inventoryItem.ItemData.ItemId}";
    }

    /// <summary>
    /// カーソルがItemの上に入った時、Item情報Tooltipを表示します。
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null)
        {
            return;
        }

        Canvas canvas =
            GetComponentInParent<Canvas>()?.rootCanvas;

        if (canvas == null)
        {
            return;
        }

        MerchantStockInventory merchantStock =
            FindMerchantStockForTooltip();

        InventoryItemTooltipUI.Show(
            inventoryItem,
            canvas,
            merchantStock
        );
    }

    /// <summary>
    /// このItemUIが商人の商品Inventoryに属している場合、
    /// 対応するMerchantStockInventoryを返します。
    ///
    /// Player Inventoryや通常ItemBoxではnullを返すため、
    /// Tooltip側に購入金額は表示されません。
    /// </summary>
    private MerchantStockInventory FindMerchantStockForTooltip()
    {
        if (gridUI == null ||
            gridUI.IsPlayerInventory ||
            gridUI.ItemBox == null)
        {
            return null;
        }

        MerchantStockInventory merchantStock =
            gridUI.ItemBox.GetComponent<MerchantStockInventory>();

        if (merchantStock == null)
        {
            merchantStock =
                gridUI.ItemBox.GetComponentInParent<MerchantStockInventory>();
        }

        return merchantStock;
    }

    /// <summary>
    /// カーソルがItemから外れた時、Tooltipを閉じます。
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryItemTooltipUI.HideFor(inventoryItem);
    }

    private void OnDisable()
    {
        if (ActiveDragUI == this) ActiveDragUI = null;
        InventoryItemTooltipUI.HideFor(inventoryItem);
    }

    /// <summary>
    /// 診断用。OnPointerClickまで届かない場合でも、
    /// PointerDownが届いているかをConsoleで確認できます。
    /// </summary>
    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (!showQuickSellDiagnostics ||
            eventData == null ||
            eventData.button !=
                PointerEventData.InputButton.Left)
        {
            return;
        }

        PawnShopUIController pawnShop =
            FindPawnShopForQuickSell();

        QuickSellDiagnostic(
            $"PointerDown / Version={QuickSellDiagnosticVersion} / " +
            $"Item={(inventoryItem != null && inventoryItem.ItemData != null ? inventoryItem.ItemData.DisplayName : "null")} / " +
            $"Grid={(gridUI != null ? GetTransformPath(gridUI.transform) : "null")} / " +
            $"GridIsPlayer={(gridUI != null && gridUI.IsPlayerInventory)} / " +
            $"PawnShop={(pawnShop != null ? GetTransformPath(pawnShop.transform) : "null")} / " +
            $"ShopOpen={(pawnShop != null && pawnShop.IsOpen)} / " +
            $"SellPanelVisible={(pawnShop != null && pawnShop.IsSellPanelActuallyVisible)} / " +
            $"SellCartGridVisible={(pawnShop != null && pawnShop.IsSellCartGridActuallyVisible)} / " +
            $"SellTab={(pawnShop != null && pawnShop.IsSellTabActive)}"
        );
    }

    // クリック処理
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null)
        {
            QuickSellDiagnostic(
                $"OnPointerClick早期終了 / " +
                $"Dragging={isDragging} / ItemNull={inventoryItem == null} / " +
                $"ItemDataNull={(inventoryItem != null && inventoryItem.ItemData == null)} / GridNull={gridUI == null}"
            );
            return;
        }

        if (eventData.button ==
            PointerEventData.InputButton.Left)
        {
            PawnShopUIController diagnosticPawnShop =
                FindPawnShopForQuickSell();

            QuickSellDiagnostic(
                $"OnPointerClick LEFT / Version={QuickSellDiagnosticVersion} / " +
                $"Item={inventoryItem.ItemData.DisplayName} / " +
                $"Grid={GetTransformPath(gridUI.transform)} / " +
                $"GridIsPlayer={gridUI.IsPlayerInventory} / " +
                $"PawnShop={(diagnosticPawnShop != null ? GetTransformPath(diagnosticPawnShop.transform) : "null")} / " +
                $"ShopOpen={(diagnosticPawnShop != null && diagnosticPawnShop.IsOpen)} / " +
                $"SellPanelVisible={(diagnosticPawnShop != null && diagnosticPawnShop.IsSellPanelActuallyVisible)} / " +
                $"SellCartGridVisible={(diagnosticPawnShop != null && diagnosticPawnShop.IsSellCartGridActuallyVisible)} / " +
                $"SellTab={(diagnosticPawnShop != null && diagnosticPawnShop.IsSellTabActive)}"
            );
        }

        // 武器修理画面ではPlayer Inventoryの武器を
        // 左クリックした瞬間に修理Controllerへ直接通知する。
        if (eventData.button ==
                PointerEventData.InputButton.Left &&
            gridUI.IsPlayerInventory &&
            inventoryItem.ItemData is WeaponItemData)
        {
            MerchantWeaponRepairController
                repairController =
                    MerchantWeaponRepairController
                        .ActiveInstance;

            if (repairController != null &&
                repairController.IsOpen &&
                repairController
                    .TryToggleRepairSelectionFromItemUI(
                        inventoryItem
                    ))
            {
                // 修理選択として処理したので、
                // 通常の右クリックMenu処理へは進まない。
                return;
            }
        }

        // 通常取引の「売却」タブ中は、
        // Player InventoryのItemを左クリックすると
        // SellCartの空き位置へ自動移動します。
        if (eventData.button ==
                PointerEventData.InputButton.Left &&
            gridUI.IsPlayerInventory)
        {
            PawnShopUIController pawnShop =
                PawnShopUIController.ActiveInstance;

            QuickSellDiagnostic(
                $"売却分岐判定 / PawnShopNull={pawnShop == null} / " +
                $"IsSellTabActive={(pawnShop != null && pawnShop.IsSellTabActive)} / " +
                $"SellPanelVisible={(pawnShop != null && pawnShop.IsSellPanelActuallyVisible)} / " +
                $"SellCartGridVisible={(pawnShop != null && pawnShop.IsSellCartGridActuallyVisible)} / " +
                $"PawnShopPath={(pawnShop != null ? GetTransformPath(pawnShop.transform) : "null")}"
            );

            if (pawnShop != null &&
                pawnShop.IsSellTabActive)
            {
                InventoryItemTooltipUI.HideFor(
                    inventoryItem
                );

                QuickSellDiagnostic(
                    $"PawnShop.TryQuickAddPlayerItemToSellCart呼び出し / " +
                    $"Item={inventoryItem.ItemData.DisplayName}"
                );

                bool moved =
                    pawnShop
                        .TryQuickAddPlayerItemToSellCart(
                            inventoryItem
                        );

                QuickSellDiagnostic(
                    $"TryQuickAdd結果={moved} / Item={inventoryItem.ItemData.DisplayName}"
                );

                if (moved)
                {
                    PlayInventorySound(
                        "Place",
                        player => player.PlayPlace()
                    );

                    Log(
                        $"左クリックで売却カートへ追加：" +
                        $"{inventoryItem.ItemData.DisplayName}"
                    );
                }
                else
                {
                    PlayInventorySound(
                        "Failed",
                        player => player.PlayFailed()
                    );
                }

                // 売却タブ中の左クリックは、
                // 成功・失敗に関係なく通常処理へ流さない。
                return;
            }
        }

        // ここから下は従来の右クリックContextMenu。
        if (eventData.button !=
            PointerEventData.InputButton.Right)
        {
            return;
        }

        // 右クリックメニューとTooltipが重ならないように一度閉じる。
        InventoryItemTooltipUI.HideFor(inventoryItem);

        InventoryContextMenuUI contextMenuUI =
            gridUI.ContextMenuUI;

        if (contextMenuUI == null)
        {
            Debug.LogWarning(
                "InventoryItemUI: ContextMenuUI が設定されていません。",
                this
            );

            PlayInventorySound("Failed", player => player.PlayFailed());
            return;
        }

        // 応急ItemBoxだけは『使用する・情報・戻る』専用メニュー。
        // Inspectorで明示的に有効化した箱にのみ適用する。
        bool openedEmergencyMenu = false;
        if (gridUI.IsItemBoxInventory && gridUI.ItemBox != null)
        {
            ItemBoxUIController boxController =
                gridUI.GetComponentInParent<ItemBoxUIController>();

            if (boxController == null ||
                !boxController.UsesEmergencyItemMenuFor(gridUI.ItemBox))
            {
                // ControllerがCanvasの別階層にあるシーンにも対応。
                ItemBoxUIController[] candidates =
                    Object.FindObjectsByType<ItemBoxUIController>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                foreach (ItemBoxUIController candidate in candidates)
                {
                    if (candidate != null &&
                        candidate.UsesEmergencyItemMenuFor(gridUI.ItemBox))
                    {
                        boxController = candidate;
                        break;
                    }
                }
            }

            if (boxController != null &&
                boxController.UsesEmergencyItemMenuFor(gridUI.ItemBox))
            {
                contextMenuUI.ShowEmergencyBoxItem(
                    inventoryItem,
                    gridUI.ItemBox,
                    boxController.GetPlayerInventoryForEmergencyMenu(),
                    eventData.position);
                openedEmergencyMenu = true;
            }
        }

        // 通常のInventoryと箱／ショップの右クリック挙動は維持。
        if (!openedEmergencyMenu)
        {
            if (gridUI.IsPlayerInventory && gridUI.Controller != null)
            {
                contextMenuUI.Show(
                    inventoryItem,
                    gridUI.Controller,
                    eventData.position);
            }
            else
            {
                contextMenuUI.ShowReadOnlyItem(
                    inventoryItem,
                    eventData.position);
            }
        }

        Log(
            $"右クリックメニューを開きました：" +
            $"{inventoryItem.ItemData.DisplayName}"
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.ContainsItem(inventoryItem))
        {
            return;
        }

        // 武器修理画面ではInventory内のItemを移動させず、
        // 左クリック選択専用にする。
        MerchantWeaponRepairController
            repairController =
                MerchantWeaponRepairController
                    .ActiveInstance;

        if (gridUI.IsPlayerInventory &&
            repairController != null &&
            repairController.IsOpen)
        {
            return;
        }

        // ドラッグ開始時はTooltipを消す。
        InventoryItemTooltipUI.HideFor(inventoryItem);

        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (rootCanvas == null)
        {
            Debug.LogWarning(
                "InventoryItemUI: 親階層にCanvasが見つかりません。",
                this
            );
            return;
        }

        isDragging = true;
        ActiveDragUI = this;
        originalParent = transform.parent;

        // ドラッグ開始時の向きを保存
        dragIsRotated = inventoryItem.IsRotated;
        dragRotationSteps = inventoryItem.RotationSteps;
        if (rotationAnimation != null) { StopCoroutine(rotationAnimation); rotationAnimation = null; }
        visualIconAngle = -90f * dragRotationSteps;

        // 掴んだマスを保存
        if (gridUI.TryGetGridPosition(
                eventData.position,
                eventData.pressEventCamera,
                out Vector2Int clickedGridPosition))
        {
            dragCellOffset =
                clickedGridPosition - inventoryItem.GridPosition;

            Vector2Int itemSize = GetSize(dragRotationSteps);

            dragCellOffset.x = Mathf.Clamp(
                dragCellOffset.x,
                0,
                itemSize.x - 1
            );

            dragCellOffset.y = Mathf.Clamp(
                dragCellOffset.y,
                0,
                itemSize.y - 1
            );
        }
        else
        {
            dragCellOffset = Vector2Int.zero;
        }

        // マウスで掴んだ位置を保存
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                itemRect,
                eventData.position,
                eventData.pressEventCamera,
                out dragPointerOffset))
        {
            dragPointerOffset = Vector2.zero;
        }

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;

        // ドラッグ中はCanvas直下へ出して最前面にする
        transform.SetParent(rootCanvas.transform, false);

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0f, 1f);

        ApplyVisuals(dragRotationSteps);

        transform.SetAsLastSibling();

        UpdateDragPosition(eventData.position);

        PlayInventorySound("PickUp", player => player.PlayPickUp());

        Log(
            $"ドラッグ開始：{inventoryItem.ItemData.DisplayName} / " +
            $"向き={dragRotationSteps * 90}度 / " +
            $"サイズ={GetSize(dragRotationSteps).x}×" +
            $"{GetSize(dragRotationSteps).y}"
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        // 先に装備スロットへのドロップを判定する
        if (TryDropToEquipmentSlot(
                eventData,
                out bool wasOverEquipmentSlot))
        {
            PlayInventorySound("Place", player => player.PlayPlace());

            Log(
                $"装備成功：{inventoryItem.ItemData.DisplayName}"
            );

            FinishDrag();
            return;
        }

        // 装備スロットの上にはいたが、
        // 種類違い・枠が埋まっているなどで失敗した場合
        if (wasOverEquipmentSlot)
        {
            PlayInventorySound("Failed", player => player.PlayFailed());
            FinishDrag();
            return;
        }

        bool moved = false;

        if (TryFindTargetGrid(
                eventData,
                out InventoryGridUI targetGridUI,
                out Vector2Int pointerGridPosition))
        {
            // 通常移動より先にスタック結合を試す。
            // ・Player Inventory内 → 従来どおり同一Grid結合
            // ・ItemBox(Storage) → Player Inventory → クロスGrid結合
            // 例：30 + 20 = 50。80 + 20なら90 + 10。
            int transferredAmount = 0;
            bool merged = false;

            if (targetGridUI == gridUI)
            {
                merged = gridUI.TryMergeItemAt(
                    inventoryItem,
                    pointerGridPosition.x,
                    pointerGridPosition.y,
                    out transferredAmount
                );
            }
            else
            {
                merged = targetGridUI.TryMergeItemFrom(
                    gridUI,
                    inventoryItem,
                    pointerGridPosition.x,
                    pointerGridPosition.y,
                    out transferredAmount
                );
            }

            if (merged)
            {
                PlayInventorySound(
                    "Place",
                    player => player.PlayPlace()
                );

                Log(
                    $"スタック結合成功：" +
                    $"{inventoryItem.ItemData.DisplayName} / " +
                    $"移動数={transferredAmount} / " +
                    $"別Grid={(targetGridUI != gridUI)}"
                );

                FinishDrag();
                return;
            }

            Vector2Int targetPosition =
                pointerGridPosition - dragCellOffset;

            if (targetGridUI == gridUI)
            {
                moved = gridUI.TryMoveItem(
                    inventoryItem,
                    targetPosition.x,
                    targetPosition.y,
                    dragRotationSteps
                );
            }
            else
            {
                moved = gridUI.TryTransferItemTo(
                    inventoryItem,
                    targetGridUI,
                    targetPosition.x,
                    targetPosition.y,
                    dragRotationSteps
                );
            }

            if (moved)
            {
                PlayInventorySound("Place", player => player.PlayPlace());

                Log(
                    $"ドロップ成功：{inventoryItem.ItemData.DisplayName} / " +
                    $"位置={targetPosition.x},{targetPosition.y}"
                );
            }
            else
            {
                PlayInventorySound("Failed", player => player.PlayFailed());

                Log(
                    $"ドロップ失敗：{inventoryItem.ItemData.DisplayName}"
                );
            }
        }
        else
        {
            // Gridのセル間の隙間など、見た目上まだInventory Grid内にいる場合は
            // 誤って地面へ捨てず、従来どおり元の位置へ戻す。
            if (IsPointerInsideAnyInventoryGrid(eventData))
            {
                PlayInventorySound("Failed", player => player.PlayFailed());
                Log("ドロップ失敗：Inventory Grid内ですが配置できない場所です。");
            }
            else if (TryDropItemOutsideInventory())
            {
                PlayInventorySound("Trash", player => player.PlayTrash());
                Log("Inventoryの外へドロップしたため、アイテムを地面へ捨てました。");
                FinishDrag();
                return;
            }
            else
            {
                PlayInventorySound("Failed", player => player.PlayFailed());
                Log("Inventory外へのドロップに失敗したため、元の位置へ戻します。");
            }
        }

        FinishDrag();
    }

    private bool TryDropToEquipmentSlot(
        PointerEventData eventData,
        out bool wasOverEquipmentSlot)
    {
        wasOverEquipmentSlot = false;

        if (gridUI == null ||
            EventSystem.current == null ||
            eventData == null)
        {
            Log(
                "装備スロット判定を開始できません：" +
                $"GridUI={(gridUI != null ? "あり" : "なし")} / " +
                $"EventSystem={(EventSystem.current != null ? "あり" : "なし")}"
            );
            return false;
        }

        // 先に「本当に装備スロットの上か」を判定します。
        // ItemBoxPanelや透明Imageが前面にある場合、RaycastAllだけでは
        // EquipmentSlotUIを拾えないことがあるため、RectTransformの座標判定も併用します。
        if (!TryFindEquipmentSlotUnderPointer(
                eventData,
                out EquipmentSlotUI equipmentSlotUI))
        {
            Log(
                "装備スロットを検出できませんでした。" +
                $" Pointer={eventData.position}"
            );
            return false;
        }

        wasOverEquipmentSlot = true;

        // 通常のPlayer Inventoryからは従来どおり装備可能。
        bool isPlayerInventorySource =
            gridUI.IsPlayerInventory;

        // ItemBoxからは「装備スロットへ直接装備」を許可したStorageだけ対応。
        ItemBoxInventory sourceItemBox =
            gridUI.IsItemBoxInventory
                ? gridUI.ItemBox
                : null;

        bool isAllowedItemBoxSource =
            sourceItemBox != null &&
            sourceItemBox.AllowsDirectEquipToEquipmentSlot;

        if (!isPlayerInventorySource &&
            !isAllowedItemBoxSource)
        {
            Log(
                "装備スロット上ですが、移動元Inventoryからの直接装備が許可されていません。" +
                $" PlayerInventory={isPlayerInventorySource} / " +
                $" ItemBox={(sourceItemBox != null ? sourceItemBox.name : "なし")} / " +
                $" DirectTransfer={(sourceItemBox != null && sourceItemBox.AllowsDirectItemTransfer)} / " +
                $" DirectEquip={(sourceItemBox != null && sourceItemBox.AllowsDirectEquipToEquipmentSlot)}"
            );
            return false;
        }

        bool equipped;

        if (isPlayerInventorySource)
        {
            equipped =
                equipmentSlotUI.TryEquipDroppedItem(
                    inventoryItem,
                    dragRotationSteps,
                    out EquipmentResult result
                );

            if (!equipped)
            {
                Log(
                    $"装備スロットへのドロップ失敗：" +
                    $"{inventoryItem.ItemData.DisplayName} / {result}"
                );
            }

            return equipped;
        }

        equipped =
            equipmentSlotUI.TryEquipDroppedItemFromItemBox(
                inventoryItem,
                dragIsRotated,
                sourceItemBox,
                out EquipmentResult itemBoxResult
            );

        if (equipped)
        {
            Log(
                $"保管箱から直接装備成功：" +
                $"{inventoryItem.ItemData.DisplayName}"
            );
        }
        else
        {
            Log(
                $"保管箱から装備スロットへのドロップ失敗：" +
                $"{inventoryItem.ItemData.DisplayName} / {itemBoxResult}"
            );
        }

        return equipped;
    }

    /// <summary>
    /// ポインター下のEquipmentSlotUIを探します。
    /// 通常のUI Raycastで見つからない場合は、全ての有効なEquipmentSlotUIの
    /// RectTransformに対して座標判定を行います。
    /// </summary>
    private bool TryFindEquipmentSlotUnderPointer(
        PointerEventData eventData,
        out EquipmentSlotUI equipmentSlotUI)
    {
        equipmentSlotUI = null;

        if (eventData == null)
        {
            return false;
        }

        // 1. 通常のGraphic Raycast。
        if (EventSystem.current != null)
        {
            List<RaycastResult> results =
                new List<RaycastResult>();

            EventSystem.current.RaycastAll(
                eventData,
                results
            );

            foreach (RaycastResult raycastResult in results)
            {
                if (raycastResult.gameObject == null)
                {
                    continue;
                }

                EquipmentSlotUI found =
                    raycastResult.gameObject
                        .GetComponentInParent<EquipmentSlotUI>();

                if (found == null ||
                    !found.isActiveAndEnabled ||
                    !found.gameObject.activeInHierarchy)
                {
                    continue;
                }

                equipmentSlotUI = found;
                Log(
                    $"装備スロット検出：UI Raycast / " +
                    $"Object={found.gameObject.name} / Slot={found.SlotType}"
                );
                return true;
            }
        }

        // 2. ItemBoxPanelなど別UIがRaycastを遮っていても拾えるよう、
        // RectTransformの画面座標でフォールバック判定する。
        EquipmentSlotUI[] allSlots =
            Object.FindObjectsByType<EquipmentSlotUI>(
                FindObjectsInactive.Exclude
            );

        foreach (EquipmentSlotUI candidate in allSlots)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform rect =
                candidate.transform as RectTransform;

            if (rect == null)
            {
                continue;
            }

            Canvas slotCanvas =
                candidate.GetComponentInParent<Canvas>();

            Camera slotCamera = null;

            if (slotCanvas != null &&
                slotCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                slotCamera = slotCanvas.worldCamera != null
                    ? slotCanvas.worldCamera
                    : eventData.pressEventCamera;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    eventData.position,
                    slotCamera))
            {
                continue;
            }

            equipmentSlotUI = candidate;
            Log(
                $"装備スロット検出：RectTransformフォールバック / " +
                $"Object={candidate.gameObject.name} / Slot={candidate.SlotType}"
            );
            return true;
        }

        return false;
    }

    /// <summary>
    /// プレイヤーInventoryからドラッグしたアイテムを、
    /// すべてのInventory Gridの外で離した時に地面へ捨てます。
    /// ItemData.CanDiscard=false のアイテムや、箱・ショップ側のアイテムは捨てません。
    /// </summary>
    private bool TryDropItemOutsideInventory()
    {
        if (!dropItemWhenReleasedOutsideInventory ||
            gridUI == null ||
            !gridUI.IsPlayerInventory ||
            inventoryItem == null ||
            inventoryItem.ItemData == null)
        {
            return false;
        }

        if (!FindPlayerItemDropper())
        {
            Log("PlayerItemDropperが見つからないため、Inventory外へ捨てられません。");
            return false;
        }

        return playerItemDropper.TryDropItem(inventoryItem);
    }

    /// <summary>
    /// ポインタがどれかのInventory GridのRect内にあるかを確認します。
    /// セル間のSpacing上で離した時に、誤って地面へ捨てるのを防ぐために使います。
    /// </summary>
    private bool IsPointerInsideAnyInventoryGrid(
        PointerEventData eventData)
    {
        InventoryGridUI[] allGridUIs =
            Object.FindObjectsByType<InventoryGridUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryGridUI candidate in allGridUIs)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.GridRect == null)
            {
                continue;
            }

            Canvas canvas =
                candidate.GetComponentInParent<Canvas>()?.rootCanvas;

            Camera uiCamera =
                canvas == null ||
                canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    candidate.GridRect,
                    eventData.position,
                    uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    private bool FindPlayerItemDropper()
    {
        if (playerItemDropper != null)
        {
            return true;
        }

        // プレイヤーInventoryControllerと同じObjectにある構成を優先。
        InventoryController controller =
            gridUI != null ? gridUI.Controller : null;

        if (controller != null)
        {
            playerItemDropper =
                controller.GetComponent<PlayerItemDropper>();

            if (playerItemDropper == null)
            {
                playerItemDropper =
                    controller.GetComponentInParent<PlayerItemDropper>();
            }
        }

        if (playerItemDropper == null)
        {
            playerItemDropper =
                Object.FindAnyObjectByType<PlayerItemDropper>(
                    FindObjectsInactive.Include
                );
        }

        return playerItemDropper != null;
    }

    private bool TryFindTargetGrid(
        PointerEventData eventData,
        out InventoryGridUI targetGridUI,
        out Vector2Int pointerGridPosition)
    {
        targetGridUI = null;
        pointerGridPosition = Vector2Int.zero;

        InventoryGridUI[] allGridUIs =
            Object.FindObjectsByType<InventoryGridUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryGridUI candidate in allGridUIs)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.TryGetGridPosition(
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2Int gridPosition))
            {
                continue;
            }

            targetGridUI = candidate;
            pointerGridPosition = gridPosition;
            return true;
        }

        return false;
    }

    private void TryRotateDuringDrag()
    {
        if (inventoryItem == null || inventoryItem.ItemData == null) return;
        if (!inventoryItem.CanRotate) { soundPlayer?.PlayFailed(); return; }
        Vector2Int oldSize = GetSize(dragRotationSteps);
        int nextSteps = (dragRotationSteps + 1) & 3;
        // R rotates clockwise: pointer keeps holding the same occupied cell.
        dragCellOffset = new Vector2Int(oldSize.y - 1 - dragCellOffset.y, dragCellOffset.x);
        dragRotationSteps = nextSteps;
        dragIsRotated = (nextSteps & 1) != 0;
        Vector2Int size = GetSize(nextSteps);
        dragCellOffset.x = Mathf.Clamp(dragCellOffset.x, 0, size.x - 1);
        dragCellOffset.y = Mathf.Clamp(dragCellOffset.y, 0, size.y - 1);
        float fromAngle = visualIconAngle;
        ApplyVisuals(nextSteps); // cells snap immediately so placement guidance is accurate.
        if (rotationAnimation != null) StopCoroutine(rotationAnimation);
        rotationAnimation = StartCoroutine(AnimateIconRotation(fromAngle, fromAngle - 90f, nextSteps));
        UpdateDragPosition(Input.mousePosition);
        soundPlayer?.PlayRotate();
    }

    private IEnumerator AnimateIconRotation(float from, float to, int finalSteps)
    {
        float duration = Mathf.Max(0.01f, rotationAnimationSeconds);
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float eased = t * t * (3f - 2f * t);
            visualIconAngle = Mathf.LerpUnclamped(from, to, eased);
            if (iconImage != null)
            {
                iconImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, visualIconAngle);
                float pop = 1f + 0.09f * Mathf.Sin(t * Mathf.PI);
                iconImage.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            yield return null;
        }
        visualIconAngle = -90f * finalSteps;
        if (iconImage != null)
        {
            iconImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, visualIconAngle);
            iconImage.rectTransform.localScale = Vector3.one;
        }
        rotationAnimation = null;
    }

    private void UpdateDragPosition(Vector2 screenPosition)
    {
        if (rootCanvas == null)
        {
            return;
        }

        Camera canvasCamera = rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                screenPosition,
                canvasCamera,
                out Vector2 canvasLocalPosition))
        {
            return;
        }

        itemRect.anchoredPosition =
            canvasLocalPosition - dragPointerOffset;
    }

    private void FinishDrag()
    {
        if (ActiveDragUI == this) ActiveDragUI = null;
        isDragging = false;
        if (rotationAnimation != null) { StopCoroutine(rotationAnimation); rotationAnimation = null; }
        if (iconImage != null) iconImage.rectTransform.localScale = Vector3.one;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
        }
        else if (gridUI != null)
        {
            transform.SetParent(gridUI.ItemRoot, false);
        }

        transform.SetAsLastSibling();

        Refresh();
    }

    private Vector2Int GetSize(bool isRotated)
    {
        if (inventoryItem == null || inventoryItem.ItemData == null)
        {
            return Vector2Int.one;
        }

        return inventoryItem.ItemData.GetSize(isRotated);
    }

    /// <summary>
    /// 商人取引中のPlayer Inventory表示状態が変わった時だけ
    /// Itemの見た目を再適用します。
    /// </summary>
    private void RefreshMerchantSellVisualIfNeeded()
    {
        if (isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.IsPlayerInventory)
        {
            return;
        }

        MerchantStockInventory merchant =
            GetActiveMerchantForSellVisual();

        bool isUnsellable =
            merchant != null &&
            IsUnsellableDuringMerchantTrade();

        if (hasSellVisualState &&
            lastMerchantForSellVisual == merchant &&
            lastUnsellableVisualState == isUnsellable)
        {
            return;
        }

        lastMerchantForSellVisual = merchant;
        lastUnsellableVisualState = isUnsellable;
        hasSellVisualState = true;

        ApplyVisuals(inventoryItem.RotationSteps);
    }

    /// <summary>
    /// 現在PawnShopへ設定されている商人を返します。
    /// Shopを閉じるとSellCartInventory.ClearMerchantStock()でnullになるため、
    /// 通常Inventoryではグレー表示されません。
    /// </summary>
    private MerchantStockInventory GetActiveMerchantForSellVisual()
    {
        if (!grayUnsellableItemsDuringMerchantTrade ||
            gridUI == null ||
            !gridUI.IsPlayerInventory)
        {
            return null;
        }

        SellCartInventory sellCart =
            FindSellCartForVisual();

        return sellCart != null
            ? sellCart.CurrentMerchantStock
            : null;
    }

    /// <summary>
    /// 既存のSellCartInventory.CanAcceptItem()をそのまま使い、
    /// 共通売却条件 + 現在商人の買取条件を両方判定します。
    /// </summary>
    private bool IsUnsellableDuringMerchantTrade()
    {
        if (!grayUnsellableItemsDuringMerchantTrade ||
            gridUI == null ||
            !gridUI.IsPlayerInventory ||
            inventoryItem == null ||
            inventoryItem.ItemData == null)
        {
            return false;
        }

        SellCartInventory sellCart =
            FindSellCartForVisual();

        if (sellCart == null ||
            sellCart.CurrentMerchantStock == null)
        {
            return false;
        }

        return !sellCart.CanAcceptItem(
            inventoryItem,
            out _
        );
    }

    private static SellCartInventory FindSellCartForVisual()
    {
        if (cachedSellCart != null)
        {
            return cachedSellCart;
        }

        cachedSellCart =
            Object.FindAnyObjectByType<SellCartInventory>(
                FindObjectsInactive.Include
            );

        return cachedSellCart;
    }

    /// <summary>
    /// 商人Inventoryの商品選択状態が変わった時だけ見た目を更新します。
    /// </summary>
    private void RefreshMerchantPurchaseSelectionVisualIfNeeded()
    {
        if (isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            gridUI.IsPlayerInventory)
        {
            return;
        }

        bool isSelected =
            IsSelectedForMerchantPurchase();

        if (hasPurchaseSelectedVisualState &&
            lastPurchaseSelectedVisualState == isSelected)
        {
            return;
        }

        lastPurchaseSelectedVisualState = isSelected;
        hasPurchaseSelectedVisualState = true;

        ApplyVisuals(inventoryItem.RotationSteps);
    }

    /// <summary>
    /// このItemが、現在開いている商人Shopで購入対象に選択されているか返します。
    /// </summary>
    private bool IsSelectedForMerchantPurchase()
    {
        if (gridUI == null ||
            gridUI.IsPlayerInventory ||
            inventoryItem == null ||
            inventoryItem.ItemData == null)
        {
            return false;
        }

        MerchantPurchaseController purchaseController =
            MerchantPurchaseController.ActiveInstance;

        if (purchaseController == null ||
            !purchaseController.IsOpen ||
            purchaseController.CurrentStock == null ||
            !purchaseController.IsItemSelectedForPurchase(inventoryItem))
        {
            return false;
        }

        MerchantStockInventory merchantStock =
            FindMerchantStockForTooltip();

        return merchantStock != null &&
               merchantStock == purchaseController.CurrentStock;
    }

    /// <summary>
    /// 武器修理画面のOpen/Close、または武器の選択状態が変わった時に
    /// Player Inventory Itemの見た目を更新します。
    /// 武器以外を灰色へするため、全Itemで修理モード状態を監視します。
    /// </summary>
    private void RefreshWeaponRepairSelectionVisualIfNeeded()
    {
        if (isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.IsPlayerInventory)
        {
            return;
        }

        bool repairModeActive =
            IsWeaponRepairModeActive();

        bool isSelected =
            IsSelectedForWeaponRepair();

        if (hasRepairSelectedVisualState &&
            lastRepairModeVisualState == repairModeActive &&
            lastRepairSelectedVisualState == isSelected)
        {
            return;
        }

        lastRepairModeVisualState =
            repairModeActive;

        lastRepairSelectedVisualState =
            isSelected;

        hasRepairSelectedVisualState =
            true;

        ApplyVisuals(
            inventoryItem.RotationSteps
        );
    }

    /// <summary>
    /// 現在開いている武器修理画面で、この武器が修理対象として
    /// 選択されているかを返します。
    /// </summary>
    private bool IsSelectedForWeaponRepair()
    {
        if (gridUI == null ||
            !gridUI.IsPlayerInventory ||
            inventoryItem == null ||
            !(inventoryItem.ItemData is WeaponItemData))
        {
            return false;
        }

        MerchantWeaponRepairController
            repairController =
                MerchantWeaponRepairController
                    .ActiveInstance;

        return
            repairController != null &&
            repairController.IsOpen &&
            repairController
                .IsItemSelectedForRepair(
                    inventoryItem
                );
    }

    private bool IsWeaponRepairModeActive()
    {
        if (gridUI == null ||
            !gridUI.IsPlayerInventory)
        {
            return false;
        }

        MerchantWeaponRepairController
            repairController =
                MerchantWeaponRepairController
                    .ActiveInstance;

        return
            repairController != null &&
            repairController.IsOpen;
    }

    private bool IsUnavailableForWeaponRepair()
    {
        return
            grayNonWeaponItemsDuringRepair &&
            IsWeaponRepairModeActive() &&
            inventoryItem != null &&
            !(inventoryItem.ItemData is WeaponItemData);
    }

    private Vector2Int GetSize(int rotationSteps) =>
        inventoryItem == null || inventoryItem.ItemData == null
            ? Vector2Int.one : inventoryItem.ItemData.GetSize(rotationSteps);

    private void ApplyVisuals(int rotationSteps)
    {
        if (inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null)
        {
            return;
        }

        Vector2Int itemSize = GetSize(rotationSteps);

        itemRect.sizeDelta = gridUI.GetItemPixelSize(
            itemSize.x,
            itemSize.y
        );

        bool isUnsellable =
            IsUnsellableDuringMerchantTrade();

        bool isPurchaseSelected =
            IsSelectedForMerchantPurchase();

        bool isRepairSelected =
            IsSelectedForWeaponRepair();

        bool isRepairUnavailable =
            IsUnavailableForWeaponRepair();

        // 選択時は真鍮の縁取り、それ以外は古い革張りのアイテム背景。
        if (useExpeditionMedievalTheme && backgroundImage != null)
        {
            bool selected = isRepairSelected || isPurchaseSelected;
            Sprite sprite = selected && medievalSelectedItemSprite != null
                ? medievalSelectedItemSprite : medievalItemSprite;
            backgroundImage.sprite = sprite != null ? sprite : GetDefaultSprite();
            backgroundImage.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }

        // 修理選択色を最優先。
        // 修理中の武器以外は次に灰色を優先します。
        if (isRepairSelected)
        {
            backgroundImage.color = useExpeditionMedievalTheme
                ? medievalSelectedTint : repairSelectedBackgroundColor;
        }
        else if (isRepairUnavailable)
        {
            backgroundImage.color =
                repairUnavailableBackgroundColor;
        }
        else if (isPurchaseSelected)
        {
            backgroundImage.color = useExpeditionMedievalTheme
                ? medievalSelectedTint : purchaseSelectedBackgroundColor;
        }
        else if (isUnsellable)
        {
            backgroundImage.color =
                unsellableBackgroundColor;
        }
        else
        {
            if (!(useExpeditionMedievalTheme && inventoryItem.ItemData.HasCustomShape))
                UpdateCustomShapeBackground(rotationSteps);
        }

        // 不規則形状は選択中も占有セルだけ描画・クリック可能にする。
        if (useExpeditionMedievalTheme && inventoryItem.ItemData.HasCustomShape)
        {
            UpdateCustomShapeBackground(rotationSteps);
            Color currentTint = isRepairSelected || isPurchaseSelected
                ? medievalSelectedTint
                : isRepairUnavailable ? repairUnavailableBackgroundColor
                : isUnsellable ? unsellableBackgroundColor
                : medievalItemTint;
            Sprite currentSprite = isRepairSelected || isPurchaseSelected
                ? (medievalSelectedItemSprite != null ? medievalSelectedItemSprite : medievalItemSprite)
                : medievalItemSprite;
            foreach (Image cell in customBackgroundCells)
            {
                if (cell == null || !cell.gameObject.activeSelf) continue;
                cell.color = currentTint;
                if (currentSprite != null)
                {
                    cell.sprite = currentSprite;
                    cell.type = Image.Type.Sliced;
                }
            }
        }

        Sprite icon = inventoryItem.ItemData.Icon;

        iconImage.sprite = icon != null
            ? icon
            : GetDefaultSprite();

        if (isRepairSelected)
        {
            iconImage.color =
                repairSelectedIconColor;
        }
        else if (isRepairUnavailable)
        {
            iconImage.color =
                repairUnavailableIconColor;
        }
        else if (isPurchaseSelected)
        {
            iconImage.color =
                purchaseSelectedIconColor;
        }
        else if (isUnsellable)
        {
            iconImage.color = unsellableIconColor;
        }
        else
        {
            iconImage.color = icon != null
                ? Color.white
                : new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        iconImage.preserveAspect = true;

        iconImage.rectTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            -90f * rotationSteps
        );

        bool shouldShowAmount =
            showStackAmount &&
            inventoryItem.CanStack;

        amountText.gameObject.SetActive(shouldShowAmount);

        if (shouldShowAmount)
        {
            amountText.text = inventoryItem.Amount.ToString();

            if (isRepairSelected)
            {
                amountText.color =
                    repairSelectedAmountTextColor;
            }
            else if (isRepairUnavailable)
            {
                amountText.color =
                    repairUnavailableAmountTextColor;
            }
            else if (isPurchaseSelected)
            {
                amountText.color =
                    purchaseSelectedAmountTextColor;
            }
            else
            {
                amountText.color =
                    isUnsellable
                        ? unsellableAmountTextColor
                        : (useExpeditionMedievalTheme ? medievalAmountTint : Color.white);
            }
        }
    }

    /// <summary>
    /// アイテムの背景だけをセル単位の形状に切り替える。
    /// クリック判定も占有セルだけにするため、くぼみは空いている。
    /// </summary>
    private void UpdateCustomShapeBackground(int rotationSteps)
    {
        bool custom = inventoryItem.ItemData.HasCustomShape;
        backgroundImage.color = custom ? Color.clear
            : (useExpeditionMedievalTheme ? medievalItemTint : backgroundColor);
        backgroundImage.raycastTarget = !custom;

        if (!custom)
        {
            if (customShapeRoot != null)
                customShapeRoot.gameObject.SetActive(false);
            return;
        }

        if (customShapeRoot == null)
        {
            GameObject root = new GameObject("CustomShapeCells", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            customShapeRoot = root.GetComponent<RectTransform>();
            customShapeRoot.anchorMin = new Vector2(0f, 1f);
            customShapeRoot.anchorMax = new Vector2(0f, 1f);
            customShapeRoot.pivot = new Vector2(0f, 1f);
            customShapeRoot.SetAsFirstSibling();
        }

        customShapeRoot.gameObject.SetActive(true);
        customShapeRoot.anchoredPosition = Vector2.zero;
        customShapeRoot.sizeDelta = itemRect.sizeDelta;

        int count = 0;
        foreach (Vector2Int cell in inventoryItem.ItemData.GetOccupiedCells(rotationSteps))
        {
            Image cellImage;
            if (count >= customBackgroundCells.Count)
            {
                GameObject cellObject = new GameObject(
                    "ShapeCell", typeof(RectTransform), typeof(Image));
                cellObject.transform.SetParent(customShapeRoot, false);
                cellImage = cellObject.GetComponent<Image>();
                cellImage.sprite = GetDefaultSprite();
                cellImage.raycastTarget = true;
                customBackgroundCells.Add(cellImage);
            }
            else
            {
                cellImage = customBackgroundCells[count];
            }

            cellImage.gameObject.SetActive(true);
            if (useExpeditionMedievalTheme && medievalItemSprite != null)
            {
                cellImage.sprite = medievalItemSprite;
                cellImage.type = Image.Type.Sliced;
            }
            cellImage.color = useExpeditionMedievalTheme ? medievalItemTint : backgroundColor;
            RectTransform rect = cellImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(gridUI.CellSize, gridUI.CellSize);
            rect.anchoredPosition = gridUI.GetCellPosition(cell.x, cell.y);
            count++;
        }

        for (int i = count; i < customBackgroundCells.Count; i++)
            customBackgroundCells[i].gameObject.SetActive(false);
    }

    private void FindSoundPlayer()
    {
        if (soundPlayer != null)
        {
            LogSoundDiagnostic(
                $"既存SoundPlayerを使用：{GetTransformPath(soundPlayer.transform)} / " +
                $"GridUI={(gridUI != null ? GetTransformPath(gridUI.transform) : "null")}"
            );
            return;
        }

        if (gridUI != null)
        {
            soundPlayer = gridUI.GetComponent<InventorySoundPlayer>();

            if (soundPlayer != null)
            {
                LogSoundDiagnostic(
                    $"GridUIと同じObjectからSoundPlayer取得：{GetTransformPath(soundPlayer.transform)}"
                );
            }
        }

        if (soundPlayer == null)
        {
            soundPlayer = GetComponentInParent<InventorySoundPlayer>();

            if (soundPlayer != null)
            {
                LogSoundDiagnostic(
                    $"親階層からSoundPlayer取得：{GetTransformPath(soundPlayer.transform)}"
                );
            }
        }

        if (soundPlayer == null)
        {
            InventorySoundPlayer[] candidates =
                FindObjectsByType<InventorySoundPlayer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            if (candidates.Length > 0)
            {
                soundPlayer = candidates[0];

                LogSoundWarning(
                    $"GridUI/親階層にSoundPlayerが無かったため、Scene内候補から自動取得しました。" +
                    $"選択={GetTransformPath(soundPlayer.transform)} / 候補数={candidates.Length}。" +
                    "複数ある場合は意図しないSoundPlayerを掴む可能性があります。"
                );

                if (showSoundDiagnostics && candidates.Length > 1)
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        InventorySoundPlayer candidate = candidates[i];
                        if (candidate == null)
                        {
                            continue;
                        }

                        Debug.Log(
                            $"[InventorySound診断][ItemUI] SoundPlayer候補[{i}]=" +
                            $"{GetTransformPath(candidate.transform)} / " +
                            $"Active={candidate.gameObject.activeInHierarchy} / Enabled={candidate.enabled}",
                            this
                        );
                    }
                }
            }
        }

        if (soundPlayer == null)
        {
            LogSoundWarning(
                $"InventorySoundPlayerが見つかりません。ItemUI={GetTransformPath(transform)} / " +
                $"GridUI={(gridUI != null ? GetTransformPath(gridUI.transform) : "null")}"
            );
        }
    }

    private void PlayInventorySound(
        string soundName,
        System.Action<InventorySoundPlayer> playAction)
    {
        FindSoundPlayer();

        if (soundPlayer == null)
        {
            LogSoundWarning(
                $"再生要求 [{soundName}] 失敗：soundPlayer=null / " +
                $"Item={(inventoryItem != null && inventoryItem.ItemData != null ? inventoryItem.ItemData.DisplayName : "未設定")}"
            );
            return;
        }

        LogSoundDiagnostic(
            $"再生要求 [{soundName}] → SoundPlayer={GetTransformPath(soundPlayer.transform)} / " +
            $"SoundPlayerActive={soundPlayer.gameObject.activeInHierarchy} / " +
            $"SoundPlayerEnabled={soundPlayer.enabled} / " +
            $"Item={(inventoryItem != null && inventoryItem.ItemData != null ? inventoryItem.ItemData.DisplayName : "未設定")}"
        );

        playAction?.Invoke(soundPlayer);
    }

    private void LogSoundDiagnostic(string message)
    {
        if (!showSoundDiagnostics || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        //Debug.Log($"[InventorySound診断][ItemUI] {message}", this);
    }

    private void LogSoundWarning(string message)
    {
        if (!showSoundDiagnostics || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        //Debug.LogWarning($"[InventorySound診断][ItemUI] {message}", this);
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void EnsureVisuals()
    {
        itemRect = GetComponent<RectTransform>();

        backgroundImage = GetComponent<Image>();

        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        backgroundImage.sprite = useExpeditionMedievalTheme && medievalItemSprite != null
            ? medievalItemSprite : GetDefaultSprite();
        backgroundImage.type = useExpeditionMedievalTheme && medievalItemSprite != null
            ? Image.Type.Sliced : Image.Type.Simple;
        backgroundImage.raycastTarget = true;

        if (iconImage == null)
        {
            GameObject iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image)
            );

            iconObject.transform.SetParent(transform, false);

            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();

            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
            iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);

            iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
        }

        if (amountText == null)
        {
            GameObject amountObject = new GameObject(
                "AmountText",
                typeof(RectTransform),
                typeof(Text)
            );

            amountObject.transform.SetParent(transform, false);

            RectTransform amountRect =
                amountObject.GetComponent<RectTransform>();

            amountRect.anchorMin = Vector2.zero;
            amountRect.anchorMax = Vector2.one;
            amountRect.offsetMin = new Vector2(4f, 2f);
            amountRect.offsetMax = new Vector2(-4f, -2f);

            amountText = amountObject.GetComponent<Text>();
            amountText.font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf"
                );

            amountText.fontSize = 22;
            amountText.alignment = TextAnchor.LowerRight;
            amountText.color = Color.white;
            amountText.raycastTarget = false;
        }
    }

    /// <summary>
    /// ActiveInstanceだけに依存せず、
    /// 実際に売却Panelが表示されているPawnShopUIControllerを優先します。
    /// </summary>
    private PawnShopUIController FindPawnShopForQuickSell()
    {
        PawnShopUIController active =
            PawnShopUIController.ActiveInstance;

        if (active != null &&
            active.IsSellTabActive)
        {
            return active;
        }

        PawnShopUIController[] candidates =
            Object.FindObjectsByType<PawnShopUIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (PawnShopUIController candidate in candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            if (candidate.IsSellTabActive)
            {
                QuickSellDiagnostic(
                    $"売却中PawnShopを自動補正：{GetTransformPath(candidate.transform)}"
                );
                return candidate;
            }

            if (candidate.IsOpen &&
                !candidate.IsRepairMode &&
                (candidate.IsSellPanelActuallyVisible ||
                 candidate.IsSellCartGridActuallyVisible))
            {
                QuickSellDiagnostic(
                    $"売却UI表示状態からPawnShopを補正：" +
                    $"{GetTransformPath(candidate.transform)} / " +
                    $"SellPanel={candidate.IsSellPanelActuallyVisible} / " +
                    $"SellCartGrid={candidate.IsSellCartGridActuallyVisible}"
                );
                return candidate;
            }
        }

        return active;
    }

    private void QuickSellDiagnostic(
        string message)
    {
        if (!showQuickSellDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"[クリック売却診断][ItemUI] {message}",
            this
        );
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[InventoryItemUI] {message}", this);
        }
    }

    private static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null)
        {
            return defaultSprite;
        }

        defaultSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f)
        );

        defaultSprite.name = "InventoryItemUI_DefaultSprite";

        return defaultSprite;
    }
}
