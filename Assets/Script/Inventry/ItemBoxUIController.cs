using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemBoxUIController : MonoBehaviour
{
    [Header("パネル")]
    [Tooltip("プレイヤー用と箱用のInventoryGridUIを入れた親Panel")]
    [SerializeField] private GameObject itemBoxPanel;

    [Header("グリッドUI")]
    [Tooltip("このPanel内のプレイヤー用InventoryGridUI")]
    [SerializeField] private InventoryGridUI playerGridUI;

    [Tooltip("このPanel内の箱用InventoryGridUI。InspectorのInventory Controllerは空でOKです")]
    [SerializeField] private InventoryGridUI itemBoxGridUI;

    [Tooltip(
        "Player本体が使用しているInventoryController。" +
        "ItemBox画面のPlayer側Gridも必ずこれを参照します。" +
        "未設定ならPlayerから自動検索します。"
    )]
    [SerializeField]
    private InventoryController playerInventoryController;

    [Header("見出し")]
    [SerializeField] private TMP_Text titleText;

    [SerializeField] private string storageTitleFormat = "{0}";
    [SerializeField] private string shopTitleFormat = "ショップ：{0}";

    [Header("既存インベントリとの連携")]
    [Tooltip("通常Tabインベントリを閉じてから箱を開きたい時に設定")]
    [SerializeField] private InventoryPanelToggle inventoryPanelToggle;

    [Header("重量表示")]
    [Tooltip(
        "ItemBoxPanel内に置いたWeightUIを設定します。" +
        "未設定ならItemBoxPanelの子から自動検索します。"
    )]
    [SerializeField] private WeightUI itemBoxWeightUI;

    [Tooltip(
        "Playerに付いているPlayerWeightController。" +
        "未設定なら自動検索します。"
    )]
    [SerializeField]
    private PlayerWeightController playerWeightController;

    [Tooltip(
        "オンならItemBoxを開いている間も重量表示を継続更新します。" +
        "箱とInventory間でItemを移動した直後の表示漏れ対策です。"
    )]
    [SerializeField] private bool refreshWeightUIWhileOpen = true;

    [Header("一括回収")]
    [Tooltip(
        "オンの場合、ItemBoxを開いている時にTキーを長押しすると、" +
        "箱の中身をPlayer Inventoryへ可能な限り一括回収します。"
    )]
    [SerializeField] private bool enableTakeAllWithT = true;

    [Tooltip("Tキーを何秒長押ししたら一括回収するか。")]
    [SerializeField, Min(0.1f)] private float takeAllHoldDuration = 1f;

    [Tooltip(
        "T長押し中の進行度を表示するSlider。" +
        "ItemBoxPanel内に作成し、ここへ設定してください。"
    )]
    [SerializeField] private Slider takeAllHoldSlider;

    [Header("一括回収：Text表示")]
    [Tooltip(
        "通常は「T　すべて回収」、Inventoryがいっぱいの時は" +
        "「インベントリがいっぱいです」を表示する共通Textです。"
    )]
    [SerializeField] private TMP_Text takeAllStatusText;

    [Tooltip("通常時に表示するText。")]
    [SerializeField] private string takeAllPromptText = "T　すべて回収";

    [Tooltip("すべて回収できなかった時に表示するText。")]
    [SerializeField]
    private string inventoryFullText = "インベントリがいっぱいです";

    [Tooltip("満杯メッセージを表示する秒数。")]
    [SerializeField, Min(0.1f)]
    private float inventoryFullMessageDuration = 3f;

    [Header("一括回収：満杯Textの揺れ")]
    [Tooltip("満杯メッセージ表示時にTextを揺らす秒数。")]
    [SerializeField, Min(0f)]
    private float inventoryFullShakeDuration = 0.45f;

    [Tooltip("満杯Textの揺れ幅（UIピクセル）。")]
    [SerializeField, Min(0f)]
    private float inventoryFullShakeStrength = 8f;

    [Header("一括回収：サウンド")]
    [Tooltip(
        "一括回収の成功/失敗音を鳴らすAudioSource。" +
        "未設定ならこのGameObjectから自動取得します。"
    )]
    [SerializeField] private AudioSource takeAllAudioSource;

    [Tooltip("ItemBox内をすべて回収できた時の音。")]
    [SerializeField] private AudioClip takeAllSuccessSound;

    [Tooltip("Inventoryがいっぱいで、すべて回収できなかった時の音。")]
    [SerializeField] private AudioClip takeAllFailedSound;

    [SerializeField, Range(0f, 1f)]
    private float takeAllSuccessVolume = 1f;

    [SerializeField, Range(0f, 1f)]
    private float takeAllFailedVolume = 1f;

    [Header("一括回収：診断ログ")]
    [Tooltip(
        "オンの場合、Tキー入力・ItemBox状態・各Itemの回収結果をConsoleへ詳しく表示します。"
    )]
    [SerializeField] private bool enableTakeAllDebugLogs = true;

    [Header("プレイヤー操作")]
    [Tooltip("オンの場合、アイテムボックスを開いている間は移動・ジャンプを止めます")]
    [SerializeField] private bool lockPlayerMovementWhileOpen = true;

    [Tooltip("Playerに付いているPlayerMove。未設定なら自動検索します")]
    [SerializeField] private PlayerMove playerMove;

    [Header("武器操作")]
    [Tooltip("オンの場合、アイテムボックスを開いている間は照準・射撃・リロードを止めます")]
    [SerializeField] private bool lockWeaponControlsWhileOpen = true;

    [Tooltip("Playerに付いているPlayerEquipmentVisualController。未設定なら自動検索します")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    private Rigidbody2D playerRigidbody;
    private bool wasPlayerMoveEnabledBeforeOpen;
    private bool hasLockedPlayerMovement;
    private bool hasLockedWeaponControls;

    private float takeAllHoldElapsed;
    private bool isTakeAllHolding;
    private bool takeAllTriggeredForCurrentHold;

    private Coroutine inventoryFullMessageCoroutine;
    private bool isInventoryFullMessageVisible;
    private RectTransform takeAllStatusRect;
    private Vector2 takeAllStatusBaseAnchoredPosition;

    public bool IsOpen =>
        itemBoxPanel != null &&
        itemBoxPanel.activeInHierarchy &&
        currentItemBox != null;

    public ItemBoxInventory CurrentItemBox => currentItemBox;

    private ItemBoxInventory currentItemBox;

    private void Awake()
    {
        FindPlayerMove();
        FindPlayerInventoryController();
        FindEquipmentVisualController();
        FindPlayerWeightController();
        FindItemBoxWeightUI();

        InitializeTakeAllHoldSlider();
        InitializeTakeAllStatusText();

        if (takeAllAudioSource == null)
        {
            takeAllAudioSource = GetComponent<AudioSource>();
        }

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            CancelTakeAllHold();
            return;
        }

        if (!enableTakeAllWithT || !IsOpen)
        {
            CancelTakeAllHold();
            return;
        }

        var tKey = Keyboard.current.tKey;

        // 「インベントリがいっぱいです」表示中にTをもう一度押した場合は、
        // その入力では回収を開始せず、通常表示へ戻すだけにする。
        if (isInventoryFullMessageVisible &&
            tKey.wasPressedThisFrame)
        {
            DismissInventoryFullMessage();

            takeAllHoldElapsed = 0f;
            isTakeAllHolding = false;

            // このTを押し続けても回収が始まらないよう、
            // 一度Tを離すまで今回のHoldを消費済み扱いにする。
            takeAllTriggeredForCurrentHold = true;
            SetTakeAllSliderProgress(0f, false);
            return;
        }

        if (tKey.wasPressedThisFrame)
        {
            BeginTakeAllHold();
        }

        // ItemBoxを開く直前からTを押していた場合でも、
        // 開いた後に長押しを開始できるよう補完する。
        if (tKey.isPressed && !isTakeAllHolding &&
            !takeAllTriggeredForCurrentHold)
        {
            BeginTakeAllHold();
        }

        if (tKey.isPressed &&
            isTakeAllHolding &&
            !takeAllTriggeredForCurrentHold)
        {
            takeAllHoldElapsed += Time.unscaledDeltaTime;

            float duration = Mathf.Max(0.1f, takeAllHoldDuration);
            float progress = Mathf.Clamp01(
                takeAllHoldElapsed / duration
            );

            SetTakeAllSliderProgress(progress, true);

            if (takeAllHoldElapsed >= duration)
            {
                takeAllTriggeredForCurrentHold = true;
                isTakeAllHolding = false;

                SetTakeAllSliderProgress(1f, false);

                if (enableTakeAllDebugLogs)
                {
                    Debug.Log(
                        "[ItemBox TakeAll診断][長押し完了] " +
                        $"HoldDuration={takeAllHoldElapsed:F2}s / " +
                        "一括回収を実行します。",
                        this
                    );
                }

                TakeAllItems();
            }
        }

        if (tKey.wasReleasedThisFrame)
        {
            if (enableTakeAllDebugLogs &&
                !takeAllTriggeredForCurrentHold &&
                takeAllHoldElapsed > 0f)
            {
                Debug.Log(
                    "[ItemBox TakeAll診断][長押しキャンセル] " +
                    $"Elapsed={takeAllHoldElapsed:F2}s",
                    this
                );
            }

            ResetTakeAllHoldState();
        }
    }

    private void LateUpdate()
    {
        if (!refreshWeightUIWhileOpen || !IsOpen)
        {
            return;
        }

        RefreshWeightUI(false);
    }

    private void OnDisable()
    {
        // CanvasやこのControllerが無効になった時も、
        // プレイヤーだけ操作不能のまま残らないようにする
        CancelTakeAllHold();
        StopInventoryFullMessage(false);
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    private void OnDestroy()
    {
        CancelTakeAllHold();
        StopInventoryFullMessage(false);
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    public void Open(ItemBoxInventory itemBox)
    {
        if (itemBox == null || IsOpen)
        {
            return;
        }

        if (playerGridUI == null || itemBoxGridUI == null)
        {
            Debug.LogWarning(
                "ItemBoxUIController: Player Grid UI または " +
                "Item Box Grid UI が設定されていません。",
                this
            );
            return;
        }

        // 通常のTabインベントリと二重表示にならないようにする
        inventoryPanelToggle?.CloseInventory();

        // ItemBox画面のPlayer側Gridを、必ずPlayer本体の
        // InventoryControllerへ接続する。
        // これにより通常InventoryとItemBox画面で、
        // 配置・削除・追加状態が食い違う問題を防ぐ。
        if (!FindPlayerInventoryController())
        {
            Debug.LogWarning(
                "ItemBoxUIController: Player の InventoryController が " +
                "見つからないためItemBoxを開けません。",
                this
            );
            return;
        }

        playerGridUI.BindPlayerInventory(
            playerInventoryController
        );

        currentItemBox = itemBox;
        ResetTakeAllHoldState();

        // 箱用Gridだけを、今回開いた箱へ動的に接続する
        itemBoxGridUI.BindItemBoxInventory(currentItemBox);

        RefreshTitle();

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(true);
        }

        ShowTakeAllPrompt();

        // ItemBoxPanelが有効になった直後に、
        // Playerの現在重量を再計算してWeightUIへ反映する。
        RefreshWeightUI(true);

        // パネル表示と同時に、移動・ジャンプと武器操作を止める
        LockPlayerMovement();
        LockWeaponControls();

        playerGridUI.RefreshInventoryUI();
        itemBoxGridUI.RefreshInventoryUI();

        // Grid更新後にももう一度反映する。
        RefreshWeightUI(false);
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        ResetTakeAllHoldState();
        StopInventoryFullMessage(false);

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(false);
        }

        currentItemBox = null;

        // Tabや閉じるボタンで箱を閉じたら、元の操作状態へ戻す
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    private void InitializeTakeAllStatusText()
    {
        if (takeAllStatusText == null)
        {
            return;
        }

        takeAllStatusRect =
            takeAllStatusText.rectTransform;

        if (takeAllStatusRect != null)
        {
            takeAllStatusBaseAnchoredPosition =
                takeAllStatusRect.anchoredPosition;
        }

        takeAllStatusText.text = takeAllPromptText;
    }

    private void ShowTakeAllPrompt()
    {
        StopInventoryFullMessage(false);

        if (takeAllStatusText == null)
        {
            return;
        }

        RestoreTakeAllTextPosition();

        takeAllStatusText.text = takeAllPromptText;

        bool shouldShow =
            enableTakeAllWithT &&
            currentItemBox != null &&
            currentItemBox.AllowsDirectItemTransfer;

        takeAllStatusText.gameObject.SetActive(shouldShow);
    }

    private void ShowInventoryFullMessage()
    {
        if (takeAllStatusText == null)
        {
            return;
        }

        StopInventoryFullMessage(false);

        isInventoryFullMessageVisible = true;
        takeAllStatusText.gameObject.SetActive(true);
        takeAllStatusText.text = inventoryFullText;

        inventoryFullMessageCoroutine =
            StartCoroutine(InventoryFullMessageRoutine());
    }

    private IEnumerator InventoryFullMessageRoutine()
    {
        float totalDuration =
            Mathf.Max(0.1f, inventoryFullMessageDuration);

        float shakeDuration =
            Mathf.Clamp(
                inventoryFullShakeDuration,
                0f,
                totalDuration
            );

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (takeAllStatusRect != null &&
                elapsed <= shakeDuration &&
                inventoryFullShakeStrength > 0f)
            {
                Vector2 shakeOffset =
                    UnityEngine.Random.insideUnitCircle *
                    inventoryFullShakeStrength;

                takeAllStatusRect.anchoredPosition =
                    takeAllStatusBaseAnchoredPosition +
                    shakeOffset;
            }
            else
            {
                RestoreTakeAllTextPosition();
            }

            yield return null;
        }

        inventoryFullMessageCoroutine = null;
        isInventoryFullMessageVisible = false;

        ShowTakeAllPrompt();
    }

    private void DismissInventoryFullMessage()
    {
        StopInventoryFullMessage(true);
    }

    private void StopInventoryFullMessage(bool showPrompt)
    {
        if (inventoryFullMessageCoroutine != null)
        {
            StopCoroutine(inventoryFullMessageCoroutine);
            inventoryFullMessageCoroutine = null;
        }

        isInventoryFullMessageVisible = false;
        RestoreTakeAllTextPosition();

        if (showPrompt && takeAllStatusText != null)
        {
            takeAllStatusText.text = takeAllPromptText;

            bool shouldShow =
                enableTakeAllWithT &&
                IsOpen &&
                currentItemBox != null &&
                currentItemBox.AllowsDirectItemTransfer;

            takeAllStatusText.gameObject.SetActive(shouldShow);
        }
    }

    private void RestoreTakeAllTextPosition()
    {
        if (takeAllStatusRect != null)
        {
            takeAllStatusRect.anchoredPosition =
                takeAllStatusBaseAnchoredPosition;
        }
    }

    private void PlayTakeAllResultSound(bool success)
    {
        if (takeAllAudioSource == null)
        {
            takeAllAudioSource = GetComponent<AudioSource>();
        }

        if (takeAllAudioSource == null)
        {
            return;
        }

        AudioClip clip = success
            ? takeAllSuccessSound
            : takeAllFailedSound;

        if (clip == null)
        {
            return;
        }

        float volume = success
            ? Mathf.Clamp01(takeAllSuccessVolume)
            : Mathf.Clamp01(takeAllFailedVolume);

        takeAllAudioSource.PlayOneShot(
            clip,
            volume
        );
    }

    private void InitializeTakeAllHoldSlider()
    {
        if (takeAllHoldSlider == null)
        {
            return;
        }

        takeAllHoldSlider.minValue = 0f;
        takeAllHoldSlider.maxValue = 1f;
        takeAllHoldSlider.wholeNumbers = false;
        takeAllHoldSlider.interactable = false;
        takeAllHoldSlider.value = 0f;
        takeAllHoldSlider.gameObject.SetActive(false);
    }

    private void BeginTakeAllHold()
    {
        if (isTakeAllHolding ||
            takeAllTriggeredForCurrentHold ||
            !IsOpen)
        {
            return;
        }

        takeAllHoldElapsed = 0f;
        isTakeAllHolding = true;

        SetTakeAllSliderProgress(0f, true);

        if (enableTakeAllDebugLogs)
        {
            Debug.Log(
                "[ItemBox TakeAll診断][長押し開始] " +
                $"Required={Mathf.Max(0.1f, takeAllHoldDuration):F2}s",
                this
            );
        }
    }

    private void CancelTakeAllHold()
    {
        if (!isTakeAllHolding &&
            !takeAllTriggeredForCurrentHold &&
            takeAllHoldElapsed <= 0f)
        {
            SetTakeAllSliderProgress(0f, false);
            return;
        }

        ResetTakeAllHoldState();
    }

    private void ResetTakeAllHoldState()
    {
        takeAllHoldElapsed = 0f;
        isTakeAllHolding = false;
        takeAllTriggeredForCurrentHold = false;

        SetTakeAllSliderProgress(0f, false);
    }

    private void SetTakeAllSliderProgress(
        float progress,
        bool visible)
    {
        if (takeAllHoldSlider == null)
        {
            return;
        }

        takeAllHoldSlider.value = Mathf.Clamp01(progress);

        if (takeAllHoldSlider.gameObject.activeSelf != visible)
        {
            takeAllHoldSlider.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 現在開いているItemBoxから、入る物だけPlayer Inventoryへ一括回収します。
    /// スタック品は既存スタックを優先して自動追加し、
    /// 非スタック品は同じInventoryItem本体を移すため、
    /// 武器の残弾・耐久などの個別情報を維持します。
    /// </summary>
    public void TakeAllItems()
    {
        if (enableTakeAllDebugLogs)
        {
            int boxItemCount =
                currentItemBox != null &&
                currentItemBox.Grid != null &&
                currentItemBox.Grid.Items != null
                    ? currentItemBox.Grid.Items.Count
                    : -1;

            Debug.Log(
                "[ItemBox TakeAll診断][TakeAll開始] " +
                $"IsOpen={IsOpen} / " +
                $"CurrentItemBox={(currentItemBox != null ? currentItemBox.name : "null")} / " +
                $"Grid={(currentItemBox != null && currentItemBox.Grid != null ? "OK" : "null")} / " +
                $"BoxItemCount={boxItemCount} / " +
                $"PlayerInventory={(playerInventoryController != null ? playerInventoryController.name : "null")}",
                this
            );
        }

        if (!IsOpen ||
            currentItemBox == null ||
            currentItemBox.Grid == null)
        {
            if (enableTakeAllDebugLogs)
            {
                Debug.LogWarning(
                    "[ItemBox TakeAll診断][中断] " +
                    "IsOpen / CurrentItemBox / Grid のどれかが無効です。",
                    this
                );
            }

            return;
        }

        // Shopなど、直接移動が禁止されている箱からは一括回収しない。
        if (enableTakeAllDebugLogs)
        {
            Debug.Log(
                "[ItemBox TakeAll診断][箱設定] " +
                $"AllowsDirectItemTransfer={currentItemBox.AllowsDirectItemTransfer} / " +
                $"BoxKind={currentItemBox.BoxKind}",
                currentItemBox
            );
        }

        if (!currentItemBox.AllowsDirectItemTransfer)
        {
            Debug.LogWarning(
                "[ItemBox TakeAll診断][中断] " +
                "AllowsDirectItemTransfer=false のため一括回収できません。",
                currentItemBox
            );
            return;
        }

        bool foundPlayerInventory =
            FindPlayerInventoryController();

        if (enableTakeAllDebugLogs)
        {
            Debug.Log(
                "[ItemBox TakeAll診断][Player Inventory確認] " +
                $"FindResult={foundPlayerInventory} / " +
                $"Controller={(playerInventoryController != null ? playerInventoryController.name : "null")} / " +
                $"Grid={(playerInventoryController != null && playerInventoryController.Grid != null ? "OK" : "null")}",
                this
            );
        }

        if (!foundPlayerInventory ||
            playerInventoryController == null ||
            playerInventoryController.Grid == null)
        {
            Debug.LogWarning(
                "[ItemBox TakeAll診断][中断] " +
                "Player InventoryController または Grid が見つかりません。",
                this
            );
            return;
        }

        bool hadItemsBeforeTakeAll =
            currentItemBox.Grid.Items != null &&
            currentItemBox.Grid.Items.Count > 0;

        // 回収中にGrid.Itemsが変更されるので、先にコピーしてから処理する。
        List<InventoryItem> boxItems =
            new List<InventoryItem>();

        foreach (InventoryItem item in currentItemBox.Grid.Items)
        {
            if (item != null && item.ItemData != null)
            {
                boxItems.Add(item);
            }
        }

        int fullyTakenStacks = 0;
        int partiallyTakenStacks = 0;
        int movedUniqueItems = 0;
        int remainingItems = 0;

        foreach (InventoryItem item in boxItems)
        {
            if (item == null ||
                item.ItemData == null ||
                !currentItemBox.ContainsItem(item))
            {
                if (enableTakeAllDebugLogs)
                {
                    Debug.LogWarning(
                        "[ItemBox TakeAll診断][Itemスキップ] " +
                        "null / ItemDataなし / 既にBoxから消えているItemです。",
                        this
                    );
                }

                continue;
            }

            if (enableTakeAllDebugLogs)
            {
                Debug.Log(
                    "[ItemBox TakeAll診断][Item処理開始] " +
                    $"Name={item.ItemData.DisplayName} / " +
                    $"Amount={item.Amount} / " +
                    $"CanStack={item.CanStack} / " +
                    $"GridPos=({item.GridX},{item.GridY}) / " +
                    $"Rotated={item.IsRotated}",
                    currentItemBox
                );
            }

            if (item.CanStack)
            {
                int originalAmount = Mathf.Max(0, item.Amount);

                if (originalAmount <= 0)
                {
                    continue;
                }

                // Player Inventoryの既存スタック→空きマスの順で自動追加。
                playerInventoryController.TryAddItem(
                    item.ItemData,
                    originalAmount,
                    out int remainingAmount
                );

                int acceptedAmount =
                    Mathf.Clamp(
                        originalAmount - remainingAmount,
                        0,
                        originalAmount
                    );

                if (enableTakeAllDebugLogs)
                {
                    Debug.Log(
                        "[ItemBox TakeAll診断][スタック追加結果] " +
                        $"Name={item.ItemData.DisplayName} / " +
                        $"Original={originalAmount} / " +
                        $"Accepted={acceptedAmount} / " +
                        $"RemainingInBox予定={remainingAmount}",
                        this
                    );
                }

                if (acceptedAmount > 0)
                {
                    int removedAmount =
                        currentItemBox.RemoveItemAmount(
                            item,
                            acceptedAmount
                        );

                    // 通常は一致します。万一Box側から減らせなかった場合だけ警告。
                    if (removedAmount != acceptedAmount)
                    {
                        Debug.LogWarning(
                            $"[ItemBox] 一括回収時の個数同期に失敗しました。 " +
                            $"Item={item.ItemData.DisplayName}, " +
                            $"Player追加={acceptedAmount}, " +
                            $"Box削除={removedAmount}",
                            this
                        );
                    }
                }

                if (remainingAmount <= 0)
                {
                    fullyTakenStacks++;
                }
                else if (acceptedAmount > 0)
                {
                    partiallyTakenStacks++;
                    remainingItems++;
                }
                else
                {
                    remainingItems++;
                }

                continue;
            }

            // 銃などの非スタック品は、新しいItemを作り直さず
            // 同じInventoryItemを移動して個別状態を維持する。
            if (!playerInventoryController.TryFindAutoPlacement(
                    item,
                    out int targetX,
                    out int targetY,
                    out bool targetRotated))
            {
                if (enableTakeAllDebugLogs)
                {
                    Debug.LogWarning(
                        "[ItemBox TakeAll診断][配置場所なし] " +
                        $"Name={item.ItemData.DisplayName} / " +
                        "Player Inventoryに空き領域がありません。",
                        this
                    );
                }

                remainingItems++;
                continue;
            }

            if (enableTakeAllDebugLogs)
            {
                Debug.Log(
                    "[ItemBox TakeAll診断][配置候補] " +
                    $"Name={item.ItemData.DisplayName} / " +
                    $"Target=({targetX},{targetY}) / " +
                    $"Rotated={targetRotated}",
                    this
                );
            }

            int sourceX = item.GridX;
            int sourceY = item.GridY;
            bool sourceRotated = item.IsRotated;

            if (!currentItemBox.RemoveItem(item))
            {
                remainingItems++;
                continue;
            }

            bool placed =
                playerInventoryController.TryMoveItem(
                    item,
                    targetX,
                    targetY,
                    targetRotated
                );

            if (placed)
            {
                if (enableTakeAllDebugLogs)
                {
                    Debug.Log(
                        "[ItemBox TakeAll診断][個別Item回収成功] " +
                        $"Name={item.ItemData.DisplayName} / " +
                        $"Target=({targetX},{targetY})",
                        this
                    );
                }

                movedUniqueItems++;
                continue;
            }

            // 万一Player側への配置に失敗した場合は箱へ戻す。
            bool restored =
                currentItemBox.TryMoveItem(
                    item,
                    sourceX,
                    sourceY,
                    sourceRotated
                );

            if (!restored)
            {
                Debug.LogError(
                    $"[ItemBox] {item.ItemData.DisplayName} を " +
                    "Player Inventoryへ移せず、箱への復元にも失敗しました。",
                    this
                );
            }

            remainingItems++;
        }

        playerGridUI?.RefreshInventoryUI();
        itemBoxGridUI?.RefreshInventoryUI();
        RefreshWeightUI(true);

        bool hasRemainingItems =
            currentItemBox != null &&
            currentItemBox.Grid != null &&
            currentItemBox.Grid.Items != null &&
            currentItemBox.Grid.Items.Count > 0;

        if (hadItemsBeforeTakeAll)
        {
            if (hasRemainingItems)
            {
                // 一部は取れていても、1つでも箱に残ったら
                // 「すべて回収できなかった」として満杯警告を出す。
                ShowInventoryFullMessage();
                PlayTakeAllResultSound(false);
            }
            else
            {
                ShowTakeAllPrompt();
                PlayTakeAllResultSound(true);
            }
        }
        else
        {
            ShowTakeAllPrompt();
        }

        Debug.Log(
            $"[ItemBox] T長押し一括回収完了 / " +
            $"スタック全回収={fullyTakenStacks}, " +
            $"スタック一部回収={partiallyTakenStacks}, " +
            $"個別Item回収={movedUniqueItems}, " +
            $"箱に残ったItem={remainingItems}, " +
            $"結果={(hasRemainingItems ? "Inventory満杯/一部残り" : "全回収成功")}",
            currentItemBox
        );
    }

    /// <summary>
    /// ItemBox画面に表示する重量UIを最新状態へ更新します。
    /// forceRecalculate=trueの場合はPlayerWeightController側も再計算します。
    /// </summary>
    public void RefreshWeightUI(bool forceRecalculate = false)
    {
        if (forceRecalculate && FindPlayerWeightController())
        {
            playerWeightController.RecalculateWeight();
        }

        if (FindItemBoxWeightUI())
        {
            itemBoxWeightUI.RefreshUI();
        }
    }

    private bool FindItemBoxWeightUI()
    {
        if (itemBoxWeightUI != null)
        {
            return true;
        }

        if (itemBoxPanel == null)
        {
            return false;
        }

        WeightUI[] weightUIs =
            itemBoxPanel.GetComponentsInChildren<WeightUI>(true);

        if (weightUIs != null && weightUIs.Length > 0)
        {
            itemBoxWeightUI = weightUIs[0];
        }

        return itemBoxWeightUI != null;
    }

    /// <summary>
    /// Playerが実際に使用しているInventoryControllerを取得します。
    /// ItemBoxPanel自身に付いた別InventoryControllerを誤って使わないよう、
    /// Player参照を最優先します。
    /// </summary>
    private bool FindPlayerInventoryController()
    {
        if (playerInventoryController != null)
        {
            return true;
        }

        // PlayerMoveと同じGameObjectにあるInventoryControllerを最優先。
        if (playerMove != null)
        {
            playerInventoryController =
                playerMove.GetComponent<InventoryController>();
        }

        // PlayerWeightControllerと同じPlayer上にある場合も確認。
        if (playerInventoryController == null &&
            playerWeightController != null)
        {
            playerInventoryController =
                playerWeightController.GetComponent<InventoryController>();
        }

        // 最後の補完。通常は上記Player参照で見つかる想定です。
        if (playerInventoryController == null)
        {
            playerInventoryController =
                FindAnyObjectByType<InventoryController>(
                    FindObjectsInactive.Include
                );
        }

        return playerInventoryController != null;
    }

    private bool FindPlayerWeightController()
    {
        if (playerWeightController != null)
        {
            return true;
        }

        if (playerMove != null)
        {
            playerWeightController =
                playerMove.GetComponent<PlayerWeightController>();
        }

        if (playerWeightController == null)
        {
            playerWeightController =
                FindAnyObjectByType<PlayerWeightController>(
                    FindObjectsInactive.Include
                );
        }

        return playerWeightController != null;
    }

    private void LockPlayerMovement()
    {
        if (!lockPlayerMovementWhileOpen ||
            hasLockedPlayerMovement ||
            !FindPlayerMove())
        {
            return;
        }

        // 死亡などで元から無効だった場合は、閉じても勝手に有効化しない
        wasPlayerMoveEnabledBeforeOpen = playerMove.enabled;
        hasLockedPlayerMovement = true;

        playerMove.enabled = false;

        // 開く直前に歩いていた場合、そのまま滑り続けないように止める
        if (playerRigidbody != null)
        {
            Vector2 velocity = playerRigidbody.linearVelocity;
            velocity.x = 0f;
            playerRigidbody.linearVelocity = velocity;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (!hasLockedPlayerMovement)
        {
            return;
        }

        if (playerMove != null &&
            wasPlayerMoveEnabledBeforeOpen)
        {
            playerMove.enabled = true;
        }

        hasLockedPlayerMovement = false;
        wasPlayerMoveEnabledBeforeOpen = false;
    }

    private void LockWeaponControls()
    {
        if (!lockWeaponControlsWhileOpen ||
            hasLockedWeaponControls ||
            !FindEquipmentVisualController())
        {
            return;
        }

        equipmentVisualController.SetWeaponControlLock(
            this,
            true
        );

        hasLockedWeaponControls = true;
    }

    private void UnlockWeaponControls()
    {
        if (!hasLockedWeaponControls)
        {
            return;
        }

        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(
                this,
                false
            );
        }

        hasLockedWeaponControls = false;
    }

    private bool FindEquipmentVisualController()
    {
        if (equipmentVisualController != null)
        {
            return true;
        }

        if (playerMove != null)
        {
            equipmentVisualController =
                playerMove.GetComponent<
                    PlayerEquipmentVisualController
                >();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                FindAnyObjectByType<
                    PlayerEquipmentVisualController
                >(FindObjectsInactive.Include);
        }

        return equipmentVisualController != null;
    }

    private bool FindPlayerMove()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (playerRigidbody == null && playerMove != null)
        {
            playerRigidbody = playerMove.GetComponent<Rigidbody2D>();
        }

        if (playerInventoryController == null && playerMove != null)
        {
            playerInventoryController =
                playerMove.GetComponent<InventoryController>();
        }

        return playerMove != null;
    }

    private void RefreshTitle()
    {
        if (titleText == null || currentItemBox == null)
        {
            return;
        }

        string format = currentItemBox.BoxKind == ItemBoxKind.Shop
            ? shopTitleFormat
            : storageTitleFormat;

        titleText.text = string.Format(
            format,
            currentItemBox.BoxDisplayName
        );
    }

    private void OnValidate()
    {
        takeAllHoldDuration =
            Mathf.Max(0.1f, takeAllHoldDuration);

        inventoryFullMessageDuration =
            Mathf.Max(0.1f, inventoryFullMessageDuration);

        inventoryFullShakeDuration =
            Mathf.Max(0f, inventoryFullShakeDuration);

        inventoryFullShakeStrength =
            Mathf.Max(0f, inventoryFullShakeStrength);

        takeAllSuccessVolume =
            Mathf.Clamp01(takeAllSuccessVolume);

        takeAllFailedVolume =
            Mathf.Clamp01(takeAllFailedVolume);

        FindItemBoxWeightUI();
    }
}
