using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Merges two layers into one raster layer.
/// </summary>
public static class LayerMerger
{
    /// <summary>
    /// Draws <paramref name="lower"/> and then <paramref name="upper"/> (each with its own opacity)
    /// into a new image layer covering both. The new layer is fully opaque as a layer; opacity is baked in.
    /// The caller owns the result. Returns null if the combined area is empty or larger than
    /// <see cref="ImageLimits.MaxSide"/> on a side.
    /// </summary>
    public static ImageLayer? TryMerge(Layer upper, Layer lower)
    {
        var rect = SKRect.Union(LayerRenderer.GetBounds(lower), LayerRenderer.GetBounds(upper));

        // One extra pixel on each side so antialiased edges are not cut off.
        rect.Inflate(1f, 1f);
        var bounds = new SKRectI(
            (int)MathF.Floor(rect.Left),
            (int)MathF.Floor(rect.Top),
            (int)MathF.Ceiling(rect.Right),
            (int)MathF.Ceiling(rect.Bottom));
        if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > ImageLimits.MaxSide || bounds.Height > ImageLimits.MaxSide)
        {
            return null;
        }

        var bitmap = new SKBitmap(bounds.Width, bounds.Height);
        try
        {
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(-bounds.Left, -bounds.Top);

                // Pixel-exact: both layers sit on whole pixels, so no filtering is needed.
                var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
                foreach (var layer in new[] { lower, upper })
                {
                    if (layer.IsVisible && layer.Opacity > 0f)
                    {
                        LayerRenderer.Draw(layer, canvas, sampling);
                    }
                }
            }

            return new ImageLayer(bitmap)
            {
                Name = lower.Name,
                X = bounds.Left,
                Y = bounds.Top,
            };
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
