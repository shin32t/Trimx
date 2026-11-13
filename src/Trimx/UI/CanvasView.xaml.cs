using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// Displays the document of a <see cref="DocumentTab"/>. The image is centered in the view;
/// when zoomed larger than the view, scroll bars appear for the overflowing direction.
/// Zoom is changed with the mouse wheel. Zoom and scroll position are stored in the tab.
/// Clicking selects the layer under the pointer; dragging moves it.
/// </summary>
/// <remarks>
/// Scrolling is implemented by offsetting the drawing rather than with a ScrollViewer,
/// so the Skia surface always stays the size of the visible area.
/// All positions here are in device pixels so that 100% zoom maps one image pixel to one screen pixel.
/// </remarks>
public partial class CanvasView : UserControl
{
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 32f;
    private const double ScrollBarThickness = 12; // Must match the bar sizes in CanvasView.xaml.
    private const double ScrollStep = 48;

    private DocumentTab? _tab;

    // Set while scroll bars are updated from code, so their ValueChanged is not treated as user input.
    private bool _isSyncing;

    public CanvasView()
    {
        InitializeComponent();
        InitializeInteraction();
        InitializeTextEditor();

        // The window background follows the theme, so the canvas does too.
        Loaded += (_, _) => ThemeService.Changed += OnThemeChanged;
        Unloaded += (_, _) => ThemeService.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Surface.InvalidateVisual();

    public DocumentTab? Tab
    {
        get => _tab;
        set
        {
            if (ReferenceEquals(_tab, value))
            {
                return;
            }

            EndDrag();
            CloseTextEditor();

            if (_tab is not null)
            {
                _tab.Document.Changed -= OnDocumentChanged;
                _tab.Document.SizeChanged -= OnDocumentSizeChanged;
                _tab.PropertyChanged -= OnTabPropertyChanged;
            }

            _tab = value;

            if (_tab is not null)
            {
                _tab.Document.Changed += OnDocumentChanged;
                _tab.Document.SizeChanged += OnDocumentSizeChanged;
                _tab.PropertyChanged += OnTabPropertyChanged;
            }

            if (_tab?.NeedsFit == true)
            {
                FitToView();
            }
            else
            {
                UpdateViewport();
            }
        }
    }

    private float DeviceScale => (float)VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <summary>Zooms so the whole image fits (never above 100%) and centers it.</summary>
    public void FitToView()
    {
        if (_tab is null)
        {
            UpdateViewport();
            return;
        }

        if (Root.ActualWidth <= 0 || Root.ActualHeight <= 0)
        {
            // Not laid out yet; fit once the size is known.
            _tab.NeedsFit = true;
            return;
        }

        var scale = DeviceScale;
        var document = _tab.Document;
        _tab.Zoom = Math.Min(1f, Math.Min(
            (float)(Root.ActualWidth * scale) / document.Width,
            (float)(Root.ActualHeight * scale) / document.Height));
        _tab.ScrollX = 0;
        _tab.ScrollY = 0;
        _tab.NeedsFit = false;
        UpdateViewport();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_tab?.NeedsFit == true)
        {
            FitToView();
        }
        else
        {
            UpdateViewport();
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        SyncTextEditor();
        Surface.InvalidateVisual();
    }

    // A different canvas size is easiest to follow by fitting the view again, except for crop / undo / redo,
    // which keep the zoom the user chose (the scroll position is re-clamped to the new size).
    private void OnDocumentSizeChanged(object? sender, EventArgs e)
    {
        if (_tab?.KeepZoomOnSizeChange == true)
        {
            UpdateViewport();
        }
        else
        {
            FitToView();
        }
    }

    // Redraw for the selection outline.
    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTab.SelectedLayer) or nameof(DocumentTab.Selection))
        {
            Surface.InvalidateVisual();
        }
    }

    /// <summary>
    /// Decides which scroll bars are needed for the current zoom, clamps the scroll position
    /// and pushes the ranges to the scroll bars.
    /// </summary>
    private void UpdateViewport()
    {
        // The in-place editor is positioned for the old zoom / scroll, so it is simply closed.
        CloseTextEditor();

        if (_tab is null || Root.ActualWidth <= 0 || Root.ActualHeight <= 0)
        {
            HorizontalBar.Visibility = Visibility.Collapsed;
            VerticalBar.Visibility = Visibility.Collapsed;
            Surface.InvalidateVisual();
            return;
        }

        var scale = DeviceScale;
        var fullWidth = Root.ActualWidth * scale;
        var fullHeight = Root.ActualHeight * scale;
        var bar = ScrollBarThickness * scale;
        var contentWidth = _tab.Document.Width * (double)_tab.Zoom;
        var contentHeight = _tab.Document.Height * (double)_tab.Zoom;

        // A bar in one direction reduces the space in the other, which may require the second bar.
        var needHorizontal = contentWidth > fullWidth;
        var needVertical = contentHeight > fullHeight;
        if (needHorizontal && !needVertical)
        {
            needVertical = contentHeight > fullHeight - bar;
        }

        if (needVertical && !needHorizontal)
        {
            needHorizontal = contentWidth > fullWidth - bar;
        }

        var viewWidth = fullWidth - (needVertical ? bar : 0);
        var viewHeight = fullHeight - (needHorizontal ? bar : 0);
        var maxX = Math.Max(0, contentWidth - viewWidth);
        var maxY = Math.Max(0, contentHeight - viewHeight);
        _tab.ScrollX = needHorizontal ? Math.Clamp(_tab.ScrollX, 0, maxX) : 0;
        _tab.ScrollY = needVertical ? Math.Clamp(_tab.ScrollY, 0, maxY) : 0;

        _isSyncing = true;
        try
        {
            ConfigureBar(HorizontalBar, needHorizontal, maxX, viewWidth, _tab.ScrollX);
            ConfigureBar(VerticalBar, needVertical, maxY, viewHeight, _tab.ScrollY);
        }
        finally
        {
            _isSyncing = false;
        }

        Surface.InvalidateVisual();
    }

    private static void ConfigureBar(ScrollBar scrollBar, bool visible, double maximum, double viewport, double value)
    {
        scrollBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        scrollBar.Minimum = 0;
        scrollBar.Maximum = maximum;
        scrollBar.ViewportSize = viewport;
        scrollBar.LargeChange = viewport * 0.9;
        scrollBar.SmallChange = ScrollStep;
        scrollBar.Value = value;
    }

    private void OnScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncing || _tab is null)
        {
            return;
        }

        if (ReferenceEquals(sender, HorizontalBar))
        {
            _tab.ScrollX = e.NewValue;
        }
        else
        {
            _tab.ScrollY = e.NewValue;
        }

        Surface.InvalidateVisual();
    }

    /// <summary>Returns where the document origin is drawn on a surface of the given device size.</summary>
    private SKPoint GetOrigin(DocumentTab tab, float viewWidth, float viewHeight)
    {
        var contentWidth = tab.Document.Width * tab.Zoom;
        var contentHeight = tab.Document.Height * tab.Zoom;

        // Centered when it fits, otherwise positioned by the scroll bar.
        // Rounded to whole pixels so 100% zoom stays pixel-aligned.
        var x = contentWidth <= viewWidth ? (viewWidth - contentWidth) / 2f : -(float)tab.ScrollX;
        var y = contentHeight <= viewHeight ? (viewHeight - contentHeight) / 2f : -(float)tab.ScrollY;
        return new SKPoint(MathF.Round(x), MathF.Round(y));
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(ThemeService.CanvasBackground);

        if (_tab is null)
        {
            return;
        }

        var document = _tab.Document;
        var origin = GetOrigin(_tab, e.Info.Width, e.Info.Height);
        canvas.Save();
        canvas.Translate(origin.X, origin.Y);
        canvas.Scale(_tab.Zoom);

        DrawCheckerboard(canvas, document.Width, document.Height);

        // Layers are clipped to the canvas, as they will be on export.
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, document.Width, document.Height));
        var sampling = _tab.Zoom >= 1f
            ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        CanvasRenderer.Render(document, canvas, sampling, exclude: _editingLayer);
        if (_penPoints is not null)
        {
            LayerRenderer.DrawStroke(canvas, _penPoints, PenColor, PenWidth);
        }

        canvas.Restore();

        // Drawn outside the clip so a layer partly off the canvas can still be seen and grabbed.
        if (_tab.Selection is { } selection)
        {
            DrawSelection(canvas, selection, new SKSize(document.Width, document.Height), _tab.Zoom, DeviceScale);
        }

        if (!IsPenMode && !IsSelectMode && _tab.SelectedLayer is { IsVisible: true } selected && !ReferenceEquals(selected, _editingLayer))
        {
            DrawSelectionOutline(canvas, LayerRenderer.GetBounds(selected), _tab.Zoom);
            if (selected is ShapeLayer shape)
            {
                DrawHandles(canvas, shape, _tab.Zoom, DeviceScale);
            }
        }

        canvas.Restore();
    }

    private static void DrawSelectionOutline(SKCanvas canvas, SKRect bounds, float zoom)
    {
        // Two strokes (light under accent) stay visible on both light and dark images.
        // Widths are divided by zoom so the outline is 1 device pixel at any zoom.
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 3f / zoom, Color = SKColors.White.WithAlpha(160) };
        canvas.DrawRect(bounds, paint);
        paint.StrokeWidth = 1f / zoom;
        paint.Color = new SKColor(0x3A, 0x7B, 0xD5);
        canvas.DrawRect(bounds, paint);
    }

    private static void DrawCheckerboard(SKCanvas canvas, int width, int height)
    {
        using var light = new SKPaint { Color = new SKColor(0xCC, 0xCC, 0xCC) };
        using var dark = new SKPaint { Color = new SKColor(0x99, 0x99, 0x99) };
        const int cell = 8;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, width, height));
        canvas.DrawRect(0, 0, width, height, light);
        for (var y = 0; y < height; y += cell)
        {
            for (var x = (y / cell % 2) * cell; x < width; x += cell * 2)
            {
                canvas.DrawRect(x, y, cell, cell, dark);
            }
        }

        canvas.Restore();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_tab is null)
        {
            return;
        }

        var scale = DeviceScale;
        var pos = e.GetPosition(Surface);
        var anchor = new SKPoint((float)pos.X * scale, (float)pos.Y * scale);
        ZoomTo(_tab.Zoom * (e.Delta > 0 ? 1.1f : 1f / 1.1f), anchor);
        e.Handled = true;
    }

    /// <summary>Multiplies the zoom by a factor, keeping the center of the view fixed.</summary>
    public void ZoomByFactor(float factor)
    {
        if (_tab is not null)
        {
            ZoomTo(_tab.Zoom * factor);
        }
    }

    /// <summary>
    /// Sets the zoom (1 = one image pixel per screen pixel), clamped to the allowed range.
    /// </summary>
    /// <param name="anchor">
    /// A point in the view (device pixels) that stays over the same image point where scrolling allows it;
    /// defaults to the center of the view.
    /// </param>
    public void ZoomTo(float newZoom, SKPoint? anchor = null)
    {
        if (_tab is null)
        {
            return;
        }

        var scale = DeviceScale;
        var viewWidth = (float)(Surface.ActualWidth * scale);
        var viewHeight = (float)(Surface.ActualHeight * scale);
        var fixedPoint = anchor ?? new SKPoint(viewWidth / 2f, viewHeight / 2f);

        var oldZoom = _tab.Zoom;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (newZoom == oldZoom)
        {
            return;
        }

        var origin = GetOrigin(_tab, viewWidth, viewHeight);
        var documentX = (fixedPoint.X - origin.X) / oldZoom;
        var documentY = (fixedPoint.Y - origin.Y) / oldZoom;
        _tab.Zoom = newZoom;
        _tab.ScrollX = documentX * newZoom - fixedPoint.X;
        _tab.ScrollY = documentY * newZoom - fixedPoint.Y;

        // The user chose a zoom, so a later resize of the window must not fit the image again.
        _tab.NeedsFit = false;
        UpdateViewport();
    }
}
