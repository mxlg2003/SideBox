using System.Globalization;

namespace WinDock.Core;

public enum UiLanguage
{
    System,
    Chinese,
    English
}

/// <summary>Resolves the UI language from settings or Windows, and picks zh/en copy.</summary>
public static class UiLocale
{
    private static UiLanguage _preference = UiLanguage.System;
    private static readonly AsyncLocal<UiLanguage?> Override = new();
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public static event Action? Changed;

    public static UiLanguage Preference => Override.Value ?? _preference;

    public static UiLanguage Resolved
    {
        get
        {
            var preference = Preference;
            if (preference == UiLanguage.Chinese)
            {
                return UiLanguage.Chinese;
            }

            if (preference == UiLanguage.English)
            {
                return UiLanguage.English;
            }

            return IsChineseCulture(CultureInfo.CurrentUICulture) ? UiLanguage.Chinese : UiLanguage.English;
        }
    }

    public static bool IsChinese => Resolved == UiLanguage.Chinese;

    public static CultureInfo Culture => IsChinese ? ChineseCulture : EnglishCulture;

    public static string T(string zh, string en) => IsChinese ? zh : en;

    public static void Apply(UiLanguage preference)
    {
        if (Override.Value is null && _preference == preference)
        {
            return;
        }

        var before = Resolved;
        _preference = preference;
        if (Override.Value is null && before == Resolved)
        {
            return;
        }

        Changed?.Invoke();
    }

    public static IDisposable Push(UiLanguage language)
    {
        var prior = Override.Value;
        Override.Value = language;
        return new Pop(() => Override.Value = prior);
    }

    public static bool IsChineseCulture(CultureInfo culture)
    {
        var name = culture.Name;
        if (string.IsNullOrEmpty(name))
        {
            name = culture.TwoLetterISOLanguageName;
        }

        return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Pop(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
