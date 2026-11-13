using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Editing;

namespace Trimx.UI;

// Pen mode: dragging on the canvas draws a freehand stroke as a new layer.
public partial class CanvasView
{
    // Samples closer than this (in device pixels) are dropped, which keeps strokes light without visible loss.
    private const float PenMinSampleDistance = 1.5f;

    private bool _isPenMode;
    private List<SKPoint>? _penPoints;

    /// <summary>Raised when pen mode is switched, including by the Escape key.</summary>
    public event EventHandler? PenModeChanged;

    public bool IsPenMode
    {
        get => _isPenMode;
        set
        {
            if (_isPenMode == value)
            {
                return;
            }

            if (value && _isSelectMode)
            {
                IsSelectMode = false;
            }

            EndDrag();
            _isPenMode = value;
            Cursor = ModeCursor;
            Surface.InvalidateVisual();
            PenModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SKColor PenColor { get; set; } = SKColors.Red;

    /// <summary>Pen thickness in document pixels.</summary>
    public float PenWidth { get; set; } = 6f;

    private void BeginStroke(DocumentTab tab, Point position)
    {
        _dragMode = DragMode.Pen;
        _penPoints = new List<SKPoint> { ToDocument(tab, position) };
        Surface.CaptureMouse();
        Surface.InvalidateVisual();
    }

    private void ContinueStroke(DocumentTab tab, Point position)
    {
        if (_penPoints is null)
        {
            return;
        }

        var point = ToDocument(tab, position);
        if (SKPoint.Distance(point, _penPoints[^1]) * tab.Zoom >= PenMinSampleDistance * DeviceScale
            && _penPoints.Count < PaintStrokeCommand.MaxPoints)
        {
            _penPoints.Add(point);
            Surface.InvalidateVisual();
        }
    }

    // A visible image layer that is selected takes the stroke in its pixels. Anything else (text, shapes,
    // nothing selected) gets a new transparent layer, which is then selected so the next strokes follow into it.
    private void CommitStroke()
    {
        var points = _penPoints;
        _penPoints = null;
        if (points is { Count: > 0 } && _tab is not null)
        {
            try
            {
                if (_tab.SelectedLayer is ImageLayer { IsVisible: true } target)
                {
                    if (PaintStrokeCommand.Create(target, points, PenColor, PenWidth) is { } command)
                    {
                        _tab.Execute(command);
                    }
                }
                else
                {
                    _tab.AddLayerAboveSelection(LayerFactory.CreateStrokeImage(_tab.Document, points, PenColor, PenWidth, "ペン"));
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or OutOfMemoryException)
            {
                // Typically a failed pixel allocation for a very large canvas; the stroke is dropped.
                MessageBox.Show(Window.GetWindow(this), $"線を描けませんでした。\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        Surface.InvalidateVisual();
    }
}
