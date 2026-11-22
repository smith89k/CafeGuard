using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CafeGuard
{
    /// <summary>
    /// An invisible window that hears Windows announcements:
    /// lid closed, going to sleep, woke up, and shutdown / sign-out requests.
    /// </summary>
    internal sealed class SystemWatcher : NativeWindow, IDisposable
    {
        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;

        private static readonly Guid GUID_LIDSWITCH_STATE_CHANGE = new("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, string reason);

        [DllImport("user32.dll")]
        private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

        private IntPtr _lidNotify;

        /// <summary>Last known lid position (true = open). Windows reports it right after we register.</summary>
        public bool IsLidOpen { get; private set; } = true;

        /// <summary>The lid was just closed.</summary>
        public event Action? LidClosed;
        /// <summary>Windows is about to sleep or hibernate (a couple of seconds' warning).</summary>
        public event Action? GoingToSleep;
        /// <summary>Windows just woke up.</summary>
        public event Action? WokeUp;
        /// <summary>Someone is shutting down / restarting / signing out. Return true to try to block it.</summary>
        public event Func<bool>? ShutdownRequested;

        public SystemWatcher()
        {
            // A normal (never shown) top-level window: needed to receive power broadcasts and shutdown requests.
            CreateHandle(new CreateParams { Caption = "CafeGuardWatcher" });

            Guid lid = GUID_LIDSWITCH_STATE_CHANGE;
            _lidNotify = RegisterPowerSettingNotification(Handle, ref lid, 0);
        }

        /// <summary>Removes the "blocking shutdown" note once the guard is off.</summary>
        public void AllowShutdown()
        {
            if (Handle != IntPtr.Zero) ShutdownBlockReasonDestroy(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_POWERBROADCAST:
                    int evt = m.WParam.ToInt32();
                    if (evt == PBT_APMSUSPEND)
                    {
                        GoingToSleep?.Invoke();
                    }
                    else if (evt == PBT_APMRESUMEAUTOMATIC || evt == PBT_APMRESUMESUSPEND)
                    {
                        WokeUp?.Invoke();
                    }
                    else if (evt == PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
                    {
                        // POWERBROADCAST_SETTING: Guid (16 bytes), DataLength (4 bytes), Data
                        Guid setting = Marshal.PtrToStructure<Guid>(m.LParam);
                        if (setting == GUID_LIDSWITCH_STATE_CHANGE)
                        {
                            int lidOpen = Marshal.ReadInt32(m.LParam, 20); // 0 = closed, 1 = open
                            IsLidOpen = lidOpen != 0;
                            if (lidOpen == 0) LidClosed?.Invoke();
                        }
                    }
                    m.Result = (IntPtr)1;
                    return;

                case WM_QUERYENDSESSION:
                    if (ShutdownRequested?.Invoke() == true)
                    {
                        ShutdownBlockReasonCreate(Handle, "Cafe Guard is ON. Turn it OFF from the tray icon first.");
                        m.Result = IntPtr.Zero; // ask Windows not to shut down
                        return;
                    }
                    break;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_lidNotify != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(_lidNotify);
                _lidNotify = IntPtr.Zero;
            }
            AllowShutdown();
            DestroyHandle();
        }
    }
}
