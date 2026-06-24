using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class InventoryPanelToggle : MonoBehaviour
{
    [Header("表示・非表示するインベントリUI")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("開閉キー")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("音")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    [SerializeField, Range(0f, 1f)] private float openVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float closeVolume = 0.8f;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // UI音なので、プレイヤーから離れても音量が変わらない設定
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning(
                "InventoryPanelToggle: Inventory Panel が設定されていません。"
            );
            return;
        }

        bool willOpen = !inventoryPanel.activeSelf;

        inventoryPanel.SetActive(willOpen);

        if (willOpen)
        {
            PlaySound(openClip, openVolume);
        }
        else
        {
            PlaySound(closeClip, closeVolume);
        }
    }

    public void OpenInventory()
    {
        if (inventoryPanel == null || inventoryPanel.activeSelf)
        {
            return;
        }

        inventoryPanel.SetActive(true);
        PlaySound(openClip, openVolume);
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null || !inventoryPanel.activeSelf)
        {
            return;
        }

        inventoryPanel.SetActive(false);
        PlaySound(closeClip, closeVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }
}