using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Converts text and shape layers into raster image layers.
/// </summary>
public static class LayerRasterizer
{
    /// <summary>Largest side of a rasterized layer; guards against absurd shape sizes.</summary>
    private const int MaxSide = 16384;

    /// <summary>
    /// Returns true if the layer can be rasterized (text or shape with something to draw).
    /// </summary>
    public static bool CanRasterize(Layer layer)
    {
        return layer is TextLayer or ShapeLayer && TryGetPixelBounds(layer, out _);
    }

    /// <summary>
    /// Renders the layer into a new <see cref="ImageLayer"/> that keeps name, visibility and opacity.
    /// The caller owns the result. Returns null if the layer cannot be rasterized.
    /// </summary>
    public static ImageLayer? TryRasterize(Layer layer)
    {
        if (layer is not (TextLayer or ShapeLayer) || !TryGetPixelBounds(layer, out var bounds))
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

                // Opacity is kept on the new layer, so draw at full opacity here.
                LayerRenderer.Draw(layer, canvas, new SKSamplingOptions(SKFilterMode.Linear), applyOpacity: false);
            }

            return new ImageLayer(bitmap)
            {
                Name = layer.Name,
                IsVisible = layer.IsVisible,
                Opacity = layer.Opacity,
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

    private static bool TryGetPixelBounds(Layer layer, out SKRectI bounds)
    {
        // One extra pixel on each side so antialiased edges are not cut off.
        var rect = LayerRenderer.GetBounds(layer);
        rect.Inflate(1f, 1f);
        bounds = new SKRectI(
            (int)MathF.Floor(rect.Left),
            (int)MathF.Floor(rect.Top),
            (int)MathF.Ceiling(rect.Right),
            (int)MathF.Ceiling(rect.Bottom));

        var empty = layer is TextLayer { Text.Length: 0 } || layer is ShapeLayer { Width: 0, Height: 0 };
        return !empty && bounds.Width is > 0 and <= MaxSide && bounds.Height is > 0 and <= MaxSide;
    }
}
