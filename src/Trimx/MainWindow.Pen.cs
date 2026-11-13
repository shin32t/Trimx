using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using Trimx.Services;
using Trimx.UI;

namespace Trimx;

// Pen mode: toolbar button, and the popup for color and thickness.
public partial class MainWindow
{
    private const int PenSwatchSize = 28;

    // Same layout as the Windows Snipping Tool palette: grays, then warm to cool hues.
    private static readonly SKColor[] _penPalette =
    {
        SKColor.Parse("#000000"), SKColor.Parse("#FFFFFF"), SKColor.Parse("#D0D0D0"), SKColor.Parse("#A6A6A6"), SKColor.Parse("#808080"), SKColor.Parse("#595959"),
        SKColor.Parse("#B5155F"), SKColor.Parse("#E81F1F"), SKColor.Parse("#FF5A00"), SKColor.Parse("#FFA800"), SKColor.Parse("#FFCC00"), SKColor.Parse("#FFE600"),
        SKColor.Parse("#A6E01F"), SKColor.Parse("#2BE81F"), SKColor.Parse("#008060"), SKColor.Parse("#00AACC"), SKColor.Parse("#0050E6"), SKColor.Parse("#4300B8"),
        SKColor.Parse("#6A00C8"), SKColor.Parse("#5E0080"), SKColor.Parse("#F8D8C4"), SKColor.Parse("#BC9468"), SKColor.Parse("#8B5A2B"), SKColor.Parse("#5E3B2B"),
        SKColor.Parse("#FF80FF"), SKColor.Parse("#FFC080"), SKColor.Parse("#FFFF80"), SKColor.Parse("#80FF99"), SKColor.Parse("#80D4FF"), SKColor.Parse("#BFB3FF"),
    };

    private readonly List<Border> _penSwatches = new();
    private bool _penUiReady;

    // Set while the popup updates its own controls, so those changes are not applied twice.
    private bool _isPenSyncing;

    private SKColor _penColor = SKColor.Parse("#E81F1F");
    private float _penWidth = 6f;

    /// <summary>Takes the pen color and thickness from the remembered state (at startup, and after a reset).</summary>
    private void LoadPenFromState()
    {
        var pen = StateService.Current.Pen;
        _penColor = StateService.ParseColor(pen.Color, SKColor.Parse("#E81F1F")).WithAlpha(255);
        _penWidth = float.IsFinite(pen.Width) ? Math.Clamp(pen.Width, 1f, 40f) : 6f;
        Canvas.PenColor = _penColor;
        Canvas.PenWidth = _penWidth;

        if (_penUiReady)
        {
            _isPenSyncing = true;
            PenWidthSlider.Value = _penWidth;
            _isPenSyncing = false;
            UpdatePenUi();
        }
    }

    private void RememberPen()
    {
        StateService.Current.Pen = new PenStyle { Color = StateService.FormatColor(_penColor), Width = _penWidth };
        StateService.MarkChanged();
    }

    // The palette is built on first use so it does not slow down startup.
    private void EnsurePenUi()
    {
        if (_penUiReady)
        {
            return;
        }

        _penUiReady = true;
        foreach (var color in _penPalette)
        {
            var swatch = new Border
            {
                Width = PenSwatchSize,
                Height = PenSwatchSize,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(PenSwatchSize / 2.0),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = color,
                ToolTip = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}",
                Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)),
            };
            swatch.MouseLeftButtonUp += (_, _) => SetPenColor(color);
            _penSwatches.Add(swatch);
            PenColorGrid.Children.Add(swatch);
        }

        RebuildMyColors(MyColorService.Load());

        _isPenSyncing = true;
        PenWidthSlider.Value = _penWidth;
        _isPenSyncing = false;
        UpdatePenUi();
    }

    private void OnPenButtonClick(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is null)
        {
            return;
        }

        EnsurePenUi();

        // Colors may have been added from the text / shape color fields since the popup was last shown.
        RebuildMyColors(MyColorService.Load());
        SetPenMode(true);
        PenPopup.IsOpen = true;
    }

    // Always shows every slot so the palette's size is visible; an empty slot is just an outline.
    private void RebuildMyColors(IReadOnlyList<SKColor> colors)
    {
        MyColorPanel.Children.Clear();
        for (var i = 0; i < MyColorService.Capacity; i++)
        {
            var color = i < colors.Count ? colors[i] : (SKColor?)null;
            var slot = new Border
            {
                Width = PenSwatchSize,
                Height = PenSwatchSize,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(PenSwatchSize / 2.0),
                BorderThickness = new Thickness(2),
                BorderBrush = (Brush)FindResource("BorderBrush"),
            };
            if (color is { } c)
            {
                slot.Background = new SolidColorBrush(Color.FromRgb(c.Red, c.Green, c.Blue));
                slot.Cursor = Cursors.Hand;
                slot.ToolTip = $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
                slot.MouseLeftButtonUp += (_, _) => SetPenColor(c);
            }

            MyColorPanel.Children.Add(slot);
        }
    }

    private void OnPenStopClick(object sender, RoutedEventArgs e)
    {
        PenPopup.IsOpen = false;
        SetPenMode(false);
    }

    private void OnPenModeChanged(object? sender, EventArgs e) => UpdatePenButton();

    private void SetPenMode(bool enabled)
    {
        Canvas.IsPenMode = enabled;
        Canvas.PenColor = _penColor;
        Canvas.PenWidth = _penWidth;
        UpdatePenButton();
    }

    // The button stays highlighted while pen mode is on, since the mode is otherwise invisible.
    private void UpdatePenButton()
    {
        if (Canvas.IsPenMode)
        {
            PenButtonHost.SetResourceReference(Border.BackgroundProperty, "SelectedBrush");
        }
        else
        {
            PenButtonHost.Background = Brushes.Transparent;
        }
    }

    private void SetPenColor(SKColor color)
    {
        _penColor = color.WithAlpha(255);
        Canvas.PenColor = _penColor;
        UpdatePenUi();
        RememberPen();
    }

    private void OnPenWidthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Sliders also report values while the XAML loads, before the popup is built.
        if (_isPenSyncing || !_penUiReady)
        {
            return;
        }

        _penWidth = (float)Math.Round(e.NewValue);
        Canvas.PenWidth = _penWidth;
        UpdatePenUi();
        RememberPen();
    }

    private void OnPenCustomClick(object sender, RoutedEventArgs e)
    {
        // The popup closes when focus moves to the dialog, so reopen it after a color was chosen.
        var dialog = new ColorPickerDialog(_penColor) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            SetPenColor(dialog.SelectedColor);
            RebuildMyColors(MyColorService.Add(dialog.SelectedColor));
        }

        PenPopup.IsOpen = true;
    }

    /// <summary>Shows the current color and thickness in the popup (selection ring, sliders, preview).</summary>
    private void UpdatePenUi()
    {
        _isPenSyncing = true;
        try
        {
            foreach (var swatch in _penSwatches)
            {
                swatch.BorderBrush = swatch.Tag is SKColor c && c == _penColor
                    ? (Brush)FindResource("ForegroundBrush")
                    : Brushes.Transparent;
            }

            PenPreview.Stroke = new SolidColorBrush(Color.FromRgb(_penColor.Red, _penColor.Green, _penColor.Blue));
            PenPreview.StrokeThickness = _penWidth;
            PenWidthText.Text = $"{_penWidth:0} px";
        }
        finally
        {
            _isPenSyncing = false;
        }
    }
}
