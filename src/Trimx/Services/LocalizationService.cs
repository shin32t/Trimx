using System.Windows;
using Trimx.Core.Localization;

namespace Trimx.Services;

/// <summary>
/// Applies the language setting. The texts of the chosen language are put into the application resources
/// (key to string), and the XAML refers to them with DynamicResource, so open windows follow immediately,
/// the same way <see cref="ThemeService"/> does for colors. Code uses <see cref="Loc.Get(string)"/> directly.
/// </summary>
public static class LocalizationService
{
    /// <summary>Setting value that follows the OS display language.</summary>
    public const string System = "System";

    private static ResourceDictionary? _dictionary;

    /// <summary>Turns a saved setting ("System", "ja", "en"; anything else counts as "System") into a language code.</summary>
    public static string Resolve(string? setting)
    {
        if (string.Equals(setting, Loc.Japanese, StringComparison.OrdinalIgnoreCase))
        {
            return Loc.Japanese;
        }

        return string.Equals(setting, Loc.English, StringComparison.OrdinalIgnoreCase) ? Loc.English : Loc.GetSystemLanguage();
    }

    public static void Apply(string? setting)
    {
        Loc.SetLanguage(Resolve(setting));

        var dictionary = new ResourceDictionary();
        foreach (var (key, text) in Loc.Strings)
        {
            dictionary[key] = text;
        }

        // One swap, so each DynamicResource refreshes once rather than once per key.
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_dictionary is not null)
        {
            merged.Remove(_dictionary);
        }

        merged.Add(dictionary);
        _dictionary = dictionary;
    }
}
