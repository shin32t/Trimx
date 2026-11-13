using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Resizes the whole image: the canvas and every layer are scaled. Raster layers are resampled;
/// text and shapes stay editable and have their geometry scaled instead.
/// </summary>
/// <remarks>
/// Undo needs the original bitmaps, so they are kept until the command leaves the history.
/// Create instances with <see cref="Create"/>, which allocates the resampled bitmaps up front.
/// </remarks>
public sealed class ResizeImageCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly int _newWidth;
    private readonly int _newHeight;
    private readonly List<IDocumentCommand> _steps;
    private int _oldWidth;
    private int _oldHeight;

    private ResizeImageCommand(ImageDocument document, int newWidth, int newHeight, List<IDocumentCommand> steps)
    {
        _document = document;
        _newWidth = newWidth;
        _newHeight = newHeight;
        _steps = steps;
    }

    public Layer? TargetLayer => null;

    /// <summary>Builds the command. May throw if a bitmap cannot be allocated; nothing is leaked in that case.</summary>
    public static ResizeImageCommand Create(ImageDocument document, int newWidth, int newHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newHeight);

        var scaleX = newWidth / (double)document.Width;
        var scaleY = newHeight / (double)document.Height;
        var scaleAverage = (float)((scaleX + scaleY) / 2d);
        var steps = new List<IDocumentCommand>();

        try
        {
            foreach (var layer in document.Layers)
            {
                var x = (int)Math.Round(layer.X * scaleX);
                var y = (int)Math.Round(layer.Y * scaleY);

                switch (layer)
                {
                    case ImageLayer image:
                        steps.Add(new ReplaceLayerCommand(document, image, ScaleImageLayer(image, x, y, scaleX, scaleY)));
                        break;

                    case TextLayer text:
                        steps.Add(new SetLayerPositionCommand(text, x, y));
                        steps.Add(LayerCommands.SetFontSize(text, text.FontSize * scaleAverage));
                        steps.Add(LayerCommands.SetShadowBlur(text, text.ShadowBlur * scaleAverage));
                        steps.Add(LayerCommands.SetShadowOffsetX(text, text.ShadowOffsetX * scaleAverage));
                        steps.Add(LayerCommands.SetShadowOffsetY(text, text.ShadowOffsetY * scaleAverage));
                        steps.Add(LayerCommands.SetOutlineWidth(text, text.OutlineWidth * scaleAverage));
                        break;

                    case ShapeLayer shape:
                        steps.Add(new SetLayerPositionCommand(shape, x, y));
                        steps.Add(LayerCommands.SetShapeWidth(shape, (int)Math.Round(shape.Width * scaleX)));
                        steps.Add(LayerCommands.SetShapeHeight(shape, (int)Math.Round(shape.Height * scaleY)));
                        steps.Add(LayerCommands.SetStrokeWidth(shape, shape.StrokeWidth * scaleAverage));
                        steps.Add(LayerCommands.SetCornerRadius(shape, shape.CornerRadius * scaleAverage));
                        break;
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

        return new ResizeImageCommand(document, newWidth, newHeight, steps);
    }

    public void Execute()
    {
        _oldWidth = _document.Width;
        _oldHeight = _document.Height;
        _document.SetSize(_newWidth, _newHeight);
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

    private static ImageLayer ScaleImageLayer(ImageLayer source, int x, int y, double scaleX, double scaleY)
    {
        var width = Math.Max(1, (int)Math.Round(source.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(source.Height * scaleY));

        var bitmap = new SKBitmap(width, height);
        try
        {
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint())
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale((float)width / source.Width, (float)height / source.Height);
                canvas.DrawBitmap(source.Bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            }

            return new ImageLayer(bitmap)
            {
                Name = source.Name,
                IsVisible = source.IsVisible,
                Opacity = source.Opacity,
                X = x,
                Y = y,
            };
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
