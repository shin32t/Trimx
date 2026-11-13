using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;

namespace Trimx.Core.Commands;

/// <summary>
/// Paints one pen stroke into an image layer's pixels.
/// </summary>
/// <remarks>
/// Only the touched rectangle is kept (before and after), so undo costs memory proportional to the
/// stroke, not to the layer. Pixels are copied row by row rather than drawn through a canvas, because
/// layer bitmaps are marked immutable and the copy is the only write path.
/// </remarks>
public sealed class PaintStrokeCommand : IDocumentCommand
{
    /// <summary>Upper bound on points per stroke, so one stroke cannot grow without limit.</summary>
    public const int MaxPoints = 1_000_000;

    private readonly ImageLayer _layer;
    private readonly SKRectI _area; // In layer pixels.
    private readonly SKBitmap _before;
    private readonly SKBitmap _after;

    private PaintStrokeCommand(ImageLayer layer, SKRectI area, SKBitmap before, SKBitmap after)
    {
        _layer = layer;
        _area = area;
        _before = before;
        _after = after;
    }

    public Layer? TargetLayer => _layer;

    /// <summary>
    /// Builds the command for a stroke given in document pixels. Returns null if the stroke does not touch the layer.
    /// The caller owns the command (it is not applied yet).
    /// </summary>
    public static PaintStrokeCommand? Create(ImageLayer layer, IReadOnlyList<SKPoint> points, SKColor color, float width)
    {
        var area = GetArea(points, width, layer.X, layer.Y);
        area.Intersect(new SKRectI(0, 0, layer.Width, layer.Height));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return null;
        }

        var before = new SKBitmap(layer.Bitmap.Info.WithSize(area.Width, area.Height));
        SKBitmap? after = null;
        try
        {
            CopyRows(layer.Bitmap, area.Left, area.Top, before, 0, 0, area.Width, area.Height);
            after = before.Copy() ?? throw new InvalidOperationException("Failed to copy the stroke area.");
            using (var canvas = new SKCanvas(after))
            {
                // Document pixels -> layer pixels -> patch pixels.
                canvas.Translate(-layer.X - area.Left, -layer.Y - area.Top);
                LayerRenderer.DrawStroke(canvas, points, color, width);
            }

            return new PaintStrokeCommand(layer, area, before, after);
        }
        catch
        {
            before.Dispose();
            after?.Dispose();
            throw;
        }
    }

    /// <summary>Draws a stroke into a bitmap whose top-left is the document origin (for a brand-new layer).</summary>
    public static void DrawInto(SKBitmap bitmap, IReadOnlyList<SKPoint> points, SKColor color, float width)
    {
        using var canvas = new SKCanvas(bitmap);
        LayerRenderer.DrawStroke(canvas, points, color, width);
    }

    public void Execute() => Apply(_after);

    public void Undo() => Apply(_before);

    public void Dispose()
    {
        _before.Dispose();
        _after.Dispose();
    }

    private void Apply(SKBitmap patch)
    {
        CopyRows(patch, 0, 0, _layer.Bitmap, _area.Left, _area.Top, _area.Width, _area.Height);
        _layer.NotifyPixelsChanged();
    }

    // One extra pixel on each side so antialiased edges are included.
    private static SKRectI GetArea(IReadOnlyList<SKPoint> points, float width, int offsetX, int offsetY)
    {
        var left = points[0].X;
        var top = points[0].Y;
        var right = left;
        var bottom = top;
        foreach (var point in points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        var reach = width / 2f + 1f;
        return new SKRectI(
            (int)MathF.Floor(left - reach) - offsetX,
            (int)MathF.Floor(top - reach) - offsetY,
            (int)MathF.Ceiling(right + reach) - offsetX,
            (int)MathF.Ceiling(bottom + reach) - offsetY);
    }

    internal static void CopyRows(SKBitmap source, int sourceX, int sourceY, SKBitmap target, int targetX, int targetY, int width, int height)
    {
        var bytesPerPixel = source.BytesPerPixel;
        var from = source.GetPixelSpan();
        var to = target.GetPixelSpan();
        for (var row = 0; row < height; row++)
        {
            from.Slice((sourceY + row) * source.RowBytes + sourceX * bytesPerPixel, width * bytesPerPixel)
                .CopyTo(to.Slice((targetY + row) * target.RowBytes + targetX * bytesPerPixel));
        }
    }
}
