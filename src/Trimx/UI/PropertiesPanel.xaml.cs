using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using SkiaSharp;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Services;

namespace Trimx.UI;

/// <summary>
/// Edits the properties of the selected text or shape layer, and rasterizes it.
/// </summary>
/// <remarks>
/// Every change is applied as a command while the user edits (live preview). Changes to the same
/// property merge into one undo step until focus leaves the panel.
/// </remarks>
public partial class PropertiesPanel : UserControl
{
    private DocumentTab? _tab;
    private Layer? _layer;

    // Set while the panel updates controls from the layer, so those changes are not applied back.
    private bool _isSyncing;

    private bool _fontsLoaded;

    public PropertiesPanel()
    {
        InitializeComponent();
    }

    public DocumentTab? Tab
    {
        get => _tab;
        set
        {
            if (ReferenceEquals(_tab, value))
            {
                return;
            }

            if (_tab is not null)
            {
                _tab.Document.Changed -= OnDocumentChanged;
                _tab.PropertyChanged -= OnTabPropertyChanged;
            }

            _tab = value;

            if (_tab is not null)
            {
                _tab.Document.Changed += OnDocumentChanged;
                _tab.PropertyChanged += OnTabPropertyChanged;
            }

            Refresh();
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => UpdateValues();

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTab.SelectedLayer))
        {
            Refresh();
        }
    }

    /// <summary>Shows the sections that match the selected layer.</summary>
    private void Refresh()
    {
        _layer = _tab?.SelectedLayer;
        FontPopup.IsOpen = false;

        var text = _layer as TextLayer;
        var shape = _layer as ShapeLayer;
        RootPanel.Visibility = text is not null || shape is not null ? Visibility.Visible : Visibility.Collapsed;
        TextSection.Visibility = text is not null ? Visibility.Visible : Visibility.Collapsed;
        ShapeSection.Visibility = shape is not null ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = RootPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        HeaderText.Text = text is not null ? "テキストの設定" : "図形の設定";

        UpdateValues();
    }

    private void UpdateValues()
    {
        _isSyncing = true;
        try
        {
            switch (_layer)
            {
                case TextLayer text:
                    // Keep what the user is typing when it already means the same value.
                    if (ContentBox.Text != text.Text)
                    {
                        ContentBox.Text = text.Text;
                    }

                    FontButton.Content = text.FontFamily;
                    SetNumber(FontSizeBox, text.FontSize);
                    BoldCheck.IsChecked = text.IsBold;
                    ItalicCheck.IsChecked = text.IsItalic;
                    ShadowCheck.IsChecked = text.HasShadow;
                    ShadowOptions.IsEnabled = text.HasShadow;
                    ShadowOpacitySlider.Value = MathF.Round(text.ShadowOpacity * 100f);
                    ShadowOpacityText.Text = $"{MathF.Round(text.ShadowOpacity * 100f)}%";
                    ShadowBlurSlider.Value = MathF.Round(text.ShadowBlur);
                    ShadowBlurText.Text = $"{MathF.Round(text.ShadowBlur)}px";
                    SetNumber(ShadowOffsetXBox, text.ShadowOffsetX);
                    SetNumber(ShadowOffsetYBox, text.ShadowOffsetY);
                    SetNumber(OutlineWidthBox, text.OutlineWidth);
                    OutlineColorField.SetColor(text.OutlineColor);
                    TextColorField.SetColor(text.Color);
                    break;

                case ShapeLayer shape:
                    SetNumber(ShapeWidthBox, shape.Width);
                    SetNumber(ShapeHeightBox, shape.Height);
                    SetNumber(StrokeWidthBox, shape.StrokeWidth);
                    SolidRadio.IsChecked = shape.Dash == ShapeDash.Solid;
                    DashedRadio.IsChecked = shape.Dash == ShapeDash.Dashed;
                    DottedRadio.IsChecked = shape.Dash == ShapeDash.Dotted;
                    SetNumber(CornerRadiusBox, shape.CornerRadius);

                    // Only rectangles have corners.
                    RadiusSection.Visibility = shape.Kind == ShapeKind.Rectangle ? Visibility.Visible : Visibility.Collapsed;
                    StrokeColorField.SetColor(shape.StrokeColor);
                    FillColorField.SetColor(shape.FillColor);
                    FillSection.Visibility = shape.Kind.IsSegment() ? Visibility.Collapsed : Visibility.Visible;
                    break;
            }

        }
        finally
        {
            _isSyncing = false;
        }
    }

    private static void SetNumber(TextBox box, float value)
    {
        if (!TryParseNumber(box.Text, out var shown) || shown != value)
        {
            box.Text = value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private static bool TryParseNumber(string text, out float value)
    {
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value);
    }

    private void Apply(IDocumentCommand command)
    {
        _tab?.Execute(command, merge: true);

        // What the user just set is what the next text or shape they add should start from.
        if (_layer is not null)
        {
            StateService.Capture(_layer);
        }
    }

    private void OnLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        // Focus leaving a field ends the continuous edit, so the next edit is a separate undo step.
        _tab?.History.EndMerge();
    }

    private void OnColorEditCompleted(object? sender, EventArgs e) => _tab?.History.EndMerge();

    // --- Text ---

    private void OnContentChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text && text.Text != ContentBox.Text)
        {
            Apply(LayerCommands.SetText(text, ContentBox.Text));
        }
    }

    private void OnFontSizeChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text && TryParseNumber(FontSizeBox.Text, out var size)
            && size >= 1f && size <= 2000f && size != text.FontSize)
        {
            Apply(LayerCommands.SetFontSize(text, size));
        }
    }

    private void OnBoldClick(object sender, RoutedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            Apply(LayerCommands.SetBold(text, BoldCheck.IsChecked == true));
            _tab?.History.EndMerge();
        }
    }

    private void OnItalicClick(object sender, RoutedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            Apply(LayerCommands.SetItalic(text, ItalicCheck.IsChecked == true));
            _tab?.History.EndMerge();
        }
    }

    private void OnShadowClick(object sender, RoutedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            Apply(LayerCommands.SetHasShadow(text, ShadowCheck.IsChecked == true));
            _tab?.History.EndMerge();
        }
    }

    private void OnShadowOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            var opacity = (float)Math.Round(e.NewValue) / 100f;
            ShadowOpacityText.Text = $"{Math.Round(e.NewValue)}%";
            if (opacity != text.ShadowOpacity)
            {
                Apply(LayerCommands.SetShadowOpacity(text, opacity));
            }
        }
    }

    private void OnShadowBlurChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            var blur = (float)Math.Round(e.NewValue);
            ShadowBlurText.Text = $"{blur}px";
            if (blur != text.ShadowBlur)
            {
                Apply(LayerCommands.SetShadowBlur(text, blur));
            }
        }
    }

    // Ends the continuous edit when a slider drag finishes, so one drag is one undo step.
    private void OnSliderDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _tab?.History.EndMerge();
    }

    private void OnShadowOffsetChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing || _layer is not TextLayer text)
        {
            return;
        }

        if (sender == ShadowOffsetXBox && TryParseNumber(ShadowOffsetXBox.Text, out var x)
            && Math.Abs(x) <= 1000f && x != text.ShadowOffsetX)
        {
            Apply(LayerCommands.SetShadowOffsetX(text, x));
        }
        else if (sender == ShadowOffsetYBox && TryParseNumber(ShadowOffsetYBox.Text, out var y)
            && Math.Abs(y) <= 1000f && y != text.ShadowOffsetY)
        {
            Apply(LayerCommands.SetShadowOffsetY(text, y));
        }
    }

    private void OnOutlineWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is TextLayer text && TryParseNumber(OutlineWidthBox.Text, out var width)
            && width >= 0f && width <= 500f && width != text.OutlineWidth)
        {
            Apply(LayerCommands.SetOutlineWidth(text, width));
        }
    }

    private void OnOutlineColorChanged(object? sender, SKColor color)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            Apply(LayerCommands.SetOutlineColor(text, color));
        }
    }

    private void OnTextColorChanged(object? sender, SKColor color)
    {
        if (!_isSyncing && _layer is TextLayer text)
        {
            Apply(LayerCommands.SetTextColor(text, color));
        }
    }

    private void OnFontButtonClick(object sender, RoutedEventArgs e)
    {
        // Enumerating system fonts takes noticeable time, so it is done on first use only.
        if (!_fontsLoaded)
        {
            LoadFonts();
        }

        _isSyncing = true;
        FontList.SelectedItem = (_layer as TextLayer)?.FontFamily;
        _isSyncing = false;
        FontPopup.IsOpen = true;
        if (FontList.SelectedItem is not null)
        {
            FontList.ScrollIntoView(FontList.SelectedItem);
        }
    }

    private void LoadFonts()
    {
        // The English family name is used because that is what Skia resolves reliably.
        var english = XmlLanguage.GetLanguage("en-us");
        var names = Fonts.SystemFontFamilies
            .Select(family => family.FamilyNames.TryGetValue(english, out var name) ? name : family.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Keep the current font selectable even if it is not an installed family name.
        if (_layer is TextLayer { FontFamily: var current } && !names.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            names.Insert(0, current);
        }

        FontList.ItemsSource = names;
        _fontsLoaded = true;
    }

    private void OnFontListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || _layer is not TextLayer text || FontList.SelectedItem is not string family)
        {
            return;
        }

        FontPopup.IsOpen = false;
        if (family != text.FontFamily)
        {
            Apply(LayerCommands.SetFontFamily(text, family));
            _tab?.History.EndMerge();
        }
    }

    // --- Shapes ---

    private void OnShapeWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is ShapeLayer shape && TryParseNumber(ShapeWidthBox.Text, out var value))
        {
            var width = (int)Math.Round(Math.Clamp(value, -100000f, 100000f));
            if (width != shape.Width)
            {
                Apply(LayerCommands.SetShapeWidth(shape, width));
            }
        }
    }

    private void OnShapeHeightChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is ShapeLayer shape && TryParseNumber(ShapeHeightBox.Text, out var value))
        {
            var height = (int)Math.Round(Math.Clamp(value, -100000f, 100000f));
            if (height != shape.Height)
            {
                Apply(LayerCommands.SetShapeHeight(shape, height));
            }
        }
    }

    private void OnStrokeWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is ShapeLayer shape && TryParseNumber(StrokeWidthBox.Text, out var value)
            && value >= 0f && value <= 1000f && value != shape.StrokeWidth)
        {
            Apply(LayerCommands.SetStrokeWidth(shape, value));
        }
    }

    private void OnDashClick(object sender, RoutedEventArgs e)
    {
        if (_isSyncing || _layer is not ShapeLayer shape)
        {
            return;
        }

        var dash = DashedRadio.IsChecked == true ? ShapeDash.Dashed
            : DottedRadio.IsChecked == true ? ShapeDash.Dotted
            : ShapeDash.Solid;
        if (dash != shape.Dash)
        {
            Apply(LayerCommands.SetDash(shape, dash));
            _tab?.History.EndMerge();
        }
    }

    private void OnCornerRadiusChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncing && _layer is ShapeLayer shape && TryParseNumber(CornerRadiusBox.Text, out var value)
            && value >= 0f && value <= 10000f && value != shape.CornerRadius)
        {
            Apply(LayerCommands.SetCornerRadius(shape, value));
        }
    }

    private void OnStrokeColorChanged(object? sender, SKColor color)
    {
        if (!_isSyncing && _layer is ShapeLayer shape)
        {
            Apply(LayerCommands.SetStrokeColor(shape, color));
        }
    }

    private void OnFillColorChanged(object? sender, SKColor color)
    {
        if (!_isSyncing && _layer is ShapeLayer shape)
        {
            Apply(LayerCommands.SetFillColor(shape, color));
        }
    }
}
