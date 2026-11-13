using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Editing;

/// <summary>
/// Creates new documents.
/// </summary>
public static class DocumentFactory
{
    /// <summary>Creates a document the size of the bitmap with it as the only layer. Takes ownership of the bitmap.</summary>
    public static ImageDocument CreateFromImage(SKBitmap bitmap, string title, string layerName)
    {
        var document = new ImageDocument(bitmap.Width, bitmap.Height, title);
        document.AddLayer(new ImageLayer(bitmap) { Name = layerName });
        return document;
    }

    /// <summary>
    /// Creates an empty document. A white or black background becomes an image layer;
    /// a transparent background means no layer at all.
    /// </summary>
    public static ImageDocument CreateBlank(int width, int height, CanvasBackground background, string title, string backgroundLayerName)
    {
        var document = new ImageDocument(width, height, title);
        SKColor? color = background switch
        {
            CanvasBackground.White => SKColors.White,
            CanvasBackground.Black => SKColors.Black,
            _ => null,
        };

        if (color is { } fill)
        {
            var bitmap = new SKBitmap(width, height);
            bitmap.Erase(fill);
            document.AddLayer(new ImageLayer(bitmap) { Name = backgroundLayerName });
        }

        return document;
    }
}
