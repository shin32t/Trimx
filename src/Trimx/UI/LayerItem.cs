using System.ComponentModel;
using System.Windows.Media.Imaging;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// A row of the layer list: the layer plus its cached thumbnail.
/// </summary>
/// <remarks>
/// The thumbnail is rendered once and refreshed only when the layer's appearance changes,
/// not on every move or opacity change, so dragging stays cheap even with large images.
/// </remarks>
public sealed class LayerItem : INotifyPropertyChanged, IDisposable
{
    // Rendered larger than it is shown, so it stays sharp on high-DPI displays.
    private const int ThumbnailPixels = 72;

    private BitmapSource? _thumbnail;

    public LayerItem(Layer layer)
    {
        Layer = layer;
        _thumbnail = CreateThumbnail();
        layer.PropertyChanged += OnLayerPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Layer Layer { get; }

    public BitmapSource? Thumbnail => _thumbnail;

    public void Dispose()
    {
        Layer.PropertyChanged -= OnLayerPropertyChanged;
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // These do not change what the layer looks like by itself.
        if (e.PropertyName is nameof(Layer.Name) or nameof(Layer.IsVisible) or nameof(Layer.Opacity)
            or nameof(Layer.X) or nameof(Layer.Y))
        {
            return;
        }

        _thumbnail = CreateThumbnail();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
    }

    private BitmapSource? CreateThumbnail()
    {
        try
        {
            using var bitmap = LayerThumbnailRenderer.Render(Layer, ThumbnailPixels);
            return BitmapConverter.ToBitmapSource(bitmap);
        }
        catch (Exception)
        {
            // The thumbnail is decoration; a layer that cannot be drawn small just shows an empty box.
            return null;
        }
    }
}
