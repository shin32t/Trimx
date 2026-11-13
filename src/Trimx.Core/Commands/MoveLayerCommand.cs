using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Changes the stacking position of a layer.
/// </summary>
public sealed class MoveLayerCommand : IDocumentCommand
{
    private readonly ImageDocument _document;
    private readonly Layer _layer;
    private readonly int _newIndex;
    private int _oldIndex = -1;

    public MoveLayerCommand(ImageDocument document, Layer layer, int newIndex)
    {
        _document = document;
        _layer = layer;
        _newIndex = newIndex;
    }

    public Layer? TargetLayer => _layer;

    public void Execute()
    {
        _oldIndex = _document.IndexOf(_layer);
        _document.MoveLayer(_layer, _newIndex);
    }

    public void Undo()
    {
        _document.MoveLayer(_layer, _oldIndex);
    }

    public void Dispose()
    {
    }
}
