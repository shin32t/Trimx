using System.ComponentModel;

namespace Trimx.Core.Documents;

/// <summary>
/// A document consisting of a canvas size and an ordered list of layers (index 0 is the bottom).
/// Does not depend on WPF.
/// </summary>
public sealed class ImageDocument : IDisposable
{
    private readonly List<Layer> _layers = new();

    public ImageDocument(int width, int height, string title)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Title = title;
    }

    public string Title { get; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Raised when the canvas size changes.</summary>
    public event EventHandler? SizeChanged;

    /// <summary>Changes the canvas size without touching layers. Used by resize commands.</summary>
    internal void SetSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (width == Width && height == Height)
        {
            return;
        }

        Width = width;
        Height = height;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces a layer in place, keeping its stacking position. Ownership of the old layer
    /// passes back to the caller; the document takes ownership of the new one.
    /// </summary>
    internal void ReplaceLayer(Layer oldLayer, Layer newLayer)
    {
        var index = _layers.IndexOf(oldLayer);
        if (index < 0)
        {
            throw new InvalidOperationException("The layer does not belong to this document.");
        }

        if (_layers.Contains(newLayer))
        {
            throw new InvalidOperationException("The layer already belongs to this document.");
        }

        oldLayer.PropertyChanged -= OnLayerPropertyChanged;
        _layers[index] = newLayer;
        newLayer.PropertyChanged += OnLayerPropertyChanged;
        RaiseLayersChanged();
    }

    /// <summary>Layers from bottom to top.</summary>
    public IReadOnlyList<Layer> Layers => _layers;

    /// <summary>Raised when anything that affects rendering changes (structure or layer properties).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when layers are added, removed or reordered.</summary>
    public event EventHandler? LayersChanged;

    public int IndexOf(Layer layer) => _layers.IndexOf(layer);

    /// <summary>Adds a layer on top. The document takes ownership of the layer.</summary>
    public void AddLayer(Layer layer) => InsertLayer(_layers.Count, layer);

    /// <summary>Inserts a layer at the given index. The document takes ownership of the layer.</summary>
    public void InsertLayer(int index, Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _layers.Count);
        if (_layers.Contains(layer))
        {
            throw new InvalidOperationException("The layer already belongs to this document.");
        }

        _layers.Insert(index, layer);
        layer.PropertyChanged += OnLayerPropertyChanged;
        RaiseLayersChanged();
    }

    /// <summary>
    /// Removes a layer. Ownership passes back to the caller, who must dispose it
    /// (or keep it alive, e.g. for undo).
    /// </summary>
    public bool RemoveLayer(Layer layer)
    {
        if (!_layers.Remove(layer))
        {
            return false;
        }

        layer.PropertyChanged -= OnLayerPropertyChanged;
        RaiseLayersChanged();
        return true;
    }

    /// <summary>Moves a layer to a new index (0 is the bottom).</summary>
    public void MoveLayer(Layer layer, int newIndex)
    {
        var oldIndex = _layers.IndexOf(layer);
        if (oldIndex < 0)
        {
            throw new InvalidOperationException("The layer does not belong to this document.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, _layers.Count);
        if (oldIndex == newIndex)
        {
            return;
        }

        _layers.RemoveAt(oldIndex);
        _layers.Insert(newIndex, layer);
        RaiseLayersChanged();
    }

    public void Dispose()
    {
        foreach (var layer in _layers)
        {
            layer.PropertyChanged -= OnLayerPropertyChanged;
            layer.Dispose();
        }

        _layers.Clear();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseLayersChanged()
    {
        LayersChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
