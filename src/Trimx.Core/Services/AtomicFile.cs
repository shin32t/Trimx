namespace Trimx.Core.Services;

/// <summary>
/// Writes files so a failure never leaves a half-written file in place of a good one.
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// Writes through a temporary file in the same folder and replaces the target only when the
    /// write has completed, so an existing file survives a failed save.
    /// </summary>
    public static void Write(string path, Action<Stream> write)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(stream);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleanup only; the original error is what matters to the caller.
        }
    }
}
