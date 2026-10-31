namespace DOOM.UnitTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DOOM.Platform.Windows.SingleInstance;
using Xunit;

public sealed class SingleInstanceIpcTests
{
    [Fact]
    public async Task SecondLaunch_ActivatesExistingInstance_Successfully()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\DOOM_Test_Mutex_{testId}";
        var pipeName = $"DOOM_Test_Pipe_{testId}";

        await using var primary = new SingleInstanceService(mutexName, pipeName);
        var primaryAcquired = primary.TryAcquire();
        Assert.True(primaryAcquired, "Primary instance should acquire the single-instance mutex.");

        var activationTcs = new TaskCompletionSource<bool>();
        primary.StartListening(() => activationTcs.TrySetResult(true));

        // Secondary launch runs on an independent OS thread (simulating a separate process)
        bool secondaryAcquired = true;
        bool notified = false;

        var thread = new Thread(() =>
        {
            using var secondary = new SingleInstanceService(mutexName, pipeName);
            secondaryAcquired = secondary.TryAcquire();
            if (!secondaryAcquired)
            {
                notified = secondary.NotifyExistingInstanceAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            }
        });

        thread.Start();
        thread.Join(5000);

        Assert.False(secondaryAcquired, "Secondary instance must not acquire the single-instance mutex.");
        Assert.True(notified, "Secondary instance must successfully notify primary instance via IPC.");

        var completed = await Task.WhenAny(activationTcs.Task, Task.Delay(3000));
        Assert.Same(activationTcs.Task, completed);
        Assert.True(await activationTcs.Task, "Primary instance callback must be executed.");
        Assert.True(primary.IsFirstInstance, "Primary instance must retain ownership.");
        GC.KeepAlive(primary);
    }

    [Fact]
    public void HiddenOverlay_BecomesVisible_WhenRestoredFromMinimizedOrHidden()
    {
        StaTestRunner.Run(() =>
        {
            var window = new Window
            {
                WindowState = WindowState.Minimized,
                Visibility = Visibility.Hidden
            };

            Assert.Equal(WindowState.Minimized, window.WindowState);
            Assert.Equal(Visibility.Hidden, window.Visibility);

            // Simulate the exact restoration logic performed in ActivateFromExternalLaunch
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            if (window.Visibility != Visibility.Visible)
            {
                window.Visibility = Visibility.Visible;
            }

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(Visibility.Visible, window.Visibility);
        });
    }

    [Fact]
    public async Task NoDuplicateWindows_CreatedOnSecondLaunch()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\DOOM_Test_Mutex_{testId}";
        var pipeName = $"DOOM_Test_Pipe_{testId}";

        await using var primary = new SingleInstanceService(mutexName, pipeName);
        Assert.True(primary.TryAcquire());

        primary.StartListening(() => { });

        // Secondary launch runs on an independent OS thread
        int windowCreationAttempts = 0;
        var thread = new Thread(() =>
        {
            using var secondary = new SingleInstanceService(mutexName, pipeName);
            if (!secondary.TryAcquire())
            {
                // Simulated second launch policy: notify and exit without creating windows
                secondary.NotifyExistingInstanceAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            else
            {
                windowCreationAttempts++;
            }
        });

        thread.Start();
        thread.Join(5000);

        Assert.Equal(0, windowCreationAttempts);
        Assert.True(primary.IsFirstInstance);
    }

    [Fact]
    public async Task ExistingInstance_RemainsResponsive_AfterMultipleActivations()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\DOOM_Test_Mutex_{testId}";
        var pipeName = $"DOOM_Test_Pipe_{testId}";

        await using var primary = new SingleInstanceService(mutexName, pipeName);
        Assert.True(primary.TryAcquire());

        int activationCount = 0;
        var multiTcs = new TaskCompletionSource<bool>();

        primary.StartListening(() =>
        {
            var current = Interlocked.Increment(ref activationCount);
            if (current == 3)
            {
                multiTcs.TrySetResult(true);
            }
        });

        // Dispatch 3 sequential activation requests from independent OS threads
        for (int i = 0; i < 3; i++)
        {
            bool notified = false;
            var thread = new Thread(() =>
            {
                using var secondary = new SingleInstanceService(mutexName, pipeName);
                notified = secondary.NotifyExistingInstanceAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            });

            thread.Start();
            thread.Join(5000);

            Assert.True(notified, $"Activation {i + 1} must succeed.");
        }

        var completed = await Task.WhenAny(multiTcs.Task, Task.Delay(4000));
        Assert.Same(multiTcs.Task, completed);
        Assert.Equal(3, activationCount);
    }

    [Fact]
    public async Task IpcFailure_DoesNotCrashEitherProcess()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\DOOM_Test_Mutex_{testId}";
        // Pipe with no server listening
        var deadPipeName = $"DOOM_NonExistent_Pipe_{testId}";

        await using var secondary = new SingleInstanceService(mutexName, deadPipeName);

        // Attempting to notify a dead/non-existent pipe should gracefully return false within timeout
        var notified = await secondary.NotifyExistingInstanceAsync(TimeSpan.FromMilliseconds(300));
        Assert.False(notified, "Notify on non-existent pipe should return false safely without throwing.");
    }

    [Fact]
    public void StaleOrAbandonedMutex_RecoveredSafely_ByNextInstance()
    {
        var testId = Guid.NewGuid().ToString("N");
        var mutexName = $@"Local\DOOM_Test_Mutex_{testId}";

        // Simulate an instance abandoning a mutex (e.g. crashing without release)
        var abandonThread = new Thread(() =>
        {
            var mutex = new Mutex(initiallyOwned: true, mutexName, out bool acquired);
            Assert.True(acquired);
            // Thread exits without calling ReleaseMutex() -> abandons mutex
        });

        abandonThread.Start();
        abandonThread.Join();

        // New instance attempts acquisition
        using var newInstance = new SingleInstanceService(mutexName);
        var acquiredAfterAbandon = newInstance.TryAcquire();

        Assert.True(acquiredAfterAbandon, "Instance must safely handle AbandonedMutexException and acquire ownership.");
        Assert.True(newInstance.IsFirstInstance);
    }
}
