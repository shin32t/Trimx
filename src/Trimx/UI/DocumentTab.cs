using System.ComponentModel;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Editing;
using Trimx.Core.Rendering;
using SkiaSharp;

namespace Trimx.UI;

/// <summary>
/// A document opened in a tab, together with its undo history and per-tab view state
/// (zoom, scroll, selected layer). Owns the document and disposes it with the tab.
/// </summary>
/// <remarks>All document edits go through <see cref="History"/> so they can be undone.</remarks>
public sealed class DocumentTab : INotifyPropertyChanged, IDisposable
{
    private Layer? _selectedLayer;
    private string _title;
    private float _zoom = 1f;
    private SKRectI? _selection;

    public DocumentTab(ImageDocument document)
    {
        Document = document;
        _title = document.Title;
        _selectedLayer = document.Layers.Count > 0 ? document.Layers[^1] : null;
        History.Changed += (_, _) => RaiseModifiedChanged();

        // A selection is in canvas pixels, so it means nothing once the canvas changes size.
        Document.SizeChanged += (_, _) => Selection = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageDocument Document { get; }

    public CommandHistory History { get; } = new();

    /// <summary>Text of the tab without the modified mark. Follows the file name once the document is saved or opened from a file.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
            }
        }
    }

    /// <summary>True if the document has changes that were not saved or exported.</summary>
    public bool IsModified => History.IsModified;

    /// <summary>The title shown on the tab: <see cref="Title"/> with a mark while there are unsaved changes.</summary>
    public string DisplayTitle => IsModified ? _title + " ●" : _title;

    /// <summary>Records the current state as saved, so <see cref="IsModified"/> becomes false.</summary>
    public void MarkSaved() => History.MarkSaved();

    private void RaiseModifiedChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
    }

    /// <summary>The project file this document was opened from or last saved to; null if it has none yet.</summary>
    public string? FilePath { get; set; }

    public Layer? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (ReferenceEquals(_selectedLayer, value))
            {
                return;
            }

            _selectedLayer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLayer)));
        }
    }

    /// <summary>The selected rectangle in document pixels, or null. It is view state and not part of the undo history.</summary>
    public SKRectI? Selection
    {
        get => _selection;
        set
        {
            if (_selection != value)
            {
                _selection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selection)));
            }
        }
    }

    /// <summary>Crops the canvas to the selection (raster layers are trimmed to it), then clears the selection.</summary>
    public bool CropToSelection()
    {
        if (_selection is not { } area)
        {
            return false;
        }

        var command = CropCanvasCommand.Create(Document, area);
        var selectedIndex = _selectedLayer is null ? -1 : Document.IndexOf(_selectedLayer);
        KeepZoomOnSizeChange = true;
        try
        {
            History.Execute(command);
        }
        finally
        {
            KeepZoomOnSizeChange = false;
        }

        // Raster layers were replaced by trimmed copies; keep the same position in the stack selected.
        if (selectedIndex >= 0 && selectedIndex < Document.Layers.Count)
        {
            SelectedLayer = Document.Layers[selectedIndex];
        }
        else
        {
            RestoreSelection(null);
        }

        return true;
    }

    /// <summary>Zoom factor (document pixel to device pixel).</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            if (_zoom != value)
            {
                _zoom = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Zoom)));
            }
        }
    }

    /// <summary>Horizontal scroll position in device pixels (used only while the image is wider than the view).</summary>
    public double ScrollX { get; set; }

    /// <summary>Vertical scroll position in device pixels (used only while the image is taller than the view).</summary>
    public double ScrollY { get; set; }

    /// <summary>True until the view has been fitted to the canvas size once.</summary>
    public bool NeedsFit { get; set; } = true;

    /// <summary>Inserts a layer just above the selected one (or on top) and selects it. Takes ownership of the layer.</summary>
    public void AddLayerAboveSelection(Layer layer)
    {
        var index = _selectedLayer is null ? -1 : Document.IndexOf(_selectedLayer);
        History.Execute(new AddLayerCommand(Document, layer, index < 0 ? Document.Layers.Count : index + 1));
        SelectedLayer = layer;
    }

    /// <summary>Moves the selected layer up (positive) or down (negative) in the stack.</summary>
    public void MoveSelectedLayer(int delta)
    {
        if (_selectedLayer is null)
        {
            return;
        }

        var index = Document.IndexOf(_selectedLayer);
        if (index >= 0)
        {
            MoveLayerTo(_selectedLayer, index + delta);
        }
    }

    /// <summary>Moves a layer to a stacking index (0 is the bottom); out-of-range values are clamped.</summary>
    public void MoveLayerTo(Layer layer, int newIndex)
    {
        var index = Document.IndexOf(layer);
        newIndex = Math.Clamp(newIndex, 0, Document.Layers.Count - 1);
        if (index >= 0 && newIndex != index)
        {
            History.Execute(new MoveLayerCommand(Document, layer, newIndex));
        }
    }

    /// <summary>Adds a transparent, canvas-sized image layer above the selection.</summary>
    public void AddEmptyLayer(string name)
    {
        AddLayerAboveSelection(LayerFactory.CreateEmptyImage(Document, name));
    }

    /// <summary>Duplicates the selected layer just above itself and selects the copy.</summary>
    public void DuplicateSelectedLayer(string copyNameFormat)
    {
        if (_selectedLayer is not { } layer)
        {
            return;
        }

        var copy = layer.Clone();
        copy.Name = string.Format(copyNameFormat, layer.Name);
        AddLayerAboveSelection(copy);
    }

    /// <summary>True if the selected layer can be merged into the one below it.</summary>
    public bool CanMergeSelectedDown()
    {
        // Hidden layers contribute nothing, so merging them would silently drop content.
        return _selectedLayer is { IsVisible: true } layer
            && Document.IndexOf(layer) is > 0 and var index
            && Document.Layers[index - 1].IsVisible;
    }

    /// <summary>
    /// Merges the selected layer into the one below. Returns false if that is not possible,
    /// including when the merged area would exceed the size limit.
    /// </summary>
    public bool MergeSelectedDown()
    {
        if (!CanMergeSelectedDown() || _selectedLayer is not { } upper)
        {
            return false;
        }

        var lower = Document.Layers[Document.IndexOf(upper) - 1];
        var merged = LayerMerger.TryMerge(upper, lower);
        if (merged is null)
        {
            return false;
        }

        History.Execute(new MergeLayersCommand(Document, upper, lower, merged));
        SelectedLayer = merged;
        return true;
    }

    /// <summary>Removes the selected layer, then selects the layer below it.</summary>
    public void RemoveSelectedLayer()
    {
        var layer = _selectedLayer;
        if (layer is null)
        {
            return;
        }

        var index = Document.IndexOf(layer);
        if (index < 0)
        {
            return;
        }

        var layers = Document.Layers;
        SelectedLayer = index > 0 ? layers[index - 1] : layers.Count > 1 ? layers[1] : null;
        History.Execute(new RemoveLayerCommand(Document, layer));
    }

    /// <summary>
    /// Runs a command through the history. Pass <paramref name="merge"/> for each step of a continuous
    /// edit (typing, dragging), and call <c>History.EndMerge()</c> when it ends.
    /// </summary>
    public void Execute(IDocumentCommand command, bool merge = false) => History.Execute(command, merge);

    /// <summary>Converts the selected text / shape layer to an image layer. Returns false if it cannot be rasterized.</summary>
    public bool RasterizeSelectedLayer()
    {
        if (_selectedLayer is not { } layer)
        {
            return false;
        }

        var raster = LayerRasterizer.TryRasterize(layer);
        if (raster is null)
        {
            return false;
        }

        History.Execute(new ReplaceLayerCommand(Document, layer, raster));
        SelectedLayer = raster;
        return true;
    }

    /// <summary>Changes the canvas size; layers keep their pixels and are shifted by the anchor (0..2 per axis).</summary>
    public void ResizeCanvas(int width, int height, int anchorX, int anchorY)
    {
        History.Execute(new ResizeCanvasCommand(Document, width, height, anchorX, anchorY));
    }

    /// <summary>Scales the whole image (resampling raster layers).</summary>
    public void ResizeImage(int width, int height)
    {
        History.Execute(ResizeImageCommand.Create(Document, width, height));

        // A selected raster layer was replaced by a resampled copy; fall back to the top layer.
        RestoreSelection(null);
    }

    public void SetLayerVisibility(Layer layer, bool isVisible)
    {
        if (layer.IsVisible != isVisible)
        {
            History.Execute(LayerCommands.SetVisibility(layer, isVisible));
        }
    }

    /// <summary>
    /// Sets opacity. Consecutive calls for the same layer merge into one undo step
    /// until <see cref="CommandHistory.EndMerge"/> is called.
    /// </summary>
    public void SetLayerOpacity(Layer layer, float opacity)
    {
        if (layer.Opacity != Math.Clamp(opacity, 0f, 1f))
        {
            History.Execute(LayerCommands.SetOpacity(layer, opacity), allowMerge: true);
        }
    }

    public void Undo()
    {
        KeepZoomOnSizeChange = true;
        try
        {
            RestoreSelection(History.Undo());
        }
        finally
        {
            KeepZoomOnSizeChange = false;
        }
    }

    public void Redo()
    {
        KeepZoomOnSizeChange = true;
        try
        {
            RestoreSelection(History.Redo());
        }
        finally
        {
            KeepZoomOnSizeChange = false;
        }
    }

    /// <summary>
    /// True while an edit changes the canvas size but the view should keep its zoom (crop, undo, redo),
    /// so the image does not jump to a different magnification.
    /// </summary>
    public bool KeepZoomOnSizeChange { get; private set; }

    public void Dispose()
    {
        _selectedLayer = null;

        // The history first, so commands release only the layers the document no longer holds.
        History.Dispose();
        Document.Dispose();
    }

    /// <summary>Selects the layer affected by undo / redo, or keeps the selection valid.</summary>
    private void RestoreSelection(IDocumentCommand? command)
    {
        if (command?.TargetLayer is { } target && Document.IndexOf(target) >= 0)
        {
            SelectedLayer = target;
        }
        else if (_selectedLayer is null || Document.IndexOf(_selectedLayer) < 0)
        {
            SelectedLayer = Document.Layers.Count > 0 ? Document.Layers[^1] : null;
        }
    }
}
