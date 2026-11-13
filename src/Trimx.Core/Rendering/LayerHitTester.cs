using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Finds the layer under a point, so layers can be selected by clicking the canvas.
/// </summary>
public static class LayerHitTester
{
    /// <summary>Returns the topmost visible layer at the point (document coordinates), or null.</summary>
    /// <param name="tolerance">Extra reach in document pixels, so thin lines are easy to hit.</param>
    public static Layer? HitTest(ImageDocument document, SKPoint point, float tolerance)
    {
        for (var i = document.Layers.Count - 1; i >= 0; i--)
        {
            var layer = document.Layers[i];
            if (layer.IsVisible && layer.Opacity > 0f && Contains(layer, point, tolerance))
            {
                return layer;
            }
        }

        return null;
    }

    private static bool Contains(Layer layer, SKPoint point, float tolerance)
    {
        switch (layer)
        {
            case ImageLayer image:
            {
                var x = (int)MathF.Floor(point.X) - image.X;
                var y = (int)MathF.Floor(point.Y) - image.Y;

                // Transparent pixels do not count, so layers below can be picked through them.
                return x >= 0 && y >= 0 && x < image.Width && y < image.Height
                    && image.Bitmap.GetPixel(x, y).Alpha > 0;
            }

            case ShapeLayer shape:
                return ContainsShape(shape, point, tolerance);

            default:
                return LayerRenderer.GetBounds(layer).Contains(point);
        }
    }

    private static bool ContainsShape(ShapeLayer shape, SKPoint point, float tolerance)
    {
        var reach = shape.StrokeWidth / 2f + tolerance;
        var rect = SKRect.Create(shape.X, shape.Y, shape.Width, shape.Height).Standardized;

        switch (shape.Kind)
        {
            case ShapeKind.Arrow:
            case ShapeKind.Line:
            {
                var start = new SKPoint(shape.X, shape.Y);
                var end = new SKPoint(shape.X + shape.Width, shape.Y + shape.Height);
                var headHalfWidth = shape.Kind == ShapeKind.Arrow ? LayerRenderer.GetArrowHeadLength(shape.StrokeWidth) * 0.5f : 0f;
                return DistanceToSegment(point, start, end) <= reach
                    || (shape.Kind == ShapeKind.Arrow && SKPoint.Distance(point, end) <= headHalfWidth + tolerance);
            }

            case ShapeKind.Ellipse:
            {
                var rx = Math.Max(rect.Width / 2f, 0.5f);
                var ry = Math.Max(rect.Height / 2f, 0.5f);
                var dx = (point.X - rect.MidX) / rx;
                var dy = (point.Y - rect.MidY) / ry;
                var d = MathF.Sqrt(dx * dx + dy * dy); // 1 on the outline.
                var scale = Math.Min(rx, ry);
                return shape.FillColor.Alpha > 0
                    ? d <= 1f + reach / scale
                    : MathF.Abs(d - 1f) * scale <= reach;
            }

            default:
            {
                var outer = rect;
                outer.Inflate(reach, reach);
                if (!outer.Contains(point))
                {
                    return false;
                }

                if (shape.FillColor.Alpha > 0)
                {
                    return true;
                }

                // Outline only: the interior stays click-through.
                var inner = rect;
                inner.Inflate(-reach, -reach);
                return inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(point);
            }
        }
    }

    private static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        if (lengthSquared < 1e-6f)
        {
            return SKPoint.Distance(p, a);
        }

        var t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSquared, 0f, 1f);
        return SKPoint.Distance(p, new SKPoint(a.X + t * abx, a.Y + t * aby));
    }
}
