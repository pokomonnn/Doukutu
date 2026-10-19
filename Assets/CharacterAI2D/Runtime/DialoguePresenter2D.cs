using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>DialogueData2D用の独立したuGUI表示。既存の洞窟NPC UIとは別に動作します。</summary>
[DisallowMultipleComponent]
public sealed class DialoguePresenter2D : MonoBehaviour
{
    [Header("UI参照（EditorのテストUI作成で自動割当）")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonTemplate;
    [SerializeField] private Button nextButton;
    [Header("文字送り（Eキー通常会話）")]
    [Tooltip("ONでセリフ本文を1文字ずつ表示します。途中で「次へ」を押すと全文表示します。")]
    [SerializeField] private bool useTypewriter = true;
    [Tooltip("1文字表示する間隔（秒）。0.04～0.07程度がおすすめ。")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.045f;

    public static DialoguePresenter2D Active { get; private set; }
    public bool IsPlaying => currentData != null;

    private DialogueData2D currentData;
    private DialogueActor2D currentActor;
    private DialogueNode2D currentNode;
    private readonly List<Button> activeChoices = new List<Button>();
    private Coroutine revealRoutine;
    private bool textFullyRevealed;
    private int visibleChoiceCount;

    private void Awake()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning("[Dialogue Studio] DialoguePresenter2Dが複数あります。先に有効化された1つだけを使用します。", this);
            enabled = false;
            return;
        }
        Active = this;
        SetPanelVisible(false);
        if (choiceButtonTemplate != null) choiceButtonTemplate.gameObject.SetActive(false);
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(Next);
            nextButton.onClick.AddListener(Next);
        }
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    public bool Play(DialogueData2D data, DialogueActor2D actor)
    {
        if (data == null || IsPlaying || !isActiveAndEnabled) return false;
        if (panel == null || speakerText == null || bodyText == null ||
            choicesRoot == null || choiceButtonTemplate == null || nextButton == null)
        {
            Debug.LogError("[Dialogue Studio] UIの参照が不足しています。テスト用UIを作り直すかInspectorで割り当ててください。", this);
            return false;
        }
        string startId = data.ResolveStartNodeId(actor);
        if (string.IsNullOrEmpty(startId) || data.FindNode(startId) == null)
        {
            Debug.LogWarning("[Dialogue Studio] 条件分岐の開始ノードが見つかりません: " + startId, data);
            return false;
        }
        currentData = data;
        currentActor = actor;
        SetPanelVisible(true);
        ShowNode(startId);
        return true;
    }

    public void Next()
    {
        if (!IsPlaying || currentNode == null) return;
        if (!textFullyRevealed)
        {
            RevealImmediately();
            return;
        }
        if (visibleChoiceCount > 0) return;
        GoTo(currentNode.ResolveNextNodeId(currentActor));
    }

    public void EndDialogue()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
        ClearChoices();
        currentNode = null;
        currentData = null;
        currentActor = null;
        textFullyRevealed = false;
        visibleChoiceCount = 0;
        SetPanelVisible(false);
    }

    private void GoTo(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            EndDialogue();
            return;
        }
        if (currentData == null || currentData.FindNode(nodeId) == null)
        {
            Debug.LogWarning("[Dialogue Studio] 遷移先ノードが見つかりません: " + nodeId, currentData);
            EndDialogue();
            return;
        }
        ShowNode(nodeId);
    }

    private void ShowNode(string nodeId)
    {
        if (currentData == null) return;
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
        ClearChoices();
        currentNode = currentData.FindNode(nodeId);
        if (currentNode == null) { EndDialogue(); return; }
        speakerText.text = string.IsNullOrWhiteSpace(currentNode.SpeakerName)
            ? currentData.DisplayName : currentNode.SpeakerName;
        bodyText.text = currentNode.Body ?? string.Empty;
        // 全文はレイアウト用に設定し、表示文字数のみ0にして一括表示を防ぐ。
        bodyText.maxVisibleCharacters = 0;
        textFullyRevealed = false;
        visibleChoiceCount = 0;
        // 文字送り途中でも「次へ」を押せば全文表示できる。
        nextButton.interactable = true;

        if (currentNode.Choices != null)
        {
            foreach (DialogueChoice2D choice in currentNode.Choices)
            {
                if (choice == null || !DialogueConditions2D.AllMatch(choice.Conditions, currentActor)) continue;
                visibleChoiceCount++;
                DialogueChoice2D captured = choice;
                Button button = Instantiate(choiceButtonTemplate, choicesRoot);
                button.gameObject.SetActive(true);
                button.interactable = false;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = captured.Label;
                button.onClick.AddListener(() => Choose(captured));
                activeChoices.Add(button);
            }
        }
        nextButton.gameObject.SetActive(visibleChoiceCount == 0);
        // STEP4: アイテム/受注/取引の実行は誤発火を防ぐため選択肢クリック限定。
        if (currentData.FindAction(currentNode.OnEnterEventId) == null)
            currentActor?.RaiseDialogueEvent(currentNode.OnEnterEventId);
        else
            Debug.LogWarning("[Dialogue STEP4] アクションは選択肢のイベントIDへ設定してください: " + currentNode.OnEnterEventId, currentActor);
        revealRoutine = StartCoroutine(RevealText());
    }

    private IEnumerator RevealText()
    {
        bodyText.maxVisibleCharacters = 0;
        // TMPのリッチテキストタグ（<color>等）は表示文字数に含まず数える。
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        if (!useTypewriter || secondsPerCharacter <= 0f || total <= 0)
        {
            RevealImmediately();
            yield break;
        }

        // Time.timeScale=0でも通常会話の文字送りが止まらない。
        float progress = 0f;
        while (progress < total)
        {
            progress += Time.unscaledDeltaTime / Mathf.Max(0.005f, secondsPerCharacter);
            bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(progress));
            if (progress < total) yield return null;
        }
        revealRoutine = null;
        textFullyRevealed = true;
        SetButtonsInteractable(true);
    }

    private void RevealImmediately()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
        bodyText.maxVisibleCharacters = int.MaxValue;
        textFullyRevealed = true;
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool value)
    {
        for (int i = 0; i < activeChoices.Count; i++)
            if (activeChoices[i] != null) activeChoices[i].interactable = value;
        // 「次へ」は途中クリックで全文表示するため、表示中は常時押せる状態にする。
        if (nextButton != null) nextButton.interactable = true;
    }

    private void Choose(DialogueChoice2D choice)
    {
        if (!textFullyRevealed || choice == null || !IsPlaying) return;
        DialogueAction2D action = currentData != null ? currentData.FindAction(choice.EventId) : null;
        if (action != null)
        {
            if (!DialogueActionRunner2D.TryExecute(action, currentActor, out string message, out bool closeDialogue))
            {
                // 失敗した際に次のノードへ進まず、同じ選択肢を再試行可能。
                bodyText.text = (currentNode != null ? currentNode.Body : "") +
                                "\n<color=#FFBA93>" + (message ?? "実行できませんでした") + "</color>";
                RevealImmediately();
                return;
            }
            currentActor?.RaiseDialogueEvent(choice.EventId);
            if (closeDialogue) { EndDialogue(); return; }
        }
        else
        {
            currentActor?.RaiseDialogueEvent(choice.EventId);
        }
        GoTo(choice.NextNodeId);
    }

    private void ClearChoices()
    {
        for (int i = 0; i < activeChoices.Count; i++)
            if (activeChoices[i] != null) Destroy(activeChoices[i].gameObject);
        activeChoices.Clear();
    }

    private void OnValidate()
    {
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panel == null) return;
        panel.alpha = visible ? 1f : 0f;
        panel.blocksRaycasts = visible;
        panel.interactable = visible;
    }
}
