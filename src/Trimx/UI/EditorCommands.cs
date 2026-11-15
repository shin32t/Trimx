using System.Windows.Input;
using Trimx.Core.Localization;

namespace Trimx.UI;

/// <summary>
/// Application-specific routed commands. Standard ones (New, Paste) come from <see cref="ApplicationCommands"/>.
/// </summary>
public static class EditorCommands
{
    public static readonly RoutedUICommand FitToView = new(
        Loc.Get("tb.fit"),
        nameof(FitToView),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.D0, ModifierKeys.Control),
            new KeyGesture(Key.NumPad0, ModifierKeys.Control),
        });

    public static readonly RoutedUICommand ZoomIn = new(
        Loc.Get("cmd.zoomin"),
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
        Loc.Get("cmd.zoomout"),
        nameof(ZoomOut),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.OemMinus, ModifierKeys.Control),
            new KeyGesture(Key.Subtract, ModifierKeys.Control),
        });

    /// <summary>Shows the image at 100% (one image pixel per screen pixel).</summary>
    public static readonly RoutedUICommand ActualSize = new(
        Loc.Get("cmd.actualsize"),
        nameof(ActualSize),
        typeof(EditorCommands),
        new InputGestureCollection
        {
            new KeyGesture(Key.D1, ModifierKeys.Control),
            new KeyGesture(Key.NumPad1, ModifierKeys.Control),
        });

    public static readonly RoutedUICommand AddText = new(Loc.Get("cmd.addtext"), nameof(AddText), typeof(EditorCommands));

    /// <summary>Adds a shape layer. The command parameter is a <see cref="Core.Documents.ShapeKind"/>.</summary>
    public static readonly RoutedUICommand AddShape = new(Loc.Get("cmd.addshape"), nameof(AddShape), typeof(EditorCommands));

    /// <summary>Exports the composited image as PNG / JPEG / WebP.</summary>
    public static readonly RoutedUICommand Export = new(
        Loc.Get("common.export"),
        nameof(Export),
        typeof(EditorCommands),
        new InputGestureCollection { new KeyGesture(Key.E, ModifierKeys.Control) });

    /// <summary>Crops the canvas to the selected rectangle.</summary>
    public static readonly RoutedUICommand Crop = new(Loc.Get("common.crop"), nameof(Crop), typeof(EditorCommands));

    public static readonly RoutedUICommand ResizeCanvas = new(Loc.Get("resize.canvastitle"), nameof(ResizeCanvas), typeof(EditorCommands));

    public static readonly RoutedUICommand ResizeImage = new(Loc.Get("common.imagesize"), nameof(ResizeImage), typeof(EditorCommands));

    /// <summary>Closes a tab. The command parameter is the <see cref="DocumentTab"/> (or null for the active tab).</summary>
    public static readonly RoutedUICommand CloseTab = new(Loc.Get("cmd.closetab"), nameof(CloseTab), typeof(EditorCommands));

    static EditorCommands()
    {
        // The command texts are read once at construction, so they are set again when the language changes.
        Loc.Changed += (_, _) => RefreshTexts();
    }

    private static void RefreshTexts()
    {
        FitToView.Text = Loc.Get("tb.fit");
        ZoomIn.Text = Loc.Get("cmd.zoomin");
        ZoomOut.Text = Loc.Get("cmd.zoomout");
        ActualSize.Text = Loc.Get("cmd.actualsize");
        AddText.Text = Loc.Get("cmd.addtext");
        AddShape.Text = Loc.Get("cmd.addshape");
        Export.Text = Loc.Get("common.export");
        Crop.Text = Loc.Get("common.crop");
        ResizeCanvas.Text = Loc.Get("resize.canvastitle");
        ResizeImage.Text = Loc.Get("common.imagesize");
        CloseTab.Text = Loc.Get("cmd.closetab");
    }
}
