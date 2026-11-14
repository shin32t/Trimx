using SkiaSharp;

namespace Trimx.Core.Documents;

/// <summary>
/// A vector shape layer (rectangle, ellipse or arrow).
/// </summary>
/// <remarks>
/// The shape occupies the box (<see cref="Layer.X"/>, <see cref="Layer.Y"/>, <see cref="Width"/>, <see cref="Height"/>).
/// For <see cref="ShapeKind.Arrow"/> the arrow runs from (X, Y) to (X + Width, Y + Height),
/// so Width and Height may be negative to express direction.
/// </remarks>
public sealed class ShapeLayer : Layer, IShadowed
{
    private ShapeKind _kind;
    private int _width;
    private int _height;
    private SKColor _strokeColor = SKColors.Black;
    private float _strokeWidth = 4f;
    private SKColor _fillColor = SKColors.Transparent;
    private ShapeDash _dash;
    private float _cornerRadius;
    private bool _hasShadow;
    private float _shadowOpacity = IShadowed.DefaultOpacity;
    private float _shadowBlur = IShadowed.DefaultBlur;
    private float _shadowOffsetX = IShadowed.DefaultOffset;
    private float _shadowOffsetY = IShadowed.DefaultOffset;

    public override Layer Clone()
    {
        return CopyBaseTo(new ShapeLayer
        {
            _kind = _kind,
            _width = _width,
            _height = _height,
            _strokeColor = _strokeColor,
            _strokeWidth = _strokeWidth,
            _fillColor = _fillColor,
            _dash = _dash,
            _cornerRadius = _cornerRadius,
            _hasShadow = _hasShadow,
            _shadowOpacity = _shadowOpacity,
            _shadowBlur = _shadowBlur,
            _shadowOffsetX = _shadowOffsetX,
            _shadowOffsetY = _shadowOffsetY,
        });
    }

    public ShapeKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public int Width
    {
        get => _width;
        set => SetField(ref _width, value);
    }

    public int Height
    {
        get => _height;
        set => SetField(ref _height, value);
    }

    public SKColor StrokeColor
    {
        get => _strokeColor;
        set => SetField(ref _strokeColor, value);
    }

    /// <summary>Stroke width in document pixels (0 disables the stroke).</summary>
    public float StrokeWidth
    {
        get => _strokeWidth;
        set => SetField(ref _strokeWidth, Math.Max(0f, value));
    }

    /// <summary>Fill color; ignored for arrows. Transparent means no fill.</summary>
    public SKColor FillColor
    {
        get => _fillColor;
        set => SetField(ref _fillColor, value);
    }

    /// <summary>Outline style. Also applies to the shaft of an arrow.</summary>
    public ShapeDash Dash
    {
        get => _dash;
        set => SetField(ref _dash, value);
    }

    /// <summary>Corner radius of a rectangle in document pixels; limited to half the shorter side when drawn.</summary>
    public float CornerRadius
    {
        get => _cornerRadius;
        set => SetField(ref _cornerRadius, Math.Max(0f, value));
    }

    /// <summary>Whether the drop shadow is drawn. The shadow follows the drawn outline and fill (an unfilled shape casts a ring).</summary>
    public bool HasShadow
    {
        get => _hasShadow;
        set => SetField(ref _hasShadow, value);
    }

    public float ShadowOpacity
    {
        get => _shadowOpacity;
        set => SetField(ref _shadowOpacity, Math.Clamp(value, 0f, 1f));
    }

    public float ShadowBlur
    {
        get => _shadowBlur;
        set => SetField(ref _shadowBlur, Math.Max(0f, value));
    }

    public float ShadowOffsetX
    {
        get => _shadowOffsetX;
        set => SetField(ref _shadowOffsetX, value);
    }

    public float ShadowOffsetY
    {
        get => _shadowOffsetY;
        set => SetField(ref _shadowOffsetY, value);
    }
}
