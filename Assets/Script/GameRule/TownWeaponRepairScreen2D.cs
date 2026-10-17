using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>町の新しい修理画面の開閉アニメ。修理ロジックは既存Controllerに任せる。</summary>
[DisallowMultipleComponent]
public sealed class TownWeaponRepairScreen2D : MonoBehaviour
{
    [SerializeField] private PawnShopUIController pawnShop;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform window;
    [SerializeField] private CanvasGroup windowGroup;
    [SerializeField, Min(0f)] private float openDuration = 0.2f;
    [SerializeField, Min(0f)] private float closeDuration = 0.25f;
    [Header("修理画面の効果音")]
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.85f;

    private Coroutine anim;
    private bool closing;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (window != null) baseScale = window.localScale;
        if (closeButton != null) closeButton.onClick.AddListener(CloseAnimated);
    }
    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseAnimated);
    }
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        transform.SetAsLastSibling();
        closing = false;
        Play(openSound);
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(OpenRoutine());
    }
    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (anim != null) { StopCoroutine(anim); anim = null; }
        ResetVisuals();
    }
    private IEnumerator OpenRoutine()
    {
        float duration = Mathf.Max(0.001f, openDuration), elapsed = 0;
        if (windowGroup != null) { windowGroup.alpha = 0f; windowGroup.interactable = false; windowGroup.blocksRaycasts = false; }
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            if (window != null) window.localScale = baseScale * Mathf.Lerp(0.94f, 1f, t);
            if (windowGroup != null) windowGroup.alpha = t;
            yield return null;
        }
        ResetVisuals(); anim = null;
    }
    private void CloseAnimated()
    {
        if (closing) return;
        closing = true;
        Play(closeSound);
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(CloseRoutine());
    }
    private IEnumerator CloseRoutine()
    {
        if (windowGroup != null) { windowGroup.interactable = false; windowGroup.blocksRaycasts = false; }
        float duration = Mathf.Max(0.001f, closeDuration), elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            if (window != null) window.localScale = baseScale * Mathf.Lerp(1f, 0.94f, t);
            if (windowGroup != null) windowGroup.alpha = 1f - t;
            yield return null;
        }
        anim = null;
        if (pawnShop == null || pawnShop.TryClosePawnShop())
        {
            if (pawnShop == null) gameObject.SetActive(false);
        }
        else { closing = false; ResetVisuals(); }
    }
    private void Play(AudioClip clip)
    {
        if (clip != null && soundSource != null)
            soundSource.PlayOneShot(clip, soundVolume);
    }

    private void ResetVisuals()
    {
        if (window != null) window.localScale = baseScale;
        if (windowGroup != null) { windowGroup.alpha = 1f; windowGroup.interactable = true; windowGroup.blocksRaycasts = true; }
    }
}
