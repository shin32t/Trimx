using System.Windows;
using System.Windows.Controls;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// App settings. Each change applies at once and is saved to settings.json, so there is no OK / Cancel.
/// </summary>
public partial class SettingsDialog : Window
{
    // Set while the dialog fills its own controls, so that is not treated as the user changing a setting.
    private readonly bool _isLoading = true;

    public SettingsDialog()
    {
        InitializeComponent();

        var settings = SettingsService.Load();
        switch (ThemeService.Parse(settings.Theme))
        {
            case AppTheme.Dark:
                DarkRadio.IsChecked = true;
                break;
            case AppTheme.Light:
                LightRadio.IsChecked = true;
                break;
            default:
                SystemRadio.IsChecked = true;
                break;
        }

        LabelsCheck.IsChecked = settings.ShowToolbarLabels;
        DefaultTextBox.Text = settings.DefaultTextLabel;
        CopyrightText.Text = $"Trimx {GetVersion()}  ©  2026 shin32t";
        _isLoading = false;
    }

    // Version comes from <Version> in Trimx.csproj. The build metadata after '+' (commit hash) is dropped.
    private static string GetVersion()
    {
        var info = typeof(SettingsDialog).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;
        return info?.Split('+')[0] ?? "";
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var theme = DarkRadio.IsChecked == true ? AppTheme.Dark : LightRadio.IsChecked == true ? AppTheme.Light : AppTheme.System;
        ThemeService.Apply(theme);

        // Load again before saving so settings written elsewhere meanwhile (My colors) are kept.
        var settings = SettingsService.Load();
        settings.Theme = theme.ToString();
        SettingsService.Save(settings);
    }

    private void OnResetDrawingClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "ペン・テキスト・図形の、前回の色や太さなどの記憶を消して、初期値に戻します。\nマイカラーと、画像サイズの履歴は残ります。",
            "Trimx",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.OK)
        {
            StateService.ResetDrawingStyles();
        }
    }

    private void OnDefaultTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var settings = SettingsService.Load();
        settings.DefaultTextLabel = DefaultTextBox.Text;
        SettingsService.Save(settings);
    }

    private void OnLabelsClick(object sender, RoutedEventArgs e)
    {
        var show = LabelsCheck.IsChecked == true;
        UiOptions.Instance.ShowToolbarLabels = show;

        var settings = SettingsService.Load();
        settings.ShowToolbarLabels = show;
        SettingsService.Save(settings);
    }
}
