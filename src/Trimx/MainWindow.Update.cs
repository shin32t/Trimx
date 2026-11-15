using System.IO;
using System.Net.Http;
using System.Windows;
using Trimx.Core.Updates;
using Trimx.Services;
using Trimx.UI;
using Trimx.Core.Localization;

namespace Trimx;

// Self-update (see UpdateService): the update button at the right end of the toolbar and the update flow.
public partial class MainWindow
{
    private UpdateInfo? _pendingUpdate;

    // Set while the app quits for an update: the close prompts are skipped (the tabs were backed up) and the backup is kept.
    private bool _closingForUpdate;

    /// <summary>Shows or hides the update button. Debug mode keeps it visible so the UI can be checked without a release.</summary>
    private void ShowUpdateButton(bool updateAvailable)
    {
        UpdateButton.Visibility = updateAvailable || DebugMode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateButton.ToolTip = updateAvailable && _pendingUpdate is { } update
            ? Loc.Get("upd.tip.available", update.Version)
            : Loc.Get("upd.tip.debug");
    }

    /// <summary>
    /// Looks for a newer release in the background after startup. Failures (offline, no release) just mean no button.
    /// </summary>
    private async void CheckForUpdateAsync()
    {
        // A run from the build folder (dotnet run) or a read-only install cannot replace itself, so do not offer it.
        if (!UpdateService.CanUpdateThisCopy())
        {
            return;
        }

        UpdateService.CleanUp();
        var update = await UpdateService.CheckAsync();
        if (update is not null)
        {
            _pendingUpdate = update;
            ShowUpdateButton(updateAvailable: true);

            // The "update automatically" setting: do what the button would do, but without error pop-ups at startup.
            // If it does not work, the button stays for the user to try.
            if (SettingsService.Load().AutoUpdate)
            {
                await RunUpdateAsync(update, silentOnError: true);
            }
        }
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is not { } update)
        {
            MessageBox.Show(this, Loc.Get("upd.msg.debug"), "Trimx", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunUpdateAsync(update, silentOnError: false);
    }

    /// <summary>Downloads the update, backs up the tabs, and restarts into the new version.</summary>
    private async Task RunUpdateAsync(UpdateInfo update, bool silentOnError)
    {
        var dialog = new UpdateProgressWindow { Owner = this };
        var progress = new Progress<double>(dialog.SetProgress);
        dialog.Show();
        IsEnabled = false;

        string packageDirectory;
        try
        {
            packageDirectory = await UpdateService.PrepareAsync(update, progress, dialog.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (!silentOnError)
            {
                MessageBox.Show(this, Loc.Get("err.updatedownload", ex.Message), "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }
        finally
        {
            IsEnabled = true;
            dialog.Close();
        }

        // Keep every open tab (even saved ones) so the restart comes back to the same place.
        BackupNow(includeSavedTabs: true);
        try
        {
            UpdateService.LaunchApplier(packageDirectory);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            if (!silentOnError)
            {
                MessageBox.Show(this, Loc.Get("err.updatestart", ex.Message), "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        _closingForUpdate = true;
        Application.Current.Shutdown();
    }
}
