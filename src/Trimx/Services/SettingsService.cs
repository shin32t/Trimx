using System.IO;
using System.Text.Json;

namespace Trimx.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as TrimxSetting.json under %LOCALAPPDATA%\Trimx.
/// </summary>
public static class SettingsService
{
    private const string FileName = "TrimxSetting.json";

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    internal static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Trimx");

    private static string SettingsPath => Path.Combine(DataDirectory, FileName);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var stream = File.OpenRead(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(stream, _jsonOptions) ?? new AppSettings();
            }

            // First run after the file was renamed: carry over what the old settings.json held.
            return LegacySettingsFile.Read<AppSettings>() ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Settings are only a convenience; a missing or corrupt file falls back to defaults.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failing to remember preferences must not interrupt editing.
        }
    }
}
