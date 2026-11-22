using System;
using System.Runtime.InteropServices;

namespace CafeGuard
{
    /// <summary>
    /// Is Task Manager open? Checked every monitor tick while ON: opening it sounds the alarm, because on an
    /// administrator account Task Manager runs with full rights and could otherwise close Cafe Guard quietly.
    /// Looks for Task Manager's window (cheap, no process list).
    /// </summary>
    public static class TaskManagerDetector
    {
        private const string WindowClass = "TaskManagerWindow";

        public static bool IsOpen() => IsOpen(cls => FindWindow(cls, null) != IntPtr.Zero);

        public static bool IsOpen(Func<string, bool> windowExists) => windowExists(WindowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string? windowName);
    }
}
