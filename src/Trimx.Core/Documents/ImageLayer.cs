using SkiaSharp;

namespace Trimx.Core.Documents;

/// <summary>
/// A raster image layer. Owns its bitmap and disposes it with the layer.
/// </summary>
public sealed class ImageLayer : Layer
{
    public ImageLayer(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // Skia copies mutable bitmaps every time they are drawn; marking it immutable lets
        // each frame share the pixels instead of duplicating the whole image.
        bitmap.SetImmutable();
        Bitmap = bitmap;
    }

    public SKBitmap Bitmap { get; }

    public int Width => Bitmap.Width;

    public int Height => Bitmap.Height;

    /// <summary>
    /// Call after the pixels were changed in place, so cached copies are dropped and views redraw.
    /// </summary>
    public void NotifyPixelsChanged()
    {
        Bitmap.NotifyPixelsChanged();
        RaisePropertyChanged("Pixels");
    }

    public override Layer Clone()
    {
        // The pixels are copied because each layer owns (and disposes) its own bitmap.
        var copy = Bitmap.Copy() ?? throw new InvalidOperationException("Failed to copy the layer bitmap.");
        return CopyBaseTo(new ImageLayer(copy));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Bitmap.Dispose();
        }

        base.Dispose(disposing);
    }
}
