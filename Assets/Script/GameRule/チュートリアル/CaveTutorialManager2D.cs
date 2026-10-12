using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum CaveTutorialStep2D
{
    Move,
    Jump,
    Prone,
    WallClimb,
    Loot,
    CollectItem,
    WallDescend,
    Inventory,
    Equip,
    Reload,
    ShootEnemy,
    PickUpAdventurer,
    ShoulderAdventurer,
    RescueAdventurer,
    DropAdventurer,
    EnterElevator,
    Complete
}

/// <summary>
/// 洞窟チュートリアルの進行・文字表示・例外案内を管理します。
/// 「実際に操作が成功した時」に Integration 側から Notify～ を呼んで進めます。
/// </summary>
[DisallowMultipleComponent]
public sealed class CaveTutorialManager2D : MonoBehaviour
{
    [Serializable]
    public class TutorialSpot
    {
        [Tooltip("この説明を表示する地点。未設定の場合、その地点指定ステップは表示されません。")]
        public Transform point;

        [Min(0.1f)]
        public float radius = 2.5f;
    }

    public static CaveTutorialManager2D Instance { get; private set; }

    public event Action TutorialCompleted;
    public event Action<CaveTutorialStep2D> StepChanged;

    public CaveTutorialStep2D CurrentStep => currentStep;
    public GameObject TutorialAdventurer => tutorialAdventurer;
    public GameObject TutorialEnemy => tutorialEnemy;
    public bool IsCompleted => completed;

    // Inspector診断用：1～11の大項目が完了済みかを返します。
    // 進行は一本道なので、現在Stepが次の区切りへ到達したかで判定できます。
    public bool Progress01MoveCompleted => HasReached(CaveTutorialStep2D.Jump);
    public bool Progress02JumpCompleted => HasReached(CaveTutorialStep2D.Prone);
    public bool Progress03ProneCompleted => HasReached(CaveTutorialStep2D.WallClimb);
    public bool Progress04WallClimbCompleted => HasReached(CaveTutorialStep2D.Loot);
    public bool Progress05ItemBoxCompleted => HasReached(CaveTutorialStep2D.WallDescend);
    public bool Progress06WallDescendCompleted => HasReached(CaveTutorialStep2D.Inventory);
    public bool Progress07InventoryCompleted => HasReached(CaveTutorialStep2D.Equip);
    public bool Progress08WeaponCompleted => HasReached(CaveTutorialStep2D.ShootEnemy);
    public bool Progress09EnemyCompleted => HasReached(CaveTutorialStep2D.PickUpAdventurer);
    public bool Progress10NpcCarryCompleted => HasReached(CaveTutorialStep2D.RescueAdventurer);
    public bool Progress11NpcRescueCompleted => completed;

    [Header("プレイヤー・対象")]
    [SerializeField] private Transform player;

    [Tooltip("チュートリアルで救出する冒険者。指定すると他のNPCでは進みません。")]
    [SerializeField] private GameObject tutorialAdventurer;

    [Tooltip("チュートリアルで撃つ敵。指定するとこの敵への命中だけで進みます。")]
    [SerializeField] private GameObject tutorialEnemy;

    [Header("メインUI")]
    [SerializeField] private CanvasGroup mainGroup;
    [SerializeField] private GameObject movementPanel;
    [SerializeField] private TMP_Text leftMoveText;
    [SerializeField] private TMP_Text rightMoveText;
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TMP_Text instructionText;

    [Min(0.1f)]
    [SerializeField] private float fadeSeconds = 0.65f;

    [Range(0.05f, 1f)]
    [SerializeField] private float completedTextAlpha = 0.27f;

    [SerializeField] private bool allowSpaceKeyToCompleteJump;

    [Min(0f)]
    [SerializeField] private float completeMessageSeconds = 3f;


    [Header("チュートリアルText（Inspectorで編集可能）")]
    [TextArea(1, 3)] [SerializeField] private string moveLeftMessage = "Aで左に移動";
    [TextArea(1, 3)] [SerializeField] private string moveRightMessage = "Dで右に移動";
    [TextArea(1, 3)] [SerializeField] private string jumpMessage = "SPACEでジャンプしよう";
    [TextArea(1, 3)] [SerializeField] private string proneMessage = "左ALTでほふくしよう";
    [TextArea(1, 3)] [SerializeField] private string wallClimbMessage = "W ＋ A または D を同時押しして壁を登ろう";
    [TextArea(1, 3)] [SerializeField] private string lootMessage = "EでItemBoxを開こう";
    [TextArea(1, 3)] [SerializeField] private string collectItemMessage = "ItemBoxからアイテムをインベントリへ回収しよう";
    [TextArea(1, 3)] [SerializeField] private string wallDescendMessage = "（ItemBoxが開いていたらTABで閉じよう） 壁登り中はSで下へ下がろう";
    [TextArea(1, 3)] [SerializeField] private string inventoryMessage = "TABでインベントリを開こう";
    [TextArea(1, 3)] [SerializeField] private string equipMessage = "武器をドラッグ＆ドロップで装備しよう";
    [TextArea(1, 3)] [SerializeField] private string reloadMessage = "TABでインベントリを閉じ、Rで銃をリロードしよう";
    [TextArea(1, 3)] [SerializeField] private string shootEnemyMessage = "カーソルを敵に向けて、左クリックで攻撃しよう";
    [TextArea(1, 3)] [SerializeField] private string pickUpAdventurerMessage = "Eで冒険者を持ち上げよう";
    [TextArea(1, 3)] [SerializeField] private string shoulderAdventurerMessage = "もう一度Eで冒険者を担ごう";
    [TextArea(1, 3)] [SerializeField] private string rescueAdventurerMessage = "冒険者を救出せよ！ 矢印のエレベーターへ向かおう";
    [TextArea(1, 3)] [SerializeField] private string dropAdventurerMessage = "エレベーター前でFを押し、冒険者を下ろそう";
    [TextArea(1, 3)] [SerializeField] private string enterElevatorMessage = "冒険者の収納を確認中…";
    [TextArea(1, 3)] [SerializeField] private string completeMessage = "チュートリアル完了！";

    [Header("例外Text（Inspectorで編集可能）")]
    [TextArea(1, 3)] [SerializeField] private string fractureMessage = "骨折！ インベントリからNaegiを使用して治療しよう";
    [TextArea(1, 3)] [SerializeField] private string lowTorchMessage = "松明が50%以下！ 1キーで明るくしよう";

    [Header("近づいた時に表示する地点")]
    [SerializeField] private TutorialSpot wallClimbSpot = new TutorialSpot();
    [SerializeField] private TutorialSpot lootSpot = new TutorialSpot();
    [SerializeField] private TutorialSpot wallDescendSpot = new TutorialSpot();
    [SerializeField] private TutorialSpot proneSpot = new TutorialSpot();
    [SerializeField] private TutorialSpot inventorySpot = new TutorialSpot();
    [SerializeField] private TutorialSpot adventurerSpot = new TutorialSpot();

    [Header("救出先：エレベーター")]
    [SerializeField] private TutorialSpot rescueSpot = new TutorialSpot();
    [SerializeField] private CaveTutorialArrow2D rescueArrow;

    [Header("例外メッセージ")]
    [SerializeField] private CanvasGroup exceptionGroup;
    [SerializeField] private TMP_Text exceptionText;

    [Tooltip("Exception UIが未設定でも、通常のInstruction Textを使って骨折・松明警告を表示します。")]
    [SerializeField] private bool useMainTextAsExceptionFallback = true;

    [Header("途中のチュートリアルを飛ばせる設定")]
    [Tooltip("ONなら、前のチュートリアルを未完了のまま次の地点へ進んでも、その地点のチュートリアルへ自動で進みます。")]
    [SerializeField] private bool allowLocationBasedSkip = true;

    [Header("骨折状態の直接監視")]
    [Tooltip("未設定ならPlayerまたはSceneから自動検索します。Integration側の通知を取りこぼしても骨折Textを表示できます。")]
    [SerializeField] private PlayerStatusConditionController statusConditions;

    [Header("診断")]
    [SerializeField] private bool logStepChanges = true;

    [Tooltip("ONなら、Textが表示されない理由・Step完了が拒否された理由を詳しくConsoleへ出します。")]
    [SerializeField] private bool showDetailedDebugLogs = true;

    [Tooltip("地点条件を満たせずTextが隠れている時、何秒おきに理由を再表示するか。")]
    [SerializeField, Min(0.5f)] private float hiddenStepLogInterval = 2f;

    private CaveTutorialStep2D currentStep = CaveTutorialStep2D.Move;

    private bool pressedA;
    private bool pressedD;
    private bool transitioning;
    private bool completed;

    private bool fractured;
    private bool torchAtOrBelowHalf;
    private bool torchNoticeDismissed;
    private bool exceptionUsingMainFallback;
    private float nextStatusReferenceSearchTime;

    private float targetMainAlpha;
    private float targetExceptionAlpha;

    private Color leftInitialColor;
    private Color rightInitialColor;

    private bool acceptedAdventurerDrop;
    private bool acceptedAdventurerStorage;
    private bool pendingElevatorEntry;
    private float completedAt;

    private bool lastStageCanBeShown;
    private bool stageVisibilityInitialized;
    private float nextHiddenStageDiagnosticTime;
    private string lastRejectedCompletionSignature = string.Empty;
    private float nextRejectedCompletionLogTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("CaveTutorialManager2D がシーン内に2つあります。", this);
            enabled = false;
            return;
        }

        if (player == null ||
            mainGroup == null ||
            movementPanel == null ||
            leftMoveText == null ||
            rightMoveText == null ||
            instructionPanel == null ||
            instructionText == null)
        {
            Debug.LogError(
                "CaveTutorialManager2D: Playerまたは必須UI参照が設定されていません。",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;

        ResolveExceptionUIReferences();
        ResolveStatusConditionReference();

        leftInitialColor = leftMoveText.color;
        rightInitialColor = rightMoveText.color;

        mainGroup.alpha = 0f;
        mainGroup.interactable = false;
        mainGroup.blocksRaycasts = false;

        if (exceptionGroup != null)
        {
            exceptionGroup.alpha = 0f;
            exceptionGroup.interactable = false;
            exceptionGroup.blocksRaycasts = false;
        }

        ValidateSpots();
        RenderStep();
        LogBootDiagnostics();

        if (rescueArrow != null)
        {
            rescueArrow.Initialize(
                rescueSpot != null ? rescueSpot.point : null
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        RefreshFractureStateFromPlayer();

        if (!transitioning && !completed)
        {
            // 前のチュートリアルを飛ばして先の地点へ進んだ場合、
            // 現在地に対応するチュートリアルへ追いつかせます。
            TryAdvanceToReachedLaterSpot();
        }

        if (!transitioning && !completed)
        {
            if (currentStep == CaveTutorialStep2D.Move)
            {
                if (CaveTutorialKeyInput2D.WasPressed(
                        CaveTutorialKeyInput2D.Key.A))
                {
                    pressedA = true;
                }

                if (CaveTutorialKeyInput2D.WasPressed(
                        CaveTutorialKeyInput2D.Key.D))
                {
                    pressedD = true;
                }

                UpdateMoveText();

                if (pressedA && pressedD)
                {
                    Advance(CaveTutorialStep2D.Jump);
                }
            }
            else if (
                currentStep == CaveTutorialStep2D.Jump &&
                allowSpaceKeyToCompleteJump &&
                CaveTutorialKeyInput2D.WasPressed(
                    CaveTutorialKeyInput2D.Key.Space))
            {
                Advance(CaveTutorialStep2D.Prone);
            }
            else if (
                currentStep == CaveTutorialStep2D.RescueAdventurer &&
                Near(rescueSpot))
            {
                Advance(CaveTutorialStep2D.DropAdventurer);
            }
            else if (
                (currentStep == CaveTutorialStep2D.RescueAdventurer ||
                 currentStep == CaveTutorialStep2D.DropAdventurer ||
                 currentStep == CaveTutorialStep2D.EnterElevator) &&
                acceptedAdventurerStorage)
            {
                Advance(CaveTutorialStep2D.Complete);
            }
            else if (
                currentStep == CaveTutorialStep2D.EnterElevator &&
                pendingElevatorEntry)
            {
                pendingElevatorEntry = false;
                Advance(CaveTutorialStep2D.Complete);
            }
        }

        // 例外Textを先に計算します。
        // Exception UIが無い場合は、通常Instruction Textをフォールバック表示します。
        UpdateExceptionText();

        if (!transitioning)
        {
            bool canShow = completed
                ? (Time.unscaledTime - completedAt < completeMessageSeconds)
                : StageCanBeShown();

            targetMainAlpha = exceptionUsingMainFallback || canShow
                ? 1f
                : 0f;

            UpdateStageVisibilityDiagnostics(canShow);
        }

        mainGroup.alpha = Mathf.MoveTowards(
            mainGroup.alpha,
            targetMainAlpha,
            Time.unscaledDeltaTime / fadeSeconds
        );

        if (exceptionGroup != null)
        {
            exceptionGroup.alpha = Mathf.MoveTowards(
                exceptionGroup.alpha,
                targetExceptionAlpha,
                Time.unscaledDeltaTime / fadeSeconds
            );
        }

        if (rescueArrow != null)
        {
            bool showRescueArrow =
                !completed &&
                !transitioning &&
                !exceptionUsingMainFallback &&
                (
                    currentStep == CaveTutorialStep2D.RescueAdventurer ||
                    (
                        currentStep == CaveTutorialStep2D.DropAdventurer &&
                        !Near(rescueSpot)
                    )
                );

            rescueArrow.SetVisible(showRescueArrow);
        }
    }

    private void ValidateSpots()
    {
        WarnSpot(wallClimbSpot, "壁登り");
        WarnSpot(lootSpot, "ItemBox");
        WarnSpot(wallDescendSpot, "壁下降");
        WarnSpot(proneSpot, "ほふく");
        WarnSpot(inventorySpot, "Inventory");
        WarnSpot(adventurerSpot, "冒険者");
        WarnSpot(rescueSpot, "救出先・エレベーター");

        if (tutorialAdventurer == null)
        {
            Debug.LogWarning(
                "Tutorial Adventurer が未設定です。他のCarryableでも進行する可能性があります。",
                this
            );
        }

        if (tutorialEnemy == null)
        {
            Debug.LogWarning(
                "Tutorial Enemy が未設定です。攻撃対象を限定できません。",
                this
            );
        }

        if (rescueArrow == null)
        {
            Debug.LogWarning(
                "Rescue Arrow が未設定です。救出先の矢印は表示されません。",
                this
            );
        }

        if (exceptionGroup == null || exceptionText == null)
        {
            Debug.LogWarning(
                useMainTextAsExceptionFallback
                    ? "Exception UI が未設定です。骨折・松明警告は通常Instruction Textへフォールバック表示します。"
                    : "Exception UI が未設定です。骨折・松明警告を表示できません。",
                this
            );
        }
    }

    private void ResolveExceptionUIReferences()
    {
        if (exceptionText == null && exceptionGroup != null)
        {
            exceptionText = exceptionGroup.GetComponentInChildren<TMP_Text>(true);
        }

        if (exceptionText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (TMP_Text text in texts)
            {
                if (text != null && text.gameObject.name == "ExceptionText")
                {
                    exceptionText = text;
                    break;
                }
            }
        }

        if (exceptionGroup == null && exceptionText != null)
        {
            exceptionGroup = exceptionText.GetComponentInParent<CanvasGroup>(true);
        }

        if (exceptionGroup == null)
        {
            CanvasGroup[] groups = FindObjectsByType<CanvasGroup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (CanvasGroup group in groups)
            {
                if (group != null && group.gameObject.name == "TutorialException")
                {
                    exceptionGroup = group;
                    break;
                }
            }
        }

        if (exceptionText == null && exceptionGroup != null)
        {
            exceptionText = exceptionGroup.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void ResolveStatusConditionReference()
    {
        if (statusConditions != null)
        {
            return;
        }

        if (player != null)
        {
            statusConditions = player.GetComponent<PlayerStatusConditionController>();

            if (statusConditions == null)
            {
                statusConditions = player.GetComponentInParent<PlayerStatusConditionController>();
            }

            if (statusConditions == null)
            {
                statusConditions = player.GetComponentInChildren<PlayerStatusConditionController>(true);
            }
        }

        if (statusConditions == null)
        {
            statusConditions = FindAnyObjectByType<PlayerStatusConditionController>(
                FindObjectsInactive.Include
            );
        }
    }

    private void RefreshFractureStateFromPlayer()
    {
        if (statusConditions == null &&
            Time.unscaledTime >= nextStatusReferenceSearchTime)
        {
            nextStatusReferenceSearchTime = Time.unscaledTime + 1f;
            ResolveStatusConditionReference();

            if (showDetailedDebugLogs && statusConditions == null)
            {
                Debug.LogWarning(
                    "[CaveTutorialDiag][FRACTURE] PlayerStatusConditionControllerが見つかりません。",
                    this
                );
            }
        }

        if (statusConditions != null)
        {
            SetFractured(statusConditions.IsFractured);
        }
    }

    private void TryAdvanceToReachedLaterSpot()
    {
        if (!allowLocationBasedSkip || transitioning || completed)
        {
            return;
        }

        CaveTutorialStep2D target = currentStep;
        string spotName = string.Empty;

        // 後ろの地点から確認し、複数のRadiusが重なっている場合は
        // より先の地点を優先します。
        if (IsLaterStep(CaveTutorialStep2D.PickUpAdventurer) && Near(adventurerSpot))
        {
            target = CaveTutorialStep2D.PickUpAdventurer;
            spotName = adventurerSpot.point != null ? adventurerSpot.point.name : "AdventurerSpot";
        }
        else if (IsLaterStep(CaveTutorialStep2D.Inventory) && Near(inventorySpot))
        {
            target = CaveTutorialStep2D.Inventory;
            spotName = inventorySpot.point != null ? inventorySpot.point.name : "InventorySpot";
        }
        else if (IsLaterStep(CaveTutorialStep2D.WallDescend) && Near(wallDescendSpot))
        {
            target = CaveTutorialStep2D.WallDescend;
            spotName = wallDescendSpot.point != null ? wallDescendSpot.point.name : "WallDescendSpot";
        }
        else if (IsLaterStep(CaveTutorialStep2D.Loot) && Near(lootSpot))
        {
            target = CaveTutorialStep2D.Loot;
            spotName = lootSpot.point != null ? lootSpot.point.name : "LootSpot";
        }
        else if (IsLaterStep(CaveTutorialStep2D.WallClimb) && Near(wallClimbSpot))
        {
            target = CaveTutorialStep2D.WallClimb;
            spotName = wallClimbSpot.point != null ? wallClimbSpot.point.name : "WallClimbSpot";
        }
        else if (IsLaterStep(CaveTutorialStep2D.Prone) && Near(proneSpot))
        {
            target = CaveTutorialStep2D.Prone;
            spotName = proneSpot.point != null ? proneSpot.point.name : "ProneSpot";
        }

        if (target == currentStep)
        {
            return;
        }

        if (showDetailedDebugLogs)
        {
            Debug.Log(
                $"[CaveTutorialDiag][AUTO-SKIP] {currentStep} → {target} / " +
                $"Playerが先の地点 {spotName} に到達したため、その地点のチュートリアルから再開します。",
                this
            );
        }

        Advance(target);
    }

    private bool IsLaterStep(CaveTutorialStep2D candidate)
    {
        return (int)candidate > (int)currentStep;
    }

    private void WarnSpot(TutorialSpot spot, string label)
    {
        if (spot == null || spot.point == null)
        {
            Debug.LogWarning(
                "CaveTutorialManager2D: 地点未設定 → " + label,
                this
            );
        }
    }

    private bool Near(TutorialSpot spot)
    {
        if (player == null ||
            spot == null ||
            spot.point == null)
        {
            return false;
        }

        return (
            (Vector2)player.position -
            (Vector2)spot.point.position
        ).sqrMagnitude <= spot.radius * spot.radius;
    }

    private bool StageCanBeShown()
    {
        switch (currentStep)
        {
            case CaveTutorialStep2D.WallClimb:
                return Near(wallClimbSpot);

            case CaveTutorialStep2D.Loot:
                return Near(lootSpot);

            case CaveTutorialStep2D.WallDescend:
                return Near(wallDescendSpot);

            case CaveTutorialStep2D.Prone:
                return Near(proneSpot);

            case CaveTutorialStep2D.Inventory:
                return Near(inventorySpot);

            case CaveTutorialStep2D.PickUpAdventurer:
                return Near(adventurerSpot);

            default:
                return true;
        }
    }

    private void RenderStep()
    {
        bool isMove = currentStep == CaveTutorialStep2D.Move;

        movementPanel.SetActive(isMove);
        instructionPanel.SetActive(!isMove);

        if (isMove)
        {
            leftMoveText.text = moveLeftMessage;
            rightMoveText.text = moveRightMessage;
            UpdateMoveText();
        }
        else
        {
            instructionText.text = StepMessage(currentStep);
        }

        if (logStepChanges)
        {
            Debug.Log("[CaveTutorial] " + currentStep, this);
        }

        if (showDetailedDebugLogs)
        {
            Debug.Log(
                $"[CaveTutorialDiag][STEP] Current={currentStep} / Message=\"{StepMessage(currentStep)}\" / " +
                $"NeedsSpot={StepNeedsSpot(currentStep)} / {DescribeCurrentSpot()}",
                this
            );
        }

        stageVisibilityInitialized = false;
        nextHiddenStageDiagnosticTime = 0f;
        StepChanged?.Invoke(currentStep);
    }

    private void UpdateMoveText()
    {
        leftMoveText.color = pressedA
            ? Dimmed(leftInitialColor)
            : leftInitialColor;

        rightMoveText.color = pressedD
            ? Dimmed(rightInitialColor)
            : rightInitialColor;
    }

    private Color Dimmed(Color original)
    {
        return new Color(
            original.r * 0.45f,
            original.g * 0.45f,
            original.b * 0.45f,
            original.a * completedTextAlpha
        );
    }

    private string StepMessage(CaveTutorialStep2D step)
    {
        switch (step)
        {
            case CaveTutorialStep2D.Jump:
                return jumpMessage;

            case CaveTutorialStep2D.Prone:
                return proneMessage;

            case CaveTutorialStep2D.WallClimb:
                return wallClimbMessage;

            case CaveTutorialStep2D.Loot:
                return lootMessage;

            case CaveTutorialStep2D.CollectItem:
                return collectItemMessage;

            case CaveTutorialStep2D.WallDescend:
                return wallDescendMessage;

            case CaveTutorialStep2D.Inventory:
                return inventoryMessage;

            case CaveTutorialStep2D.Equip:
                return equipMessage;

            case CaveTutorialStep2D.Reload:
                return reloadMessage;

            case CaveTutorialStep2D.ShootEnemy:
                return shootEnemyMessage;

            case CaveTutorialStep2D.PickUpAdventurer:
                return pickUpAdventurerMessage;

            case CaveTutorialStep2D.ShoulderAdventurer:
                return shoulderAdventurerMessage;

            case CaveTutorialStep2D.RescueAdventurer:
                return rescueAdventurerMessage;

            case CaveTutorialStep2D.DropAdventurer:
                return dropAdventurerMessage;

            case CaveTutorialStep2D.EnterElevator:
                return enterElevatorMessage;

            case CaveTutorialStep2D.Complete:
                return completeMessage;

            default:
                return string.Empty;
        }
    }

    private bool IsCorrectAdventurer(GameObject npc)
    {
        if (tutorialAdventurer == null)
        {
            return true;
        }

        if (npc == null)
        {
            return false;
        }

        return npc == tutorialAdventurer ||
               npc.transform.IsChildOf(tutorialAdventurer.transform) ||
               tutorialAdventurer.transform.IsChildOf(npc.transform);
    }

    private bool IsCorrectEnemy(GameObject enemy)
    {
        if (tutorialEnemy == null)
        {
            return enemy != null;
        }

        if (enemy == null)
        {
            return false;
        }

        return enemy == tutorialEnemy ||
               enemy.transform.IsChildOf(tutorialEnemy.transform) ||
               tutorialEnemy.transform.IsChildOf(enemy.transform);
    }

    private void TryComplete(
        CaveTutorialStep2D expected,
        CaveTutorialStep2D next,
        bool requireCurrentSpot = false)
    {
        if (completed)
        {
            LogRejectedCompletion(expected, next, "TutorialはすでにCompleteです。");
            return;
        }

        if (transitioning)
        {
            LogRejectedCompletion(expected, next, "フェード切替中のため一時的に受理できません。");
            return;
        }

        if (currentStep != expected)
        {
            LogRejectedCompletion(
                expected,
                next,
                $"CurrentStepが違います。Current={currentStep}"
            );
            return;
        }

        if (requireCurrentSpot && !StageCanBeShown())
        {
            LogRejectedCompletion(
                expected,
                next,
                "操作成功イベントは届きましたが、指定Spotの範囲外なので進行を拒否しました。" +
                DescribeCurrentSpot()
            );
            return;
        }

        if (showDetailedDebugLogs)
        {
            Debug.Log(
                $"[CaveTutorialDiag][COMPLETE-OK] {expected} → {next} / {DescribeCurrentSpot()}",
                this
            );
        }

        Advance(next);
    }

    private void Advance(CaveTutorialStep2D next)
    {
        if (transitioning || completed)
        {
            return;
        }

        StartCoroutine(AdvanceRoutine(next));
    }

    private IEnumerator AdvanceRoutine(CaveTutorialStep2D next)
    {
        transitioning = true;
        targetMainAlpha = 0f;

        if (rescueArrow != null)
        {
            rescueArrow.SetVisible(false);
        }

        while (mainGroup.alpha > 0.005f)
        {
            yield return null;
        }

        currentStep = next;
        completed = next == CaveTutorialStep2D.Complete;

        if (completed)
        {
            completedAt = Time.unscaledTime;
        }

        RenderStep();
        transitioning = false;

        if (completed)
        {
            TutorialCompleted?.Invoke();
        }
    }

    public void NotifyJumped()
    {
        TryComplete(
            CaveTutorialStep2D.Jump,
            CaveTutorialStep2D.Prone
        );
    }

    public void NotifyWallClimbed()
    {
        TryComplete(
            CaveTutorialStep2D.WallClimb,
            CaveTutorialStep2D.Loot,
            true
        );
    }

    public void NotifyItemBoxOpened()
    {
        TryComplete(
            CaveTutorialStep2D.Loot,
            CaveTutorialStep2D.CollectItem,
            true
        );
    }

    // 旧Relay互換。
    public void NotifyLooted()
    {
        NotifyItemBoxOpened();
    }

    public void NotifyItemCollected()
    {
        TryComplete(
            CaveTutorialStep2D.CollectItem,
            CaveTutorialStep2D.WallDescend
        );
    }

    public void NotifyWallDescended()
    {
        TryComplete(
            CaveTutorialStep2D.WallDescend,
            CaveTutorialStep2D.Inventory,
            true
        );
    }

    public void NotifyProneEntered()
    {
        TryComplete(
            CaveTutorialStep2D.Prone,
            CaveTutorialStep2D.WallClimb,
            true
        );
    }

    public void NotifyInventoryOpened()
    {
        TryComplete(
            CaveTutorialStep2D.Inventory,
            CaveTutorialStep2D.Equip,
            true
        );
    }

    public void NotifyWeaponEquipped()
    {
        TryComplete(
            CaveTutorialStep2D.Equip,
            CaveTutorialStep2D.Reload
        );
    }

    public void NotifyReloadCompleted()
    {
        TryComplete(
            CaveTutorialStep2D.Reload,
            CaveTutorialStep2D.ShootEnemy
        );
    }

    public void NotifyEnemyHit(GameObject enemy)
    {
        if (!IsCorrectEnemy(enemy))
        {
            return;
        }

        TryComplete(
            CaveTutorialStep2D.ShootEnemy,
            CaveTutorialStep2D.PickUpAdventurer
        );
    }

    public void NotifyEnemyHit()
    {
        NotifyEnemyHit(tutorialEnemy);
    }

    public void NotifyAdventurerPickedUp(GameObject adventurer)
    {
        if (IsCorrectAdventurer(adventurer))
        {
            TryComplete(
                CaveTutorialStep2D.PickUpAdventurer,
                CaveTutorialStep2D.ShoulderAdventurer,
                true
            );
        }
    }

    public void NotifyAdventurerShouldered(GameObject adventurer)
    {
        if (IsCorrectAdventurer(adventurer))
        {
            TryComplete(
                CaveTutorialStep2D.ShoulderAdventurer,
                CaveTutorialStep2D.RescueAdventurer
            );
        }
    }

    public void NotifyAdventurerDropped(GameObject adventurer)
    {
        if (completed ||
            acceptedAdventurerDrop ||
            !Near(rescueSpot))
        {
            return;
        }

        if (currentStep != CaveTutorialStep2D.DropAdventurer &&
            currentStep != CaveTutorialStep2D.RescueAdventurer)
        {
            return;
        }

        if (!IsCorrectAdventurer(adventurer))
        {
            return;
        }

        acceptedAdventurerDrop = true;
    }

    public void NotifyAdventurerStored(GameObject adventurer)
    {
        if (!IsCorrectAdventurer(adventurer) || completed)
        {
            if (showDetailedDebugLogs)
            {
                Debug.LogWarning(
                    $"[CaveTutorialDiag][NPC-STORED-NG] Target={(adventurer != null ? adventurer.name : "NULL")} / " +
                    $"Correct={IsCorrectAdventurer(adventurer)} / Completed={completed}",
                    this
                );
            }
            return;
        }

        // ElevatorCarryReceiver2Dが受理したという事実を最終判定にします。
        // Dropイベントを一時的に取りこぼしていても救出完了できるようにします。
        acceptedAdventurerDrop = true;
        acceptedAdventurerStorage = true;

        if (showDetailedDebugLogs)
        {
            Debug.Log(
                $"[CaveTutorialDiag][NPC-STORED-OK] Target={adventurer.name} / Step={currentStep} / " +
                "Elevatorへの収納を確認 → Completeへ進みます。",
                this
            );
        }

        if (!transitioning &&
            (currentStep == CaveTutorialStep2D.RescueAdventurer ||
             currentStep == CaveTutorialStep2D.DropAdventurer ||
             currentStep == CaveTutorialStep2D.EnterElevator))
        {
            Advance(CaveTutorialStep2D.Complete);
        }
    }

    public void NotifyElevatorEntered()
    {
        if (!acceptedAdventurerDrop ||
            !acceptedAdventurerStorage ||
            completed)
        {
            return;
        }

        if (currentStep == CaveTutorialStep2D.RescueAdventurer ||
            currentStep == CaveTutorialStep2D.DropAdventurer ||
            currentStep == CaveTutorialStep2D.EnterElevator)
        {
            pendingElevatorEntry = true;
        }
    }

    public void SetFractured(bool isFractured)
    {
        if (fractured == isFractured)
        {
            return;
        }

        fractured = isFractured;

        if (showDetailedDebugLogs)
        {
            Debug.Log(
                $"[CaveTutorialDiag][FRACTURE] SetFractured({isFractured}) / " +
                $"ExceptionGroup={(exceptionGroup != null ? exceptionGroup.name : "NULL")} / " +
                $"ExceptionText={(exceptionText != null ? exceptionText.name : "NULL")} / " +
                $"Fallback={useMainTextAsExceptionFallback} / " +
                $"StatusController={(statusConditions != null ? statusConditions.name : "NULL")}",
                this
            );
        }
    }

    public void NotifyNaegiUsed()
    {
        fractured = false;

        if (showDetailedDebugLogs)
        {
            Debug.Log("[CaveTutorialDiag][FRACTURE] Naegi使用通知を受信 → 骨折案内を消します。", this);
        }
    }

    public void SetTorchLevel01(float level)
    {
        bool low = Mathf.Clamp01(level) <= 0.5f;

        if (!low)
        {
            torchNoticeDismissed = false;
        }

        torchAtOrBelowHalf = low;
    }

    public void NotifyTorchBrightened()
    {
        torchNoticeDismissed = true;
    }

    private void UpdateExceptionText()
    {
        string message = fractured
            ? fractureMessage
            : (
                torchAtOrBelowHalf && !torchNoticeDismissed
                    ? lowTorchMessage
                    : string.Empty
            );

        bool hasExceptionUi =
            exceptionGroup != null && exceptionText != null;

        if (hasExceptionUi)
        {
            if (exceptionText.text != message)
            {
                exceptionText.text = message;
            }

            targetExceptionAlpha = string.IsNullOrEmpty(message)
                ? 0f
                : 1f;

            if (exceptionUsingMainFallback)
            {
                exceptionUsingMainFallback = false;
                RestoreCurrentStepVisuals();
            }

            return;
        }

        targetExceptionAlpha = 0f;

        if (!useMainTextAsExceptionFallback)
        {
            return;
        }

        bool shouldUseFallback = !string.IsNullOrEmpty(message);

        if (shouldUseFallback)
        {
            exceptionUsingMainFallback = true;
            movementPanel.SetActive(false);
            instructionPanel.SetActive(true);

            if (instructionText.text != message)
            {
                instructionText.text = message;
            }

            return;
        }

        if (exceptionUsingMainFallback)
        {
            exceptionUsingMainFallback = false;
            RestoreCurrentStepVisuals();
        }
    }

    private void RestoreCurrentStepVisuals()
    {
        bool isMove = currentStep == CaveTutorialStep2D.Move;

        movementPanel.SetActive(isMove);
        instructionPanel.SetActive(!isMove);

        if (isMove)
        {
            leftMoveText.text = moveLeftMessage;
            rightMoveText.text = moveRightMessage;
            UpdateMoveText();
        }
        else
        {
            instructionText.text = StepMessage(currentStep);
        }
    }


    private bool HasReached(CaveTutorialStep2D step)
    {
        if (completed)
        {
            return true;
        }

        return (int)currentStep >= (int)step;
    }

    private void LogBootDiagnostics()
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        Debug.Log(
            "[CaveTutorialDiag][SEQUENCE] Move → Jump → Prone → WallClimb → Loot → CollectItem → WallDescend → Inventory → Equip → Reload → ShootEnemy → PickUpAdventurer → ShoulderAdventurer → RescueAdventurer → DropAdventurer → NPC収納確認 → Complete / 地点スキップ=" + allowLocationBasedSkip,
            this
        );

        Debug.Log(
            "[CaveTutorialDiag][BOOT] Manager起動 / " +
            $"Player={(player != null ? player.name : "NULL")} / " +
            $"MainGroup={(mainGroup != null ? mainGroup.name : "NULL")} / " +
            $"InstructionText={(instructionText != null ? instructionText.name : "NULL")} / " +
            $"ExceptionUI={(exceptionGroup != null && exceptionText != null ? "OK" : "NG")} / " +
            $"ExceptionFallback={useMainTextAsExceptionFallback} / " +
            $"StatusController={(statusConditions != null ? statusConditions.name : "NULL")}",
            this
        );

        LogSpotDiagnostic("WallClimb", wallClimbSpot);
        LogSpotDiagnostic("Loot", lootSpot);
        LogSpotDiagnostic("WallDescend", wallDescendSpot);
        LogSpotDiagnostic("Prone", proneSpot);
        LogSpotDiagnostic("Inventory", inventorySpot);
        LogSpotDiagnostic("Adventurer", adventurerSpot);
        LogSpotDiagnostic("Rescue", rescueSpot);
    }

    private void UpdateStageVisibilityDiagnostics(bool canShow)
    {
        if (!showDetailedDebugLogs || completed)
        {
            return;
        }

        if (!stageVisibilityInitialized || canShow != lastStageCanBeShown)
        {
            stageVisibilityInitialized = true;
            lastStageCanBeShown = canShow;

            Debug.Log(
                $"[CaveTutorialDiag][DISPLAY] Step={currentStep} / CanShow={canShow} / " +
                DescribeCurrentSpot(),
                this
            );

            nextHiddenStageDiagnosticTime =
                Time.unscaledTime + Mathf.Max(0.5f, hiddenStepLogInterval);
            return;
        }

        if (!canShow && Time.unscaledTime >= nextHiddenStageDiagnosticTime)
        {
            Debug.LogWarning(
                $"[CaveTutorialDiag][TEXT-HIDDEN] Step={currentStep} のTextが現在非表示です。" +
                DescribeCurrentSpot(),
                this
            );

            nextHiddenStageDiagnosticTime =
                Time.unscaledTime + Mathf.Max(0.5f, hiddenStepLogInterval);
        }
    }

    private static bool StepNeedsSpot(CaveTutorialStep2D step)
    {
        return step == CaveTutorialStep2D.WallClimb ||
               step == CaveTutorialStep2D.Loot ||
               step == CaveTutorialStep2D.WallDescend ||
               step == CaveTutorialStep2D.Prone ||
               step == CaveTutorialStep2D.Inventory ||
               step == CaveTutorialStep2D.PickUpAdventurer;
    }

    private TutorialSpot GetCurrentSpot()
    {
        switch (currentStep)
        {
            case CaveTutorialStep2D.WallClimb:
                return wallClimbSpot;
            case CaveTutorialStep2D.Loot:
                return lootSpot;
            case CaveTutorialStep2D.WallDescend:
                return wallDescendSpot;
            case CaveTutorialStep2D.Prone:
                return proneSpot;
            case CaveTutorialStep2D.Inventory:
                return inventorySpot;
            case CaveTutorialStep2D.PickUpAdventurer:
                return adventurerSpot;
            case CaveTutorialStep2D.RescueAdventurer:
            case CaveTutorialStep2D.DropAdventurer:
                return rescueSpot;
            default:
                return null;
        }
    }

    private string DescribeCurrentSpot()
    {
        if (!StepNeedsSpot(currentStep) &&
            currentStep != CaveTutorialStep2D.RescueAdventurer &&
            currentStep != CaveTutorialStep2D.DropAdventurer)
        {
            return "Spot不要";
        }

        TutorialSpot spot = GetCurrentSpot();

        if (spot == null)
        {
            return "Spot=NULL";
        }

        if (spot.point == null)
        {
            return $"Spot Point=NULL / Radius={spot.radius:0.00}";
        }

        if (player == null)
        {
            return $"Spot={spot.point.name} / Player=NULL / Radius={spot.radius:0.00}";
        }

        float distance = Vector2.Distance(
            player.position,
            spot.point.position
        );

        return $"Spot={spot.point.name} / Distance={distance:0.00} / Radius={spot.radius:0.00} / Inside={distance <= spot.radius}";
    }

    private void LogSpotDiagnostic(string label, TutorialSpot spot)
    {
        if (spot == null || spot.point == null)
        {
            Debug.LogWarning(
                $"[CaveTutorialDiag][SPOT] {label}: Point=NULL",
                this
            );
            return;
        }

        Debug.Log(
            $"[CaveTutorialDiag][SPOT] {label}: Point={spot.point.name} / Radius={spot.radius:0.00} / Position={spot.point.position}",
            this
        );
    }

    private void LogRejectedCompletion(
        CaveTutorialStep2D expected,
        CaveTutorialStep2D next,
        string reason)
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        string signature =
            $"{expected}>{next}|{currentStep}|{transitioning}|{completed}|{reason}";

        if (signature == lastRejectedCompletionSignature &&
            Time.unscaledTime < nextRejectedCompletionLogTime)
        {
            return;
        }

        lastRejectedCompletionSignature = signature;
        nextRejectedCompletionLogTime = Time.unscaledTime + 1f;

        Debug.LogWarning(
            $"[CaveTutorialDiag][COMPLETE-NG] Expected={expected} → {next} / " +
            $"Current={currentStep} / Transitioning={transitioning} / Completed={completed} / Reason={reason}",
            this
        );
    }

    [ContextMenu("Log Tutorial Manager Diagnostics Now")]
    private void LogTutorialManagerDiagnosticsNow()
    {
        Debug.Log(
            $"[CaveTutorialDiag][MANAGER-NOW] Step={currentStep} / Transitioning={transitioning} / " +
            $"Completed={completed} / MainAlpha={(mainGroup != null ? mainGroup.alpha.ToString("0.00") : "NULL")} / " +
            $"CanShow={StageCanBeShown()} / {DescribeCurrentSpot()} / Fractured={fractured} / " +
            $"ExceptionUI={(exceptionGroup != null && exceptionText != null ? "OK" : "NG")}",
            this
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawSpot(wallClimbSpot, Color.yellow);
        DrawSpot(lootSpot, Color.cyan);
        DrawSpot(wallDescendSpot, Color.yellow);
        DrawSpot(proneSpot, Color.white);
        DrawSpot(inventorySpot, Color.cyan);
        DrawSpot(adventurerSpot, Color.green);
        DrawSpot(rescueSpot, Color.magenta);
    }

    private static void DrawSpot(TutorialSpot spot, Color color)
    {
        if (spot == null || spot.point == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(
            spot.point.position,
            spot.radius
        );
    }
#endif
}
