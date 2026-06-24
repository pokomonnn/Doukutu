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

    [SerializeField, Range(0f, 1f)]
    private float contextMenuOpenVolume = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float contextMenuCloseVolume = 0.8f;

    [Header("アイテム使用音")]
    [Tooltip("各回復アイテムの Use Sound を鳴らす時の音量")]
    [SerializeField, Range(0f, 1f)] private float useVolume = 0.9f;

    

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // UI音なので距離による音量変化を無効化
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    public void PlayPickUp()
    {
        Play(pickUpClip, pickUpVolume);
    }

    public void PlayRotate()
    {
        Play(rotateClip, rotateVolume);
    }

    public void PlayPlace()
    {
        Play(placeClip, placeVolume);
    }

    public void PlayFailed()
    {
        Play(failedClip, failedVolume);
    }

    // ConsumableItemData の Use Sound を再生する
    public void PlayUseSound(AudioClip useClip)
    {
        Play(useClip, useVolume);
    }

    public void PlayInformation()
    {
        Play(informationClip, informationVolume);
    }

    public void PlayTrash()
    {
        Play(trashClip, trashVolume);
    }

    public void PlayClose()
    {
        Play(closeClip, closeVolume);
    }

    public void PlayHealthFull()
    {
        Play(healthFullClip, healthFullVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayContextMenuOpen()
    {
        Play(contextMenuOpenClip, contextMenuOpenVolume);
    }

    public void PlayContextMenuClose()
    {
        Play(contextMenuCloseClip, contextMenuCloseVolume);
    }   
}