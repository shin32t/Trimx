# Third-party notices

Trimx itself is released under the MIT License (see `LICENSE`). It uses the following components.
License names are taken from each package's NuGet metadata. Before publishing a release, check the
full license texts shipped in the packages and include them with the distributed files.

| Component | License | Notes |
|---|---|---|
| .NET runtime / WPF | MIT | Microsoft |
| SkiaSharp, SkiaSharp.Views.WPF, SkiaSharp.Views.Desktop.Common, SkiaSharp.NativeAssets.Win32 | MIT | The bundled native Skia library is BSD-3-Clause (Google) |
| OpenTK (and its sub-packages) | MIT | Pulled in by SkiaSharp.Views.WPF |
| OpenTK.GLWpfControl | MIT | Pulled in by SkiaSharp.Views.WPF |
| OpenTK.redist.glfw (GLFW) | zlib/libpng | Pulled in by SkiaSharp.Views.WPF |

The toolbar icons use the "Segoe Fluent Icons" font that ships with Windows; the font is not redistributed with Trimx.
