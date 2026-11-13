# Project overview

Trimx is a lightweight image editor for Windows 11. After `Win + Shift + S`, it lets the user quickly edit, annotate, and export the clipboard image.

- No screen-capture feature (use the built-in Windows tool).
- Single process. Multiple images are handled as tabs, not as new processes.
- Priorities: **fast startup** first, then **intuitive operation** and **layer editing** (text and shapes stay editable).
- Primary source for requirements: `docs/plan.md` (Japanese). Read it first when unsure about behavior.

## Tech stack

- C# / .NET 10 (`net10.0-windows10.0.19041.0`) / WPF / SkiaSharp
- Nullable and ImplicitUsings enabled
- Solution `Trimx.slnx`; projects `src/Trimx/Trimx.csproj` (WPF UI) and `src/Trimx.Core/Trimx.Core.csproj` (core)
- Root namespaces `Trimx` / `Trimx.Core`
- Do not change `TargetFramework` to `windows7.0`. SkiaSharp.Views.WPF would fall back to its .NET Framework build and pull in OpenTK.

## Build and run

```powershell
dotnet build Trimx.slnx
dotnet run --project src/Trimx/Trimx.csproj
```

Always confirm the build passes after making changes.

## Distribution (release)

- The version lives only in `<Version>` in `src/Trimx/Trimx.csproj` (currently 0.1.0). The build script and the installer read it from there.
- Publishing uses `src/Trimx/Properties/PublishProfiles/Release-win-x64.pubxml`: win-x64, **self-contained (.NET runtime bundled)**, ReadyToRun, neither single-file nor trimmed (WPF does not support trimming, and a folder starts faster). Only the ja / en UI languages are kept, and debug symbols are excluded (`libSkiaSharp.pdb` is about 100 MB). Output is `src/Trimx/bin/publish/win-x64/` (about 210 MB, about 80 MB zipped).
- `installer/build.ps1`: publish, then a portable zip (`installer/output/Trimx-<version>-win-x64.zip`), then, if Inno Setup 6 or later is installed (7 is searched first), an installer (`...-setup.exe`) from `installer/Trimx.iss`. Output goes to `installer/output/` (not tracked by git).
- The installer is per-user by default (no admin rights). Never change the `AppId` GUID; it identifies upgrades.
- Binaries are not signed (SmartScreen warns). The app icon and README banner are in `assets/branding/`; `ApplicationIcon` embeds the multi-resolution `.ico` in the exe.
- Release files include `LICENSE` and `THIRD-PARTY-NOTICES.md`.
- Releases are automated by `.github/workflows/release.yml`: pushing a tag `vX.Y.Z` builds on a Windows runner with `installer/build.ps1` and creates a **draft** GitHub Release (installer + zip, generated notes). The tag must equal `v` + `<Version>` in `src/Trimx/Trimx.csproj`, or the run fails. "Run workflow" in the Actions tab builds without releasing and keeps the files as an artifact. The workflow has not run on GitHub yet.
- The published exe has been verified to start (window shown, clean exit). The installer builds successfully with Inno Setup 7.1.1 (`installer/output/Trimx-<version>-win-x64-setup.exe`, about 60 MB), but installing and uninstalling it has not been tested.

## Implementation status

- Phases 1-4 are implemented: image display and zoom, tabs, layers (image / text / shape), Undo / Redo, text and shape editing, pen, rectangular selection and crop, canvas / image resize, project save (`.trimx`), PNG / JPEG / WebP export, clipboard copy, open files and drag and drop, theme and settings.
- Not implemented: single-instance behavior and optimization (Phase 5); BMP export (under consideration).
- Real-screen verification is partial. Build, startup, Core behavior, and button operations are verified; mouse and keyboard interactions are not.

## Data files (`%LOCALAPPDATA%\Trimx\`)

- `TrimxSetting.json`: choices the user makes in the settings dialog (`SettingsService` / `AppSettings`)
- `TrimxState.json`: things the app remembers on its own (`StateService` / `AppState`). Call `MarkChanged()` after changing it; `Flush()` runs on exit.

Do not mix the two. Read and write settings as "reload, modify, save" so other fields (such as my colors) are not overwritten.

## License

MIT (`LICENSE`; author shin32t). Third-party libraries (SkiaSharp, OpenTK family, GLFW) are listed in `THIRD-PARTY-NOTICES.md`. When you add a dependency, update that file too.
