using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Renders a document at 100% into a single bitmap, for export and the clipboard.
/// </summary>
public static class ExportRenderer
{
    /// <summary>
    /// Composites all visible layers over <paramref name="background"/>. The caller owns the bitmap.
    /// The result is premultiplied BGRA, which both Skia encoders and WPF consume directly.
    /// </summary>
    public static SKBitmap Render(ImageDocument document, SKColor background)
    {
        var info = new SKImageInfo(document.Width, document.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(background);

            // Pixel-exact at 100%; no filtering so raster layers are not softened.
            CanvasRenderer.Render(document, canvas, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renders only a rectangle of the composite (document pixels, inside the canvas). The caller owns the bitmap.
    /// </summary>
    public static SKBitmap RenderRegion(ImageDocument document, SKRectI region, SKColor background)
    {
        var bitmap = CreateBitmap(region.Width, region.Height);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(background);
            canvas.Translate(-region.Left, -region.Top);
            CanvasRenderer.Render(document, canvas, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renders one layer's own pixels for a rectangle (document pixels), transparent elsewhere and at full
    /// opacity, like copying from a layer in an image editor. Works for text and shapes too. The caller owns the bitmap.
    /// </summary>
    public static SKBitmap RenderLayerRegion(Layer layer, SKRectI region)
    {
        var bitmap = CreateBitmap(region.Width, region.Height);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(-region.Left, -region.Top);
            LayerRenderer.Draw(layer, canvas, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), applyOpacity: false);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static SKBitmap CreateBitmap(int width, int height)
    {
        return new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
    }
}
