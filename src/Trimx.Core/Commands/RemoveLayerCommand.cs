using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Removes a layer. While applied, the command keeps the layer alive for undo and disposes it on release.
/// </summary>
public sealed class RemoveLayerCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly Layer _layer;
    private int _index = -1;
    private bool _isApplied;

    public RemoveLayerCommand(ImageDocument document, Layer layer)
    {
        _document = document;
        _layer = layer;
    }

    public Layer? TargetLayer => _layer;

    public void Execute()
    {
        _index = _document.IndexOf(_layer);
        if (_index < 0)
        {
            throw new InvalidOperationException("The layer does not belong to this document.");
        }

        _document.RemoveLayer(_layer);
        _isApplied = true;
    }

    public void Undo()
    {
        _document.InsertLayer(_index, _layer);
        _isApplied = false;
    }

    public void Dispose()
    {
        // When not applied, the document owns the layer again.
        if (_isApplied)
        {
            _layer.Dispose();
        }
    }
}
