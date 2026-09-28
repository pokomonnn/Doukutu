using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("最初の生成位置")]
    [SerializeField] private Transform firstSpawnPoint;

    [Header("Player設定")]
    [SerializeField] private string playerTag = "Player";

    [Header("出撃地点連携")]
    [Tooltip("町の出撃パネルで選択した出撃地点から開始した時のログを表示します。")]
    [InspectorName("出撃地点スポーンのログ表示")]
    [SerializeField] private bool showExpeditionEntrySpawnLogs = true;

    [Header("死亡BGM")]
    [SerializeField] private AudioSource deathBgmSource;
    [SerializeField] private AudioClip deathBgmClip;

    [SerializeField, Range(0f, 1f)]
    private float deathBgmVolume = 0.8f;

    [Tooltip("オンなら死亡BGMをループ再生します")]
    [SerializeField] private bool loopDeathBgm = false;

    [Header("ゲームオーバー演出")]
    [Tooltip("死亡してからゲームを停止・UI表示するまでの待機時間")]
    [SerializeField, Min(0f)] private float gameOverDelay = 1f;

    [Header("リトライ音")]
    [SerializeField] private AudioSource retrySoundSource;
    [SerializeField] private AudioClip retryClickClip;

    [SerializeField, Range(0f, 1f)]
    private float retryClickVolume = 0.8f;

    [Header("リトライ時の画面フェード")]
    [Tooltip("Canvas直下に置く、画面全体を覆う黒いPanel")]
    [SerializeField] private GameObject retryFadePanel;

    [Tooltip("RetryFadePanelに付けるCanvasGroup")]
    [SerializeField] private CanvasGroup retryFadeCanvasGroup;

    [Tooltip("黒画面になるまでの秒数")]
    [SerializeField, Min(0.01f)]
    private float retryFadeDuration = 0.35f;

    [Tooltip("Retryを押してからシーンを再読み込みするまでの合計秒数")]
    [SerializeField, Min(0.01f)]
    private float retryRestartDelay = 1f;

    private bool isGameOver;
    private bool isRestarting;

    private Coroutine gameOverCoroutine;
    private Coroutine restartCoroutine;

    // シーンを再読み込みしても、同じプレイ中は保持されるチェックポイント情報
    private static bool hasCheckpoint;
    private static int currentCheckpointNumber;
    private static Vector3 currentSpawnPosition;
    private static int checkpointSceneBuildIndex = -1;
    private static string checkpointSceneName = string.Empty;

    private void Awake()
    {
        Time.timeScale = 1f;

        isGameOver = false;
        isRestarting = false;

        SetupDeathBgmSource();
        SetupRetrySoundSource();
        SetupRetryFadePanel();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        Scene activeScene = SceneManager.GetActiveScene();

        bool checkpointMatchesActiveScene =
            !string.IsNullOrWhiteSpace(checkpointSceneName)
                ? string.Equals(
                    checkpointSceneName,
                    activeScene.name,
                    System.StringComparison.OrdinalIgnoreCase
                )
                : checkpointSceneBuildIndex == activeScene.buildIndex;

        if (hasCheckpoint && !checkpointMatchesActiveScene)
        {
            ClearCheckpoint();
        }
    }

    private void Start()
    {
        StartCoroutine(MovePlayerToSpawnPoint());
    }

    public void SetCheckpoint(int checkpointNumber, Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("チェックポイントのSpawnPointが設定されていません。");
            return;
        }

        if (hasCheckpoint && checkpointNumber <= currentCheckpointNumber)
        {
            return;
        }

        hasCheckpoint = true;
        currentCheckpointNumber = checkpointNumber;
        currentSpawnPosition = spawnPoint.position;
        checkpointSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        checkpointSceneName = SceneManager.GetActiveScene().name;

        Debug.Log($"チェックポイント更新：{checkpointNumber}");
    }

    private IEnumerator MovePlayerToSpawnPoint()
    {
        yield return null;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            Debug.LogWarning($"Tagが「{playerTag}」のプレイヤーが見つかりません。");
            yield break;
        }

        Vector3 spawnPosition;
        string spawnSource;

        if (hasCheckpoint)
        {
            spawnPosition = currentSpawnPosition;
            spawnSource = $"Checkpoint {currentCheckpointNumber}";
        }
        else if (TryGetSelectedExpeditionSpawnPosition(
                     out Vector3 selectedEntryPosition,
                     out string selectedEntryDescription))
        {
            spawnPosition = selectedEntryPosition;
            spawnSource = selectedEntryDescription;
        }
        else if (firstSpawnPoint != null)
        {
            spawnPosition = firstSpawnPoint.position;
            spawnSource = "First Spawn Point";
        }
        else
        {
            yield break;
        }

        if (showExpeditionEntrySpawnLogs)
        {
            Debug.Log(
                $"[GameManager] Player生成位置を決定：" +
                $"{spawnSource} / Position={spawnPosition}",
                this
            );
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            player.transform.position = spawnPosition;
        }

        // FallDamage.Start()はGameManagerの移動より先に実行される場合があります。
        // その状態で高い初期位置→低いチェックポイントへワープすると、
        // ワープ距離を「落下距離」と誤認して骨折するため、ここで計測をリセットします。
        FallDamage fallDamage = player.GetComponent<FallDamage>();

        if (fallDamage == null)
        {
            fallDamage =
                player.GetComponentInChildren<FallDamage>(true);
        }

        fallDamage?.ResetAfterTeleport();
    }

    private bool TryGetSelectedExpeditionSpawnPosition(
        out Vector3 spawnPosition,
        out string description)
    {
        spawnPosition = Vector3.zero;
        description = string.Empty;

        if (!ExpeditionEntrySelectionSession.TryGetSelection(
                out string selectedStageId,
                out string selectedEntryPointId,
                out string selectedDisplayName))
        {
            return false;
        }

        StageExpeditionSpawnPoint[] spawnPoints =
            FindObjectsByType<StageExpeditionSpawnPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        StageExpeditionSpawnPoint matchedPoint = null;
        int matchCount = 0;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            StageExpeditionSpawnPoint candidate =
                spawnPoints[i];

            if (candidate == null ||
                !candidate.Matches(
                    selectedStageId,
                    selectedEntryPointId))
            {
                continue;
            }

            matchCount++;

            if (matchedPoint == null)
            {
                matchedPoint = candidate;
            }
        }

        if (matchedPoint == null)
        {
            Debug.LogWarning(
                "[GameManager] " +
                $"選択された出撃地点に一致するStageExpeditionSpawnPointが見つかりません。" +
                $" StageId={selectedStageId} / EntryPointId={selectedEntryPointId}。" +
                " First Spawn Pointへフォールバックします。",
                this
            );
            return false;
        }

        if (matchCount > 1)
        {
            Debug.LogWarning(
                "[GameManager] " +
                $"同じ出撃地点IDのStageExpeditionSpawnPointが{matchCount}個あります。" +
                $" 最初に見つかった『{matchedPoint.name}』を使用します。" +
                $" StageId={selectedStageId} / EntryPointId={selectedEntryPointId}",
                this
            );
        }

        spawnPosition = matchedPoint.SpawnPosition;

        string resolvedName =
            string.IsNullOrWhiteSpace(selectedDisplayName)
                ? selectedEntryPointId
                : selectedDisplayName;

        description =
            $"出撃地点 {resolvedName} " +
            $"({selectedStageId}/{selectedEntryPointId})";

        return true;
    }

    // PlayerDeathHandler の On Player Died から呼ぶ
    public void HandlePlayerDied()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        // プレイヤー本体はPlayerDeathHandlerで即座に停止済み
        gameOverCoroutine = StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        yield return new WaitForSecondsRealtime(gameOverDelay);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        PlayDeathBgm();

        Time.timeScale = 0f;

        gameOverCoroutine = null;
    }

    // RetryButton の OnClick から呼ぶ
    public void RestartGame()
    {
        if (isRestarting)
        {
            return;
        }

        isRestarting = true;

        if (gameOverCoroutine != null)
        {
            StopCoroutine(gameOverCoroutine);
            gameOverCoroutine = null;
        }

        restartCoroutine = StartCoroutine(RestartSequence());
    }

    private IEnumerator RestartSequence()
    {
        PlayRetrySound();

        if (retryFadePanel != null)
        {
            retryFadePanel.SetActive(true);
        }

        float fadeDuration = Mathf.Max(0.01f, retryFadeDuration);

        if (retryFadeCanvasGroup != null)
        {
            retryFadeCanvasGroup.alpha = 0f;
            retryFadeCanvasGroup.blocksRaycasts = true;
            retryFadeCanvasGroup.interactable = false;

            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;

                retryFadeCanvasGroup.alpha = Mathf.Clamp01(
                    elapsedTime / fadeDuration
                );

                yield return null;
            }

            retryFadeCanvasGroup.alpha = 1f;
        }
        else
        {
            yield return new WaitForSecondsRealtime(fadeDuration);
        }

        float remainingTime = Mathf.Max(
            0f,
            retryRestartDelay - fadeDuration
        );

        if (remainingTime > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingTime);
        }

        Time.timeScale = 1f;

        if (deathBgmSource != null)
        {
            deathBgmSource.Stop();
        }

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void ClearCheckpoint()
    {
        hasCheckpoint = false;
        currentCheckpointNumber = 0;
        currentSpawnPosition = Vector3.zero;
        checkpointSceneBuildIndex = -1;
        checkpointSceneName = string.Empty;
    }


    public static SavedCheckpointData CreateCheckpointSaveData()
    {
        return new SavedCheckpointData
        {
            HasCheckpoint = hasCheckpoint,
            CheckpointNumber = currentCheckpointNumber,
            SceneName = checkpointSceneName,
            SceneBuildIndex = checkpointSceneBuildIndex,
            PositionX = currentSpawnPosition.x,
            PositionY = currentSpawnPosition.y,
            PositionZ = currentSpawnPosition.z
        };
    }

    public static void ApplyCheckpointSaveData(SavedCheckpointData data)
    {
        if (data == null || !data.HasCheckpoint)
        {
            hasCheckpoint = false;
            currentCheckpointNumber = 0;
            currentSpawnPosition = Vector3.zero;
            checkpointSceneBuildIndex = -1;
            checkpointSceneName = string.Empty;
            return;
        }

        hasCheckpoint = true;
        currentCheckpointNumber = Mathf.Max(0, data.CheckpointNumber);
        currentSpawnPosition = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
        checkpointSceneBuildIndex = data.SceneBuildIndex;
        checkpointSceneName = data.SceneName?.Trim() ?? string.Empty;
    }

    private void SetupDeathBgmSource()
    {
        if (deathBgmSource == null)
        {
            deathBgmSource = GetComponent<AudioSource>();
        }

        if (deathBgmSource == null)
        {
            return;
        }

        deathBgmSource.playOnAwake = false;
        deathBgmSource.spatialBlend = 0f;
        deathBgmSource.loop = loopDeathBgm;
        deathBgmSource.ignoreListenerPause = true;
    }

    private void SetupRetrySoundSource()
    {
        if (retrySoundSource == null)
        {
            retrySoundSource = deathBgmSource;
        }

        if (retrySoundSource == null)
        {
            retrySoundSource = GetComponent<AudioSource>();
        }

        if (retrySoundSource == null)
        {
            return;
        }

        retrySoundSource.playOnAwake = false;
        retrySoundSource.spatialBlend = 0f;
        retrySoundSource.ignoreListenerPause = true;
    }

    private void SetupRetryFadePanel()
    {
        if (retryFadePanel == null &&
            retryFadeCanvasGroup != null)
        {
            retryFadePanel = retryFadeCanvasGroup.gameObject;
        }

        if (retryFadeCanvasGroup == null &&
            retryFadePanel != null)
        {
            retryFadeCanvasGroup =
                retryFadePanel.GetComponent<CanvasGroup>();
        }

        if (retryFadeCanvasGroup != null)
        {
            retryFadeCanvasGroup.alpha = 0f;
            retryFadeCanvasGroup.blocksRaycasts = true;
            retryFadeCanvasGroup.interactable = false;
        }

        if (retryFadePanel != null)
        {
            retryFadePanel.SetActive(false);
        }
    }

    private void PlayDeathBgm()
    {
        if (deathBgmSource == null || deathBgmClip == null)
        {
            return;
        }

        deathBgmSource.Stop();

        deathBgmSource.clip = deathBgmClip;
        deathBgmSource.volume = deathBgmVolume;
        deathBgmSource.loop = loopDeathBgm;
        deathBgmSource.Play();
    }

    private void PlayRetrySound()
    {
        if (retrySoundSource == null || retryClickClip == null)
        {
            return;
        }

        retrySoundSource.PlayOneShot(
            retryClickClip,
            retryClickVolume
        );
    }

    private void OnValidate()
    {
        gameOverDelay = Mathf.Max(0f, gameOverDelay);

        retryFadeDuration = Mathf.Max(0.01f, retryFadeDuration);

        retryRestartDelay = Mathf.Max(
            retryFadeDuration,
            retryRestartDelay
        );
    }
}