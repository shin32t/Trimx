using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;

namespace Trimx.Services;

/// <summary>
/// Applies the dark or light palette by replacing the brushes in the application resources.
/// The XAML refers to them with DynamicResource, so open windows follow immediately.
/// </summary>
public static class ThemeService
{
    private static readonly Dictionary<string, string> _dark = new()
    {
        ["WindowBackgroundBrush"] = "#1E1E1E",
        ["PanelBackgroundBrush"] = "#252526",
        ["ControlBackgroundBrush"] = "#333337",
        ["BorderBrush"] = "#3F3F46",
        ["ForegroundBrush"] = "#E0E0E0",
        ["SecondaryForegroundBrush"] = "#9A9A9A",
        ["HoverBrush"] = "#3E3E42",
        ["PressedBrush"] = "#505055",
        ["SelectedBrush"] = "#094771",
        ["AccentBrush"] = "#3A7BD5",
        ["ErrorBrush"] = "#FF6B6B",
        ["ScrollThumbBrush"] = "#5A5A5E",
        ["ScrollThumbHoverBrush"] = "#7A7A7E",
        ["ScrollThumbDragBrush"] = "#9A9A9E",
    };

    private static readonly Dictionary<string, string> _light = new()
    {
        ["WindowBackgroundBrush"] = "#F3F3F3",
        ["PanelBackgroundBrush"] = "#FFFFFF",
        ["ControlBackgroundBrush"] = "#E9E9EC",
        ["BorderBrush"] = "#D0D0D5",
        ["ForegroundBrush"] = "#1F1F1F",
        ["SecondaryForegroundBrush"] = "#6B6B6B",
        ["HoverBrush"] = "#DDDDE2",
        ["PressedBrush"] = "#C8C8CE",
        ["SelectedBrush"] = "#CCE4F7",
        ["AccentBrush"] = "#3A7BD5",
        ["ErrorBrush"] = "#D13438",
        ["ScrollThumbBrush"] = "#B4B4B8",
        ["ScrollThumbHoverBrush"] = "#9A9AA0",
        ["ScrollThumbDragBrush"] = "#7E7E85",
    };

    private static AppTheme _mode = AppTheme.Dark;
    private static bool _isHooked;

    /// <summary>Raised after the palette changed, for drawing that does not use WPF brushes (the canvas).</summary>
    public static event EventHandler? Changed;

    /// <summary>The color behind the document on the canvas.</summary>
    public static SKColor CanvasBackground { get; private set; } = new SKColor(0x1E, 0x1E, 0x1E);

    public static AppTheme Parse(string? text)
    {
        return Enum.TryParse<AppTheme>(text, ignoreCase: true, out var theme) && Enum.IsDefined(theme) ? theme : AppTheme.System;
    }

    public static void Apply(AppTheme mode)
    {
        _mode = mode;
        if (!_isHooked)
        {
            _isHooked = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        var palette = IsLight(mode) ? _light : _dark;
        var resources = Application.Current.Resources;
        foreach (var (key, hex) in palette)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            resources[key] = brush;
        }

        CanvasBackground = SKColor.Parse(palette["WindowBackgroundBrush"]);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static bool IsLight(AppTheme mode)
    {
        return mode switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is 1,
        };
    }

    // Raised on a system thread; only the "follow Windows" mode cares.
    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode == AppTheme.System && e.Category == UserPreferenceCategory.General)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        }
    }
}
