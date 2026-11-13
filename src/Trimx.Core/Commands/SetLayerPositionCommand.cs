using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Moves a layer. Consecutive moves of the same layer merge, so a drag is one undo step.
/// </summary>
/// <remarks>The old position is captured at construction, so create the command right before executing it.</remarks>
public sealed class SetLayerPositionCommand : IMergeableCommand
{
    private readonly Layer _layer;
    private readonly int _oldX;
    private readonly int _oldY;
    private int _newX;
    private int _newY;

    public SetLayerPositionCommand(Layer layer, int x, int y)
    {
        _layer = layer;
        _oldX = layer.X;
        _oldY = layer.Y;
        _newX = x;
        _newY = y;
    }

    public Layer? TargetLayer => _layer;

    public void Execute()
    {
        _layer.X = _newX;
        _layer.Y = _newY;
    }

    public void Undo()
    {
        _layer.X = _oldX;
        _layer.Y = _oldY;
    }

    public bool TryMerge(IDocumentCommand next)
    {
        if (next is SetLayerPositionCommand other && ReferenceEquals(other._layer, _layer))
        {
            _newX = other._newX;
            _newY = other._newY;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
    }
}
