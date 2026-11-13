using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Trimx.Core.Documents;
using Trimx.Core.Editing;
using Trimx.Services;
using Trimx.UI;

namespace Trimx;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DocumentTab> _tabs = new();
    private int _clipboardTabCount;
    private int _untitledTabCount;

    public MainWindow()
    {
        InitializeComponent();
        LoadPenFromState();
        LoadLayoutFromState();
        Canvas.PenModeChanged += OnPenModeChanged;
        Canvas.SelectModeChanged += OnSelectModeChanged;
        TabStrip.ItemsSource = _tabs;
        // Show the window first; load the clipboard image afterwards to keep startup fast.
        ContentRendered += OnFirstContentRendered;
    }

    // Tracked in a field rather than read from TabStrip, because command CanExecute handlers
    // run during InitializeComponent before TabStrip is assigned.
    private DocumentTab? ActiveTab { get; set; }

    private void OnFirstContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnFirstContentRendered;
        Canvas.Focus();
        OpenClipboardImageInNewTab();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        // Ask for each document with unsaved changes; cancelling any one of them keeps the app open.
        foreach (var tab in _tabs.ToList())
        {
            if (!ConfirmClose(tab))
            {
                e.Cancel = true;
                return;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Canvas.Tab = null;
        LayerPanel.Tab = null;
        PropertiesPanel.Tab = null;
        foreach (var tab in _tabs)
        {
            tab.Dispose();
        }

        _tabs.Clear();
        base.OnClosed(e);
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTab.Zoom))
        {
            UpdateZoomText();
        }
        else if (e.PropertyName is nameof(DocumentTab.Selection) or nameof(DocumentTab.SelectedLayer))
        {
            // Crop and Cut are only available with a selection (and, for Cut, an image layer).
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Shows the zoom of the active tab in the status bar (100% = one image pixel per screen pixel).</summary>
    private void UpdateZoomText()
    {
        ZoomText.Text = ActiveTab is { } tab ? $"{tab.Zoom * 100:0}%" : string.Empty;
    }

    private void CanExecuteWithDocument(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveTab is not null;
    }

    private void CanDeleteLayer(object sender, CanExecuteRoutedEventArgs e)
    {
        // In a text box the Delete key edits the text, so it only removes a layer elsewhere.
        e.CanExecute = ActiveTab?.SelectedLayer is not null
            && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase;
    }

    private void OnDeleteLayer(object sender, ExecutedRoutedEventArgs e) => ActiveTab?.RemoveSelectedLayer();

    private void CanUndo(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveTab?.History.CanUndo == true;
    }

    private void CanRedo(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveTab?.History.CanRedo == true;
    }

    private void OnUndo(object sender, ExecutedRoutedEventArgs e) => ActiveTab?.Undo();

    private void OnRedo(object sender, ExecutedRoutedEventArgs e) => ActiveTab?.Redo();

    private void OnNew(object sender, ExecutedRoutedEventArgs e)
    {
        // Initial size: the clipboard image if any, otherwise the last size created.
        var state = StateService.Current;
        var (width, height) = ClipboardService.TryGetImageSize()
            ?? (state.LastNewImageWidth, state.LastNewImageHeight);
        width = Math.Clamp(width, 1, NewImageDialog.MaxSize);
        height = Math.Clamp(height, 1, NewImageDialog.MaxSize);

        var dialog = new NewImageDialog(width, height) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ImageDocument document;
        try
        {
            document = DocumentFactory.CreateBlank(
                dialog.ImageWidth,
                dialog.ImageHeight,
                dialog.SelectedBackground,
                $"無題 {++_untitledTabCount}",
                "背景");
        }
        catch (Exception ex)
        {
            // Typically a failed pixel allocation for a very large canvas.
            MessageBox.Show(this, $"画像を作成できませんでした。\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AddTab(document);

        state.LastNewImageWidth = dialog.ImageWidth;
        state.LastNewImageHeight = dialog.ImageHeight;
        StateService.MarkChanged();
    }

    private void OnPaste(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab)
        {
            OpenClipboardImageInNewTab();
            return;
        }

        var bitmap = ClipboardService.TryGetImage();
        if (bitmap is null)
        {
            return;
        }

        tab.AddLayerAboveSelection(LayerFactory.CreateImage(tab.Document, bitmap, "貼り付け"));
    }

    private void OnFitToView(object sender, ExecutedRoutedEventArgs e) => Canvas.FitToView();

    // Each step changes the zoom by 25%, which is about as fast as the wheel but easy to repeat from the keyboard.
    private void OnZoomIn(object sender, ExecutedRoutedEventArgs e) => Canvas.ZoomByFactor(1.25f);

    private void OnZoomOut(object sender, ExecutedRoutedEventArgs e) => Canvas.ZoomByFactor(1f / 1.25f);

    private void OnActualSize(object sender, ExecutedRoutedEventArgs e) => Canvas.ZoomTo(1f);

    private void OnShapeMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
        {
            ShapePopup.IsOpen = true;
        }
    }

    // The command adds the shape; this only closes the menu.
    private void OnShapeChosen(object sender, RoutedEventArgs e) => ShapePopup.IsOpen = false;

    private void OnSelectButtonClick(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
        {
            Canvas.IsSelectMode = !Canvas.IsSelectMode;
        }
    }

    private void OnSelectModeChanged(object? sender, EventArgs e)
    {
        if (Canvas.IsSelectMode)
        {
            SelectButtonHost.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "SelectedBrush");
        }
        else
        {
            SelectButtonHost.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    // The side panel width and the inspector / layer split are remembered between sessions.
    private void LoadLayoutFromState()
    {
        var state = StateService.Current;
        if (state.SidePanelWidth is { } width && double.IsFinite(width))
        {
            SidePanelColumn.Width = new GridLength(Math.Clamp(width, SidePanelColumn.MinWidth, SidePanelColumn.MaxWidth));
        }

        if (state.InspectorHeight is { } height && double.IsFinite(height))
        {
            // Stays below a sane ceiling so a stale value cannot push the layer list out of a smaller window.
            InspectorRow.Height = new GridLength(Math.Clamp(height, InspectorRow.MinHeight, 600));
        }
    }

    // When the window gets taller or shorter, the inspector keeps its share of the side panel instead of
    // leaving all the change to the layer list. It stays within its limits and leaves the layer list its minimum.
    private void OnSidePanelGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double SplitterHeight = 5;
        const double LayerListMinHeight = 150;

        if (!e.HeightChanged || e.PreviousSize.Height <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        var share = InspectorRow.ActualHeight / e.PreviousSize.Height;
        var maxHeight = Math.Max(InspectorRow.MinHeight, e.NewSize.Height - SplitterHeight - LayerListMinHeight);
        InspectorRow.Height = new GridLength(Math.Clamp(share * e.NewSize.Height, InspectorRow.MinHeight, maxHeight));
    }

    private void OnSidePanelSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        StateService.Current.SidePanelWidth = SidePanelColumn.ActualWidth;
        StateService.MarkChanged();
    }

    private void OnInspectorSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        StateService.Current.InspectorHeight = InspectorRow.ActualHeight;
        StateService.MarkChanged();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        new SettingsDialog { Owner = this }.ShowDialog();

        // "Reset" in the dialog changes the remembered pen.
        LoadPenFromState();
    }

    private void CanCrop(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveTab?.Selection is not null;
    }

    private void OnCrop(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (ActiveTab is { } tab && tab.CropToSelection())
            {
                Canvas.IsSelectMode = false;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or OutOfMemoryException)
        {
            // Typically a failed pixel allocation; nothing was changed.
            MessageBox.Show(this, $"切り抜けませんでした。\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnZoomButtonClick(object sender, RoutedEventArgs e)
    {
        if (ActiveTab is not null)
        {
            ZoomPopup.IsOpen = true;
        }
    }

    private void OnZoomPresetSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ZoomList.SelectedItem is not System.Windows.Controls.ListBoxItem { Tag: string tag })
        {
            return;
        }

        ZoomPopup.IsOpen = false;
        if (tag == "fit")
        {
            Canvas.FitToView();
        }
        else if (float.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zoom))
        {
            Canvas.ZoomTo(zoom);
        }

        // Clear the selection so the same entry can be chosen again next time.
        ZoomList.SelectedItem = null;
    }

    private void OnResizeCanvas(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        var dialog = new ResizeDialog(isCanvasSize: true, tab.Document.Width, tab.Document.Height) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            tab.ResizeCanvas(dialog.NewWidth, dialog.NewHeight, dialog.AnchorX, dialog.AnchorY);
        }
    }

    private void OnResizeImage(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        var dialog = new ResizeDialog(isCanvasSize: false, tab.Document.Width, tab.Document.Height) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            tab.ResizeImage(dialog.NewWidth, dialog.NewHeight);
        }
        catch (Exception ex)
        {
            // Typically a failed pixel allocation for a very large result; the command rolls back nothing
            // because it is only created (not executed) when allocation fails.
            MessageBox.Show(this, $"画像サイズを変更できませんでした。\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Choosing another drawing action takes over from the pen, so the user never has to stop the pen first.
    private void LeavePenMode()
    {
        PenPopup.IsOpen = false;
        Canvas.IsPenMode = false;
        Canvas.IsSelectMode = false;
    }

    private void OnAddText(object sender, ExecutedRoutedEventArgs e)
    {
        LeavePenMode();
        if (ActiveTab is { } tab)
        {
            var label = SettingsService.Load().DefaultTextLabel;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = AppSettings.DefaultTextLabelFallback;
            }

            tab.AddLayerAboveSelection(LayerFactory.CreateText(tab.Document, label, StateService.ApplyText));
        }
    }

    private void OnAddShape(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab || e.Parameter is not ShapeKind kind)
        {
            return;
        }

        LeavePenMode();

        var name = kind switch
        {
            ShapeKind.Ellipse => "楕円",
            ShapeKind.Arrow => "矢印",
            ShapeKind.Line => "線",
            _ => "矩形",
        };
        tab.AddLayerAboveSelection(LayerFactory.CreateShape(tab.Document, kind, name, shape => StateService.ApplyShape(shape, tab.Document.Width, tab.Document.Height)));
    }

    private void OnCloseTab(object sender, ExecutedRoutedEventArgs e)
    {
        if ((e.Parameter as DocumentTab ?? ActiveTab) is { } tab)
        {
            CloseTab(tab);
        }
    }

    private void OnActiveTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var tab = TabStrip.SelectedItem as DocumentTab;
        if (ActiveTab is not null)
        {
            ActiveTab.PropertyChanged -= OnActiveTabPropertyChanged;
        }

        ActiveTab = tab;
        if (tab is not null)
        {
            tab.PropertyChanged += OnActiveTabPropertyChanged;
        }

        UpdateZoomText();
        Canvas.Tab = tab;
        LayerPanel.Tab = tab;
        PropertiesPanel.Tab = tab;
        EmptyHint.Visibility = tab is null ? Visibility.Visible : Visibility.Collapsed;
        CommandManager.InvalidateRequerySuggested();
    }

    private void OpenClipboardImageInNewTab()
    {
        var bitmap = ClipboardService.TryGetImage();
        if (bitmap is null)
        {
            return;
        }

        AddTab(DocumentFactory.CreateFromImage(bitmap, $"クリップボード {++_clipboardTabCount}", "クリップボード"));
    }

    private DocumentTab AddTab(ImageDocument document)
    {
        var tab = new DocumentTab(document);
        _tabs.Add(tab);
        TabStrip.SelectedItem = tab;
        TabStrip.ScrollIntoView(tab);
        return tab;
    }

    /// <summary>
    /// Returns true if the document may be closed: it has no unsaved changes, or the user chose to save
    /// them (and the save succeeded) or to discard them.
    /// </summary>
    private bool ConfirmClose(DocumentTab tab)
    {
        if (!tab.IsModified)
        {
            return true;
        }

        // Show the document first so it is clear which one is being asked about.
        TabStrip.SelectedItem = tab;
        var result = MessageBox.Show(
            this,
            $"「{tab.Title}」への変更は保存されていません。保存しますか？\n（書き出しやコピーをした後の変更のみが対象です）",
            "Trimx",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => SaveProject(tab, saveAs: false),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private void CloseTab(DocumentTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0 || !ConfirmClose(tab))
        {
            return;
        }

        // Switch away first so the canvas and panel release the tab before it is disposed.
        if (ReferenceEquals(tab, ActiveTab))
        {
            TabStrip.SelectedItem = index + 1 < _tabs.Count ? _tabs[index + 1]
                : index > 0 ? _tabs[index - 1]
                : null;
        }

        _tabs.RemoveAt(index);
        tab.Dispose();
    }
}
