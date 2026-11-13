using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Inserts a layer. Takes ownership of the layer: while undone, the command disposes it on release.
/// </summary>
public sealed class AddLayerCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly Layer _layer;
    private readonly int _index;
    private bool _isApplied;

    public AddLayerCommand(ImageDocument document, Layer layer, int index)
    {
        _document = document;
        _layer = layer;
        _index = index;
    }

    public Layer? TargetLayer => _layer;

    public void Execute()
    {
        _document.InsertLayer(_index, _layer);
        _isApplied = true;
    }

    public void Undo()
    {
        _document.RemoveLayer(_layer);
        _isApplied = false;
    }

    public void Dispose()
    {
        // When applied, the document owns the layer.
        if (!_isApplied)
        {
            _layer.Dispose();
        }
    }
}
