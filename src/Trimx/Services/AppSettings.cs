namespace Trimx.Services;

/// <summary>
/// Choices the user makes in the settings dialog. Saved as TrimxSetting.json.
/// What the app remembers by itself lives in <see cref="AppState"/> instead.
/// </summary>
public sealed class AppSettings
{
    /// <summary>"System", "Dark" or "Light". Kept as text so a hand-edited typo cannot make the whole file unreadable.</summary>
    public string Theme { get; set; } = nameof(AppTheme.System);

    public const string DefaultTextLabelFallback = "テキスト";

    /// <summary>Text a new text layer starts with.</summary>
    public string DefaultTextLabel { get; set; } = DefaultTextLabelFallback;

    /// <summary>Whether toolbar buttons show their name under the icon.</summary>
    public bool ShowToolbarLabels { get; set; }
}
