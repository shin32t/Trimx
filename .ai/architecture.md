# Architecture

## Layers

| Layer | Location | Role |
|---|---|---|
| Core | `src/Trimx.Core/Documents` | ImageDocument / Layer (ImageLayer, TextLayer, ShapeLayer). **Must not depend on WPF** |
| Rendering | `src/Trimx.Core/Rendering` | CanvasRenderer / LayerRenderer / ExportRenderer. SkiaSharp drawing |
| Commands | `src/Trimx.Core/Commands` | Commands for Undo / Redo |
| Editing | `src/Trimx.Core/Editing` | DocumentFactory, LayerFactory |
| Services | `src/Trimx.Core/Services` | Image import/export, project files (WPF-independent) |
| UI | `src/Trimx/` | Tabs, canvas, toolbar, panels, dialogs, and WPF-dependent services such as the clipboard and settings |

## Key principles

- **Never modify Layer properties or the Document directly from the UI.** Every change goes through `DocumentTab` (`Execute`, etc.) as a command so it lands in the history.
- Keep layer management separate from rendering.
- Make ownership of Bitmap / SKBitmap explicit, and always Dispose them when no longer needed.
- Do not redraw unchanged frames. Avoid copying whole images for Undo history.
- Startup speed comes first. Show the main window first and defer heavy initialization (such as the font list).
- A clipboard failure must never prevent the app from starting.

## Commands and history

- `IDocumentCommand` (Execute / Undo / Dispose) and `CommandHistory` (one per tab, up to 100 steps).
- Continuous operations (sliders, typing, dragging) use `Execute(command, allowMerge: true)` to collapse into one step, and call `EndMerge()` when the operation ends. Do not pass `allowMerge` for one-off operations such as clicks.
- Layer ownership: a remove command owns the layer while applied, an add command owns it while undone, and a replace command owns the layer that is not currently in use. The owner disposes it when the command leaves the history.
- Unsaved-state tracking uses `CommandHistory.IsModified` / `MarkSaved()`. A successful save, export, or copy marks the document as unmodified.

## Rendering and theming

- Always reference color brushes with **`DynamicResource`** (with `StaticResource`, theme switching has no effect). From code, use `SetResourceReference`.
- `ThemeService` swaps the brushes in Application.Resources.

## File I/O (treat input as untrusted)

- `.trimx` is a ZIP (`project.json` + one PNG per layer). Format details are in plan.md 6.3.
- The dimension limit is `ImageLimits.MaxSide`. Read ZIP entries in memory, looking up only the names listed in the manifest (never extract to disk), and cap both the declared size and the actual bytes read.
- Saves go through a temporary file and then replace the target (`AtomicFile`).
- The format version is 1. When adding fields, keep older files readable by falling back to defaults.

## Image layer pixels

- A layer's bitmap carries the immutable flag. Write pixels back with row copies (follow the approach used by `PaintStrokeCommand` and `ClearRegionCommand`).
