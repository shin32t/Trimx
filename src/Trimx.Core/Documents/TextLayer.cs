using SkiaSharp;

namespace Trimx.Core.Documents;

/// <summary>
/// An editable text layer. <see cref="Layer.X"/> / <see cref="Layer.Y"/> are the top-left of the text block.
/// Lines are separated by a line feed.
/// </summary>
public sealed class TextLayer : Layer
{
    /// <summary>Default font; covers both Japanese and Latin glyphs on Windows 11.</summary>
    public const string DefaultFontFamily = "Yu Gothic UI";

    private string _text = string.Empty;
    private string _fontFamily = DefaultFontFamily;
    private float _fontSize = 32f;
    private SKColor _color = SKColors.Black;
    private bool _isBold;
    private bool _isItalic;
    private bool _hasShadow;
    private float _shadowOpacity = 0.5f;
    private float _shadowBlur = 4f;
    private float _shadowOffsetX = 4f;
    private float _shadowOffsetY = 4f;
    private float _outlineWidth;
    private SKColor _outlineColor = SKColors.White;

    public override Layer Clone()
    {
        return CopyBaseTo(new TextLayer
        {
            _text = _text,
            _fontFamily = _fontFamily,
            _fontSize = _fontSize,
            _color = _color,
            _isBold = _isBold,
            _isItalic = _isItalic,
            _hasShadow = _hasShadow,
            _shadowOpacity = _shadowOpacity,
            _shadowBlur = _shadowBlur,
            _shadowOffsetX = _shadowOffsetX,
            _shadowOffsetY = _shadowOffsetY,
            _outlineWidth = _outlineWidth,
            _outlineColor = _outlineColor,
        });
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public string FontFamily
    {
        get => _fontFamily;
        set => SetField(ref _fontFamily, value);
    }

    /// <summary>Font size in document pixels (minimum 1).</summary>
    public float FontSize
    {
        get => _fontSize;
        set => SetField(ref _fontSize, Math.Max(1f, value));
    }

    public SKColor Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    public bool IsBold
    {
        get => _isBold;
        set => SetField(ref _isBold, value);
    }

    public bool IsItalic
    {
        get => _isItalic;
        set => SetField(ref _isItalic, value);
    }

    /// <summary>Whether the drop shadow is drawn. The other shadow settings are kept while it is off.</summary>
    public bool HasShadow
    {
        get => _hasShadow;
        set => SetField(ref _hasShadow, value);
    }

    /// <summary>Blur radius of the shadow in document pixels; 0 is a sharp shadow.</summary>
    public float ShadowBlur
    {
        get => _shadowBlur;
        set => SetField(ref _shadowBlur, Math.Max(0f, value));
    }

    /// <summary>Darkness of the drop shadow, 0 to 1. The shadow is black.</summary>
    public float ShadowOpacity
    {
        get => _shadowOpacity;
        set => SetField(ref _shadowOpacity, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>Horizontal shadow offset in document pixels (positive moves right).</summary>
    public float ShadowOffsetX
    {
        get => _shadowOffsetX;
        set => SetField(ref _shadowOffsetX, value);
    }

    /// <summary>Vertical shadow offset in document pixels (positive moves down).</summary>
    public float ShadowOffsetY
    {
        get => _shadowOffsetY;
        set => SetField(ref _shadowOffsetY, value);
    }

    /// <summary>Thickness of the outline around the glyphs in document pixels; 0 draws none.</summary>
    public float OutlineWidth
    {
        get => _outlineWidth;
        set => SetField(ref _outlineWidth, Math.Max(0f, value));
    }

    public SKColor OutlineColor
    {
        get => _outlineColor;
        set => SetField(ref _outlineColor, value);
    }
}
