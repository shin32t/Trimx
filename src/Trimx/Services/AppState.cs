namespace Trimx.Services;

/// <summary>
/// What the app remembers between sessions without being asked: the last values used for drawing,
/// so the next text, shape or pen stroke starts from them. Saved as TrimxState.json.
/// </summary>
public sealed class AppState
{
    /// <summary>Size of the last image created via "New". Defaults to the first-run size.</summary>
    public int LastNewImageWidth { get; set; } = 1920;

    public int LastNewImageHeight { get; set; } = 1080;

    /// <summary>Colors chosen in the color picker, newest first, as #RRGGBB. Shared by every tool that uses colors.</summary>
    public List<string> MyColors { get; set; } = new();

    public PenStyle Pen { get; set; } = new();

    /// <summary>Last text settings, or null to use the built-in defaults.</summary>
    public TextStyle? Text { get; set; }

    /// <summary>Last shape settings, or null to use the built-in defaults.</summary>
    public ShapeStyle? Shape { get; set; }

    /// <summary>Width of the side panel (inspector and layers) in device-independent pixels; null for the default.</summary>
    public double? SidePanelWidth { get; set; }

    /// <summary>Height of the inspector above the layer list; null for the default.</summary>
    public double? InspectorHeight { get; set; }
}

public sealed class PenStyle
{
    public string Color { get; set; } = "#FFE81F1F";

    public float Width { get; set; } = 6f;
}

/// <summary>Look of a text layer. Colors are #AARRGGBB.</summary>
public sealed class TextStyle
{
    public string FontFamily { get; set; } = string.Empty;

    public float FontSize { get; set; }

    public string Color { get; set; } = string.Empty;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool HasShadow { get; set; }

    public float ShadowOpacity { get; set; }

    public float ShadowBlur { get; set; }

    public float ShadowOffsetX { get; set; }

    public float ShadowOffsetY { get; set; }

    public float OutlineWidth { get; set; }

    public string OutlineColor { get; set; } = string.Empty;
}

/// <summary>Look of a shape layer (not its position or size). Colors are #AARRGGBB.</summary>
public sealed class ShapeStyle
{
    public string StrokeColor { get; set; } = string.Empty;

    public float StrokeWidth { get; set; }

    public string FillColor { get; set; } = string.Empty;

    public string Dash { get; set; } = string.Empty;

    public float CornerRadius { get; set; }

    /// <summary>Last width and height per shape kind ("Rectangle", "Line", ...), since a line is shaped differently from a box.</summary>
    public Dictionary<string, ShapeSize> Sizes { get; set; } = new();
}

public sealed class ShapeSize
{
    public int Width { get; set; }

    public int Height { get; set; }
}
