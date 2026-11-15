# Easy-to-break areas and verification notes

## Implementation pitfalls

- **Text layer names** follow the first line of the content unless the user renamed the layer. Always change the name after changing the text. In the reverse order, the edit box that receives the change notification gets overwritten with stale text, the caret jumps to the start, and typed characters end up reversed.
- **Never reference brushes with StaticResource.** Colors stop changing when the theme is switched.
- **Never modify layers directly from the UI.** Undo stops working and the unsaved-state tracking goes out of sync.
- **Bitmap ownership.** Layers and bitmaps held by a command must be disposed when the command leaves the history. Watch for both double disposal and leaks.
- **Characters missing from the icon font (Segoe Fluent Icons)**, such as Unicode geometric symbols, render as the same empty box. Do not use them as toolbar glyphs.
- **Load the font list on first display.** Loading it at startup slows startup.
- **Overwriting settings.** Handle `TrimxSetting.json` as "reload, modify, save". Store `AppSettings.Theme` as a string so a typo from hand-editing cannot make the whole file unreadable.

- **Localized text.** A key missing from `en.json` silently shows the Japanese text (and a key missing from both shows the key itself), so add both files together. Use `{DynamicResource}` (not `StaticResource`) for text in XAML or a language change will not reach it. Text that code sets once (not through a command or `Loc.Changed`) stays in the old language until it is set again.

## Verifying on a real screen (Windows)

- Launch the app as `dotnet <temp folder>\Debug\net10.0-windows10.0.19041.0\Trimx.dll`, not as the exe (an exe in a temp folder may be refused execution).
- Button operations can be driven with UI Automation's InvokePattern, and the screen can be captured with PrintWindow (call `SetProcessDPIAware` first for DPI correctness). The buttons of a native `MessageBox` show up as a Pane without InvokePattern: send `BM_CLICK` (0x00F5) to their `NativeWindowHandle` instead.
- Synthetic mouse and keyboard input may not reach the app. A human must verify dragging, double-clicking, and key operations on a real machine. **Do not report an interaction as working if you could not verify it.**
