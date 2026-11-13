using System.Windows;
using Trimx.Services;

namespace Trimx;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Before the main window exists, so it never flashes the wrong palette.
        var settings = SettingsService.Load();
        UiOptions.Instance.ShowToolbarLabels = settings.ShowToolbarLabels;
        ThemeService.Apply(ThemeService.Parse(settings.Theme));
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Saves what the delayed save has not written yet.
        StateService.Flush();
        base.OnExit(e);
    }
}
