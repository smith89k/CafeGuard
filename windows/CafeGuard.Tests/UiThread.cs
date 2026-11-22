using System.Windows.Threading;

namespace CafeGuard.Tests;

/// <summary>One long-lived WPF (STA) thread for tests that create windows.</summary>
public static class UiThread
{
    private static readonly Dispatcher D;

    static UiThread()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? d = null;
        var t = new Thread(() =>
        {
            d = Dispatcher.CurrentDispatcher;
            _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        ready.Wait();
        D = d!;
    }

    public static void Run(Action action) => D.Invoke(action);
}
