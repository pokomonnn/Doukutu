using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class InventorySoundPlayer : MonoBehaviour
{
    [Header("音源")]
    [SerializeField] private AudioSource audioSource;

    [Header("インベントリ操作音")]
    [SerializeField] private AudioClip pickUpClip;
    [SerializeField] private AudioClip rotateClip;
    [SerializeField] private AudioClip placeClip;
    [SerializeField] private AudioClip failedClip;

    [Header("コンテキストメニュー操作音")]
    [SerializeField] private AudioClip informationClip;
    [SerializeField] private AudioClip trashClip;
    [SerializeField] private AudioClip closeClip;

    [Header("コンテキストメニュー開閉音")]
    [SerializeField] private AudioClip contextMenuOpenClip;
    [SerializeField] private AudioClip contextMenuCloseClip;

    [Header("回復アイテムを使用できない時の音")]
    [SerializeField] private AudioClip healthFullClip;

    [SerializeField, Range(0f, 1f)]
    private float healthFullVolume = 0.8f;

    [Header("音量")]
    [SerializeField, Range(0f, 1f)] private float pickUpVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float rotateVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float placeVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float failedVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float informationVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float trashVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float closeVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float contextMenuOpenVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float contextMenuCloseVolume = 0.8f;

    [Header("アイテム使用音")]
    [Tooltip("各回復アイテムの Use Sound を鳴らす時の音量")]
    [SerializeField, Range(0f, 1f)] private float useVolume = 0.9f;

    [Tooltip("オンの場合、インベントリを閉じても回復アイテムの使用音を最後まで再生します。")]
    [SerializeField] private bool keepUseSoundPlayingWhenInventoryCloses = true;

    [Header("ログ診断")]
    [Tooltip("InventorySound診断ログをConsoleへ表示します。原因特定後はOFFにできます。")]
    [SerializeField] private bool showSoundDebugLogs = true;

    [Tooltip("AudioSource、AudioListener、Clip、Hierarchy状態まで詳しく表示します。")]
    [SerializeField] private bool showDetailedSoundDiagnostics = true;

    [Tooltip("各PlayOneShot呼び出し成功時にもログを出します。")]
    [SerializeField] private bool logSuccessfulPlaybackRequests = true;

    public AudioSource AudioSource => audioSource;

    private void Awake()
    {
        EnsureAudioSource();
        ConfigureAudioSource();

        if (showDetailedSoundDiagnostics)
        {
            LogFullDiagnostics("Awake");
        }
    }

    private void OnEnable()
    {
        EnsureAudioSource();

        if (showDetailedSoundDiagnostics)
        {
            LogFullDiagnostics("OnEnable");
        }
    }

    public void PlayPickUp() => Play(pickUpClip, pickUpVolume, "PickUp");
    public void PlayRotate() => Play(rotateClip, rotateVolume, "Rotate");
    public void PlayPlace() => Play(placeClip, placeVolume, "Place");
    public void PlayFailed() => Play(failedClip, failedVolume, "Failed");

    public void PlayUseSound(AudioClip useClip)
    {
        if (keepUseSoundPlayingWhenInventoryCloses)
        {
            PlayDetachedOneShot(useClip, useVolume, "UseSound(Detached)");
            return;
        }

        Play(useClip, useVolume, "UseSound");
    }

    public void PlayInformation() => Play(informationClip, informationVolume, "Information");
    public void PlayTrash() => Play(trashClip, trashVolume, "Trash");
    public void PlayClose() => Play(closeClip, closeVolume, "Close");
    public void PlayHealthFull() => Play(healthFullClip, healthFullVolume, "HealthFull");
    public void PlayContextMenuOpen() => Play(contextMenuOpenClip, contextMenuOpenVolume, "ContextMenuOpen");
    public void PlayContextMenuClose() => Play(contextMenuCloseClip, contextMenuCloseVolume, "ContextMenuClose");

    [ContextMenu("Inventory Sound Diagnostics")]
    public void LogInventorySoundDiagnostics()
    {
        EnsureAudioSource();
        LogFullDiagnostics("ContextMenu");
    }

    [ContextMenu("Test PickUp Sound")]
    public void TestPickUpSound()
    {
        PlayPickUp();
    }

    private void Play(AudioClip clip, float volume, string soundName)
    {
        EnsureAudioSource();

        if (audioSource == null)
        {
            LogWarning($"再生失敗 [{soundName}]：AudioSourceが見つかりません。Object={GetTransformPath(transform)}");
            return;
        }

        if (clip == null)
        {
            LogWarning($"再生失敗 [{soundName}]：AudioClipが未設定です。InventorySoundPlayer={GetTransformPath(transform)}");
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            LogWarning($"再生注意 [{soundName}]：InventorySoundPlayerのGameObjectがHierarchy上で無効です。Object={GetTransformPath(transform)}");
        }

        if (!enabled)
        {
            LogWarning($"再生注意 [{soundName}]：InventorySoundPlayerコンポーネントが無効です。Object={GetTransformPath(transform)}");
        }

        if (!audioSource.enabled)
        {
            LogWarning($"再生失敗 [{soundName}]：AudioSource.enabled=falseです。AudioSource={GetTransformPath(audioSource.transform)}");
            return;
        }

        if (!audioSource.gameObject.activeInHierarchy)
        {
            LogWarning($"再生失敗 [{soundName}]：AudioSourceのGameObjectがHierarchy上で無効です。AudioSource={GetTransformPath(audioSource.transform)}");
            return;
        }

        if (audioSource.mute)
        {
            LogWarning($"再生注意 [{soundName}]：AudioSourceがMuteになっています。AudioSource={GetTransformPath(audioSource.transform)}");
        }

        if (audioSource.volume <= 0.0001f)
        {
            LogWarning($"再生注意 [{soundName}]：AudioSource.volumeが0です。InspectorのVolumeを確認してください。");
        }

        float effectiveRequestVolume = Mathf.Clamp01(volume);

        if (effectiveRequestVolume <= 0.0001f)
        {
            LogWarning($"再生注意 [{soundName}]：{soundName} Volumeが0です。");
        }

        int activeListenerCount = CountActiveAudioListeners();

        if (activeListenerCount == 0)
        {
            LogWarning($"再生注意 [{soundName}]：有効なAudioListenerが見つかりません。Main Cameraなどを確認してください。");
        }
        else if (activeListenerCount > 1)
        {
            LogWarning($"再生注意 [{soundName}]：有効なAudioListenerが{activeListenerCount}個あります。通常は1個だけにしてください。");
        }

        audioSource.PlayOneShot(clip, effectiveRequestVolume);

        if (logSuccessfulPlaybackRequests)
        {
            Log(
                $"PlayOneShot呼び出し [{soundName}] / " +
                $"Clip={clip.name} / ClipLength={clip.length:0.###}s / " +
                $"RequestVolume={effectiveRequestVolume:0.###} / " +
                $"AudioSourceVolume={audioSource.volume:0.###} / Mute={audioSource.mute} / " +
                $"SpatialBlend={audioSource.spatialBlend:0.###} / " +
                $"OutputMixer={(audioSource.outputAudioMixerGroup != null ? audioSource.outputAudioMixerGroup.name : "None")} / " +
                $"ListenerCount={activeListenerCount} / Object={GetTransformPath(audioSource.transform)}"
            );
        }
    }

    private void PlayDetachedOneShot(AudioClip clip, float volume, string soundName)
    {
        if (clip == null)
        {
            LogWarning($"再生失敗 [{soundName}]：AudioClipが未設定です。");
            return;
        }

        int activeListenerCount = CountActiveAudioListeners();
        if (activeListenerCount == 0)
        {
            LogWarning($"再生注意 [{soundName}]：有効なAudioListenerが見つかりません。");
        }

        GameObject soundObject = new GameObject($"ConsumableUseSound_{clip.name}");
        AudioSource oneShotSource = soundObject.AddComponent<AudioSource>();

        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.volume = Mathf.Clamp01(volume);
        oneShotSource.clip = clip;
        oneShotSource.Play();

        if (logSuccessfulPlaybackRequests)
        {
            Log($"Detached再生開始 [{soundName}] / Clip={clip.name} / Volume={oneShotSource.volume:0.###} / ListenerCount={activeListenerCount}");
        }

        Destroy(soundObject, Mathf.Max(0.1f, clip.length));
    }

    private bool EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return true;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            Log("AudioSource参照が未設定だったため、同じGameObjectから自動取得しました。");
            return true;
        }

        LogWarning($"AudioSourceが見つかりません。RequireComponentがあるため通常は発生しません。Object={GetTransformPath(transform)}");
        return false;
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void LogFullDiagnostics(string phase)
    {
        if (!showSoundDebugLogs)
        {
            return;
        }

        int listenerCount = CountActiveAudioListeners();

        if (audioSource == null)
        {
            LogWarning($"診断 [{phase}]：AudioSource=null / InventorySoundPlayer={GetTransformPath(transform)}");
            return;
        }

        Log(
            $"診断 [{phase}] / " +
            $"InventorySoundPlayer={GetTransformPath(transform)} / " +
            $"ComponentEnabled={enabled} / ActiveSelf={gameObject.activeSelf} / ActiveInHierarchy={gameObject.activeInHierarchy} / " +
            $"AudioSource={GetTransformPath(audioSource.transform)} / SourceEnabled={audioSource.enabled} / " +
            $"SourceActive={audioSource.gameObject.activeInHierarchy} / Mute={audioSource.mute} / " +
            $"Volume={audioSource.volume:0.###} / SpatialBlend={audioSource.spatialBlend:0.###} / " +
            $"OutputMixer={(audioSource.outputAudioMixerGroup != null ? audioSource.outputAudioMixerGroup.name : "None")} / " +
            $"ListenerCount={listenerCount} / " +
            $"Clips[PickUp={ClipName(pickUpClip)}, Rotate={ClipName(rotateClip)}, Place={ClipName(placeClip)}, Failed={ClipName(failedClip)}, " +
            $"Info={ClipName(informationClip)}, Trash={ClipName(trashClip)}, Close={ClipName(closeClip)}, " +
            $"ContextOpen={ClipName(contextMenuOpenClip)}, ContextClose={ClipName(contextMenuCloseClip)}, HealthFull={ClipName(healthFullClip)}]"
        );

        if (listenerCount == 0)
        {
            LogWarning("有効なAudioListenerが0個です。Main CameraなどのAudioListenerを確認してください。");
        }
        else if (listenerCount > 1)
        {
            LogWarning($"有効なAudioListenerが{listenerCount}個あります。通常は1個だけにしてください。");
        }
    }

    private static int CountActiveAudioListeners()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        int activeCount = 0;

        foreach (AudioListener listener in listeners)
        {
            if (listener != null &&
                listener.enabled &&
                listener.gameObject.activeInHierarchy)
            {
                activeCount++;
            }
        }

        return activeCount;
    }

    private static string ClipName(AudioClip clip)
    {
        return clip != null ? clip.name : "NULL";
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

    private void Log(string message)
    {
        if (!showSoundDebugLogs || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log($"[InventorySound診断] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showSoundDebugLogs || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning($"[InventorySound診断] {message}", this);
    }

    private void OnValidate()
    {
        pickUpVolume = Mathf.Clamp01(pickUpVolume);
        rotateVolume = Mathf.Clamp01(rotateVolume);
        placeVolume = Mathf.Clamp01(placeVolume);
        failedVolume = Mathf.Clamp01(failedVolume);
        informationVolume = Mathf.Clamp01(informationVolume);
        trashVolume = Mathf.Clamp01(trashVolume);
        closeVolume = Mathf.Clamp01(closeVolume);
        contextMenuOpenVolume = Mathf.Clamp01(contextMenuOpenVolume);
        contextMenuCloseVolume = Mathf.Clamp01(contextMenuCloseVolume);
        healthFullVolume = Mathf.Clamp01(healthFullVolume);
        useVolume = Mathf.Clamp01(useVolume);
    }
}
