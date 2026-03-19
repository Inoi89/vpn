using System.Threading;
using System.Runtime.Versioning;

namespace VpnClient.UI;

[SupportedOSPlatform("windows")]
internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\YourVpnClient.Desktop.SingleInstance";
    private const string ActivateEventName = @"Local\YourVpnClient.Desktop.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly RegisteredWaitHandle _activationRegistration;
    private readonly bool _ownsMutex;
    private bool _disposed;

    private SingleInstanceCoordinator(Mutex mutex, EventWaitHandle activateEvent, bool ownsMutex)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        _ownsMutex = ownsMutex;
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            static (state, _) => ((SingleInstanceCoordinator)state!).RaiseActivationRequested(),
            this,
            Timeout.Infinite,
            false);
    }

    public event Action? ActivationRequested;

    public static bool TryAcquirePrimary(out SingleInstanceCoordinator? coordinator)
    {
        coordinator = null;

        var mutex = new Mutex(false, MutexName);
        var ownsMutex = false;

        try
        {
            try
            {
                ownsMutex = mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                SignalExistingInstance();
                mutex.Dispose();
                return false;
            }

            var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            coordinator = new SingleInstanceCoordinator(mutex, activateEvent, ownsMutex);
            return true;
        }
        catch
        {
            if (ownsMutex)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                    // ignored
                }
            }

            mutex.Dispose();
            throw;
        }
    }

    public static void SignalExistingInstance()
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch
        {
            // If the event is missing, the other instance is likely exiting already.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activationRegistration.Unregister(null);
        _activateEvent.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // ignored
            }
        }

        _mutex.Dispose();
    }

    private void RaiseActivationRequested()
    {
        ActivationRequested?.Invoke();
    }
}
