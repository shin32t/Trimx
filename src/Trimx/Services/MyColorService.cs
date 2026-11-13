using System.Globalization;
using SkiaSharp;

namespace Trimx.Services;

/// <summary>
/// The user's own palette: the last few colors chosen in the color picker, kept between sessions.
/// </summary>
public static class MyColorService
{
    public const int Capacity = 4;

    public static IReadOnlyList<SKColor> Load()
    {
        var colors = new List<SKColor>();
        foreach (var text in StateService.Current.MyColors)
        {
            // Ignore entries a hand-edited settings file may have broken.
            var hex = text.TrimStart('#');
            if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
                && colors.Count < Capacity)
            {
                colors.Add(new SKColor(0xFF000000 | rgb));
            }
        }

        return colors;
    }

    /// <summary>Puts the color first (moving it up if already present), drops the oldest beyond the capacity, and saves.</summary>
    public static IReadOnlyList<SKColor> Add(SKColor color)
    {
        var colors = Load().Where(c => c.WithAlpha(255) != color.WithAlpha(255)).ToList();
        colors.Insert(0, color.WithAlpha(255));
        colors = colors.Take(Capacity).ToList();

        StateService.Current.MyColors = colors.Select(c => $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}").ToList();
        StateService.MarkChanged();
        return colors;
    }
}
