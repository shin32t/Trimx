using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Localization;

namespace Trimx.Core.Services;

/// <summary>
/// Reads image files (PNG, JPEG, WebP, BMP) into bitmaps, rejecting oversized images up front.
/// </summary>
public static class ImageImportService
{
    /// <summary>
    /// Decodes an image. The caller owns the bitmap.
    /// Throws <see cref="InvalidDataException"/> if the data is not a supported image or is too large.
    /// </summary>
    public static SKBitmap Decode(Stream stream)
    {
        // The size is read from the header first, so a tiny file that claims a huge image
        // cannot make the decoder allocate gigabytes.
        using var data = SKData.Create(stream);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException(Loc.Get("core.img.unsupported"));
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > ImageLimits.MaxSide || info.Height > ImageLimits.MaxSide)
        {
            throw new InvalidDataException(Loc.Get("core.img.toolarge", info.Width, info.Height, ImageLimits.MaxSide));
        }

        return SKBitmap.Decode(codec) ?? throw new InvalidDataException(Loc.Get("core.img.loadfail"));
    }

    public static SKBitmap DecodeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }
}
