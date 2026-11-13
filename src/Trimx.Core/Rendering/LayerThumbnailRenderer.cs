using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Renders a layer's content, fitted into a square, for the layer list.
/// </summary>
public static class LayerThumbnailRenderer
{
    /// <summary>
    /// Returns a transparent square bitmap with the layer drawn at full opacity, centered and scaled to fit.
    /// The caller owns the bitmap. The result is premultiplied BGRA.
    /// </summary>
    public static SKBitmap Render(Layer layer, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            var bounds = LayerRenderer.GetBounds(layer);
            if (bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return bitmap;
            }

            // A 1px margin keeps strokes from touching the edge.
            var scale = Math.Min((size - 2f) / bounds.Width, (size - 2f) / bounds.Height);
            canvas.Translate(size / 2f, size / 2f);
            canvas.Scale(scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);

            var sampling = scale < 1f
                ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            LayerRenderer.Draw(layer, canvas, sampling, applyOpacity: false);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
