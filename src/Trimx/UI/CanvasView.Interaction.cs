using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Editing;
using Trimx.Core.Rendering;

namespace Trimx.UI;

// Pointer and keyboard interaction: selecting, moving and resizing layers.
public partial class CanvasView
{
    // Handle size and grab radius in device-independent pixels, so they look the same at any DPI.
    private const float HandleSize = 8f;
    private const float HandleGrabRadius = 8f;

    private enum DragMode
    {
        None,
        MoveLayer,
        ResizeShape,
        Pen,
        SelectNew,
        SelectMove,
        SelectResize,
    }

    private DragMode _dragMode;
    private Layer? _dragLayer;
    private SKPoint _dragStart; // Document coordinates.
    private int _dragLayerStartX;
    private int _dragLayerStartY;
    private int _dragHandle;
    private ShapeGeometry _dragGeometry;

    private void InitializeInteraction()
    {
        Surface.MouseLeftButtonDown += OnSurfaceMouseDown;
        Surface.MouseMove += OnSurfaceMouseMove;
        Surface.MouseLeftButtonUp += OnSurfaceMouseUp;
        Surface.LostMouseCapture += (_, _) => EndDrag();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    /// <summary>Converts a point in the surface (WPF units) to document coordinates.</summary>
    private SKPoint ToDocument(DocumentTab tab, Point position)
    {
        var scale = DeviceScale;
        var origin = GetOrigin(tab, (float)(Surface.ActualWidth * scale), (float)(Surface.ActualHeight * scale));
        return new SKPoint(
            ((float)position.X * scale - origin.X) / tab.Zoom,
            ((float)position.Y * scale - origin.Y) / tab.Zoom);
    }

    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (_tab is null)
        {
            return;
        }

        var position = e.GetPosition(Surface);

        if (IsPenMode)
        {
            BeginStroke(_tab, position);
            e.Handled = true;
            return;
        }

        if (IsSelectMode)
        {
            BeginSelection(_tab, position);
            e.Handled = true;
            return;
        }

        // The handles of the selected shape come first, since they sit on top of other layers.
        if (_tab.SelectedLayer is ShapeLayer { IsVisible: true } selectedShape
            && HitTestHandle(_tab, selectedShape, position) is var handle and >= 0)
        {
            _dragMode = DragMode.ResizeShape;
            _dragLayer = selectedShape;
            _dragHandle = handle;
            _dragGeometry = ShapeGeometry.From(selectedShape);
            Surface.CaptureMouse();
            e.Handled = true;
            return;
        }

        var point = ToDocument(_tab, position);

        // Reach of about 4 screen pixels, so thin lines and arrows are easy to click.
        var hit = LayerHitTester.HitTest(_tab.Document, point, 4f * DeviceScale / _tab.Zoom);
        _tab.SelectedLayer = hit;
        if (hit is null)
        {
            return;
        }

        if (e.ClickCount == 2 && hit is TextLayer text)
        {
            BeginTextEdit(text);
            e.Handled = true;
            return;
        }

        _dragMode = DragMode.MoveLayer;
        _dragLayer = hit;
        _dragStart = point;
        _dragLayerStartX = hit.X;
        _dragLayerStartY = hit.Y;
        Surface.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (_tab is null)
        {
            return;
        }

        var position = e.GetPosition(Surface);
        switch (_dragMode)
        {
            case DragMode.None:
                UpdateHoverCursor(position);
                break;

            case DragMode.Pen:
                ContinueStroke(_tab, position);
                break;

            case DragMode.SelectNew:
            case DragMode.SelectMove:
            case DragMode.SelectResize:
                ContinueSelection(_tab, position);
                break;

            case DragMode.MoveLayer when _dragLayer is { } layer:
            {
                var point = ToDocument(_tab, position);
                var x = _dragLayerStartX + (int)MathF.Round(point.X - _dragStart.X);
                var y = _dragLayerStartY + (int)MathF.Round(point.Y - _dragStart.Y);
                if (x != layer.X || y != layer.Y)
                {
                    // Merged so the whole drag is one undo step (closed in EndDrag).
                    _tab.Execute(LayerCommands.SetPosition(layer, x, y), merge: true);
                }

                break;
            }

            case DragMode.ResizeShape when _dragLayer is ShapeLayer shape:
            {
                var geometry = ShapeHandles.Drag(shape.Kind, _dragGeometry, _dragHandle, ToDocument(_tab, position));
                if (geometry != ShapeGeometry.From(shape))
                {
                    _tab.Execute(LayerCommands.SetShapeGeometry(shape, geometry), merge: true);
                }

                break;
            }
        }
    }

    private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode != DragMode.None)
        {
            Surface.ReleaseMouseCapture(); // Ends the drag through LostMouseCapture.
        }
    }

    private void EndDrag()
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        if (_dragMode == DragMode.Pen)
        {
            CommitStroke();
        }
        else if (_dragMode is DragMode.SelectNew or DragMode.SelectMove or DragMode.SelectResize)
        {
            FinishSelection();
        }

        _dragMode = DragMode.None;
        _dragLayer = null;
        _tab?.History.EndMerge();
        Cursor = ModeCursor;
    }

    private void UpdateHoverCursor(Point position)
    {
        if (IsSelectMode && _tab is not null)
        {
            var selectionCursor = GetSelectionHoverCursor(_tab, position);
            if (!ReferenceEquals(Cursor, selectionCursor))
            {
                Cursor = selectionCursor;
            }

            return;
        }

        var cursor = IsPenMode ? PenCursor.Instance : Cursors.Arrow;
        if (!IsPenMode && _tab?.SelectedLayer is ShapeLayer { IsVisible: true } shape
            && !ReferenceEquals(shape, _editingLayer)
            && HitTestHandle(_tab, shape, position) is var handle and >= 0)
        {
            cursor = GetHandleCursor(shape.Kind, handle);
        }

        if (!ReferenceEquals(Cursor, cursor))
        {
            Cursor = cursor;
        }
    }

    private static Cursor GetHandleCursor(ShapeKind kind, int handle)
    {
        if (kind.IsSegment())
        {
            return Cursors.Cross;
        }

        return handle switch
        {
            0 or 4 => Cursors.SizeNWSE,
            2 or 6 => Cursors.SizeNESW,
            1 or 5 => Cursors.SizeNS,
            _ => Cursors.SizeWE,
        };
    }

    /// <summary>Returns the index of the handle under the pointer, or -1.</summary>
    private int HitTestHandle(DocumentTab tab, ShapeLayer shape, Point position)
    {
        var scale = DeviceScale;
        var origin = GetOrigin(tab, (float)(Surface.ActualWidth * scale), (float)(Surface.ActualHeight * scale));
        var pointerX = (float)position.X * scale;
        var pointerY = (float)position.Y * scale;

        var best = -1;
        var bestDistance = HandleGrabRadius * scale;
        var points = ShapeHandles.GetPoints(shape);
        for (var i = 0; i < points.Length; i++)
        {
            var distance = SKPoint.Distance(
                new SKPoint(origin.X + points[i].X * tab.Zoom, origin.Y + points[i].Y * tab.Zoom),
                new SKPoint(pointerX, pointerY));
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static void DrawHandles(SKCanvas canvas, ShapeLayer shape, float zoom, float deviceScale)
    {
        // Sizes are divided by zoom: the canvas is scaled, but handles should keep a constant screen size.
        var size = HandleSize * deviceScale / zoom;
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White };
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f / zoom, Color = new SKColor(0x3A, 0x7B, 0xD5) };
        foreach (var point in ShapeHandles.GetPoints(shape))
        {
            var rect = SKRect.Create(point.X - size / 2f, point.Y - size / 2f, size, size);
            canvas.DrawRect(rect, fill);
            canvas.DrawRect(rect, stroke);
        }
    }

    // --- Keyboard: arrow keys nudge the selected layer ---

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsPenMode)
        {
            IsPenMode = false;
            e.Handled = true;
            return;
        }

        // Escape also drops a selection, so there is always a way back to plain editing.
        if (e.Key == Key.Escape && (IsSelectMode || _tab?.Selection is not null))
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        if (e.Handled || _editingLayer is not null || _tab?.SelectedLayer is not { } layer
            || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
        {
            return;
        }

        var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
        var (dx, dy) = e.Key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0)
        {
            return;
        }

        // Holding the key repeats; the repeats merge into one undo step, closed in OnKeyUp.
        _tab.Execute(LayerCommands.SetPosition(layer, layer.X + dx, layer.Y + dy), merge: true);
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _tab?.History.EndMerge();
        }
    }
}
