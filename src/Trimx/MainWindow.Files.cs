using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SkiaSharp;
using Trimx.Core.Commands;
using Trimx.Core.Documents;
using Trimx.Core.Editing;
using Trimx.Core.Rendering;
using Trimx.Core.Services;
using Trimx.Services;
using Trimx.UI;

namespace Trimx;

// File operations: open, save, export, clipboard copy and drag & drop.
public partial class MainWindow
{
    private const string OpenFilter =
        "Trimx プロジェクトと画像|*.trimx;*.png;*.jpg;*.jpeg;*.webp;*.bmp|" +
        "Trimx プロジェクト (*.trimx)|*.trimx|" +
        "画像 (PNG, JPEG, WebP, BMP)|*.png;*.jpg;*.jpeg;*.webp;*.bmp";

    private const string ProjectFilter = "Trimx プロジェクト (*.trimx)|*.trimx";

    // Index (1-based) matches the filter order below.
    private static readonly ExportFormat[] _exportFormatsByFilter = { ExportFormat.Png, ExportFormat.Jpeg, ExportFormat.Webp };

    private const string ExportFilter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|WebP (*.webp)|*.webp";

    private void OnOpen(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = OpenFilter, Multiselect = true };
        if (dialog.ShowDialog(this) == true)
        {
            OpenFiles(dialog.FileNames);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            OpenFiles(paths);
            e.Handled = true;
        }
    }

    private void OpenFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            OpenFile(path);
        }
    }

    private void OpenFile(string path)
    {
        var isProject = string.Equals(Path.GetExtension(path), ProjectFileService.FileExtension, StringComparison.OrdinalIgnoreCase);

        // Opening a project that is already open just switches to its tab, so edits are not duplicated.
        if (isProject && _tabs.FirstOrDefault(tab => IsSamePath(tab.FilePath, path)) is { } existing)
        {
            TabStrip.SelectedItem = existing;
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (isProject)
            {
                AddTab(ProjectFileService.Load(path)).FilePath = path;
            }
            else
            {
                var name = Path.GetFileNameWithoutExtension(path);
                AddTab(DocumentFactory.CreateFromImage(ImageImportService.DecodeFile(path), name, name));
            }
        }
        catch (Exception ex)
        {
            // Any failure (missing file, bad format, out of memory) is reported to the user; the app keeps running.
            ShowError($"「{Path.GetFileName(path)}」を開けませんでした。", ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnSave(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is { } tab)
        {
            SaveProject(tab, saveAs: false);
        }
    }

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is { } tab)
        {
            SaveProject(tab, saveAs: true);
        }
    }

    /// <summary>Saves the project; returns false if the user cancelled or the save failed.</summary>
    private bool SaveProject(DocumentTab tab, bool saveAs)
    {
        var path = tab.FilePath;
        if (path is null || saveAs)
        {
            var dialog = new SaveFileDialog
            {
                Filter = ProjectFilter,
                DefaultExt = ProjectFileService.FileExtension,
                AddExtension = true,
                FileName = path is null ? ToFileName(tab.Title) : Path.GetFileNameWithoutExtension(path),
            };
            if (path is not null)
            {
                dialog.InitialDirectory = Path.GetDirectoryName(path);
            }

            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            path = dialog.FileName;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ProjectFileService.Save(tab.Document, path);
        }
        catch (Exception ex)
        {
            ShowError("プロジェクトを保存できませんでした。", ex);
            return false;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        tab.FilePath = path;
        tab.Title = Path.GetFileNameWithoutExtension(path);
        tab.MarkSaved();
        return true;
    }

    private void OnExport(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = ExportFilter,
            DefaultExt = ".png",
            AddExtension = true,
            FileName = tab.FilePath is { } projectPath ? Path.GetFileNameWithoutExtension(projectPath) : ToFileName(tab.Title),
        };
        if (tab.FilePath is { } directoryOwner)
        {
            dialog.InitialDirectory = Path.GetDirectoryName(directoryOwner);
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        // The extension the user typed decides the format; if it is not one we write
        // (e.g. ".bmp"), the selected filter decides and its extension is appended so name and content agree.
        var path = dialog.FileName;
        var format = ImageExportService.FormatFromExtension(Path.GetExtension(path));
        if (format is null)
        {
            format = _exportFormatsByFilter[Math.Clamp(dialog.FilterIndex, 1, _exportFormatsByFilter.Length) - 1];
            path += format switch
            {
                ExportFormat.Jpeg => ".jpg",
                ExportFormat.Webp => ".webp",
                _ => ".png",
            };
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            ImageExportService.ExportToFile(tab.Document, format.Value, path);

            // The image has been delivered, so there is nothing left to warn about on close
            // (see ConfirmClose); editing on makes the document modified again.
            tab.MarkSaved();
        }
        catch (Exception ex)
        {
            ShowError("画像を書き出せませんでした。", ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { } tab)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            // With a selection, copy that rectangle from the selected layer (the whole picture if no layer is selected).
            var selection = tab.Selection;
            using var bitmap = selection is { } area
                ? tab.SelectedLayer is { } layer
                    ? ExportRenderer.RenderLayerRegion(layer, area)
                    : ExportRenderer.RenderRegion(tab.Document, area, SKColors.Transparent)
                : ExportRenderer.Render(tab.Document, SKColors.Transparent);
            if (!ClipboardService.TrySetImage(bitmap))
            {
                MessageBox.Show(this, "クリップボードに書き込めませんでした。他のアプリが使用中の可能性があります。", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                // Copying the whole picture is a way of delivering the result, like exporting.
                // A partial copy is not, so it leaves the unsaved state alone.
                if (selection is null)
                {
                    tab.MarkSaved();
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("画像をコピーできませんでした。", ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void CanCut(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ActiveTab is { Selection: not null, SelectedLayer: ImageLayer { IsVisible: true } };
    }

    // Cut works on the selected image layer only: its pixels in the selection go to the clipboard and become transparent.
    private void OnCut(object sender, ExecutedRoutedEventArgs e)
    {
        if (ActiveTab is not { Selection: { } area, SelectedLayer: ImageLayer { IsVisible: true } layer } tab)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            using var bitmap = ExportRenderer.RenderLayerRegion(layer, area);

            // The clipboard comes first, so a failure there does not lose the pixels.
            if (!ClipboardService.TrySetImage(bitmap))
            {
                MessageBox.Show(this, "クリップボードに書き込めませんでした。他のアプリが使用中の可能性があります。", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ClearRegionCommand.Create(layer, area) is { } command)
            {
                tab.Execute(command);
            }
        }
        catch (Exception ex)
        {
            ShowError("切り取れませんでした。", ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, $"{message}\n{ex.Message}", "Trimx", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string ToFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(title.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }

    private static bool IsSamePath(string? a, string b)
    {
        return a is not null
            && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
