using System.IO;
using System.Text.Json;
using Trimx.Core.Services;
using Trimx.UI;

namespace Trimx.Services;

/// <summary>One backed-up tab in <see cref="BackupSession"/>.</summary>
public sealed class BackupEntry
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>The project file the tab was saved to or opened from, if any.</summary>
    public string? FilePath { get; set; }

    public bool IsModified { get; set; }

    /// <summary>
    /// False for a saved, unmodified tab that is reopened from <see cref="FilePath"/> instead of a backup copy
    /// (only recorded when the app restarts on purpose, so the open tabs come back).
    /// </summary>
    public bool HasBackupFile { get; set; } = true;
}

/// <summary>The manifest (session.json) of the backup folder.</summary>
public sealed class BackupSession
{
    public int Version { get; set; } = 1;

    public string? ActiveId { get; set; }

    public List<BackupEntry> Tabs { get; set; } = new();
}

/// <summary>
/// Keeps copies of the open tabs under %LOCALAPPDATA%\Trimx\Backup so work survives a crash or a restart
/// (for example an update). Each tab is one .trimx file named by <see cref="DocumentTab.BackupId"/>, plus session.json.
/// A normal exit calls <see cref="Clear"/>, so a backup that is still there at startup means the last session did not end normally.
/// </summary>
public static class BackupService
{
    private const string ManifestName = "session.json";

    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static string BackupDirectory => Path.Combine(SettingsService.DataDirectory, "Backup");

    public static string PathOf(string id) => Path.Combine(BackupDirectory, id + ProjectFileService.FileExtension);

    /// <summary>
    /// Writes the tabs that need a backup (those with unsaved changes or no file yet) and removes the rest.
    /// Only tabs changed since their last backup are written again.
    /// </summary>
    /// <param name="includeSavedTabs">
    /// True when the app is about to restart on purpose: saved, unmodified tabs are listed too (without a copy), so they reopen.
    /// </param>
    /// <remarks>Failing to back up must never interrupt editing, so I/O errors are swallowed; the tab stays dirty and is retried.</remarks>
    public static void Save(IEnumerable<DocumentTab> tabs, DocumentTab? active, bool includeSavedTabs = false)
    {
        var session = new BackupSession { Version = FormatVersion };
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            foreach (var tab in tabs)
            {
                if (!NeedsBackup(tab))
                {
                    if (includeSavedTabs && tab.FilePath is not null)
                    {
                        session.Tabs.Add(new BackupEntry { Id = tab.BackupId, Title = tab.Title, FilePath = tab.FilePath, HasBackupFile = false });
                        if (ReferenceEquals(tab, active))
                        {
                            session.ActiveId = tab.BackupId;
                        }
                    }

                    continue;
                }

                var path = PathOf(tab.BackupId);
                if (tab.BackupDirty || !File.Exists(path))
                {
                    try
                    {
                        ProjectFileService.Save(tab.Document, path);
                        tab.BackupDirty = false;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
                    {
                        continue;
                    }
                }

                session.Tabs.Add(new BackupEntry
                {
                    Id = tab.BackupId,
                    Title = tab.Title,
                    FilePath = tab.FilePath,
                    IsModified = tab.IsModified,
                });
                if (ReferenceEquals(tab, active))
                {
                    session.ActiveId = tab.BackupId;
                }
            }

            if (session.Tabs.Count == 0)
            {
                Clear();
                return;
            }

            // Manifest last and through a temporary file, so it never lists a tab whose file is missing or half-written.
            var manifestPath = Path.Combine(BackupDirectory, ManifestName);
            var temp = manifestPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(session, _jsonOptions));
            File.Move(temp, manifestPath, overwrite: true);

            var keep = session.Tabs.Where(entry => entry.HasBackupFile).Select(entry => PathOf(entry.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(BackupDirectory, "*" + ProjectFileService.FileExtension))
            {
                if (!keep.Contains(file))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // See remarks: a backup is a convenience.
        }
    }

    /// <summary>Reads the manifest of a previous session, or null if there is none (or it cannot be read).</summary>
    public static BackupSession? Load()
    {
        try
        {
            var manifestPath = Path.Combine(BackupDirectory, ManifestName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using var stream = File.OpenRead(manifestPath);
            var session = JsonSerializer.Deserialize<BackupSession>(stream);
            return session is { Version: FormatVersion, Tabs.Count: > 0 } ? session : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Deletes every backup. Called on a normal exit, and when nothing needs backing up.</summary>
    public static void Clear()
    {
        try
        {
            if (Directory.Exists(BackupDirectory))
            {
                Directory.Delete(BackupDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover backup is only restored once more; not worth failing the exit.
        }
    }

    /// <summary>
    /// Deletes the files this app wrote to the backup folder (the per-tab .trimx files named by a <see cref="DocumentTab.BackupId"/>
    /// and session.json) and returns how many bytes that freed. Anything else in the folder is left alone,
    /// and the folder is removed only if it ends up empty. Unlike <see cref="Clear"/>, this is for the user's "delete temporary files".
    /// </summary>
    public static long ClearFiles()
    {
        long freed = 0;
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(BackupDirectory))
            {
                if (!IsOwnFile(Path.GetFileName(file)))
                {
                    continue;
                }

                try
                {
                    var length = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // In use or locked: leave it; it is only disk space.
                }
            }

            if (!Directory.EnumerateFileSystemEntries(BackupDirectory).Any())
            {
                Directory.Delete(BackupDirectory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reporting what was freed so far is enough.
        }

        return freed;
    }

    private static bool IsOwnFile(string name)
    {
        if (name.Equals(ManifestName, StringComparison.OrdinalIgnoreCase) || name.Equals(ManifestName + ".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.EndsWith(ProjectFileService.FileExtension, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _);
    }

    private static bool NeedsBackup(DocumentTab tab) => tab.IsModified || tab.FilePath is null;
}
