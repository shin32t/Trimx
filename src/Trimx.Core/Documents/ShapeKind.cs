namespace Trimx.Core.Documents;

public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Arrow,
    Line,
}

public static class ShapeKindExtensions
{
    /// <summary>True for shapes that run from a start point to an end point (arrow, line) instead of filling a box.</summary>
    public static bool IsSegment(this ShapeKind kind) => kind is ShapeKind.Arrow or ShapeKind.Line;
}
