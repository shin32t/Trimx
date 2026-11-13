using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Trimx.Services;

/// <summary>
/// Converts Skia bitmaps to WPF image sources.
/// </summary>
public static class BitmapConverter
{
    /// <summary>
    /// Copies a premultiplied BGRA bitmap into a frozen <see cref="BitmapSource"/>.
    /// The pixels are copied, so the bitmap can be disposed afterwards.
    /// </summary>
    public static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        var source = BitmapSource.Create(
            bitmap.Width,
            bitmap.Height,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            bitmap.GetPixels(),
            bitmap.ByteCount,
            bitmap.RowBytes);
        source.Freeze();
        return source;
    }
}
