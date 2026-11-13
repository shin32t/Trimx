using System.IO;
using System.Text.Json;

namespace Trimx.Services;

/// <summary>
/// Reads the old single settings.json (before it was split into TrimxSetting.json and TrimxState.json),
/// so nothing the user had is lost. Both new files read the same JSON; unknown properties are ignored.
/// The old file itself is left in place.
/// </summary>
internal static class LegacySettingsFile
{
    public static T? Read<T>() where T : class
    {
        var path = Path.Combine(SettingsService.DataDirectory, "settings.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream);
    }
}
