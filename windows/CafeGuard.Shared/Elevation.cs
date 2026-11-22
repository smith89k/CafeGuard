using System.ComponentModel;
using System.Diagnostics;

namespace CafeGuard.Shared
{
    /// <summary>Runs a program with administrator rights (Windows shows its permission prompt).</summary>
    public static class Elevation
    {
        /// <returns>True only if the user said Yes and the program finished with exit code 0.</returns>
        public static bool RunElevated(string exePath, string arguments, int timeoutMs = 60000)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(exePath, arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                if (p == null || !p.WaitForExit(timeoutMs)) return false;
                return p.ExitCode == 0;
            }
            catch (Win32Exception)
            {
                return false; // 1223 = the user clicked No; anything else = could not start
            }
        }
    }
}
