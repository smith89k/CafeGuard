using System;
using System.Runtime.InteropServices;
using System.Windows;
using Rect = System.Windows.Rect;

namespace CafeGuard.Card
{
    /// <summary>Screen sizes in WPF units (screen pixels divided by that monitor's scaling).</summary>
    internal static class ScreenInfo
    {
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        /// <summary>Work area and full bounds of the screen under the mouse, in WPF units.</summary>
        public static (Rect WorkArea, Rect Bounds) AtCursor()
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursor);
            double scale = 1.0;
            try
            {
                IntPtr monitor = MonitorFromPoint(new POINT { X = cursor.X, Y = cursor.Y }, 2 /* nearest */);
                if (GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi > 0) scale = dpi / 96.0;
            }
            catch { }

            Rect ToDip(System.Drawing.Rectangle r) => new(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale);
            return (ToDip(screen.WorkingArea), ToDip(screen.Bounds));
        }
    }
}
