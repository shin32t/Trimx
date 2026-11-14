# Third-party notices

Trimx itself is released under the MIT License (see `LICENSE`). It uses the components below. The full license
texts are in the `licenses/` folder, which is included in the installer and the zip.
License names are taken from each package's metadata.

| Component | License | Full text in `licenses/` |
|---|---|---|
| .NET runtime / WPF (bundled, Microsoft and the .NET Foundation) | MIT | `dotnet-LICENSE.txt`, and `dotnet-THIRD-PARTY-NOTICES.txt` for the libraries inside the runtime |
| SkiaSharp, SkiaSharp.Views.WPF, SkiaSharp.Views.Desktop.Common, SkiaSharp.NativeAssets.Win32 | MIT | `SkiaSharp-LICENSE.txt` |
| Skia, ANGLE, FreeType, HarfBuzz and other libraries inside the SkiaSharp native library | BSD-3-Clause and others, as listed | `SkiaSharp-THIRD-PARTY-NOTICES.txt` |
| OpenTK (and its sub-packages) | MIT | `OpenTK-LICENSE.txt` |
| OpenTK.GLWpfControl | MIT | `OpenTK-GLWpfControl-LICENSE.txt` |
| OpenTK.redist.glfw (GLFW) | zlib/libpng | `GLFW-COPYING.md` |

OpenTK, OpenTK.GLWpfControl and GLFW are pulled in by SkiaSharp.Views.WPF.

The toolbar icons use the "Segoe Fluent Icons" font that ships with Windows; the font is not redistributed with Trimx.

## Keeping this up to date

When a dependency is added or its version changes, update the table and the files in `licenses/`.
The SkiaSharp and GLFW texts are copied from the NuGet packages (`LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `COPYING.md`);
the .NET notices come from the `dotnet` folder of the installed SDK; the OpenTK and GLWpfControl texts come from their GitHub repositories
(the packages do not include a license file).
