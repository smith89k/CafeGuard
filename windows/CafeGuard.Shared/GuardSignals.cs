using System;
using System.Threading;

namespace CafeGuard.Shared
{
    /// <summary>
    /// Lets the installer ask the running app two things without touching its window:
    /// "are you guarding right now?" and "please exit".
    /// </summary>
    public static class GuardSignals
    {
        /// <summary>Test hook so tests never collide with a real running CafeGuard.</summary>
        internal static string NameSuffix { get; set; } = string.Empty;

        private static string GuardOnName => "CafeGuard_GuardOn" + NameSuffix;
        private static string ExitRequestName => "CafeGuard_ExitRequest" + NameSuffix;

        private static EventWaitHandle? _guardOn;   // kept open by the app for its whole life
        private static EventWaitHandle? _exitRequest;

        /// <summary>App: mark guard ON / OFF (ON also covers ALARM).</summary>
        public static void SetGuarding(bool on)
        {
            _guardOn ??= new EventWaitHandle(false, EventResetMode.ManualReset, GuardOnName);
            if (on) _guardOn.Set(); else _guardOn.Reset();
        }

        /// <summary>Installer: is a running CafeGuard guarding? False when CafeGuard is not running.</summary>
        public static bool IsGuarding()
        {
            try
            {
                if (_guardOn != null) return _guardOn.WaitOne(0);
                using var ev = EventWaitHandle.OpenExisting(GuardOnName);
                return ev.WaitOne(0);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

        /// <summary>Installer: ask the running app to exit (it only does so while OFF).</summary>
        public static void RequestExit()
        {
            try
            {
                using var ev = EventWaitHandle.OpenExisting(ExitRequestName);
                ev.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { }
        }

        /// <summary>App: when the installer asks, run <paramref name="onExit"/> on the UI thread.</summary>
        public static void ListenForExitRequest(SynchronizationContext ui, Action onExit)
        {
            _exitRequest ??= new EventWaitHandle(false, EventResetMode.AutoReset, ExitRequestName);
            ThreadPool.RegisterWaitForSingleObject(_exitRequest, (_, _) => ui.Post(_ => onExit(), null),
                null, Timeout.Infinite, executeOnlyOnce: false);
        }
    }
}
