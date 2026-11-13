using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Trimx.Core.Commands;
using Trimx.Core.Documents;

namespace Trimx.UI;

// Editing a text layer in place: double-click the text to type over it.
public partial class CanvasView
{
    private TextLayer? _editingLayer;

    // Set while the editor text is changed from code, so it is not applied back to the layer.
    private bool _isEditorSyncing;

    private void InitializeTextEditor()
    {
        InlineEditor.TextChanged += OnEditorTextChanged;
        InlineEditor.LostKeyboardFocus += (_, _) => CloseTextEditor();
        InlineEditor.PreviewKeyDown += OnEditorPreviewKeyDown;
    }

    private void BeginTextEdit(TextLayer layer)
    {
        if (_tab is null)
        {
            return;
        }

        CloseTextEditor();
        _editingLayer = layer;
        PositionEditor(_tab, layer);

        _isEditorSyncing = true;
        InlineEditor.Text = layer.Text;
        _isEditorSyncing = false;

        InlineEditor.Visibility = Visibility.Visible;
        InlineEditor.UpdateLayout();
        InlineEditor.Focus();
        InlineEditor.SelectAll();

        // The layer is hidden from the canvas while the editor shows its text.
        Surface.InvalidateVisual();
    }

    /// <summary>Closes the editor, keeping the text typed so far. The layer reappears on the canvas.</summary>
    private void CloseTextEditor()
    {
        if (_editingLayer is null)
        {
            return;
        }

        _editingLayer = null;
        InlineEditor.Visibility = Visibility.Collapsed;
        _tab?.History.EndMerge();
        Surface.InvalidateVisual();
    }

    /// <summary>Keeps the editor in step with the layer after changes made elsewhere (panel, undo).</summary>
    private void SyncTextEditor()
    {
        if (_editingLayer is not { } layer || _tab is null)
        {
            return;
        }

        if (_tab.Document.IndexOf(layer) < 0)
        {
            CloseTextEditor();
            return;
        }

        if (InlineEditor.Text != layer.Text)
        {
            _isEditorSyncing = true;
            InlineEditor.Text = layer.Text;
            _isEditorSyncing = false;
        }

        PositionEditor(_tab, layer);
    }

    private void PositionEditor(DocumentTab tab, TextLayer layer)
    {
        var scale = DeviceScale;
        var origin = GetOrigin(tab, (float)(Surface.ActualWidth * scale), (float)(Surface.ActualHeight * scale));

        // The editor's 1px border sits just outside the text.
        InlineEditor.Margin = new Thickness(
            (origin.X + layer.X * tab.Zoom) / scale - 1,
            (origin.Y + layer.Y * tab.Zoom) / scale - 1,
            0,
            0);
        InlineEditor.FontFamily = new FontFamily(layer.FontFamily);
        InlineEditor.FontWeight = layer.IsBold ? FontWeights.Bold : FontWeights.Normal;
        InlineEditor.FontStyle = layer.IsItalic ? FontStyles.Italic : FontStyles.Normal;
        InlineEditor.FontSize = Math.Max(4d, layer.FontSize * tab.Zoom / scale);
        InlineEditor.Foreground = new SolidColorBrush(Color.FromArgb(layer.Color.Alpha, layer.Color.Red, layer.Color.Green, layer.Color.Blue));
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isEditorSyncing || _editingLayer is not { } layer || _tab is null || layer.Text == InlineEditor.Text)
        {
            return;
        }

        // Each keystroke is applied at once and merged, so the whole edit is one undo step.
        _tab.Execute(LayerCommands.SetText(layer, InlineEditor.Text), merge: true);
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape or Ctrl+Enter finishes editing; plain Enter adds a new line.
        if (e.Key == Key.Escape || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
        {
            e.Handled = true;

            // Moving focus away closes the editor (see LostKeyboardFocus).
            Keyboard.Focus(this);
        }
    }
}
