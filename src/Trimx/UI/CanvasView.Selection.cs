using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Editing;

namespace Trimx.UI;

// Selection mode: drag to mark a rectangle (for crop, copy, cut), then move or resize it.
public partial class CanvasView
{
    private const int MinSelectionSize = 2;

    private bool _isSelectMode;
    private SKPoint _selectionStart; // Document coordinates.
    private SKRectI _selectionOriginal;
    private int _selectionHandle;

    /// <summary>Raised when selection mode is switched, including when another tool turns it off.</summary>
    public event EventHandler? SelectModeChanged;

    public bool IsSelectMode
    {
        get => _isSelectMode;
        set
        {
            if (_isSelectMode == value)
            {
                return;
            }

            // Only one tool at a time.
            if (value && _isPenMode)
            {
                IsPenMode = false;
            }

            EndDrag();
            _isSelectMode = value;
            Cursor = ModeCursor;
            Surface.InvalidateVisual();
            SelectModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Cursor ModeCursor => IsPenMode ? PenCursor.Instance : IsSelectMode ? Cursors.Cross : Cursors.Arrow;

    /// <summary>Drops the selection and leaves selection mode.</summary>
    private void ClearSelection()
    {
        if (_tab is not null)
        {
            _tab.Selection = null;
        }

        IsSelectMode = false;
    }

    private void BeginSelection(DocumentTab tab, Point position)
    {
        var point = ToDocument(tab, position);
        if (tab.Selection is { } selection)
        {
            var handle = HitTestSelectionHandle(tab, selection, position);
            if (handle >= 0)
            {
                StartSelectionDrag(DragMode.SelectResize, point, selection, handle);
                return;
            }

            if (selection.Contains((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)))
            {
                StartSelectionDrag(DragMode.SelectMove, point, selection, 0);
                return;
            }
        }

        // Anywhere else starts a new rectangle; a plain click (no drag) therefore clears the selection.
        StartSelectionDrag(DragMode.SelectNew, ClampToCanvas(tab, point), default, 0);
        tab.Selection = null;
    }

    private void StartSelectionDrag(DragMode mode, SKPoint start, SKRectI original, int handle)
    {
        _dragMode = mode;
        _selectionStart = start;
        _selectionOriginal = original;
        _selectionHandle = handle;
        Surface.CaptureMouse();
    }

    private void ContinueSelection(DocumentTab tab, Point position)
    {
        var point = ToDocument(tab, position);
        switch (_dragMode)
        {
            case DragMode.SelectNew:
            {
                var end = ClampToCanvas(tab, point);
                var rect = FromCorners(_selectionStart, end);
                tab.Selection = rect.Width >= 1 && rect.Height >= 1 ? rect : null;
                break;
            }

            case DragMode.SelectMove:
            {
                var dx = (int)MathF.Round(point.X - _selectionStart.X);
                var dy = (int)MathF.Round(point.Y - _selectionStart.Y);

                // The whole rectangle stays inside the canvas.
                dx = Math.Clamp(dx, -_selectionOriginal.Left, tab.Document.Width - _selectionOriginal.Right);
                dy = Math.Clamp(dy, -_selectionOriginal.Top, tab.Document.Height - _selectionOriginal.Bottom);
                var moved = _selectionOriginal;
                moved.Offset(dx, dy);
                tab.Selection = moved;
                break;
            }

            case DragMode.SelectResize:
            {
                var geometry = ShapeHandles.Drag(
                    ShapeKind.Rectangle,
                    new ShapeGeometry(_selectionOriginal.Left, _selectionOriginal.Top, _selectionOriginal.Width, _selectionOriginal.Height),
                    _selectionHandle,
                    ClampToCanvas(tab, point));
                var rect = SKRect.Create(geometry.X, geometry.Y, geometry.Width, geometry.Height).Standardized;
                var rounded = new SKRectI((int)MathF.Round(rect.Left), (int)MathF.Round(rect.Top), (int)MathF.Round(rect.Right), (int)MathF.Round(rect.Bottom));
                tab.Selection = rounded.Width >= 1 && rounded.Height >= 1 ? rounded : _selectionOriginal;
                break;
            }
        }
    }

    // A tiny rectangle is almost always a stray click, so it does not count as a selection.
    private void FinishSelection()
    {
        if (_tab?.Selection is { } selection && (selection.Width < MinSelectionSize || selection.Height < MinSelectionSize))
        {
            _tab.Selection = null;
        }
    }

    private static SKPoint ClampToCanvas(DocumentTab tab, SKPoint point)
    {
        return new SKPoint(
            MathF.Round(Math.Clamp(point.X, 0, tab.Document.Width)),
            MathF.Round(Math.Clamp(point.Y, 0, tab.Document.Height)));
    }

    private static SKRectI FromCorners(SKPoint a, SKPoint b)
    {
        return new SKRectI(
            (int)Math.Min(a.X, b.X),
            (int)Math.Min(a.Y, b.Y),
            (int)Math.Max(a.X, b.X),
            (int)Math.Max(a.Y, b.Y));
    }

    private static SKPoint[] GetSelectionHandlePoints(SKRectI r)
    {
        // Same order as ShapeHandles: top-left, top, top-right, right, bottom-right, bottom, bottom-left, left.
        float midX = (r.Left + r.Right) / 2f;
        float midY = (r.Top + r.Bottom) / 2f;
        return new[]
        {
            new SKPoint(r.Left, r.Top),
            new SKPoint(midX, r.Top),
            new SKPoint(r.Right, r.Top),
            new SKPoint(r.Right, midY),
            new SKPoint(r.Right, r.Bottom),
            new SKPoint(midX, r.Bottom),
            new SKPoint(r.Left, r.Bottom),
            new SKPoint(r.Left, midY),
        };
    }

    private int HitTestSelectionHandle(DocumentTab tab, SKRectI selection, Point position)
    {
        var scale = DeviceScale;
        var origin = GetOrigin(tab, (float)(Surface.ActualWidth * scale), (float)(Surface.ActualHeight * scale));
        var pointer = new SKPoint((float)position.X * scale, (float)position.Y * scale);

        var best = -1;
        var bestDistance = HandleGrabRadius * scale;
        var points = GetSelectionHandlePoints(selection);
        for (var i = 0; i < points.Length; i++)
        {
            var distance = SKPoint.Distance(new SKPoint(origin.X + points[i].X * tab.Zoom, origin.Y + points[i].Y * tab.Zoom), pointer);
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private Cursor GetSelectionHoverCursor(DocumentTab tab, Point position)
    {
        if (tab.Selection is { } selection)
        {
            var handle = HitTestSelectionHandle(tab, selection, position);
            if (handle >= 0)
            {
                return GetHandleCursor(ShapeKind.Rectangle, handle);
            }

            var point = ToDocument(tab, position);
            if (selection.Contains((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y)))
            {
                return Cursors.SizeAll;
            }
        }

        return Cursors.Cross;
    }

    /// <summary>Dims everything outside the selection and outlines it; handles show only while selecting.</summary>
    private void DrawSelection(SKCanvas canvas, SKRectI selection, SKSize canvasSize, float zoom, float deviceScale)
    {
        var rect = SKRect.Create(selection.Left, selection.Top, selection.Width, selection.Height);

        canvas.Save();
        canvas.ClipRect(SKRect.Create(canvasSize.Width, canvasSize.Height));
        canvas.ClipRect(rect, SKClipOperation.Difference);
        // Clear would replace the pixels with this color; DrawColor blends it over them so the image stays visible.
        canvas.DrawColor(new SKColor(0, 0, 0, 110), SKBlendMode.SrcOver);
        canvas.Restore();

        // Light solid line under a dark dashed one stays readable on any image. Widths are divided by
        // zoom so the outline is 1 device pixel at any zoom.
        using var under = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f / zoom, Color = SKColors.White };
        using var dashes = SKPathEffect.CreateDash(new[] { 4f / zoom, 4f / zoom }, 0f);
        using var over = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f / zoom, Color = SKColors.Black, PathEffect = dashes };
        canvas.DrawRect(rect, under);
        canvas.DrawRect(rect, over);

        if (!IsSelectMode)
        {
            return;
        }

        var size = HandleSize * deviceScale / zoom;
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White };
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f / zoom, Color = new SKColor(0x3A, 0x7B, 0xD5) };
        foreach (var point in GetSelectionHandlePoints(selection))
        {
            var box = SKRect.Create(point.X - size / 2f, point.Y - size / 2f, size, size);
            canvas.DrawRect(box, fill);
            canvas.DrawRect(box, stroke);
        }
    }
}
