namespace Trimx.Core.Updates;

/// <summary>A newer release that can be installed.</summary>
/// <param name="Version">The release version (tag without the leading "v").</param>
/// <param name="ZipUrl">Where the portable zip is downloaded from (https).</param>
/// <param name="FileName">The zip's file name.</param>
/// <param name="Size">The size the release declares, in bytes. The download may not exceed it.</param>
/// <param name="Sha256">The expected SHA-256 of the zip, as hex.</param>
public sealed record UpdateInfo(Version Version, Uri ZipUrl, string FileName, long Size, string Sha256);
