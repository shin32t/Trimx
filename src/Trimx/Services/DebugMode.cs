namespace Trimx.Services;

/// <summary>
/// Debug mode, switched on with the <c>--debug</c> command-line argument (works in Release builds too).
/// It lets developers see UI that normally needs an outside condition, such as the update button without a newer release.
/// </summary>
public static class DebugMode
{
    public const string Argument = "--debug";

    public static bool IsEnabled { get; private set; }

    public static void Initialize(IEnumerable<string> args)
    {
        IsEnabled = args.Any(arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase));
    }
}
