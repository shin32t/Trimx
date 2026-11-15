using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Trimx.Core.Localization;
using Trimx.Core.Updates;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// App settings. Each change applies at once and is saved to settings.json, so there is no OK / Cancel.
/// </summary>
public partial class SettingsDialog : Window
{
    // Set while the dialog fills its own controls, so that is not treated as the user changing a setting.
    private bool _isLoading = true;

    public SettingsDialog()
    {
        InitializeComponent();

        var settings = SettingsService.Load();
        switch (settings.LanguageOrSystem.ToLowerInvariant())
        {
            case Loc.Japanese:
                LangJaRadio.IsChecked = true;
                break;
            case Loc.English:
                LangEnRadio.IsChecked = true;
                break;
            default:
                LangSystemRadio.IsChecked = true;
                break;
        }

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
        AutoUpdateCheck.IsChecked = settings.AutoUpdate;
        DefaultTextBox.Text = settings.GetDefaultTextLabel();
        CopyrightText.Text = $"Trimx {AppVersion.Text}  ©  2026 shin32t";
        _isLoading = false;
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var language = LangJaRadio.IsChecked == true ? Loc.Japanese : LangEnRadio.IsChecked == true ? Loc.English : LocalizationService.System;

        // Load again before saving so settings written elsewhere meanwhile (My colors) are kept.
        var settings = SettingsService.Load();
        settings.Language = language;
        SettingsService.Save(settings);
        LocalizationService.Apply(language);

        // An initial text that was never customized follows the language.
        _isLoading = true;
        DefaultTextBox.Text = settings.GetDefaultTextLabel();
        _isLoading = false;
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
            Loc.Get("settings.reset.confirm"),
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
        // Saved as "not customized" when it is just the language's default, so it keeps following the language.
        settings.DefaultTextLabel = DefaultTextBox.Text == Loc.Get("common.text") ? string.Empty : DefaultTextBox.Text;
        SettingsService.Save(settings);
    }

    private void OnAutoUpdateClick(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Load();
        settings.AutoUpdate = AutoUpdateCheck.IsChecked == true;
        SettingsService.Save(settings);
    }

    private bool _isClosed;

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        base.OnClosed(e);
    }

    /// <summary>Set when the user chose to update after a manual check. The owner starts the update once this dialog is closed.</summary>
    public UpdateInfo? UpdateToApply { get; private set; }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        if (UpdateService.IsUpdating)
        {
            ShowUpdateStatus(Loc.Get("upd.busy"));
            return;
        }

        if (!UpdateService.CanUpdateThisCopy())
        {
            ShowUpdateStatus(Loc.Get("upd.unavailable"));
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        ShowUpdateStatus(Loc.Get("upd.checking"));
        UpdateInfo? update;
        try
        {
            update = await UpdateService.CheckAsync();
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }

        // The dialog can be closed while the check runs. The owner has already read the result by then,
        // so an answer now could not start the update; drop the result instead of asking.
        if (_isClosed)
        {
            return;
        }

        if (update is null)
        {
            // CheckAsync reports every failure as "no update", so this also appears when offline.
            ShowUpdateStatus(Loc.Get("upd.none", AppVersion.Text));
            return;
        }

        ShowUpdateStatus(Loc.Get("upd.found", update.Version));
        var answer = MessageBox.Show(
            this,
            Loc.Get("upd.ask", update.Version, AppVersion.Text),
            "Trimx",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            UpdateToApply = update;
            Close();
        }
    }

    /// <summary>
    /// Raised right after the temporary files were deleted, so the owner can write the backups of open tabs again
    /// at once (this dialog may stay open for a long time).
    /// </summary>
    public event EventHandler? TempFilesDeleted;

    private void OnClearTempClick(object sender, RoutedEventArgs e)
    {
        if (UpdateService.IsUpdating)
        {
            // The update's staging folder is in use; deleting it now would break the download.
            TempStatusText.Text = Loc.Get("upd.busy");
            TempStatusText.Visibility = Visibility.Visible;
            return;
        }

        var answer = MessageBox.Show(this, Loc.Get("settings.temp.confirm"), "Trimx", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        // Only files this app created: tab backups and what an update leaves behind.
        var freed = BackupService.ClearFiles() + UpdateService.ClearTempFiles();
        TempFilesDeleted?.Invoke(this, EventArgs.Empty);

        TempStatusText.Text = freed > 0 ? Loc.Get("settings.temp.done", FormatSize(freed)) : Loc.Get("settings.temp.none");
        TempStatusText.Visibility = Visibility.Visible;
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    private void ShowUpdateStatus(string text)
    {
        UpdateStatusText.Text = text;
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private void OnWebsiteNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default browser: nothing useful to do, and the link is only a convenience.
        }

        e.Handled = true;
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
