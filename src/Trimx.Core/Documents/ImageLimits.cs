namespace Trimx.Core.Documents;

/// <summary>
/// Size limits shared by new-image creation, resizing and file import.
/// </summary>
public static class ImageLimits
{
    /// <summary>
    /// Upper bound per side. Keeps a single RGBA layer at or below 1 GiB, so a typo or a hostile
    /// file cannot exhaust memory.
    /// </summary>
    public const int MaxSide = 16384;
}
