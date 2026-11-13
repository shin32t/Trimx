using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Rendering;

/// <summary>
/// Composites all layers of an <see cref="ImageDocument"/> onto an SKCanvas in document coordinates.
/// </summary>
public static class CanvasRenderer
{
    /// <param name="exclude">A layer to skip, e.g. one being edited in place by an overlay.</param>
    public static void Render(ImageDocument document, SKCanvas canvas, SKSamplingOptions sampling, Layer? exclude = null)
    {
        foreach (var layer in document.Layers)
        {
            if (layer.IsVisible && layer.Opacity > 0f && !ReferenceEquals(layer, exclude))
            {
                LayerRenderer.Draw(layer, canvas, sampling);
            }
        }
    }
}
