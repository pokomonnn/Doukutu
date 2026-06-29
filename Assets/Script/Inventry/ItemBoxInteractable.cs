using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(ItemBoxInventory))]
[RequireComponent(typeof(AudioSource))]
public class ItemBoxInteractable : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ItemBoxInventory itemBoxInventory;
    [SerializeField] private ItemBoxUIController itemBoxUIController;

    [Header("操作")]
    [SerializeField] private KeyCode openKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    [Header("開く演出")]
    [Tooltip("Eを押してからItemBoxPanelを表示するまでの秒数")]
    [SerializeField, Min(0f)] private float openDelay = 3f;

    [Tooltip("Eを押した直後に鳴らす、箱を開く音")]
    [SerializeField] private AudioClip openSound;

    [SerializeField, Range(0f, 1f)]
    private float openSoundVolume = 0.9f;

    [Tooltip("0なら常に同じ音量、1なら箱から離れるほど小さくなります")]
    [SerializeField, Range(0f, 1f)]
    private float openSoundSpatialBlend = 0f;

    [SerializeField] private AudioSource audioSource;

    [Header("開く進行ゲージ")]
    [Tooltip("Canvas内のItemBoxOpenProgressUI。未設定なら自動検索します")]
    [SerializeField] private ItemBoxOpenProgressUI openProgressUI;

    [Header("表示")]
    [SerializeField] private TMP_Text openPromptText;

    [Tooltip("GameText の world.open などを設定")]
    [SerializeField]
    private LocalizedString openPromptLabel =
        new LocalizedString();

    [SerializeField]
    private string fallbackOpenPromptLabel = "開ける";

    [SerializeField]
    private Vector3 promptLocalPosition =
        new Vector3(0f, 0.85f, 0f);

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private string localizedOpenPromptLabel = "開ける";
    private bool isPromptLabelSubscribed;
    private bool isOpening;
    private Coroutine openCoroutine;

    private bool IsPlayerInRange => playerColliders.Count > 0;

    private void Awake()
    {
        if (itemBoxInventory == null)
        {
            itemBoxInventory = GetComponent<ItemBoxInventory>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = openSoundSpatialBlend;
        }

        FindItemBoxUIController();
        FindOpenProgressUI();
        FindPromptText();
        ApplyPromptPosition();
        RefreshPrompt();
    }

    private void OnEnable()
    {
        SubscribePromptLabel();
        RefreshPrompt();
    }

    private void OnDisable()
    {
        UnsubscribePromptLabel();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        isOpening = false;
        openProgressUI?.Hide();
    }

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        RefreshPrompt();

        if (!IsPlayerInRange ||
            isOpening ||
            itemBoxInventory == null ||
            !FindItemBoxUIController() ||
            itemBoxUIController.IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(openKey))
        {
            BeginOpen();
        }
    }

    private void BeginOpen()
    {
        if (isOpening || itemBoxInventory == null ||
            !FindItemBoxUIController())
        {
            return;
        }

        isOpening = true;
        RefreshPrompt();
        PlayOpenSound();

        FindOpenProgressUI();
        openProgressUI?.Show(0f);

        openCoroutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        if (openDelay > 0f)
        {
            float elapsedTime = 0f;

            while (elapsedTime < openDelay)
            {
                elapsedTime += Time.deltaTime;

                openProgressUI?.SetProgress(
                    elapsedTime / openDelay
                );

                yield return null;
            }
        }

        openProgressUI?.SetProgress(1f);
        openProgressUI?.Hide();

        openCoroutine = null;
        isOpening = false;

        if (itemBoxInventory != null &&
            FindItemBoxUIController() &&
            !itemBoxUIController.IsOpen)
        {
            itemBoxUIController.Open(itemBoxInventory);
        }

        RefreshPrompt();
    }

    private void PlayOpenSound()
    {
        if (audioSource == null || openSound == null)
        {
            return;
        }

        audioSource.spatialBlend = openSoundSpatialBlend;
        audioSource.PlayOneShot(openSound, openSoundVolume);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerColliders.Add(other);
        RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
        RefreshPrompt();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag) ||
            other.transform.root.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMove>() != null;
    }

    private bool FindItemBoxUIController()
    {
        if (itemBoxUIController != null)
        {
            return true;
        }

        itemBoxUIController =
            FindAnyObjectByType<ItemBoxUIController>(
                FindObjectsInactive.Include
            );

        return itemBoxUIController != null;
    }

    private bool FindOpenProgressUI()
    {
        if (openProgressUI != null)
        {
            return true;
        }

        openProgressUI =
            FindAnyObjectByType<ItemBoxOpenProgressUI>(
                FindObjectsInactive.Include
            );

        return openProgressUI != null;
    }

    private void FindPromptText()
    {
        if (openPromptText != null)
        {
            return;
        }

        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null &&
                text.gameObject.name == "OpenPrompt")
            {
                openPromptText = text;
                break;
            }
        }
    }

    private void RefreshPrompt()
    {
        if (openPromptText == null)
        {
            return;
        }

        bool shouldShow =
            IsPlayerInRange &&
            !isOpening &&
            itemBoxInventory != null &&
            (!FindItemBoxUIController() ||
             !itemBoxUIController.IsOpen);

        if (shouldShow)
        {
            openPromptText.text =
                $"{openKey}:{localizedOpenPromptLabel}";
        }

        openPromptText.enabled = shouldShow;
    }

    private void ApplyPromptPosition()
    {
        if (openPromptText != null)
        {
            openPromptText.transform.localPosition =
                promptLocalPosition;
        }
    }

    private void SubscribePromptLabel()
    {
        if (isPromptLabelSubscribed ||
            openPromptLabel == null)
        {
            return;
        }

        openPromptLabel.StringChanged +=
            HandlePromptLabelChanged;

        openPromptLabel.RefreshString();
        isPromptLabelSubscribed = true;
    }

    private void UnsubscribePromptLabel()
    {
        if (!isPromptLabelSubscribed ||
            openPromptLabel == null)
        {
            return;
        }

        openPromptLabel.StringChanged -=
            HandlePromptLabelChanged;

        isPromptLabelSubscribed = false;
    }

    private void HandlePromptLabelChanged(string localizedText)
    {
        localizedOpenPromptLabel =
            string.IsNullOrWhiteSpace(localizedText)
                ? fallbackOpenPromptLabel
                : localizedText;

        RefreshPrompt();
    }

    private void OnValidate()
    {
        openDelay = Mathf.Max(0f, openDelay);
        openSoundVolume = Mathf.Clamp01(openSoundVolume);
        openSoundSpatialBlend = Mathf.Clamp01(
            openSoundSpatialBlend
        );

        fallbackOpenPromptLabel =
            string.IsNullOrWhiteSpace(fallbackOpenPromptLabel)
                ? "開ける"
                : fallbackOpenPromptLabel;

        FindPromptText();
        ApplyPromptPosition();
    }
}
