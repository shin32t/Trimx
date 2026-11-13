using Trimx.Core.Documents;

namespace Trimx.Core.Editing;

/// <summary>Position and size of a shape layer.</summary>
public readonly record struct ShapeGeometry(int X, int Y, int Width, int Height)
{
    public static ShapeGeometry From(ShapeLayer shape) => new(shape.X, shape.Y, shape.Width, shape.Height);
}
