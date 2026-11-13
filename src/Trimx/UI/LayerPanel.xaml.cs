using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;

namespace Trimx.UI;

/// <summary>
/// Lists the layers of the active tab and edits selection, visibility, opacity, order,
/// and adds, duplicates and merges layers.
/// </summary>
public partial class LayerPanel : UserControl
{
    private const string DragFormat = "Trimx.LayerItem";

    private readonly Dictionary<Layer, LayerItem> _items = new();

    private DocumentTab? _tab;

    // Set while the panel itself updates controls, so the resulting change events are not
    // mistaken for user edits.
    private bool _isSyncing;

    // Drag & drop reordering: the row under the pointer when the button went down.
    private LayerItem? _dragCandidate;
    private Point _dragStart;

    public LayerPanel()
    {
        InitializeComponent();
        UpdateControls();
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
                _tab.Document.LayersChanged -= OnLayersChanged;
                _tab.Document.Changed -= OnDocumentChanged;
                _tab.PropertyChanged -= OnTabPropertyChanged;
            }

            _tab = value;

            if (_tab is not null)
            {
                _tab.Document.LayersChanged += OnLayersChanged;
                _tab.Document.Changed += OnDocumentChanged;
                _tab.PropertyChanged += OnTabPropertyChanged;
            }

            RefreshLayers();
        }
    }

    private void OnLayersChanged(object? sender, EventArgs e) => RefreshLayers();

    // Keeps the opacity display and button states in sync, including after undo / redo.
    private void OnDocumentChanged(object? sender, EventArgs e) => UpdateControls();

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTab.SelectedLayer))
        {
            SyncSelection();
        }
    }

    private void RefreshLayers()
    {
        var layers = _tab?.Document.Layers ?? Array.Empty<Layer>();

        // Rows (and their thumbnails) are reused for layers that are still present.
        foreach (var layer in _items.Keys.Where(l => !layers.Contains(l)).ToList())
        {
            _items[layer].Dispose();
            _items.Remove(layer);
        }

        var rows = new List<LayerItem>(layers.Count);
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            if (!_items.TryGetValue(layers[i], out var item))
            {
                item = new LayerItem(layers[i]);
                _items[layers[i]] = item;
            }

            rows.Add(item);
        }

        _isSyncing = true;
        try
        {
            LayerList.ItemsSource = rows;
        }
        finally
        {
            _isSyncing = false;
        }

        SyncSelection();
    }

    private void SyncSelection()
    {
        _isSyncing = true;
        try
        {
            LayerItem? selected = null;
            if (_tab?.SelectedLayer is { } layer)
            {
                _items.TryGetValue(layer, out selected);
            }

            LayerList.SelectedItem = selected;
            if (selected is not null)
            {
                LayerList.ScrollIntoView(selected);
            }
        }
        finally
        {
            _isSyncing = false;
        }

        UpdateControls();
    }

    private void UpdateControls()
    {
        var layer = _tab?.SelectedLayer;
        var index = layer is null || _tab is null ? -1 : _tab.Document.IndexOf(layer);
        var count = _tab?.Document.Layers.Count ?? 0;

        _isSyncing = true;
        try
        {
            OpacitySlider.IsEnabled = layer is not null;

            // Skip tiny differences so float rounding does not nudge the thumb while dragging.
            var opacity = layer?.Opacity ?? 1.0;
            if (Math.Abs(OpacitySlider.Value - opacity) > 0.001)
            {
                OpacitySlider.Value = opacity;
            }

            OpacityText.Text = layer is null ? string.Empty : $"{layer.Opacity:P0}";
        }
        finally
        {
            _isSyncing = false;
        }

        MoveUpButton.IsEnabled = index >= 0 && index < count - 1;
        MoveDownButton.IsEnabled = index > 0;
        DeleteButton.IsEnabled = index >= 0;
        AddButton.IsEnabled = _tab is not null;
        DuplicateButton.IsEnabled = index >= 0;
        MergeButton.IsEnabled = _tab?.CanMergeSelectedDown() == true;
    }

    private void OnLayerListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || _tab is null)
        {
            return;
        }

        _tab.SelectedLayer = (LayerList.SelectedItem as LayerItem)?.Layer;
    }

    private void OnVisibilityClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: LayerItem item } checkBox)
        {
            _tab?.SetLayerVisibility(item.Layer, checkBox.IsChecked == true);
        }
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncing || _tab?.SelectedLayer is not { } layer)
        {
            return;
        }

        // Each change while dragging merges into the same undo step (see OnOpacityEditEnded).
        _tab.SetLayerOpacity(layer, (float)e.NewValue);
        OpacityText.Text = $"{layer.Opacity:P0}";
    }

    private void OnOpacityEditEnded(object sender, RoutedEventArgs e)
    {
        // A finished drag (or leaving the slider) closes the undo step, so the next edit is separate.
        _tab?.History.EndMerge();
    }

    private void OnMoveUp(object sender, RoutedEventArgs e) => _tab?.MoveSelectedLayer(1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => _tab?.MoveSelectedLayer(-1);

    private void OnDelete(object sender, RoutedEventArgs e) => _tab?.RemoveSelectedLayer();

    private void OnAddEmpty(object sender, RoutedEventArgs e)
    {
        RunLayerOperation("空のレイヤーを追加できませんでした。", () => _tab?.AddEmptyLayer("新規レイヤー"));
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        RunLayerOperation("レイヤーを複製できませんでした。", () => _tab?.DuplicateSelectedLayer("{0} のコピー"));
    }

    private void OnMergeDown(object sender, RoutedEventArgs e)
    {
        RunLayerOperation("レイヤーを結合できませんでした。", () =>
        {
            if (_tab?.MergeSelectedDown() == false)
            {
                MessageBox.Show(Window.GetWindow(this), "結合後の大きさが上限を超えるため、結合できません。", "Trimx", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
    }

    private void RunLayerOperation(string failureMessage, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            // Typically a failed pixel allocation for a large canvas; the document is unchanged in that case.
            MessageBox.Show(Window.GetWindow(this), $"{failureMessage}\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Drag & drop reordering ---

    // A right click selects the row under the pointer first, so the menu acts on the layer the user pointed at.
    private void OnListPreviewRightMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<ListBoxItem>(source) is { } row)
        {
            row.IsSelected = true;
        }
    }

    // Rasterize is only offered where it can apply; merge is always listed but greyed out when impossible.
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Nothing selected (a click on empty space): there is nothing to act on, so no menu.
        if (_tab?.SelectedLayer is null)
        {
            e.Handled = true;
            return;
        }

        RasterizeMenuItem.Visibility = _tab?.SelectedLayer is { } layer && LayerRasterizer.CanRasterize(layer)
            ? Visibility.Visible
            : Visibility.Collapsed;
        MergeMenuItem.IsEnabled = _tab?.CanMergeSelectedDown() == true;
    }

    private void OnRasterize(object sender, RoutedEventArgs e)
    {
        RunLayerOperation("ラスタライズできませんでした。", () => _tab?.RasterizeSelectedLayer());
    }

    private void OnListPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragCandidate = null;

        // The visibility check box keeps its normal click behavior.
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<CheckBox>(source) is null
            && FindAncestor<ListBoxItem>(source) is { DataContext: LayerItem item })
        {
            _dragCandidate = item;
            _dragStart = e.GetPosition(this);
        }
    }

    private void OnListPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragCandidate is not { } item || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        var moved = e.GetPosition(this) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragCandidate = null;
        DragDrop.DoDragDrop(LayerList, new DataObject(DragFormat, item), DragDropEffects.Move);
        DropIndicator.Visibility = Visibility.Collapsed;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_tab is not null
            && e.Data.GetData(DragFormat) is LayerItem item
            && TryGetDropTarget(e, item.Layer, out var newIndex, out var indicatorY)
            && newIndex != _tab.Document.IndexOf(item.Layer))
        {
            e.Effects = DragDropEffects.Move;
            DropIndicator.Margin = new Thickness(0, Math.Max(0, indicatorY - 1), 0, 0);
            DropIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            DropIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void OnListDragLeave(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;
        if (_tab is not null
            && e.Data.GetData(DragFormat) is LayerItem item
            && TryGetDropTarget(e, item.Layer, out var newIndex, out _))
        {
            _tab.MoveLayerTo(item.Layer, newIndex);
            _tab.SelectedLayer = item.Layer;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Works out the stacking index the dragged layer would get if dropped here, and the vertical position
    /// of the insertion line in list coordinates.
    /// </summary>
    private bool TryGetDropTarget(DragEventArgs e, Layer moving, out int newIndex, out double indicatorY)
    {
        newIndex = 0;
        indicatorY = 0;
        var count = LayerList.Items.Count;
        var from = _tab?.Document.IndexOf(moving) ?? -1;
        if (count == 0 || from < 0)
        {
            return false;
        }

        // Find the row under the pointer; below the last row (or the blank area) means "after the last".
        var pointerY = e.GetPosition(LayerList).Y;
        var displayIndex = count - 1;
        var above = false;
        indicatorY = 0;
        var found = false;
        for (var i = 0; i < count; i++)
        {
            if (LayerList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem row)
            {
                continue;
            }

            var top = row.TranslatePoint(new Point(0, 0), LayerList).Y;
            var bottom = top + row.ActualHeight;
            if (pointerY < bottom || i == count - 1)
            {
                displayIndex = i;
                above = pointerY < top + row.ActualHeight / 2;
                indicatorY = above ? top : bottom;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        // The list shows the top layer first, so a row's stacking index is count - 1 - row.
        var target = count - 1 - displayIndex;
        newIndex = above
            ? (from < target ? target : target + 1)
            : (from < target ? target - 1 : target);
        return true;
    }

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        for (var node = start; node is not null;)
        {
            if (node is T match)
            {
                return match;
            }

            // Text runs and similar content elements are not visuals; walk their logical parent instead.
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }
}
