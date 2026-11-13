using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using SkiaSharp;
using Trimx.Core.Documents;

namespace Trimx.Services;

/// <summary>
/// Holds <see cref="AppState"/> in memory and saves it as TrimxState.json under %LOCALAPPDATA%\Trimx.
/// Changes arrive in bursts (a slider drag, typing), so saving waits for a short pause and happens once more at exit.
/// </summary>
public static class StateService
{
    private const string FileName = "TrimxState.json";

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private static AppState? _current;
    private static DispatcherTimer? _timer;

    private static string StatePath => Path.Combine(SettingsService.DataDirectory, FileName);

    /// <summary>The state, read from disk the first time it is needed.</summary>
    public static AppState Current => _current ??= Load();

    /// <summary>Schedules a save. Call after changing <see cref="Current"/>.</summary>
    public static void MarkChanged()
    {
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = SaveDelay };
            _timer.Tick += (_, _) => Flush();
        }

        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Writes the state now if a save is pending.</summary>
    public static void Flush()
    {
        _timer?.Stop();
        if (_current is not null)
        {
            Save(_current);
        }
    }

    /// <summary>Forgets the remembered drawing styles (pen, text, shapes). Size history and My colors stay.</summary>
    public static void ResetDrawingStyles()
    {
        Current.Pen = new PenStyle();
        Current.Text = null;
        Current.Shape = null;
        Flush();
    }

    // --- Capturing and applying styles ---

    /// <summary>Remembers the look of a layer the user just edited, for the next one they add.</summary>
    public static void Capture(Layer layer)
    {
        switch (layer)
        {
            case TextLayer text:
                Current.Text = new TextStyle
                {
                    FontFamily = text.FontFamily,
                    FontSize = text.FontSize,
                    Color = FormatColor(text.Color),
                    Bold = text.IsBold,
                    Italic = text.IsItalic,
                    HasShadow = text.HasShadow,
                    ShadowOpacity = text.ShadowOpacity,
                    ShadowBlur = text.ShadowBlur,
                    ShadowOffsetX = text.ShadowOffsetX,
                    ShadowOffsetY = text.ShadowOffsetY,
                    OutlineWidth = text.OutlineWidth,
                    OutlineColor = FormatColor(text.OutlineColor),
                };
                MarkChanged();
                break;

            case ShapeLayer shape:
                Current.Shape = new ShapeStyle
                {
                    StrokeColor = FormatColor(shape.StrokeColor),
                    StrokeWidth = shape.StrokeWidth,
                    FillColor = FormatColor(shape.FillColor),
                    Dash = shape.Dash.ToString(),
                    CornerRadius = shape.CornerRadius,
                    Sizes = Current.Shape?.Sizes ?? new Dictionary<string, ShapeSize>(),
                };
                Current.Shape.Sizes[shape.Kind.ToString()] = new ShapeSize { Width = shape.Width, Height = shape.Height };
                MarkChanged();
                break;
        }
    }

    /// <summary>Gives a new text layer the remembered look, if there is one.</summary>
    public static void ApplyText(TextLayer layer)
    {
        if (Current.Text is not { } style)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(style.FontFamily))
        {
            layer.FontFamily = style.FontFamily;
        }

        if (style.FontSize >= 1f)
        {
            layer.FontSize = style.FontSize;
        }

        layer.Color = ParseColor(style.Color, layer.Color);
        layer.IsBold = style.Bold;
        layer.IsItalic = style.Italic;
        layer.HasShadow = style.HasShadow;
        layer.ShadowOpacity = Finite(style.ShadowOpacity, layer.ShadowOpacity);
        layer.ShadowBlur = Finite(style.ShadowBlur, layer.ShadowBlur);
        layer.ShadowOffsetX = Finite(style.ShadowOffsetX, layer.ShadowOffsetX);
        layer.ShadowOffsetY = Finite(style.ShadowOffsetY, layer.ShadowOffsetY);
        layer.OutlineWidth = Finite(style.OutlineWidth, layer.OutlineWidth);
        layer.OutlineColor = ParseColor(style.OutlineColor, layer.OutlineColor);
    }

    /// <summary>Gives a new shape layer the remembered look and size, if there is one.</summary>
    public static void ApplyShape(ShapeLayer layer, int maxWidth, int maxHeight)
    {
        if (Current.Shape is not { } style)
        {
            return;
        }

        layer.StrokeColor = ParseColor(style.StrokeColor, layer.StrokeColor);
        layer.StrokeWidth = Finite(style.StrokeWidth, layer.StrokeWidth);
        layer.FillColor = ParseColor(style.FillColor, layer.FillColor);
        layer.Dash = Enum.TryParse<ShapeDash>(style.Dash, out var dash) && Enum.IsDefined(dash) ? dash : layer.Dash;
        layer.CornerRadius = Finite(style.CornerRadius, layer.CornerRadius);

        // The size is remembered per kind. The new layer is never larger than the canvas.
        if (style.Sizes is { } sizes && sizes.TryGetValue(layer.Kind.ToString(), out var size) && (size.Width != 0 || size.Height != 0))
        {
            layer.Width = Math.Clamp(size.Width, -maxWidth, maxWidth);
            layer.Height = Math.Clamp(size.Height, -maxHeight, maxHeight);
        }
    }

    public static string FormatColor(SKColor color) => $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    // A hand-edited file may hold anything, so a bad value keeps the built-in one.
    public static SKColor ParseColor(string? text, SKColor fallback)
    {
        return !string.IsNullOrWhiteSpace(text) && SKColor.TryParse(text, out var color) ? color : fallback;
    }

    private static float Finite(float value, float fallback) => float.IsFinite(value) ? value : fallback;

    // --- Disk ---

    private static AppState Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                using var stream = File.OpenRead(StatePath);
                return Normalize(JsonSerializer.Deserialize<AppState>(stream, _jsonOptions));
            }

            // First run after the split: carry over the size history and My colors from the old settings.json.
            return Normalize(LegacySettingsFile.Read<AppState>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // The state is only a convenience; a missing or corrupt file falls back to defaults.
            return new AppState();
        }
    }

    private static AppState Normalize(AppState? state)
    {
        state ??= new AppState();
        state.Pen ??= new PenStyle();
        state.MyColors ??= new List<string>();
        return state;
    }

    private static void Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, _jsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failing to remember state must not interrupt editing.
        }
    }
}
