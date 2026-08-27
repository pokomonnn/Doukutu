using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class InventoryPanelToggle : MonoBehaviour
{
    [Header("表示・非表示するインベントリUI")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("開閉キー")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("アイテムボックスUI")]
    [Tooltip("設定すると、ItemBox UIを開いている間のTabは箱を閉じる操作になります")]
    [SerializeField] private ItemBoxUIController itemBoxUIController;

    [Header("スキルカードUI")]
    [Tooltip("Inventory内のスキル画面。未設定なら自動検索します。Inventoryを閉じる時にスキル画面も閉じます。")]
    [SerializeField] private SkillCardPanelController skillCardPanelController;

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

        if (itemBoxUIController == null)
        {
            itemBoxUIController =
                FindAnyObjectByType<ItemBoxUIController>(
                    FindObjectsInactive.Include
                );
        }

        if (skillCardPanelController == null)
        {
            skillCardPanelController =
                FindAnyObjectByType<SkillCardPanelController>(
                    FindObjectsInactive.Include
                );
        }

        skillCardPanelController?.ClosePanel();

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey))
        {
            return;
        }

        // 箱を開いている時は、通常インベントリを開かず箱を閉じる
        if (itemBoxUIController != null && itemBoxUIController.IsOpen)
        {
            itemBoxUIController.Close();
            return;
        }

        ToggleInventory();
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

        // Inventoryを開き直した時は、まず通常Inventory画面から開始する。
        // 閉じる時もSkillPanelを残さない。
        skillCardPanelController?.ClosePanel();

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

        skillCardPanelController?.ClosePanel();
        inventoryPanel.SetActive(true);
        PlaySound(openClip, openVolume);
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null || !inventoryPanel.activeSelf)
        {
            return;
        }

        skillCardPanelController?.ClosePanel();
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
