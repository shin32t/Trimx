using SkiaSharp;
using Trimx.Core.Documents;
using Trimx.Core.Rendering;

namespace Trimx.Core.Services;

/// <summary>
/// Exports the composited document as a PNG, JPEG or WebP file.
/// </summary>
public static class ImageExportService
{
    private const int JpegQuality = 92;

    /// <summary>Returns the format for a file extension (with or without a dot), or null if unsupported.</summary>
    public static ExportFormat? FormatFromExtension(string extension)
    {
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "png" => ExportFormat.Png,
            "jpg" or "jpeg" => ExportFormat.Jpeg,
            "webp" => ExportFormat.Webp,
            _ => null,
        };
    }

    /// <summary>Writes the document to a file. An existing file is replaced only if the export succeeds.</summary>
    public static void ExportToFile(ImageDocument document, ExportFormat format, string path)
    {
        AtomicFile.Write(path, stream => Export(document, format, stream));
    }

    public static void Export(ImageDocument document, ExportFormat format, Stream stream)
    {
        // JPEG has no alpha channel, so transparent areas would turn black; flatten onto white instead.
        var background = format == ExportFormat.Jpeg ? SKColors.White : SKColors.Transparent;
        using var bitmap = ExportRenderer.Render(document, background);
        using var pixmap = bitmap.PeekPixels();

        var ok = format switch
        {
            ExportFormat.Png => pixmap.Encode(stream, new SKPngEncoderOptions()),
            ExportFormat.Jpeg => pixmap.Encode(stream, new SKJpegEncoderOptions(JpegQuality)),

            // Lossless keeps screenshot text crisp.
            ExportFormat.Webp => pixmap.Encode(stream, new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100f)),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        if (!ok)
        {
            throw new IOException($"{format} への書き出しに失敗しました。");
        }
    }
}
