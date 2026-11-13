using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Trimx.Services;

public static class ClipboardService
{
    /// <summary>
    /// Returns the clipboard image as an SKBitmap, or null if there is none or retrieval fails.
    /// The caller owns the returned bitmap.
    /// </summary>
    public static SKBitmap? TryGetImage()
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                return null;
            }

            var source = Clipboard.GetImage();
            return source is null ? null : ToSKBitmap(source);
        }
        catch (Exception)
        {
            // Clipboard access can fail (e.g. locked by another process); never block startup.
            return null;
        }
    }

    /// <summary>
    /// Returns the pixel size of the clipboard image without converting its pixels,
    /// or null if there is none or retrieval fails.
    /// </summary>
    public static (int Width, int Height)? TryGetImageSize()
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                return null;
            }

            var source = Clipboard.GetImage();
            return source is null ? null : (source.PixelWidth, source.PixelHeight);
        }
        catch (Exception)
        {
            // The size is only a default for the new-image dialog; ignore clipboard failures.
            return null;
        }
    }

    /// <summary>
    /// Puts the bitmap on the clipboard. Returns false if the clipboard could not be written.
    /// </summary>
    /// <remarks>
    /// Both a bitmap and a "PNG" entry are provided: the plain bitmap format is understood everywhere
    /// but loses transparency, while apps that read "PNG" keep it.
    /// </remarks>
    public static bool TrySetImage(SKBitmap bitmap)
    {
        try
        {
            var source = BitmapConverter.ToBitmapSource(bitmap);

            using var png = new MemoryStream();
            using (var pixmap = bitmap.PeekPixels())
            {
                if (!pixmap.Encode(png, new SKPngEncoderOptions()))
                {
                    return false;
                }
            }

            png.Position = 0;
            var data = new DataObject();
            data.SetImage(source);
            data.SetData("PNG", png);

            // copy: true keeps the data on the clipboard after the app exits.
            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or IOException)
        {
            // The clipboard may be locked by another process; the caller reports the failure.
            return false;
        }
    }

    private static SKBitmap ToSKBitmap(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var info = new SKImageInfo(converted.PixelWidth, converted.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        converted.CopyPixels(Int32Rect.Empty, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        return bitmap;
    }
}
