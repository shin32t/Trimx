using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Editing;

namespace Trimx.Core.Commands;

/// <summary>
/// Factory methods for layer property commands. Create a command right before executing it,
/// because it captures the current value as the undo value.
/// </summary>
public static class LayerCommands
{
    private const string DefaultTextLayerName = "テキスト";

    public static IDocumentCommand SetVisibility(Layer layer, bool isVisible)
    {
        return Property(layer, nameof(Layer.IsVisible), static l => l.IsVisible, static (l, v) => l.IsVisible = v, isVisible);
    }

    public static IDocumentCommand SetOpacity(Layer layer, float opacity)
    {
        return Property(layer, nameof(Layer.Opacity), static l => l.Opacity, static (l, v) => l.Opacity = v, opacity);
    }

    public static IDocumentCommand SetPosition(Layer layer, int x, int y) => new SetLayerPositionCommand(layer, x, y);

    /// <summary>
    /// Sets the text. The layer name follows the first line of the text as long as it has not been
    /// renamed, so the layer list shows what the layer says.
    /// </summary>
    public static IDocumentCommand SetText(TextLayer layer, string text)
    {
        return Property(
            layer,
            nameof(TextLayer.Text),
            static l => l.Text,
            static (l, v) =>
            {
                // Decide before changing the text, but change the text first: every property change
                // notifies listeners (the in-place editor, the properties panel) which compare the layer's
                // text with theirs. If the name changed first they would see the old text, write it back
                // into the text box and reset the caret, so typed characters came out in reverse order.
                var followsText = l.Name == NameFor(l.Text);
                l.Text = v;
                if (followsText)
                {
                    l.Name = NameFor(v);
                }
            },
            text);
    }

    public static IDocumentCommand SetFontFamily(TextLayer layer, string fontFamily)
    {
        return Property(layer, nameof(TextLayer.FontFamily), static l => l.FontFamily, static (l, v) => l.FontFamily = v, fontFamily);
    }

    public static IDocumentCommand SetFontSize(TextLayer layer, float fontSize)
    {
        return Property(layer, nameof(TextLayer.FontSize), static l => l.FontSize, static (l, v) => l.FontSize = v, fontSize);
    }

    public static IDocumentCommand SetBold(TextLayer layer, bool isBold)
    {
        return Property(layer, nameof(TextLayer.IsBold), static l => l.IsBold, static (l, v) => l.IsBold = v, isBold);
    }

    public static IDocumentCommand SetItalic(TextLayer layer, bool isItalic)
    {
        return Property(layer, nameof(TextLayer.IsItalic), static l => l.IsItalic, static (l, v) => l.IsItalic = v, isItalic);
    }

    public static IDocumentCommand SetHasShadow(TextLayer layer, bool hasShadow)
    {
        return Property(layer, nameof(TextLayer.HasShadow), static l => l.HasShadow, static (l, v) => l.HasShadow = v, hasShadow);
    }

    public static IDocumentCommand SetShadowBlur(TextLayer layer, float blur)
    {
        return Property(layer, nameof(TextLayer.ShadowBlur), static l => l.ShadowBlur, static (l, v) => l.ShadowBlur = v, blur);
    }

    public static IDocumentCommand SetShadowOpacity(TextLayer layer, float opacity)
    {
        return Property(layer, nameof(TextLayer.ShadowOpacity), static l => l.ShadowOpacity, static (l, v) => l.ShadowOpacity = v, opacity);
    }

    public static IDocumentCommand SetShadowOffsetX(TextLayer layer, float offset)
    {
        return Property(layer, nameof(TextLayer.ShadowOffsetX), static l => l.ShadowOffsetX, static (l, v) => l.ShadowOffsetX = v, offset);
    }

    public static IDocumentCommand SetShadowOffsetY(TextLayer layer, float offset)
    {
        return Property(layer, nameof(TextLayer.ShadowOffsetY), static l => l.ShadowOffsetY, static (l, v) => l.ShadowOffsetY = v, offset);
    }

    public static IDocumentCommand SetOutlineWidth(TextLayer layer, float width)
    {
        return Property(layer, nameof(TextLayer.OutlineWidth), static l => l.OutlineWidth, static (l, v) => l.OutlineWidth = v, width);
    }

    public static IDocumentCommand SetOutlineColor(TextLayer layer, SKColor color)
    {
        return Property(layer, nameof(TextLayer.OutlineColor), static l => l.OutlineColor, static (l, v) => l.OutlineColor = v, color);
    }

    public static IDocumentCommand SetTextColor(TextLayer layer, SKColor color)
    {
        return Property(layer, nameof(TextLayer.Color), static l => l.Color, static (l, v) => l.Color = v, color);
    }

    /// <summary>Sets position and size of a shape together (used while dragging a handle).</summary>
    public static IDocumentCommand SetShapeGeometry(ShapeLayer layer, ShapeGeometry geometry)
    {
        return new SetShapeGeometryCommand(layer, geometry);
    }

    public static IDocumentCommand SetShapeWidth(ShapeLayer layer, int width)
    {
        return Property(layer, nameof(ShapeLayer.Width), static l => l.Width, static (l, v) => l.Width = v, width);
    }

    public static IDocumentCommand SetShapeHeight(ShapeLayer layer, int height)
    {
        return Property(layer, nameof(ShapeLayer.Height), static l => l.Height, static (l, v) => l.Height = v, height);
    }

    public static IDocumentCommand SetStrokeWidth(ShapeLayer layer, float strokeWidth)
    {
        return Property(layer, nameof(ShapeLayer.StrokeWidth), static l => l.StrokeWidth, static (l, v) => l.StrokeWidth = v, strokeWidth);
    }

    public static IDocumentCommand SetStrokeColor(ShapeLayer layer, SKColor color)
    {
        return Property(layer, nameof(ShapeLayer.StrokeColor), static l => l.StrokeColor, static (l, v) => l.StrokeColor = v, color);
    }

    public static IDocumentCommand SetDash(ShapeLayer layer, ShapeDash dash)
    {
        return Property(layer, nameof(ShapeLayer.Dash), static l => l.Dash, static (l, v) => l.Dash = v, dash);
    }

    public static IDocumentCommand SetCornerRadius(ShapeLayer layer, float radius)
    {
        return Property(layer, nameof(ShapeLayer.CornerRadius), static l => l.CornerRadius, static (l, v) => l.CornerRadius = v, radius);
    }

    public static IDocumentCommand SetFillColor(ShapeLayer layer, SKColor color)
    {
        return Property(layer, nameof(ShapeLayer.FillColor), static l => l.FillColor, static (l, v) => l.FillColor = v, color);
    }

    /// <summary>The default name of a text layer: its first non-empty line, or a fixed label.</summary>
    public static string NameFor(string text)
    {
        var firstLine = text.Split('\n')[0].Trim('\r', ' ');
        return firstLine.Length > 0 ? firstLine : DefaultTextLayerName;
    }

    private static SetLayerPropertyCommand<TLayer, TValue> Property<TLayer, TValue>(
        TLayer layer,
        string propertyName,
        Func<TLayer, TValue> getter,
        Action<TLayer, TValue> setter,
        TValue value)
        where TLayer : Layer
    {
        return new SetLayerPropertyCommand<TLayer, TValue>(layer, propertyName, getter, setter, value);
    }
}
