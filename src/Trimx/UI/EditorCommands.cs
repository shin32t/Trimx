using System.Windows.Input;

namespace Trimx.UI;

/// <summary>
/// Application-specific routed commands. Standard ones (New, Paste) come from <see cref="ApplicationCommands"/>.
/// </summary>
public static class EditorCommands
{
    public static readonly RoutedUICommand FitToView = new(
        "フィット",
        nameof(FitToView),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.D0, ModifierKeys.Control),
            new KeyGesture(Key.NumPad0, ModifierKeys.Control),
        });

    public static readonly RoutedUICommand ZoomIn = new(
        "拡大",
        nameof(ZoomIn),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.OemPlus, ModifierKeys.Control),

            // On a Japanese keyboard "+" is Shift + ";".
            new KeyGesture(Key.OemPlus, ModifierKeys.Control | ModifierKeys.Shift),
            new KeyGesture(Key.Add, ModifierKeys.Control),
        });

    public static readonly RoutedUICommand ZoomOut = new(
        "縮小",
        nameof(ZoomOut),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.OemMinus, ModifierKeys.Control),
            new KeyGesture(Key.Subtract, ModifierKeys.Control),
        });

    /// <summary>Shows the image at 100% (one image pixel per screen pixel).</summary>
    public static readonly RoutedUICommand ActualSize = new(
        "100% 表示",
        nameof(ActualSize),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.D1, ModifierKeys.Control),
            new KeyGesture(Key.NumPad1, ModifierKeys.Control),
        });

    public static readonly RoutedUICommand AddText = new("テキストを追加", nameof(AddText), typeof(EditorCommands));

    /// <summary>Adds a shape layer. The command parameter is a <see cref="Core.Documents.ShapeKind"/>.</summary>
    public static readonly RoutedUICommand AddShape = new("図形を追加", nameof(AddShape), typeof(EditorCommands));

    /// <summary>Exports the composited image as PNG / JPEG / WebP.</summary>
    public static readonly RoutedUICommand Export = new(
        "書き出し",
        nameof(Export),
        typeof(EditorCommands),
        new InputGestureCollection { new KeyGesture(Key.E, ModifierKeys.Control) });

    /// <summary>Crops the canvas to the selected rectangle.</summary>
    public static readonly RoutedUICommand Crop = new("切り抜き", nameof(Crop), typeof(EditorCommands));

    public static readonly RoutedUICommand ResizeCanvas = new("キャンバスサイズ", nameof(ResizeCanvas), typeof(EditorCommands));

    public static readonly RoutedUICommand ResizeImage = new("画像サイズ", nameof(ResizeImage), typeof(EditorCommands));

    /// <summary>Closes a tab. The command parameter is the <see cref="DocumentTab"/> (or null for the active tab).</summary>
    public static readonly RoutedUICommand CloseTab = new("タブを閉じる", nameof(CloseTab), typeof(EditorCommands));
}
