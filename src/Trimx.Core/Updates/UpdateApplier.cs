namespace Trimx.Core.Updates;

/// <summary>
/// Replaces the installed files with an unpacked package. The running app cannot overwrite itself,
/// so this runs in the new version's exe after the old process has exited.
/// </summary>
/// <remarks>
/// Every top-level item of the install folder is moved into a ".old" folder inside it, the package is copied in,
/// and on any failure the moved items are put back. Items starting with "unins" (the installer's uninstaller) stay in place.
/// </remarks>
public static class UpdateApplier
{
    public const string MainExe = "Trimx.exe";

    /// <summary>Holds the previous version until the next start removes it (see <see cref="CleanUp"/>).</summary>
    public const string OldFolder = ".old";

    private const int Attempts = 20;

    private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Copies <paramref name="packageDirectory"/> over <paramref name="installDirectory"/>. Throws after rolling back if it cannot finish.</summary>
    public static void Apply(string packageDirectory, string installDirectory)
    {
        if (!File.Exists(Path.Combine(packageDirectory, MainExe)))
        {
            throw new InvalidDataException($"{MainExe} がパッケージにありません。");
        }

        var oldDirectory = Path.Combine(installDirectory, OldFolder);
        DeleteDirectory(oldDirectory);
        Directory.CreateDirectory(oldDirectory);

        var originals = Directory.EnumerateFileSystemEntries(installDirectory).Select(Path.GetFileName).OfType<string>().Where(IsMovable).ToList();
        var moved = new List<string>();
        try
        {
            foreach (var name in originals)
            {
                Retry(() => Move(Path.Combine(installDirectory, name), Path.Combine(oldDirectory, name)));
                moved.Add(name);
            }

            Retry(() => CopyDirectory(packageDirectory, installDirectory));
        }
        catch
        {
            RollBack(installDirectory, oldDirectory, originals, moved);
            throw;
        }
    }

    /// <summary>Removes the previous version left behind by <see cref="Apply"/>. Best effort.</summary>
    public static void CleanUp(string installDirectory)
    {
        try
        {
            DeleteDirectory(Path.Combine(installDirectory, OldFolder));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It is only disk space; the next start tries again.
        }
    }

    private static bool IsMovable(string name) =>
        !name.Equals(OldFolder, StringComparison.OrdinalIgnoreCase) && !name.StartsWith("unins", StringComparison.OrdinalIgnoreCase);

    private static void RollBack(string installDirectory, string oldDirectory, List<string> originals, List<string> moved)
    {
        // What is in the install folder now is either a partial copy of the new version or an original that was never moved.
        var untouched = originals.Except(moved).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFileSystemEntries(installDirectory))
        {
            var name = Path.GetFileName(path);
            if (IsMovable(name) && !untouched.Contains(name))
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }

        foreach (var name in moved)
        {
            Move(Path.Combine(oldDirectory, name), Path.Combine(installDirectory, name));
        }
    }

    private static void Move(string from, string to)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(from))
        {
            CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    // Antivirus scanners and Explorer can hold a file for a moment right after the old process exits.
    private static void Retry(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < Attempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(_retryDelay);
            }
        }
    }
}
