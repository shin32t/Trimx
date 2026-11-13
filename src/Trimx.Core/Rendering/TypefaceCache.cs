using SkiaSharp;

namespace Trimx.Core.Rendering;

/// <summary>
/// Caches typefaces by family name. Resolving a typeface is expensive and only a handful of
/// families are used, so they are kept for the lifetime of the process.
/// </summary>
internal static class TypefaceCache
{
    private static readonly Dictionary<string, SKTypeface> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock _lock = new();

    public static SKTypeface Get(string familyName, bool bold = false, bool italic = false)
    {
        var key = $"{familyName}|{(bold ? 'b' : '-')}{(italic ? 'i' : '-')}";
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var typeface))
            {
                // When the family has no bold / italic face, Skia picks the closest one, and the
                // text is still drawn (just without the missing style).
                var style = new SKFontStyle(
                    bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal,
                    italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
                typeface = SKTypeface.FromFamilyName(familyName, style) ?? SKTypeface.Default;
                _cache[key] = typeface;
            }

            return typeface;
        }
    }
}
