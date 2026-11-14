using System.Reflection;

namespace Trimx.Services;

/// <summary>The running version, from <c>&lt;Version&gt;</c> in Trimx.csproj.</summary>
public static class AppVersion
{
    /// <summary>The version text without build metadata after '+' (such as a commit hash).</summary>
    public static string Text { get; } =
        (typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static Version Current { get; } = Version.TryParse(Text.Split('-')[0], out var version) ? version : new Version(0, 0, 0);
}
