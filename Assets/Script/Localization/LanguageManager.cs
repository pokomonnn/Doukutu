using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[DisallowMultipleComponent]
public class LanguageManager : MonoBehaviour
{
    [Header("Locale設定")]
    [Tooltip("作成したJapanese Localeアセットを入れる")]
    [SerializeField] private Locale japaneseLocale;

    [Tooltip("作成したEnglish Localeアセットを入れる")]
    [SerializeField] private Locale englishLocale;

    [Tooltip("初回起動時の言語")]
    [SerializeField] private Locale defaultLocale;

    [Header("言語切替ボタン表示")]
    [SerializeField] private TMP_Text languageButtonText;

    [SerializeField] private string switchToEnglishLabel = "English";
    [SerializeField] private string switchToJapaneseLabel = "日本語";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public static LanguageManager Instance { get; private set; }

    public event Action<Locale> OnLanguageChanged;

    private const string LanguageSaveKey = "SelectedLanguageCode";

    private Coroutine changeLanguageCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale startLocale = GetSavedLocaleOrDefault();

        ApplyLocale(startLocale, true);
    }

    public void ToggleLanguage()
    {
        Locale currentLocale = LocalizationSettings.SelectedLocale;

        Locale nextLocale = IsSameLocale(
            currentLocale,
            japaneseLocale
        )
            ? englishLocale
            : japaneseLocale;

        SetLanguage(nextLocale);
    }

    public void SetJapanese()
    {
        SetLanguage(japaneseLocale);
    }

    public void SetEnglish()
    {
        SetLanguage(englishLocale);
    }

    public void SetLanguage(Locale targetLocale)
    {
        if (targetLocale == null)
        {
            Debug.LogWarning(
                "LanguageManager: 切り替え先のLocaleが設定されていません。",
                this
            );
            return;
        }

        if (changeLanguageCoroutine != null)
        {
            StopCoroutine(changeLanguageCoroutine);
        }

        changeLanguageCoroutine = StartCoroutine(
            SetLanguageRoutine(targetLocale)
        );
    }

    private IEnumerator SetLanguageRoutine(Locale targetLocale)
    {
        yield return LocalizationSettings.InitializationOperation;

        ApplyLocale(targetLocale, true);

        changeLanguageCoroutine = null;
    }

    private Locale GetSavedLocaleOrDefault()
    {
        string savedCode = PlayerPrefs.GetString(
            LanguageSaveKey,
            string.Empty
        );

        if (!string.IsNullOrWhiteSpace(savedCode))
        {
            if (IsLocaleCodeMatch(
                    japaneseLocale,
                    savedCode))
            {
                return japaneseLocale;
            }

            if (IsLocaleCodeMatch(
                    englishLocale,
                    savedCode))
            {
                return englishLocale;
            }
        }

        if (defaultLocale != null)
        {
            return defaultLocale;
        }

        if (japaneseLocale != null)
        {
            return japaneseLocale;
        }

        return englishLocale;
    }

    private void ApplyLocale(
        Locale targetLocale,
        bool saveToPlayerPrefs)
    {
        if (targetLocale == null)
        {
            return;
        }

        LocalizationSettings.SelectedLocale = targetLocale;

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetString(
                LanguageSaveKey,
                targetLocale.Identifier.Code
            );

            PlayerPrefs.Save();
        }

        RefreshLanguageButtonText();

        OnLanguageChanged?.Invoke(targetLocale);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[LanguageManager] 言語切替：" +
                $"{targetLocale.LocaleName} " +
                $"({targetLocale.Identifier.Code})",
                this
            );
        }
    }

    private void RefreshLanguageButtonText()
    {
        if (languageButtonText == null)
        {
            return;
        }

        languageButtonText.text = IsSameLocale(
            LocalizationSettings.SelectedLocale,
            japaneseLocale
        )
            ? switchToEnglishLabel
            : switchToJapaneseLabel;
    }

    private bool IsSameLocale(
        Locale first,
        Locale second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return string.Equals(
            first.Identifier.Code,
            second.Identifier.Code,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private bool IsLocaleCodeMatch(
        Locale locale,
        string localeCode)
    {
        if (locale == null ||
            string.IsNullOrWhiteSpace(localeCode))
        {
            return false;
        }

        return string.Equals(
            locale.Identifier.Code,
            localeCode,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [ContextMenu("Clear Saved Language")]
    public void ClearSavedLanguage()
    {
        PlayerPrefs.DeleteKey(LanguageSaveKey);
        PlayerPrefs.Save();

        Debug.Log(
            "LanguageManager: 保存済み言語を削除しました。",
            this
        );
    }
}