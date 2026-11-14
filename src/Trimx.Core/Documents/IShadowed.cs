namespace Trimx.Core.Documents;

/// <summary>
/// A layer that can cast a black drop shadow (text and shapes). The same settings and the same
/// rendering rules apply to both.
/// </summary>
public interface IShadowed
{
    public const float DefaultOpacity = 0.5f;

    public const float DefaultBlur = 4f;

    public const float DefaultOffset = 4f;

    /// <summary>Whether the drop shadow is drawn. The other shadow settings are kept while it is off.</summary>
    bool HasShadow { get; set; }

    /// <summary>Darkness of the shadow, 0 to 1. The shadow is black.</summary>
    float ShadowOpacity { get; set; }

    /// <summary>Blur radius of the shadow in document pixels; 0 is a sharp shadow.</summary>
    float ShadowBlur { get; set; }

    /// <summary>Horizontal shadow offset in document pixels (positive moves right).</summary>
    float ShadowOffsetX { get; set; }

    /// <summary>Vertical shadow offset in document pixels (positive moves down).</summary>
    float ShadowOffsetY { get; set; }
}
