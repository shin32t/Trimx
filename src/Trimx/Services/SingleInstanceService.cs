using System.Runtime.InteropServices;

namespace Trimx.Services;

/// <summary>
/// Keeps Trimx to one process per Windows user session. The first process owns the mutex and listens for a
/// signal; a later process signals it and exits, so extra images are opened as tabs instead of new windows.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    // "Local\" scopes the names to the current user session, so other users on the PC are not affected.
    private const string MutexName = @"Local\Trimx.SingleInstance";
    private const string SignalName = @"Local\Trimx.Activate";

    // How long a second process waits for the first one to finish creating its signal.
    private const int SignalRetryCount = 20;
    private const int SignalRetryDelayMs = 50;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceService(Mutex mutex, EventWaitHandle signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>
    /// Raised, on a thread-pool thread, when another Trimx process was started and asks this one to come forward.
    /// </summary>
    public event Action? SecondInstanceStarted;

    /// <summary>
    /// Returns an instance if this process is the first Trimx, or null if another one is already running.
    /// The caller keeps the result alive for the lifetime of the app.
    /// </summary>
    public static SingleInstanceService? TryBecomePrimary()
    {
        // Not initially owned: the handle only has to exist while the process lives, so there is nothing to release.
        var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        var signal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, SignalName);
        var service = new SingleInstanceService(mutex, signal);
        service._registration = ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => service.SecondInstanceStarted?.Invoke(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return service;
    }

    /// <summary>
    /// Called by a second process: asks the first one to show its window, then returns.
    /// Gives up quietly if the first one cannot be reached (for example, it runs with higher privileges).
    /// </summary>
    public static void NotifyPrimary()
    {
        // Lets the first process bring its window to the front even though this process is the one the user just started.
        AllowSetForegroundWindow(AsfwAny);

        for (var attempt = 0; attempt < SignalRetryCount; attempt++)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(SignalName);
                signal.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first process owns the mutex but has not created the signal yet; wait a moment.
                Thread.Sleep(SignalRetryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                // The first process runs at a different privilege level; there is nothing more we can do.
                return;
            }
        }
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _signal.Dispose();
        _mutex.Dispose();
    }

    private const int AsfwAny = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
