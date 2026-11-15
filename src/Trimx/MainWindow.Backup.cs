using System.IO;
using System.Windows;
using System.Windows.Threading;
using Trimx.Core.Services;
using Trimx.Services;
using Trimx.UI;
using Trimx.Core.Localization;

namespace Trimx;

// Backup and restore of open tabs (see BackupService).
public partial class MainWindow
{
    // Wait this long after the last change before writing, so a drag or typing burst writes once.
    private static readonly TimeSpan _backupDelay = TimeSpan.FromSeconds(15);

    private DispatcherTimer? _backupTimer;

    /// <summary>Asks for a backup soon. Every change, tab open and tab close calls this.</summary>
    private void ScheduleBackup()
    {
        if (_backupTimer is null)
        {
            _backupTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = _backupDelay };
            _backupTimer.Tick += (_, _) => BackupNow();
        }

        // Restart so the delay counts from the last change.
        _backupTimer.Stop();
        _backupTimer.Start();
    }

    /// <summary>Writes the backup immediately (also the entry point for a restart that skips the close prompts).</summary>
    private void BackupNow(bool includeSavedTabs = false)
    {
        _backupTimer?.Stop();
        BackupService.Save(_tabs, ActiveTab, includeSavedTabs);
    }

    /// <summary>
    /// Reopens the tabs of a previous session that did not end normally. Returns true if a backup was found,
    /// so the caller can skip opening the clipboard image on top of it.
    /// </summary>
    private bool RestoreBackup()
    {
        var session = BackupService.Load();
        if (session is null)
        {
            return false;
        }

        var failed = 0;
        DocumentTab? active = null;
        foreach (var entry in session.Tabs)
        {
            try
            {
                var source = entry.HasBackupFile ? BackupService.PathOf(entry.Id) : entry.FilePath ?? throw new InvalidDataException();
                var tab = AddTab(ProjectFileService.Load(source));
                tab.Title = entry.Title;
                tab.FilePath = entry.FilePath;
                tab.BackupId = entry.Id; // Reuses the file, which is current.
                tab.BackupDirty = false;
                if (entry.IsModified)
                {
                    tab.History.MarkModified();
                    tab.BackupDirty = false; // MarkModified raised Changed.
                }

                if (entry.Id == session.ActiveId)
                {
                    active = tab;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OutOfMemoryException)
            {
                failed++;
            }
        }

        if (active is not null)
        {
            TabStrip.SelectedItem = active;
        }

        if (failed > 0)
        {
            MessageBox.Show(this, Loc.Get("err.restore", failed), "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return _tabs.Count > 0 || failed > 0;
    }
}
