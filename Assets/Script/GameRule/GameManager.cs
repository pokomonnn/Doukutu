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

        if (hasCheckpoint &&
            checkpointSceneBuildIndex != SceneManager.GetActiveScene().buildIndex)
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

        if (hasCheckpoint)
        {
            spawnPosition = currentSpawnPosition;
        }
        else if (firstSpawnPoint != null)
        {
            spawnPosition = firstSpawnPoint.position;
        }
        else
        {
            yield break;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = spawnPosition;
        }
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