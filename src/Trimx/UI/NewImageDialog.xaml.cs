using System.Globalization;
using System.Windows;
using Trimx.Core.Documents;
using Trimx.Core.Editing;

namespace Trimx.UI;

/// <summary>
/// Asks for the size and background of a new image.
/// </summary>
public partial class NewImageDialog : Window
{
    /// <summary>Upper bound per side (shared with import and resize).</summary>
    public const int MaxSize = ImageLimits.MaxSide;

    public NewImageDialog(int initialWidth, int initialHeight)
    {
        InitializeComponent();
        WidthBox.Text = initialWidth.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = initialHeight.ToString(CultureInfo.InvariantCulture);
        Loaded += (_, _) =>
        {
            WidthBox.Focus();
            WidthBox.SelectAll();
        };
    }

    public int ImageWidth { get; private set; }

    public int ImageHeight { get; private set; }

    public CanvasBackground SelectedBackground { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TryParseSize(WidthBox.Text, out var width) || !TryParseSize(HeightBox.Text, out var height))
        {
            ErrorText.Text = $"幅と高さは 1～{MaxSize} の整数で入力してください。";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ImageWidth = width;
        ImageHeight = height;
        SelectedBackground = WhiteRadio.IsChecked == true ? CanvasBackground.White
            : BlackRadio.IsChecked == true ? CanvasBackground.Black
            : CanvasBackground.Transparent;
        DialogResult = true;
    }

    private static bool TryParseSize(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value is >= 1 and <= MaxSize;
    }
}
