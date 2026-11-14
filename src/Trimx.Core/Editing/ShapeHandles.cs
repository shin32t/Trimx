using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Editing;

/// <summary>
/// The drag handles of a shape and how dragging them changes its geometry.
/// </summary>
/// <remarks>
/// Rectangles and ellipses have eight handles: 0 top-left, 1 top, 2 top-right, 3 right,
/// 4 bottom-right, 5 bottom, 6 bottom-left, 7 left. Arrows and lines have two: 0 start and 1 end.
/// </remarks>
public static class ShapeHandles
{
    public const int BoxHandleCount = 8;

    public const int ArrowHandleCount = 2;

    public static int CountFor(ShapeKind kind) => kind.IsSegment() ? ArrowHandleCount : BoxHandleCount;

    // The end that stays put while the other one is dragged.
    private static SKPoint AnchorOf(ShapeGeometry geometry, int handle) => handle == 0
        ? new SKPoint(geometry.X + geometry.Width, geometry.Y + geometry.Height)
        : new SKPoint(geometry.X, geometry.Y);

    // Eight directions, 45 degrees apart, starting to the right and turning clockwise on screen (y grows downward).
    private static readonly (int X, int Y)[] _directions = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

    /// <summary>
    /// Moves <paramref name="pointer"/> to the nearest multiple of 45 degrees around <paramref name="anchor"/>,
    /// keeping its distance from the anchor. Diagonals get equal x and y steps, so they are exactly 45 degrees on whole pixels.
    /// </summary>
    internal static SKPoint SnapToAngle(SKPoint anchor, SKPoint pointer)
    {
        var dx = pointer.X - anchor.X;
        var dy = pointer.Y - anchor.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 0.5f)
        {
            return pointer;
        }

        var step = (int)MathF.Round(MathF.Atan2(dy, dx) / (MathF.PI / 4f));
        var (ux, uy) = _directions[((step % 8) + 8) % 8];
        var reach = ux != 0 && uy != 0 ? MathF.Round(length / MathF.Sqrt(2f)) : MathF.Round(length);
        return new SKPoint(MathF.Round(anchor.X) + ux * reach, MathF.Round(anchor.Y) + uy * reach);
    }

    /// <summary>Handle positions in document coordinates.</summary>
    public static SKPoint[] GetPoints(ShapeLayer shape)
    {
        if (shape.Kind.IsSegment())
        {
            return new[]
            {
                new SKPoint(shape.X, shape.Y),
                new SKPoint(shape.X + shape.Width, shape.Y + shape.Height),
            };
        }

        var r = SKRect.Create(shape.X, shape.Y, shape.Width, shape.Height).Standardized;
        return new[]
        {
            new SKPoint(r.Left, r.Top),
            new SKPoint(r.MidX, r.Top),
            new SKPoint(r.Right, r.Top),
            new SKPoint(r.Right, r.MidY),
            new SKPoint(r.Right, r.Bottom),
            new SKPoint(r.MidX, r.Bottom),
            new SKPoint(r.Left, r.Bottom),
            new SKPoint(r.Left, r.MidY),
        };
    }

    /// <summary>
    /// Returns the geometry after dragging <paramref name="handle"/> to <paramref name="pointer"/>,
    /// computed from the geometry when the drag started so the opposite edge or end stays put even
    /// when the pointer crosses it (the shape flips).
    /// </summary>
    /// <param name="snapAngle">
    /// True (Shift held) snaps a line or an arrow to a multiple of 45 degrees around its other end, keeping the distance to the pointer.
    /// Rectangles and ellipses are not affected.
    /// </param>
    public static ShapeGeometry Drag(ShapeKind kind, ShapeGeometry original, int handle, SKPoint pointer, bool snapAngle = false)
    {
        if (snapAngle && kind.IsSegment())
        {
            pointer = SnapToAngle(AnchorOf(original, handle), pointer);
        }

        var px = (int)MathF.Round(pointer.X);
        var py = (int)MathF.Round(pointer.Y);

        if (kind.IsSegment())
        {
            // Width / height are the vector from start to end, so they keep their sign.
            return handle == 0
                ? new ShapeGeometry(px, py, original.X + original.Width - px, original.Y + original.Height - py)
                : original with { Width = px - original.X, Height = py - original.Y };
        }

        var rect = SKRect.Create(original.X, original.Y, original.Width, original.Height).Standardized;
        var left = (int)MathF.Round(rect.Left);
        var top = (int)MathF.Round(rect.Top);
        var right = (int)MathF.Round(rect.Right);
        var bottom = (int)MathF.Round(rect.Bottom);

        if (handle is 0 or 6 or 7)
        {
            left = px;
        }

        if (handle is 2 or 3 or 4)
        {
            right = px;
        }

        if (handle is 0 or 1 or 2)
        {
            top = py;
        }

        if (handle is 4 or 5 or 6)
        {
            bottom = py;
        }

        var x = Math.Min(left, right);
        var y = Math.Min(top, bottom);
        return new ShapeGeometry(x, y, Math.Max(left, right) - x, Math.Max(top, bottom) - y);
    }
}
