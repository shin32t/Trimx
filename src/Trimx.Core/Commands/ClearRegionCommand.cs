using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Makes a rectangle of an image layer transparent (the erasing half of "cut").
/// Only the touched rectangle is kept for undo.
/// </summary>
public sealed class ClearRegionCommand : IDocumentCommand
{
    private readonly ImageLayer _layer;
    private readonly SKRectI _area; // In layer pixels.
    private readonly SKBitmap _before;
    private readonly SKBitmap _after;

    private ClearRegionCommand(ImageLayer layer, SKRectI area, SKBitmap before, SKBitmap after)
    {
        _layer = layer;
        _area = area;
        _before = before;
        _after = after;
    }

    public Layer? TargetLayer => _layer;

    /// <summary>
    /// Builds the command for a rectangle in document pixels. Returns null if it does not touch the layer.
    /// The caller owns the command (it is not applied yet).
    /// </summary>
    public static ClearRegionCommand? Create(ImageLayer layer, SKRectI documentArea)
    {
        var area = documentArea;
        area.Offset(-layer.X, -layer.Y);
        area.Intersect(new SKRectI(0, 0, layer.Width, layer.Height));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return null;
        }

        var before = new SKBitmap(layer.Bitmap.Info.WithSize(area.Width, area.Height));
        SKBitmap? after = null;
        try
        {
            PaintStrokeCommand.CopyRows(layer.Bitmap, area.Left, area.Top, before, 0, 0, area.Width, area.Height);
            after = new SKBitmap(before.Info);
            after.Erase(SKColors.Transparent);
            return new ClearRegionCommand(layer, area, before, after);
        }
        catch
        {
            before.Dispose();
            after?.Dispose();
            throw;
        }
    }

    public void Execute() => Apply(_after);

    public void Undo() => Apply(_before);

    public void Dispose()
    {
        _before.Dispose();
        _after.Dispose();
    }

    private void Apply(SKBitmap patch)
    {
        PaintStrokeCommand.CopyRows(patch, 0, 0, _layer.Bitmap, _area.Left, _area.Top, _area.Width, _area.Height);
        _layer.NotifyPixelsChanged();
    }
}
