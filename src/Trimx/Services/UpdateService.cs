using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Trimx.Core.Updates;

namespace Trimx.Services;

/// <summary>
/// Self-update from GitHub Releases: check, download, then hand over to the new version's exe, which swaps the files
/// after this process exits (<see cref="TryRunApplier"/>). Nothing here needs a server of our own.
/// </summary>
public static class UpdateService
{
    private const string Repository = "shin32t/Trimx";

    private const string ApplyArgument = "--apply-update";

    private static readonly Uri _latestReleaseUrl = new($"https://api.github.com/repos/{Repository}/releases/latest");

    private static readonly HttpClient _http = CreateClient();

    /// <summary>
    /// True while an update is being downloaded or started. The startup (automatic) and the manual flow share
    /// one staging folder, so only one may run, and the temporary files must not be deleted meanwhile.
    /// </summary>
    public static bool IsUpdating { get; private set; }

    /// <summary>Claims the update flow. Returns false if one is already running; call <see cref="EndUpdate"/> when done.</summary>
    public static bool TryBeginUpdate()
    {
        if (IsUpdating)
        {
            return false;
        }

        IsUpdating = true;
        return true;
    }

    public static void EndUpdate() => IsUpdating = false;

    /// <summary>The folder of the running exe.</summary>
    public static string InstallDirectory { get; } = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    private static string WorkDirectory => Path.Combine(SettingsService.DataDirectory, "Update");

    // Kept apart from the log, because a new download clears its folder.
    private static string DownloadDirectory => Path.Combine(WorkDirectory, "download");

    private static string LogPath => Path.Combine(WorkDirectory, "update.log");

    /// <summary>
    /// True if this is an installed copy that can replace itself: it runs as Trimx.exe (not under
    /// "dotnet run", where an update would overwrite the build output) and its folder is writable without elevation.
    /// </summary>
    public static bool CanUpdateThisCopy()
    {
        if (!string.Equals(Path.GetFileName(Environment.ProcessPath), UpdateApplier.MainExe, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var probe = Path.Combine(InstallDirectory, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Returns the newer release, or null if there is none or it cannot be checked.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await UpdateChecker.CheckAsync(_http, _latestReleaseUrl, AppVersion.Current, timeout.Token);
    }

    /// <summary>Downloads and unpacks the update; returns the folder holding the new files.</summary>
    public static Task<string> PrepareAsync(UpdateInfo info, IProgress<double> progress, CancellationToken cancellationToken) =>
        UpdateDownloader.DownloadAsync(_http, info, DownloadDirectory, progress, cancellationToken);

    /// <summary>
    /// Starts the new version's exe in "apply" mode. The caller must exit right away: the helper waits for this process to end.
    /// </summary>
    public static void LaunchApplier(string packageDirectory)
    {
        var start = new ProcessStartInfo(Path.Combine(packageDirectory, UpdateApplier.MainExe))
        {
            UseShellExecute = false,
            // Neither the install folder nor the package, which are about to be replaced / removed.
            WorkingDirectory = SettingsService.DataDirectory,
        };
        start.ArgumentList.Add(ApplyArgument);
        start.ArgumentList.Add("--pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add("--target");
        start.ArgumentList.Add(InstallDirectory);
        Process.Start(start);
    }

    /// <summary>
    /// Runs when this exe was started by <see cref="LaunchApplier"/>: waits for the old process, swaps the files, and starts the app.
    /// Returns false (and does nothing) for a normal start.
    /// </summary>
    public static bool TryRunApplier(string[] args)
    {
        if (!args.Contains(ApplyArgument))
        {
            return false;
        }

        var target = ValueOf(args, "--target");
        var packageDirectory = InstallDirectory;
        // Only ever act on our own staging folder and on a folder that already holds Trimx, so a stray
        // command line cannot move files somewhere else.
        var valid = target is not null
            && File.Exists(Path.Combine(target, UpdateApplier.MainExe))
            && packageDirectory.StartsWith(WorkDirectory, StringComparison.OrdinalIgnoreCase);
        if (valid)
        {
            try
            {
                WaitForExit(ValueOf(args, "--pid"));
                UpdateApplier.Apply(packageDirectory, target!);
                Log($"Updated {target}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Apply has rolled back, so the old version is still in place; it starts below and offers the update again.
                Log($"Update failed: {ex}");
            }

            Process.Start(new ProcessStartInfo(Path.Combine(target!, UpdateApplier.MainExe)) { UseShellExecute = false, WorkingDirectory = target! });
        }

        return true;
    }

    /// <summary>Removes leftovers of a finished update: the staging folder and the previous version's files. Best effort.</summary>
    public static void CleanUp()
    {
        UpdateApplier.CleanUp(InstallDirectory);
        try
        {
            // The log is kept so a failed update can be investigated; only the downloaded package goes.
            if (Directory.Exists(DownloadDirectory))
            {
                Directory.Delete(DownloadDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next start tries again.
        }
    }

    /// <summary>
    /// Removes what updating leaves behind: the downloaded update and the previous version's files (what <see cref="CleanUp"/>
    /// removes at startup), both folders that only this app creates. Returns how many bytes that freed.
    /// </summary>
    public static long ClearTempFiles()
    {
        if (IsUpdating)
        {
            // The staging folder is in use.
            return 0;
        }

        var before = DirectorySize(DownloadDirectory) + DirectorySize(Path.Combine(InstallDirectory, UpdateApplier.OldFolder));
        CleanUp();
        var after = DirectorySize(DownloadDirectory) + DirectorySize(Path.Combine(InstallDirectory, UpdateApplier.OldFolder));
        return Math.Max(0, before - after);
    }

    private static long DirectorySize(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length)
                : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only used to report how much was freed, so an unreadable folder counts as empty.
            return 0;
        }
    }

    private static string? ValueOf(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void WaitForExit(string? pidText)
    {
        if (!int.TryParse(pidText, out var pid))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit(TimeSpan.FromSeconds(30));
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(WorkDirectory);
            File.AppendAllText(LogPath, $"{DateTime.Now:s} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing log must not stop the update.
        }
    }

    private static HttpClient CreateClient()
    {
        // The timeout is per request; a big download is limited by the caller's cancellation instead.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Trimx-Updater", AppVersion.Text));
        return client;
    }
}
