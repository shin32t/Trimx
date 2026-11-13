using SkiaSharp;
using Trimx.Core.Documents;

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
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("対応していない画像形式です。");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > ImageLimits.MaxSide || info.Height > ImageLimits.MaxSide)
        {
            throw new InvalidDataException($"画像が大きすぎます ({info.Width} × {info.Height})。一辺 {ImageLimits.MaxSide} px までです。");
        }

        return SKBitmap.Decode(codec) ?? throw new InvalidDataException("画像を読み込めませんでした。");
    }

    public static SKBitmap DecodeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }
}
