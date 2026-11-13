using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Trimx.UI;

/// <summary>
/// Asks for a new size, either for the canvas (with an anchor) or for the whole image.
/// </summary>
public partial class ResizeDialog : Window
{
    private readonly double _ratio; // Width / height of the current image.
    private readonly RadioButton[,] _anchors = new RadioButton[3, 3];

    // Set while one box is updated from the other (aspect ratio), to avoid feedback.
    private bool _isSyncing;

    public ResizeDialog(bool isCanvasSize, int currentWidth, int currentHeight)
    {
        InitializeComponent();
        Title = isCanvasSize ? "キャンバスサイズ" : "画像サイズ";
        CurrentSizeText.Text = $"現在: {currentWidth} × {currentHeight} px";
        _ratio = currentWidth / (double)currentHeight;

        // Resampling an image should stay proportional by default; canvas edits usually should not.
        KeepRatioCheck.IsChecked = !isCanvasSize;

        if (isCanvasSize)
        {
            AnchorSection.Visibility = Visibility.Visible;
            CreateAnchorButtons();
        }

        WidthBox.Text = currentWidth.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = currentHeight.ToString(CultureInfo.InvariantCulture);
        Loaded += (_, _) =>
        {
            WidthBox.Focus();
            WidthBox.SelectAll();
        };
    }

    public int NewWidth { get; private set; }

    public int NewHeight { get; private set; }

    /// <summary>0 = left, 1 = center, 2 = right.</summary>
    public int AnchorX { get; private set; } = 1;

    /// <summary>0 = top, 1 = middle, 2 = bottom.</summary>
    public int AnchorY { get; private set; } = 1;

    private void CreateAnchorButtons()
    {
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var button = new RadioButton
                {
                    GroupName = "Anchor",
                    Margin = new Thickness(6),
                    IsChecked = x == 1 && y == 1,
                };
                _anchors[x, y] = button;
                AnchorGrid.Children.Add(button);
            }
        }
    }

    private void OnWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && KeepRatioCheck.IsChecked == true && TryParse(WidthBox.Text, out var width))
        {
            SetOther(HeightBox, Math.Round(width / _ratio));
        }
    }

    private void OnHeightChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && KeepRatioCheck.IsChecked == true && TryParse(HeightBox.Text, out var height))
        {
            SetOther(WidthBox, Math.Round(height * _ratio));
        }
    }

    private void SetOther(TextBox box, double value)
    {
        _isSyncing = true;
        box.Text = Math.Max(1, value).ToString("0", CultureInfo.InvariantCulture);
        _isSyncing = false;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TryParse(WidthBox.Text, out var width) || !TryParse(HeightBox.Text, out var height))
        {
            ErrorText.Text = $"幅と高さは 1～{NewImageDialog.MaxSize} の整数で入力してください。";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        NewWidth = (int)width;
        NewHeight = (int)height;
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                if (_anchors[x, y]?.IsChecked == true)
                {
                    AnchorX = x;
                    AnchorY = y;
                }
            }
        }

        DialogResult = true;
    }

    private static bool TryParse(string text, out double value)
    {
        return double.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value is >= 1 and <= NewImageDialog.MaxSize;
    }
}
