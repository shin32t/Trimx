using Trimx.Core.Documents;

namespace Trimx.Core.Commands;

/// <summary>
/// Changes one property of a layer. Consecutive changes of the same property on the same layer
/// can be merged into one undo step.
/// </summary>
/// <remarks>The old value is captured at construction, so create the command right before executing it.</remarks>
public sealed class SetLayerPropertyCommand<TLayer, TValue> : IMergeableCommand
    where TLayer : Layer
{
    private readonly TLayer _layer;
    private readonly string _propertyName;
    private readonly Action<TLayer, TValue> _setter;
    private readonly TValue _oldValue;
    private TValue _newValue;

    public SetLayerPropertyCommand(
        TLayer layer,
        string propertyName,
        Func<TLayer, TValue> getter,
        Action<TLayer, TValue> setter,
        TValue newValue)
    {
        _layer = layer;
        _propertyName = propertyName;
        _setter = setter;
        _oldValue = getter(layer);
        _newValue = newValue;
    }

    public Layer? TargetLayer => _layer;

    public void Execute() => _setter(_layer, _newValue);

    public void Undo() => _setter(_layer, _oldValue);

    public bool TryMerge(IDocumentCommand next)
    {
        if (next is SetLayerPropertyCommand<TLayer, TValue> other
            && ReferenceEquals(other._layer, _layer)
            && other._propertyName == _propertyName)
        {
            _newValue = other._newValue;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
    }
}
