using System.IO;
using System.Windows.Input;
using SkiaSharp;

namespace Trimx.UI;

/// <summary>
/// The mouse cursor shown in pen mode: a small pen whose tip is the hot spot.
/// </summary>
/// <remarks>
/// Drawn with Skia and packed into an in-memory .cur, so no image resource has to ship with the app.
/// Created on first use only.
/// </remarks>
public static class PenCursor
{
    private const int Size = 32;
    private const int HotSpotX = 2;
    private const int HotSpotY = 29;

    private static Cursor? _instance;

    public static Cursor Instance => _instance ??= Create();

    private static Cursor Create()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            DrawPen(canvas);
        }

        return new Cursor(new MemoryStream(BuildCursorFile(bitmap)));
    }

    private static void DrawPen(SKCanvas canvas)
    {
        // Body runs from the tip (bottom left) to the top right; a white outline keeps it visible on dark images.
        using var builder = new SKPathBuilder();
        builder.MoveTo(HotSpotX, HotSpotY);
        builder.LineTo(5, 20);
        builder.LineTo(22, 3);
        builder.LineTo(28, 9);
        builder.LineTo(11, 26);
        builder.Close();
        using var body = builder.Detach();

        using var outline = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, Color = SKColors.White, StrokeJoin = SKStrokeJoin.Round };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x30, 0x30, 0x30) };
        using var tip = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE8, 0x1F, 0x1F) };
        canvas.DrawPath(body, outline);
        canvas.DrawPath(body, fill);

        // The nib, so it is clear where the line will start.
        using var nibBuilder = new SKPathBuilder();
        nibBuilder.MoveTo(HotSpotX, HotSpotY);
        nibBuilder.LineTo(5, 20);
        nibBuilder.LineTo(11, 26);
        nibBuilder.Close();
        using var nib = nibBuilder.Detach();
        canvas.DrawPath(nib, tip);
    }

    // .cur layout: ICONDIR, one ICONDIRENTRY (planes / bit count hold the hot spot), then a DIB without a file header.
    private static byte[] BuildCursorFile(SKBitmap bitmap)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var maskRowBytes = (Size + 31) / 32 * 4;
        var imageBytes = 40 + Size * Size * 4 + maskRowBytes * Size;

        writer.Write((ushort)0); // Reserved.
        writer.Write((ushort)2); // Type: cursor.
        writer.Write((ushort)1); // Image count.

        writer.Write((byte)Size);
        writer.Write((byte)Size);
        writer.Write((byte)0); // Palette size.
        writer.Write((byte)0);
        writer.Write((ushort)HotSpotX);
        writer.Write((ushort)HotSpotY);
        writer.Write(imageBytes);
        writer.Write(22); // Offset of the image data.

        writer.Write(40); // BITMAPINFOHEADER.
        writer.Write(Size);
        writer.Write(Size * 2); // Height covers the color image and the mask.
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(Size * Size * 4 + maskRowBytes * Size);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        // Pixels are stored bottom-up.
        for (var y = Size - 1; y >= 0; y--)
        {
            for (var x = 0; x < Size; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                writer.Write(pixel.Blue);
                writer.Write(pixel.Green);
                writer.Write(pixel.Red);
                writer.Write(pixel.Alpha);
            }
        }

        // The AND mask is ignored for 32-bit images that carry alpha, so it stays zero.
        writer.Write(new byte[maskRowBytes * Size]);
        writer.Flush();
        return stream.ToArray();
    }
}
