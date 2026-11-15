using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;

namespace Trimx.UI;

// Caches the composited layers at 100% so that resizing the window, scrolling and zooming out
// only scale one image instead of redrawing every layer on each frame.
public partial class CanvasView
{
    // Beyond this the cache would cost too much memory (about 128 MB), so such documents are drawn directly.
    private const long MaxCachePixels = 32L * 1024 * 1024;

    private SKImage? _composite;
    private bool _compositeDirty = true;

    // The layer that was left out when the cache was built (the one being edited in place).
    private Layer? _compositeExclude;

    private SKBitmap? _checkerTile;

    /// <summary>Drops the cached image; it is rebuilt on the next paint that needs it.</summary>
    private void InvalidateComposite() => _compositeDirty = true;

    private void ReleaseCaches()
    {
        _composite?.Dispose();
        _composite = null;
        _compositeDirty = true;
        _checkerTile?.Dispose();
        _checkerTile = null;
    }

    /// <summary>
    /// Returns the composite of all visible layers in document coordinates, or null when the document
    /// is too large to cache. Vector layers are rasterized at 100%, so callers use this only when zoom is 1 or less.
    /// </summary>
    private SKImage? GetComposite(ImageDocument document)
    {
        if ((long)document.Width * document.Height > MaxCachePixels)
        {
            return null;
        }

        if (_composite is not null
            && !_compositeDirty
            && ReferenceEquals(_compositeExclude, _editingLayer)
            && _composite.Width == document.Width
            && _composite.Height == document.Height)
        {
            return _composite;
        }

        _composite?.Dispose();
        _composite = null;

        using var surface = SKSurface.Create(new SKImageInfo(document.Width, document.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null)
        {
            return null;
        }

        var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        CanvasRenderer.Render(document, surface.Canvas, sampling, exclude: _editingLayer);

        // The snapshot is immutable, so Skia can keep its mipmaps between frames.
        _composite = surface.Snapshot();
        _compositeExclude = _editingLayer;
        _compositeDirty = false;
        return _composite;
    }

    /// <summary>Fills the document area with a checkerboard using one small repeating tile.</summary>
    private void DrawCheckerboard(SKCanvas canvas, int width, int height)
    {
        const int cell = 8;
        if (_checkerTile is null)
        {
            var tile = new SKBitmap(cell * 2, cell * 2);
            using (var tileCanvas = new SKCanvas(tile))
            using (var light = new SKPaint { Color = new SKColor(0xCC, 0xCC, 0xCC) })
            using (var dark = new SKPaint { Color = new SKColor(0x99, 0x99, 0x99) })
            {
                tileCanvas.DrawRect(0, 0, cell * 2, cell * 2, light);
                tileCanvas.DrawRect(cell, 0, cell, cell, dark);
                tileCanvas.DrawRect(0, cell, cell, cell, dark);
            }

            _checkerTile = tile;
        }

        using var shader = _checkerTile.ToShader(
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, width, height, paint);
    }
}
