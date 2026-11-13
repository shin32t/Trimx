using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Replaces a layer with another at the same stacking position (e.g. rasterize).
/// Takes ownership of the new layer. Whichever layer is not in the document is disposed on release.
/// </summary>
public sealed class ReplaceLayerCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly Layer _oldLayer;
    private readonly Layer _newLayer;
    private bool _isApplied;

    public ReplaceLayerCommand(ImageDocument document, Layer oldLayer, Layer newLayer)
    {
        _document = document;
        _oldLayer = oldLayer;
        _newLayer = newLayer;
    }

    /// <summary>The layer that is in the document after the last Execute / Undo.</summary>
    public Layer? TargetLayer => _isApplied ? _newLayer : _oldLayer;

    public void Execute()
    {
        _document.ReplaceLayer(_oldLayer, _newLayer);
        _isApplied = true;
    }

    public void Undo()
    {
        _document.ReplaceLayer(_newLayer, _oldLayer);
        _isApplied = false;
    }

    public void Dispose()
    {
        (_isApplied ? _oldLayer : _newLayer).Dispose();
    }
}
