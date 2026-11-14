using System.IO.Compression;
using System.Security.Cryptography;

namespace Trimx.Core.Updates;

/// <summary>Downloads an update, verifies its SHA-256, and unpacks it. The zip is treated as untrusted input.</summary>
public static class UpdateDownloader
{
    // Total size of the unpacked files; the real package is about 210 MB.
    private const long MaxUnpackedBytes = 2L << 30;

    /// <summary>
    /// Downloads the zip into <paramref name="workDirectory"/>, checks it, unpacks it, and returns the folder
    /// that holds <see cref="UpdateApplier.MainExe"/>. Throws <see cref="InvalidDataException"/> if the download is not what the release declared.
    /// </summary>
    public static async Task<string> DownloadAsync(
        HttpClient http,
        UpdateInfo info,
        string workDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(workDirectory))
        {
            Directory.Delete(workDirectory, recursive: true);
        }

        Directory.CreateDirectory(workDirectory);
        var zipPath = Path.Combine(workDirectory, "package.zip");
        await DownloadFileAsync(http, info, zipPath, progress, cancellationToken);

        var packageDirectory = Path.Combine(workDirectory, "package");
        Unpack(zipPath, packageDirectory);
        File.Delete(zipPath);
        return FindRoot(packageDirectory);
    }

    private static async Task DownloadFileAsync(HttpClient http, UpdateInfo info, string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(info.ZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > info.Size)
                {
                    throw new InvalidDataException("ダウンロードしたファイルが、公開されているサイズより大きいです。");
                }

                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report((double)total / info.Size);
            }

            if (total != info.Size)
            {
                throw new InvalidDataException("ダウンロードしたファイルのサイズが、公開されているものと違います。");
            }
        }

        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("ダウンロードしたファイルのハッシュ値が、公開されているものと一致しません。");
        }
    }

    private static void Unpack(string zipPath, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var root = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zipPath);

        long declared = 0;
        foreach (var entry in archive.Entries)
        {
            declared += entry.Length;
        }

        if (declared > MaxUnpackedBytes)
        {
            throw new InvalidDataException("展開後のサイズが大きすぎます。");
        }

        long written = 0;
        foreach (var entry in archive.Entries)
        {
            // A name like "..\..\x" would write outside the folder.
            var destination = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("zip に不正なパスが含まれています。");
            }

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = entry.Open();
            using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                if (written > MaxUnpackedBytes)
                {
                    throw new InvalidDataException("展開後のサイズが大きすぎます。");
                }

                target.Write(buffer, 0, read);
            }
        }
    }

    // The zip holds either the files directly or one folder (Trimx\...) around them.
    private static string FindRoot(string packageDirectory)
    {
        if (File.Exists(Path.Combine(packageDirectory, UpdateApplier.MainExe)))
        {
            return packageDirectory;
        }

        var candidates = Directory.GetDirectories(packageDirectory)
            .Where(directory => File.Exists(Path.Combine(directory, UpdateApplier.MainExe)))
            .ToList();
        return candidates.Count == 1
            ? candidates[0]
            : throw new InvalidDataException($"zip に {UpdateApplier.MainExe} が見つかりません。");
    }
}
