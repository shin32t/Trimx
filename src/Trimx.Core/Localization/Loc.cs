using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Trimx.Core.Localization;

/// <summary>
/// Looks up UI text by key (ID) for the current language. The texts live in embedded JSON files
/// (<c>ja.json</c>, <c>en.json</c>: a flat object of key to text). Does not depend on WPF.
/// </summary>
/// <remarks>
/// A key missing from the current language falls back to Japanese (the source language), then to the key itself,
/// so a forgotten translation shows up as readable text instead of an exception.
/// </remarks>
public static class Loc
{
    public const string Japanese = "ja";

    public const string English = "en";

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new();
    private static readonly object _lock = new();

    private static string _language = Japanese;

    /// <summary>The language in use: <see cref="Japanese"/> or <see cref="English"/>.</summary>
    public static string Language => _language;

    /// <summary>Raised after <see cref="SetLanguage"/> changed the language.</summary>
    public static event EventHandler? Changed;

    /// <summary>All texts of the current language (key to text), for views that bind to them.</summary>
    public static IReadOnlyDictionary<string, string> Strings => Load(_language);

    /// <summary>The language that matches the OS display language: Japanese for a Japanese OS, English otherwise.</summary>
    public static string GetSystemLanguage() =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Japanese, StringComparison.OrdinalIgnoreCase)
            ? Japanese
            : English;

    /// <summary>Switches the language. Anything other than "ja" is treated as English.</summary>
    public static void SetLanguage(string? language)
    {
        var normalized = string.Equals(language, Japanese, StringComparison.OrdinalIgnoreCase) ? Japanese : English;
        if (normalized == _language)
        {
            return;
        }

        _language = normalized;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        if (Load(_language).TryGetValue(key, out var text))
        {
            return text;
        }

        return Load(Japanese).TryGetValue(key, out var fallback) ? fallback : key;
    }

    /// <summary>Looks up the text and fills its <c>{0}</c>, <c>{1}</c>... placeholders.</summary>
    public static string Get(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(language, out var cached))
            {
                return cached;
            }

            var loaded = Read(language);
            _cache[language] = loaded;
            return loaded;
        }
    }

    private static Dictionary<string, string> Read(string language)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Trimx.Core.Localization.{language}.json");
        if (stream is null)
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    }
}
