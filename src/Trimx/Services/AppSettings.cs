using Trimx.Core.Localization;

namespace Trimx.Services;

/// <summary>
/// Choices the user makes in the settings dialog. Saved as TrimxSetting.json.
/// What the app remembers by itself lives in <see cref="AppState"/> instead.
/// </summary>
public sealed class AppSettings
{
    /// <summary>"System", "Dark" or "Light". Kept as text so a hand-edited typo cannot make the whole file unreadable.</summary>
    public string Theme { get; set; } = nameof(AppTheme.System);

    /// <summary>
    /// "System", "ja" or "en". Kept as text for the same reason as <see cref="Theme"/>.
    /// A hand-edited <c>null</c> is possible, so read it through <see cref="LanguageOrSystem"/>.
    /// </summary>
    public string? Language { get; set; } = LocalizationService.System;

    /// <summary><see cref="Language"/>, with a missing value meaning "System".</summary>
    public string LanguageOrSystem => string.IsNullOrEmpty(Language) ? LocalizationService.System : Language;

    /// <summary>
    /// Text a new text layer starts with. Empty (or missing) means the default of the current language
    /// (see <see cref="GetDefaultTextLabel"/>), so the default follows a language change.
    /// </summary>
    public string? DefaultTextLabel { get; set; } = string.Empty;

    /// <summary>The initial text for a new text layer: the user's own text, or the language default.</summary>
    public string GetDefaultTextLabel()
    {
        // Older versions saved the Japanese default itself, which counts as "not customized".
        return string.IsNullOrEmpty(DefaultTextLabel) || DefaultTextLabel == "テキスト" ? Loc.Get("common.text") : DefaultTextLabel;
    }

    /// <summary>Whether toolbar buttons show their name under the icon.</summary>
    public bool ShowToolbarLabels { get; set; }

    /// <summary>
    /// Whether a newer version found at startup is installed at once (with a progress window) instead of
    /// waiting for the update button to be pressed.
    /// </summary>
    public bool AutoUpdate { get; set; }
}
