using SkiaSharp;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;

namespace Trimx.Core.Editing;

/// <summary>
/// Creates layers with defaults suited to annotating screenshots, centered on the canvas.
/// </summary>
public static class LayerFactory
{
    /// <summary>A saturated red that stands out on typical screenshots.</summary>
    public static readonly SKColor DefaultColor = new(0xFF, 0x3B, 0x30);

    /// <summary>Creates an image layer centered on the canvas. Takes ownership of the bitmap.</summary>
    public static ImageLayer CreateImage(ImageDocument document, SKBitmap bitmap, string name)
    {
        return new ImageLayer(bitmap)
        {
            Name = name,
            X = (document.Width - bitmap.Width) / 2,
            Y = (document.Height - bitmap.Height) / 2,
        };
    }

    /// <summary>Creates a fully transparent image layer the size of the canvas.</summary>
    /// <summary>Creates a canvas-sized transparent image layer holding one pen stroke (document pixels).</summary>
    public static ImageLayer CreateStrokeImage(ImageDocument document, IReadOnlyList<SKPoint> points, SKColor color, float width, string name)
    {
        var bitmap = new SKBitmap(document.Width, document.Height);
        try
        {
            bitmap.Erase(SKColors.Transparent);

            // Drawn before the bitmap becomes a layer, since layer bitmaps are immutable.
            PaintStrokeCommand.DrawInto(bitmap, points, color, width);
            return new ImageLayer(bitmap) { Name = name };
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static ImageLayer CreateEmptyImage(ImageDocument document, string name)
    {
        var bitmap = new SKBitmap(document.Width, document.Height);
        try
        {
            bitmap.Erase(SKColors.Transparent);
            return new ImageLayer(bitmap) { Name = name };
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <param name="applyStyle">Adjusts the new layer (for example with the last-used look) before it is measured and centered.</param>
    public static TextLayer CreateText(ImageDocument document, string text, Action<TextLayer>? applyStyle = null)
    {
        var layer = new TextLayer
        {
            Name = text,
            Text = text,
            FontSize = Math.Max(16f, MathF.Round(ShortSide(document) / 20f)),
            Color = DefaultColor,
        };
        applyStyle?.Invoke(layer);

        // Text extents depend on the font, so measure before centering.
        var bounds = LayerRenderer.GetBounds(layer);
        layer.X = (int)MathF.Round((document.Width - bounds.Width) / 2f);
        layer.Y = (int)MathF.Round((document.Height - bounds.Height) / 2f);
        return layer;
    }

    /// <param name="applyStyle">Adjusts the new layer (for example with the last-used look and size) before it is centered.</param>
    public static ShapeLayer CreateShape(ImageDocument document, ShapeKind kind, string name, Action<ShapeLayer>? applyStyle = null)
    {
        var width = Math.Max(16, document.Width / 3);
        var height = Math.Max(16, document.Height / 3);
        var layer = new ShapeLayer
        {
            Name = name,
            Kind = kind,
            Width = width,

            // A line starts out horizontal; the other shapes fill their box.
            Height = kind == ShapeKind.Line ? 0 : height,
            StrokeColor = DefaultColor,
            StrokeWidth = Math.Max(2f, MathF.Round(ShortSide(document) / 150f)),
        };
        applyStyle?.Invoke(layer);

        // Centered after the style, which may have changed the size. A negative width or height (an arrow
        // pointing left or up) still centers correctly, since the shape spans X to X + Width.
        layer.X = (document.Width - layer.Width) / 2;
        layer.Y = (document.Height - layer.Height) / 2;
        return layer;
    }

    private static int ShortSide(ImageDocument document) => Math.Min(document.Width, document.Height);
}
