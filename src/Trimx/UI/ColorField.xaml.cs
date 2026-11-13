using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// Edits a color as a hex code (#RRGGBB or #AARRGGBB) with a preview and a row of preset colors.
/// </summary>
public partial class ColorField : UserControl
{
    private static readonly SKColor[] _presets =
    {
        new(0xFF, 0x3B, 0x30), new(0xFF, 0x95, 0x00), new(0xFF, 0xCC, 0x00), new(0x34, 0xC7, 0x59),
        new(0x0A, 0x84, 0xFF), new(0xAF, 0x52, 0xDE), new(0xFF, 0xFF, 0xFF), new(0x00, 0x00, 0x00),
    };

    private SKColor _color = SKColors.Black;

    // Set while the field updates itself from code, so those changes are not reported as user edits.
    private bool _isSyncing;

    public ColorField()
    {
        InitializeComponent();
        foreach (var preset in _presets)
        {
            Presets.Children.Add(CreatePresetButton(preset));
        }

        UpdateSwatch();
    }

    /// <summary>Raised when the user changes the color.</summary>
    public event EventHandler<SKColor>? ColorChanged;

    /// <summary>Raised when an edit ends (a preset was picked or the hex box lost focus).</summary>
    public event EventHandler? EditCompleted;

    /// <summary>Shows a button that sets the color to transparent (for optional colors such as fill).</summary>
    public bool ShowNoneButton
    {
        get => NoneButton.Visibility == Visibility.Visible;
        set => NoneButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Shows the color without raising <see cref="ColorChanged"/>.</summary>
    public void SetColor(SKColor color)
    {
        _color = color;
        UpdateSwatch();

        // Leave the text alone while the user is typing a code that already means this color.
        if (!TryParse(HexBox.Text, out var shown) || shown != color)
        {
            _isSyncing = true;
            HexBox.Text = Format(color);
            _isSyncing = false;
        }
    }

    private static string Format(SKColor color)
    {
        return color.Alpha == 255
            ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
            : $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    private static bool TryParse(string text, out SKColor color)
    {
        color = default;
        var hex = text.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = hex.Length == 6 ? new SKColor(0xFF000000 | value) : new SKColor(value);
        return true;
    }

    private void UpdateSwatch()
    {
        Swatch.Background = new SolidColorBrush(Color.FromArgb(_color.Alpha, _color.Red, _color.Green, _color.Blue));
    }

    private UIElement CreatePresetButton(SKColor color)
    {
        var border = new Border
        {
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 4, 4),
            Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = Format(color),
        };
        border.MouseLeftButtonUp += (_, _) =>
        {
            Apply(color);
            EditCompleted?.Invoke(this, EventArgs.Empty);
        };
        return border;
    }

    private void Apply(SKColor color)
    {
        if (color == _color)
        {
            return;
        }

        SetColor(color);
        ColorChanged?.Invoke(this, color);
    }

    private void OnHexTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && TryParse(HexBox.Text, out var color))
        {
            Apply(color);
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs e)
    {
        // Discard a half-typed or invalid code and show the color actually in use.
        _isSyncing = true;
        HexBox.Text = Format(_color);
        _isSyncing = false;
        EditCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void OnPickerClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_color) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Apply(dialog.SelectedColor);

        // Same palette as the pen, so colors picked anywhere can be reused.
        MyColorService.Add(dialog.SelectedColor);
        EditCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void OnNoneClick(object sender, RoutedEventArgs e)
    {
        Apply(SKColors.Transparent);
        EditCompleted?.Invoke(this, EventArgs.Empty);
    }
}
