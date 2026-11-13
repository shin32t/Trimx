using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;

namespace Trimx.UI;

/// <summary>
/// Picks an opaque color from a saturation / brightness square and a hue strip,
/// with numeric entry in HSB, RGB and hex.
/// </summary>
public partial class ColorPickerDialog : Window
{
    private const double SquareSize = 256;

    // Hue is kept apart from the RGB color because grays and black have no hue of their own;
    // deriving it each time would make the hue strip jump to 0 while dragging through them.
    private float _hue;
    private float _saturation;
    private float _value;

    // Set when the color was typed as RGB / hex, so it is shown exactly instead of after an HSB round trip.
    private SKColor? _exact;

    // Set while the dialog updates its own controls, so those changes are not read back as input.
    private bool _isSyncing;

    public ColorPickerDialog(SKColor initial)
    {
        InitializeComponent();
        var original = initial.WithAlpha(255);
        OldSwatch.Background = ToBrush(original);
        SelectedColor = original;
        original.ToHsv(out _hue, out _saturation, out _value);
        Refresh();
    }

    /// <summary>The chosen color (always opaque). Valid after the dialog returns true.</summary>
    public SKColor SelectedColor { get; private set; }

    private static SolidColorBrush ToBrush(SKColor color)
    {
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnSquareMouse(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // Capture keeps the drag going when the pointer leaves the square.
        Mouse.Capture(SquareHost);
        var position = e.GetPosition(SquareHost);
        _exact = null;
        _saturation = (float)(Math.Clamp(position.X / SquareSize, 0, 1) * 100);
        _value = (float)((1 - Math.Clamp(position.Y / SquareSize, 0, 1)) * 100);
        Refresh();
    }

    private void OnHueMouse(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Mouse.Capture(HueHost);
        _exact = null;
        _hue = (float)(Math.Clamp(e.GetPosition(HueHost).Y / SquareSize, 0, 1) * 360);
        Refresh();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Mouse.Captured is not null)
        {
            Mouse.Capture(null);
        }
    }

    private void OnHsvBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if (TryParse(HueBox.Text, 360, out var hue) && TryParse(SaturationBox.Text, 100, out var saturation)
            && TryParse(ValueBox.Text, 100, out var value))
        {
            _exact = null;
            _hue = hue;
            _saturation = saturation;
            _value = value;
            Refresh(except: sender as TextBox);
        }
    }

    private void OnRgbBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if (TryParse(RedBox.Text, 255, out var red) && TryParse(GreenBox.Text, 255, out var green)
            && TryParse(BlueBox.Text, 255, out var blue))
        {
            SetFromColor(new SKColor((byte)red, (byte)green, (byte)blue), except: sender as TextBox);
        }
    }

    private void OnHexBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        var hex = HexBox.Text.Trim().TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            SetFromColor(new SKColor(0xFF000000 | rgb), except: HexBox);
        }
    }

    private void SetFromColor(SKColor color, TextBox? except)
    {
        var previousHue = _hue;
        color.ToHsv(out _hue, out _saturation, out _value);

        // A color without hue (gray, black) keeps the hue the user had.
        if (_saturation <= 0f || _value <= 0f)
        {
            _hue = previousHue;
        }

        _exact = color;
        Refresh(except);
    }

    private static bool TryParse(string text, int max, out float value)
    {
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value >= 0 && value <= max;
    }

    /// <summary>Updates everything that shows the color, except the box the user is typing in.</summary>
    private void Refresh(TextBox? except = null)
    {
        _isSyncing = true;
        try
        {
            var color = _exact ?? SKColor.FromHsv(_hue, _saturation, _value);
            SelectedColor = color;

            HueFill.Fill = ToBrush(SKColor.FromHsv(_hue, 100, 100));
            NewSwatch.Background = ToBrush(color);
            Canvas.SetLeft(SquareThumb, _saturation / 100 * SquareSize - SquareThumb.Width / 2);
            Canvas.SetTop(SquareThumb, (1 - _value / 100) * SquareSize - SquareThumb.Height / 2);
            Canvas.SetTop(HueThumb, _hue / 360 * SquareSize - HueThumb.Height / 2);

            Show(HueBox, _hue, except);
            Show(SaturationBox, _saturation, except);
            Show(ValueBox, _value, except);
            Show(RedBox, color.Red, except);
            Show(GreenBox, color.Green, except);
            Show(BlueBox, color.Blue, except);
            if (!ReferenceEquals(HexBox, except))
            {
                HexBox.Text = $"{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private static void Show(TextBox box, float value, TextBox? except)
    {
        if (!ReferenceEquals(box, except))
        {
            box.Text = Math.Round(value).ToString(CultureInfo.InvariantCulture);
        }
    }
}
