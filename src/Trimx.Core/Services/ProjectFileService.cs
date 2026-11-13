using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Core.Services;

/// <summary>
/// Saves and loads documents in the Trimx project format.
/// </summary>
/// <remarks>
/// A project file is a ZIP archive holding <c>project.json</c> (canvas size and layer properties)
/// and one lossless PNG per image layer under <c>images/</c>. Text and shapes stay editable because
/// only their properties are stored. A ZIP container keeps the format inspectable and lets image
/// data stay out of the JSON.
/// </remarks>
public static class ProjectFileService
{
    public const string FileExtension = ".trimx";

    /// <summary>Format version written by this build. Older files are readable; newer ones are rejected.</summary>
    public const int CurrentVersion = 1;

    private const string ManifestName = "project.json";
    private const int MaxLayers = 1000;
    private const long MaxManifestBytes = 16 * 1024 * 1024;
    private const long MaxImageBytes = 1L * 1024 * 1024 * 1024;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Writes the document to a project file. An existing file is replaced only if the save succeeds.</summary>
    public static void Save(ImageDocument document, string path)
    {
        AtomicFile.Write(path, stream => Save(document, stream));
    }

    public static void Save(ImageDocument document, Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        var project = new ProjectDto
        {
            Version = CurrentVersion,
            Width = document.Width,
            Height = document.Height,
        };

        var imageIndex = 0;
        foreach (var layer in document.Layers)
        {
            var dto = new LayerDto
            {
                Name = layer.Name,
                IsVisible = layer.IsVisible,
                Opacity = layer.Opacity,
                X = layer.X,
                Y = layer.Y,
            };

            switch (layer)
            {
                case ImageLayer image:
                    dto.Type = "image";
                    dto.Image = $"images/{imageIndex++}.png";
                    WriteImage(archive, dto.Image, image.Bitmap);
                    break;

                case TextLayer text:
                    dto.Type = "text";
                    dto.Text = text.Text;
                    dto.FontFamily = text.FontFamily;
                    dto.FontSize = text.FontSize;
                    dto.Bold = text.IsBold;
                    dto.Italic = text.IsItalic;
                    dto.HasShadow = text.HasShadow;
                    dto.ShadowOpacity = text.ShadowOpacity;
                    dto.ShadowBlur = text.ShadowBlur;
                    dto.ShadowOffsetX = text.ShadowOffsetX;
                    dto.ShadowOffsetY = text.ShadowOffsetY;
                    dto.OutlineWidth = text.OutlineWidth;
                    dto.OutlineColor = FormatColor(text.OutlineColor);
                    dto.Color = FormatColor(text.Color);
                    break;

                case ShapeLayer shape:
                    dto.Type = "shape";
                    dto.Shape = shape.Kind.ToString();
                    dto.Width = shape.Width;
                    dto.Height = shape.Height;
                    dto.StrokeColor = FormatColor(shape.StrokeColor);
                    dto.StrokeWidth = shape.StrokeWidth;
                    dto.FillColor = FormatColor(shape.FillColor);
                    dto.Dash = shape.Dash.ToString();
                    dto.CornerRadius = shape.CornerRadius;
                    break;

                default:
                    throw new NotSupportedException($"未対応のレイヤー種別です: {layer.GetType().Name}");
            }

            project.Layers.Add(dto);
        }

        var manifest = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
        using var manifestStream = manifest.Open();
        JsonSerializer.Serialize(manifestStream, project, _jsonOptions);
    }

    /// <summary>
    /// Loads a project file. The caller owns the returned document.
    /// Throws <see cref="InvalidDataException"/> if the file is not a valid Trimx project.
    /// </summary>
    public static ImageDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream, Path.GetFileNameWithoutExtension(path));
    }

    public static ImageDocument Load(Stream stream, string title)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var manifestEntry = archive.GetEntry(ManifestName)
            ?? throw new InvalidDataException("Trimx のプロジェクトファイルではありません。");

        ProjectDto project;
        try
        {
            using var manifestStream = new MemoryStream(ReadLimited(manifestEntry, MaxManifestBytes));
            project = JsonSerializer.Deserialize<ProjectDto>(manifestStream, _jsonOptions)
                ?? throw new InvalidDataException("プロジェクトファイルが壊れています。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("プロジェクトファイルが壊れています。", ex);
        }

        if (project.Version > CurrentVersion)
        {
            throw new InvalidDataException("新しいバージョンの Trimx で作成されたファイルです。アプリを更新してください。");
        }

        if (project.Width is < 1 or > ImageLimits.MaxSide || project.Height is < 1 or > ImageLimits.MaxSide)
        {
            throw new InvalidDataException("キャンバスサイズが不正です。");
        }

        if (project.Layers.Count > MaxLayers)
        {
            throw new InvalidDataException("レイヤー数が多すぎます。");
        }

        var document = new ImageDocument(project.Width, project.Height, title);
        try
        {
            foreach (var dto in project.Layers)
            {
                document.AddLayer(CreateLayer(archive, dto));
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static Layer CreateLayer(ZipArchive archive, LayerDto dto)
    {
        Layer layer = dto.Type switch
        {
            "image" => CreateImageLayer(archive, dto),
            "text" => new TextLayer
            {
                Text = dto.Text ?? string.Empty,
                FontFamily = string.IsNullOrWhiteSpace(dto.FontFamily) ? TextLayer.DefaultFontFamily : dto.FontFamily,
                FontSize = dto.FontSize ?? 32f,
                IsBold = dto.Bold,
                IsItalic = dto.Italic,
                // Older files had no switch: a shadow existed when its darkness was above 0.
                HasShadow = dto.HasShadow ?? (dto.ShadowOpacity ?? 0f) > 0f,
                ShadowOpacity = Finite(dto.ShadowOpacity, 0.5f) is var opacity and > 0f ? opacity : 0.5f,
                ShadowBlur = Finite(dto.ShadowBlur, 0f),
                ShadowOffsetX = Finite(dto.ShadowOffsetX, 4f),
                ShadowOffsetY = Finite(dto.ShadowOffsetY, 4f),
                OutlineWidth = Finite(dto.OutlineWidth, 0f),
                OutlineColor = ParseColor(dto.OutlineColor, SKColors.White),
                Color = ParseColor(dto.Color, SKColors.Black),
            },
            "shape" => new ShapeLayer
            {
                Kind = Enum.TryParse<ShapeKind>(dto.Shape, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
                    ? kind
                    : throw new InvalidDataException("図形の種類が不正です。"),
                Width = dto.Width ?? 0,
                Height = dto.Height ?? 0,
                StrokeColor = ParseColor(dto.StrokeColor, SKColors.Black),
                StrokeWidth = dto.StrokeWidth ?? 4f,
                FillColor = ParseColor(dto.FillColor, SKColors.Transparent),

                // Absent or unknown values (older files) fall back to a plain solid outline.
                Dash = Enum.TryParse<ShapeDash>(dto.Dash, ignoreCase: true, out var dash) && Enum.IsDefined(dash) ? dash : ShapeDash.Solid,
                CornerRadius = dto.CornerRadius is { } radius && float.IsFinite(radius) ? radius : 0f,
            },
            _ => throw new InvalidDataException($"未対応のレイヤー種別です: {dto.Type}"),
        };

        layer.Name = dto.Name ?? string.Empty;
        layer.IsVisible = dto.IsVisible;
        layer.Opacity = float.IsFinite(dto.Opacity) ? dto.Opacity : 1f; // The setter clamps to 0..1.
        layer.X = dto.X;
        layer.Y = dto.Y;
        return layer;
    }

    private static ImageLayer CreateImageLayer(ZipArchive archive, LayerDto dto)
    {
        // Entries are looked up by exactly the name in the manifest and read in memory, never extracted
        // to disk, so a crafted name cannot write outside anything.
        var entry = dto.Image is null ? null : archive.GetEntry(dto.Image);
        if (entry is null)
        {
            throw new InvalidDataException("画像データが見つかりません。");
        }

        using var stream = new MemoryStream(ReadLimited(entry, MaxImageBytes));
        return new ImageLayer(ImageImportService.Decode(stream));
    }

    private static void WriteImage(ZipArchive archive, string name, SKBitmap bitmap)
    {
        // PNG is already compressed, so storing it avoids useless extra work.
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        using var pixmap = bitmap.PeekPixels();
        if (!pixmap.Encode(entryStream, new SKPngEncoderOptions()))
        {
            throw new IOException("レイヤー画像の保存に失敗しました。");
        }
    }

    /// <summary>Reads an entry fully, counting actual bytes because the declared length can be forged.</summary>
    private static byte[] ReadLimited(ZipArchiveEntry entry, long maxBytes)
    {
        if (entry.Length > maxBytes)
        {
            throw new InvalidDataException("プロジェクトファイルのデータが大きすぎます。");
        }

        using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException("プロジェクトファイルのデータが大きすぎます。");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static float Finite(float? value, float fallback) => value is { } v && float.IsFinite(v) ? v : fallback;

    private static string FormatColor(SKColor color)
    {
        return $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    private static SKColor ParseColor(string? text, SKColor fallback)
    {
        var hex = text?.TrimStart('#');
        return hex is { Length: 8 } && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? new SKColor(value)
            : fallback;
    }

    private sealed class ProjectDto
    {
        public int Version { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public List<LayerDto> Layers { get; set; } = new();
    }

    private sealed class LayerDto
    {
        public string Type { get; set; } = string.Empty;

        public string? Name { get; set; }

        public bool IsVisible { get; set; } = true;

        public float Opacity { get; set; } = 1f;

        public int X { get; set; }

        public int Y { get; set; }

        // image
        public string? Image { get; set; }

        // text
        public string? Text { get; set; }

        public string? FontFamily { get; set; }

        public float? FontSize { get; set; }

        public string? Color { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool? HasShadow { get; set; }

        public float? ShadowOpacity { get; set; }

        public float? ShadowBlur { get; set; }

        public float? ShadowOffsetX { get; set; }

        public float? ShadowOffsetY { get; set; }

        public float? OutlineWidth { get; set; }

        public string? OutlineColor { get; set; }

        // shape
        public string? Shape { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public string? StrokeColor { get; set; }

        public float? StrokeWidth { get; set; }

        public string? FillColor { get; set; }

        public string? Dash { get; set; }

        public float? CornerRadius { get; set; }
    }
}
