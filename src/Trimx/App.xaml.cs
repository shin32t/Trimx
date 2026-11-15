using System.Windows;
using Trimx.Services;

namespace Trimx;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Started by the old version to swap the files: do that and nothing else (no window, no single-instance check).
        if (UpdateService.TryRunApplier(e.Args))
        {
            Shutdown();
            return;
        }

        // First thing, so a second launch exits quickly without loading anything.
        _singleInstance = SingleInstanceService.TryBecomePrimary();
        if (_singleInstance is null)
        {
            SingleInstanceService.NotifyPrimary();
            Shutdown();
            return;
        }

        DebugMode.Initialize(e.Args);
        _singleInstance.SecondInstanceStarted += () => Dispatcher.BeginInvoke(OnSecondInstanceStarted);

        // Before the main window exists, so it never flashes the wrong palette.
        var settings = SettingsService.Load();
        UiOptions.Instance.ShowToolbarLabels = settings.ShowToolbarLabels;
        ThemeService.Apply(ThemeService.Parse(settings.Theme));
        LocalizationService.Apply(settings.Language);
        base.OnStartup(e);
    }

    private void OnSecondInstanceStarted()
    {
        // MainWindow is null only while the window is still being created; that window loads the clipboard itself.
        (MainWindow as MainWindow)?.ActivateFromAnotherInstance();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();

        // Saves what the delayed save has not written yet.
        StateService.Flush();
        base.OnExit(e);
    }
}
