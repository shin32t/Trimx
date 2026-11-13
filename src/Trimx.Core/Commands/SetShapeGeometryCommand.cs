using Trimx.Core.Documents;
using Trimx.Core.Editing;

namespace Trimx.Core.Commands;

/// <summary>
/// Sets position and size of a shape in one step (dragging a handle). Consecutive changes of the
/// same shape merge, so a drag is one undo step.
/// </summary>
/// <remarks>The old geometry is captured at construction, so create the command right before executing it.</remarks>
public sealed class SetShapeGeometryCommand : IMergeableCommand
{
    private readonly ShapeLayer _shape;
    private readonly ShapeGeometry _old;
    private ShapeGeometry _new;

    public SetShapeGeometryCommand(ShapeLayer shape, ShapeGeometry geometry)
    {
        _shape = shape;
        _old = ShapeGeometry.From(shape);
        _new = geometry;
    }

    public Layer? TargetLayer => _shape;

    public void Execute() => Apply(_new);

    public void Undo() => Apply(_old);

    public bool TryMerge(IDocumentCommand next)
    {
        if (next is SetShapeGeometryCommand other && ReferenceEquals(other._shape, _shape))
        {
            _new = other._new;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
    }

    private void Apply(ShapeGeometry geometry)
    {
        _shape.X = geometry.X;
        _shape.Y = geometry.Y;
        _shape.Width = geometry.Width;
        _shape.Height = geometry.Height;
    }
}
