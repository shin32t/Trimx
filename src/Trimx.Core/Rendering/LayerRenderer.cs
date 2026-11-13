using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Draws a single layer in document coordinates and computes its bounds.
/// </summary>
public static class LayerRenderer
{
    private const float ArrowHeadWidthRatio = 0.5f;

    /// <param name="applyOpacity">
    /// False draws at full opacity; used when rasterizing, where the opacity is kept on the new layer instead.
    /// </param>
    public static void Draw(Layer layer, SKCanvas canvas, SKSamplingOptions sampling, bool applyOpacity = true)
    {
        var alpha = applyOpacity ? (byte)MathF.Round(layer.Opacity * 255f) : (byte)255;

        if (layer is ImageLayer imageLayer)
        {
            // Bitmaps can apply opacity directly through the paint, so no offscreen layer is needed.
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
            canvas.DrawBitmap(imageLayer.Bitmap, imageLayer.X, imageLayer.Y, sampling, paint);
            return;
        }

        // Vector content may overlap itself (e.g. arrow shaft and head), so opacity is applied
        // to the composited result through an offscreen layer.
        SKPaint? layerPaint = null;
        if (alpha < 255)
        {
            layerPaint = new SKPaint { Color = SKColors.Black.WithAlpha(alpha) };
            canvas.SaveLayer(layerPaint);
        }

        try
        {
            switch (layer)
            {
                case TextLayer textLayer:
                    DrawText(textLayer, canvas);
                    break;
                case ShapeLayer shapeLayer:
                    DrawShape(shapeLayer, canvas);
                    break;
            }
        }
        finally
        {
            if (layerPaint is not null)
            {
                canvas.Restore();
                layerPaint.Dispose();
            }
        }
    }

    /// <summary>Returns the area covered by the layer in document coordinates.</summary>
    public static SKRect GetBounds(Layer layer)
    {
        switch (layer)
        {
            case ImageLayer imageLayer:
                return SKRect.Create(imageLayer.X, imageLayer.Y, imageLayer.Width, imageLayer.Height);

            case TextLayer textLayer:
            {
                using var font = CreateFont(textLayer);
                var lines = SplitLines(textLayer.Text);
                var width = 0f;
                foreach (var line in lines)
                {
                    width = Math.Max(width, font.MeasureText(line));
                }

                var box = SKRect.Create(textLayer.X, textLayer.Y, width, font.Spacing * lines.Length);
                box.Inflate(textLayer.OutlineWidth, textLayer.OutlineWidth);

                // The shadow is part of the text's footprint, so selection and rasterizing include it.
                if (HasVisibleShadow(textLayer))
                {
                    var shadow = box;
                    shadow.Offset(textLayer.ShadowOffsetX, textLayer.ShadowOffsetY);

                    // A Gaussian blur fades out over about three sigma (sigma = blur / 2).
                    shadow.Inflate(textLayer.ShadowBlur * 1.5f, textLayer.ShadowBlur * 1.5f);
                    box = SKRect.Union(box, shadow);
                }

                return box;
            }

            case ShapeLayer shapeLayer:
            {
                var rect = SKRect.Create(shapeLayer.X, shapeLayer.Y, shapeLayer.Width, shapeLayer.Height).Standardized;
                var inflate = shapeLayer.StrokeWidth / 2f;
                if (shapeLayer.Kind == ShapeKind.Arrow)
                {
                    inflate = Math.Max(inflate, GetArrowHeadLength(shapeLayer.StrokeWidth) * ArrowHeadWidthRatio);
                }

                rect.Inflate(inflate, inflate);
                return rect;
            }

            default:
                return SKRect.Create(layer.X, layer.Y, 0, 0);
        }
    }

    /// <summary>
    /// Draws a freehand line through the points as a smooth curve with round ends.
    /// Public so the canvas can preview a stroke while it is being drawn.
    /// </summary>
    public static void DrawStroke(SKCanvas canvas, IReadOnlyList<SKPoint> points, SKColor color, float width)
    {
        if (points.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint { IsAntialias = true, Color = color };
        if (points.Count == 1)
        {
            canvas.DrawCircle(points[0], width / 2f, paint);
            return;
        }

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;

        // Curving through the midpoints of consecutive points hides the corners of raw mouse samples.
        using var builder = new SKPathBuilder();
        builder.MoveTo(points[0]);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var mid = new SKPoint((points[i].X + points[i + 1].X) / 2f, (points[i].Y + points[i + 1].Y) / 2f);
            builder.QuadTo(points[i].X, points[i].Y, mid.X, mid.Y);
        }

        builder.LineTo(points[^1]);
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    private static bool HasVisibleShadow(TextLayer layer) => layer.HasShadow && layer.ShadowOpacity > 0f;

    private static void DrawText(TextLayer layer, SKCanvas canvas)
    {
        if (layer.Text.Length == 0)
        {
            return;
        }

        using var font = CreateFont(layer);
        var lines = SplitLines(layer.Text);
        var baseline = layer.Y - font.Metrics.Ascent;

        // Back to front: shadow, outline, then the glyphs. The outline is stroked around the glyph edge, so only
        // its outer half shows once the fill is drawn on top.
        if (HasVisibleShadow(layer))
        {
            // Drawn opaque into an offscreen layer, then blurred and faded as a whole, so the glyph and its
            // outline do not add up their darkness where they overlap.
            using var blur = layer.ShadowBlur > 0f ? SKImageFilter.CreateBlur(layer.ShadowBlur / 2f, layer.ShadowBlur / 2f) : null;
            using var layerPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha((byte)MathF.Round(layer.ShadowOpacity * 255f)),
                ImageFilter = blur,
            };
            canvas.SaveLayer(layerPaint);
            DrawTextLines(canvas, font, lines, layer.X + layer.ShadowOffsetX, baseline + layer.ShadowOffsetY, SKColors.Black, layer.OutlineWidth);
            canvas.Restore();
        }

        if (layer.OutlineWidth > 0f && layer.OutlineColor.Alpha > 0)
        {
            using var outline = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = layer.OutlineWidth * 2f,
                StrokeJoin = SKStrokeJoin.Round,
                Color = layer.OutlineColor,
            };
            DrawTextLines(canvas, font, lines, layer.X, baseline, outline);
        }

        using var paint = new SKPaint { Color = layer.Color, IsAntialias = true };
        DrawTextLines(canvas, font, lines, layer.X, baseline, paint);
    }

    // The shadow covers the outline too, so it is drawn fat enough to include it.
    private static void DrawTextLines(SKCanvas canvas, SKFont font, string[] lines, float x, float baseline, SKColor color, float outlineWidth)
    {
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        DrawTextLines(canvas, font, lines, x, baseline, fill);
        if (outlineWidth > 0f)
        {
            using var stroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = outlineWidth * 2f,
                StrokeJoin = SKStrokeJoin.Round,
                Color = color,
            };
            DrawTextLines(canvas, font, lines, x, baseline, stroke);
        }
    }

    private static void DrawTextLines(SKCanvas canvas, SKFont font, string[] lines, float x, float baseline, SKPaint paint)
    {
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, baseline, SKTextAlign.Left, font, paint);
            baseline += font.Spacing;
        }
    }

    private static void DrawShape(ShapeLayer layer, SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = true };

        if (layer.Kind == ShapeKind.Arrow)
        {
            DrawArrow(layer, canvas, paint);
            return;
        }

        if (layer.Kind == ShapeKind.Line)
        {
            DrawLine(layer, canvas, paint);
            return;
        }

        var rect = SKRect.Create(layer.X, layer.Y, layer.Width, layer.Height).Standardized;

        if (layer.FillColor.Alpha > 0)
        {
            paint.Style = SKPaintStyle.Fill;
            paint.Color = layer.FillColor;
            DrawBox(layer, canvas, rect, paint);
        }

        if (layer.StrokeWidth > 0f && layer.StrokeColor.Alpha > 0)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = layer.StrokeWidth;
            paint.Color = layer.StrokeColor;
            using var dash = ApplyDash(paint, layer.Dash, layer.StrokeWidth);
            DrawBox(layer, canvas, rect, paint);
        }
    }

    private static void DrawBox(ShapeLayer layer, SKCanvas canvas, SKRect rect, SKPaint paint)
    {
        if (layer.Kind == ShapeKind.Ellipse)
        {
            canvas.DrawOval(rect, paint);
        }
        else if (layer.CornerRadius > 0f)
        {
            // A radius beyond half the shorter side would overlap itself, so it stops there.
            var radius = Math.Min(layer.CornerRadius, Math.Min(rect.Width, rect.Height) / 2f);
            canvas.DrawRoundRect(rect, radius, radius, paint);
        }
        else
        {
            canvas.DrawRect(rect, paint);
        }
    }

    /// <summary>
    /// Makes the stroke dashed or dotted. The returned effect must outlive the drawing; it is null for solid lines.
    /// </summary>
    private static SKPathEffect? ApplyDash(SKPaint paint, ShapeDash dash, float strokeWidth)
    {
        var unit = Math.Max(strokeWidth, 1f);
        SKPathEffect? effect = null;
        switch (dash)
        {
            case ShapeDash.Dashed:
                effect = SKPathEffect.CreateDash(new[] { unit * 4f, unit * 3f }, 0f);
                break;

            case ShapeDash.Dotted:
                // Zero-length dashes with round caps become dots.
                effect = SKPathEffect.CreateDash(new[] { 0f, unit * 2f }, 0f);
                paint.StrokeCap = SKStrokeCap.Round;
                break;
        }

        paint.PathEffect = effect;
        return effect;
    }

    private static void DrawLine(ShapeLayer layer, SKCanvas canvas, SKPaint paint)
    {
        if (layer.StrokeWidth <= 0f || layer.StrokeColor.Alpha == 0)
        {
            return;
        }

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = layer.StrokeWidth;
        paint.Color = layer.StrokeColor;
        paint.StrokeCap = SKStrokeCap.Butt;
        using var dash = ApplyDash(paint, layer.Dash, layer.StrokeWidth);
        canvas.DrawLine(layer.X, layer.Y, layer.X + layer.Width, layer.Y + layer.Height, paint);
    }

    private static void DrawArrow(ShapeLayer layer, SKCanvas canvas, SKPaint paint)
    {
        var start = new SKPoint(layer.X, layer.Y);
        var end = new SKPoint(layer.X + layer.Width, layer.Y + layer.Height);
        var length = SKPoint.Distance(start, end);
        if (length < 0.5f || layer.StrokeWidth <= 0f)
        {
            return;
        }

        var direction = new SKPoint((end.X - start.X) / length, (end.Y - start.Y) / length);
        var normal = new SKPoint(-direction.Y, direction.X);
        var headLength = Math.Min(length, GetArrowHeadLength(layer.StrokeWidth));
        var headHalfWidth = headLength * ArrowHeadWidthRatio;
        var headBase = new SKPoint(end.X - direction.X * headLength, end.Y - direction.Y * headLength);

        paint.Color = layer.StrokeColor;

        // End the shaft inside the head so the butt cap is hidden under the triangle.
        var shaftEnd = new SKPoint(end.X - direction.X * headLength * 0.5f, end.Y - direction.Y * headLength * 0.5f);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = layer.StrokeWidth;
        paint.StrokeCap = SKStrokeCap.Butt;
        using (var dash = ApplyDash(paint, layer.Dash, layer.StrokeWidth))
        {
            canvas.DrawLine(start, shaftEnd, paint);
        }

        // The head is always solid.
        paint.PathEffect = null;
        paint.StrokeCap = SKStrokeCap.Butt;

        using var builder = new SKPathBuilder();
        builder.MoveTo(end);
        builder.LineTo(headBase.X + normal.X * headHalfWidth, headBase.Y + normal.Y * headHalfWidth);
        builder.LineTo(headBase.X - normal.X * headHalfWidth, headBase.Y - normal.Y * headHalfWidth);
        builder.Close();
        using var head = builder.Detach();
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(head, paint);
    }

    internal static float GetArrowHeadLength(float strokeWidth) => Math.Max(strokeWidth * 4f, 12f);

    private static SKFont CreateFont(TextLayer layer)
    {
        return new SKFont(TypefaceCache.Get(layer.FontFamily, layer.IsBold, layer.IsItalic), layer.FontSize)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };
    }

    private static string[] SplitLines(string text) => text.Replace("\r\n", "\n").Split('\n');
}
