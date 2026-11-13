using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Crops the whole image to a rectangle: the canvas takes the rectangle's size, raster layers are trimmed to
/// it (pixels outside are discarded) and every layer shifts so the rectangle's top-left becomes the origin.
/// Text and shapes stay editable and are only moved.
/// </summary>
/// <remarks>
/// Undo needs the original bitmaps, so they are kept until the command leaves the history.
/// Create instances with <see cref="Create"/>, which allocates the trimmed bitmaps up front.
/// </remarks>
public sealed class CropCanvasCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly SKRectI _area;
    private readonly List<IDocumentCommand> _steps;
    private int _oldWidth;
    private int _oldHeight;

    private CropCanvasCommand(ImageDocument document, SKRectI area, List<IDocumentCommand> steps)
    {
        _document = document;
        _area = area;
        _steps = steps;
    }

    public Layer? TargetLayer => null;

    /// <summary>Builds the command. May throw if a bitmap cannot be allocated; nothing is leaked in that case.</summary>
    /// <param name="area">The part of the current canvas to keep, in document pixels. It must lie inside the canvas.</param>
    public static CropCanvasCommand Create(ImageDocument document, SKRectI area)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.Height);

        var steps = new List<IDocumentCommand>();
        try
        {
            foreach (var layer in document.Layers)
            {
                if (layer is ImageLayer image)
                {
                    steps.Add(new ReplaceLayerCommand(document, image, TrimImageLayer(image, area)));
                }
                else
                {
                    steps.Add(new SetLayerPositionCommand(layer, layer.X - area.Left, layer.Y - area.Top));
                }
            }
        }
        catch
        {
            foreach (var step in steps)
            {
                step.Dispose();
            }

            throw;
        }

        return new CropCanvasCommand(document, area, steps);
    }

    public void Execute()
    {
        _oldWidth = _document.Width;
        _oldHeight = _document.Height;
        _document.SetSize(_area.Width, _area.Height);
        foreach (var step in _steps)
        {
            step.Execute();
        }
    }

    public void Undo()
    {
        for (var i = _steps.Count - 1; i >= 0; i--)
        {
            _steps[i].Undo();
        }

        _document.SetSize(_oldWidth, _oldHeight);
    }

    public void Dispose()
    {
        foreach (var step in _steps)
        {
            step.Dispose();
        }

        _steps.Clear();
    }

    // The part of the layer inside the area, positioned relative to the new origin. A layer entirely outside
    // the area becomes a 1x1 transparent layer so it keeps its place in the stack (and its name).
    private static ImageLayer TrimImageLayer(ImageLayer source, SKRectI area)
    {
        var layerRect = SKRectI.Create(source.X, source.Y, source.Width, source.Height);
        var visible = SKRectI.Intersect(layerRect, area);
        var isEmpty = visible.Width <= 0 || visible.Height <= 0;

        var bitmap = new SKBitmap(isEmpty ? 1 : visible.Width, isEmpty ? 1 : visible.Height);
        try
        {
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                if (!isEmpty)
                {
                    using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
                    canvas.DrawBitmap(source.Bitmap, source.X - visible.Left, source.Y - visible.Top, new SKSamplingOptions(SKFilterMode.Nearest), paint);
                }
            }

            return new ImageLayer(bitmap)
            {
                Name = source.Name,
                IsVisible = source.IsVisible,
                Opacity = source.Opacity,
                X = isEmpty ? 0 : visible.Left - area.Left,
                Y = isEmpty ? 0 : visible.Top - area.Top,
            };
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
