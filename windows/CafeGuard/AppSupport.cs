using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace CafeGuard
{
    /// <summary>
    /// A small diary of what CafeGuard did, for troubleshooting:
    /// %LOCALAPPDATA%\CafeGuard\cafeguard.log
    /// </summary>
    public static class AppLog
    {
        public static readonly string Folder = CafeGuard.Shared.AppPaths.DataDir;
        private static readonly string LogFile = CafeGuard.Shared.AppPaths.LogFile;
        private static readonly object Gate = new();

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 256 * 1024)
                        File.Delete(LogFile); // keep it small
                    File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [pid {Environment.ProcessId}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Makes sure only one CafeGuard runs, and lets a second launch talk to the first one:
    /// "Are you there? Show yourself." If the first one doesn't answer, it is stuck.
    /// </summary>
    public static class SingleInstance
    {
        private const string MutexName = "CafeGuard_AntiTheft_SingleInstance_Mutex";
        private static readonly EventWaitHandle ShowMeSignal = new(false, EventResetMode.AutoReset, "CafeGuard_ShowMe");
        private static readonly EventWaitHandle AliveSignal = new(false, EventResetMode.AutoReset, "CafeGuard_IAmHere");
        private static Mutex? _mutex;

        /// <summary>Tries to become the one running copy.</summary>
        public static bool TryBecomeTheOnlyCopy(int waitMs = 0)
        {
            _mutex ??= new Mutex(false, MutexName);
            try
            {
                return _mutex.WaitOne(waitMs);
            }
            catch (AbandonedMutexException)
            {
                return true; // the previous copy died without letting go; it's ours now
            }
        }

        public static void Release()
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }

        /// <summary>Second launch: asks the running copy to show itself. True if it answered within 5 seconds.</summary>
        public static bool AskRunningCopyToShowItself()
        {
            AliveSignal.Reset();
            ShowMeSignal.Set();
            return AliveSignal.WaitOne(5000);
        }

        /// <summary>
        /// First copy: when another launch asks, run <paramref name="showMe"/> on the UI thread, then answer.
        /// Answering from the UI thread proves this copy is not stuck.
        /// </summary>
        public static void ListenForShowMe(SynchronizationContext uiThread, Action showMe)
        {
            ThreadPool.RegisterWaitForSingleObject(ShowMeSignal, (_, _) =>
            {
                uiThread.Post(_ =>
                {
                    AliveSignal.Set(); // the UI thread is running = not stuck
                    try { showMe(); } catch { }
                }, null);
            }, null, Timeout.Infinite, executeOnlyOnce: false);
        }

        /// <summary>Closes other (stuck) CafeGuard copies.</summary>
        public static void CloseOtherCopies()
        {
            int me = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("CafeGuard"))
            {
                try
                {
                    if (p.Id == me) continue;
                    AppLog.Write($"Closing stuck copy (pid {p.Id}).");
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    AppLog.Write($"Could not close pid {p.Id}: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Windows 11 hides new tray icons by default. The first time CafeGuard runs (from a given folder),
    /// this tells Windows to always show the shield. If you hide it yourself later, CafeGuard leaves it hidden.
    /// </summary>
    public static class TrayIconPinner
    {
        private const string SettingsKey = @"Control Panel\NotifyIconSettings";
        private static readonly string MarkerFile = Path.Combine(AppLog.Folder, "tray-icon-pinned.txt");

        /// <summary>Returns true if the icon was just pinned (so the caller can re-show the icon).</summary>
        public static bool PinOnce()
        {
            try
            {
                string exe = Environment.ProcessPath ?? string.Empty;
                if (exe.Length == 0) return false;
                if (File.Exists(MarkerFile) && string.Equals(File.ReadAllText(MarkerFile).Trim(), exe, StringComparison.OrdinalIgnoreCase))
                    return false; // already done for this location; respect the user's choice since then

                using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
                if (root == null) return false; // not Windows 11 style tray

                foreach (string name in root.GetSubKeyNames())
                {
                    using var entry = Registry.CurrentUser.OpenSubKey($@"{SettingsKey}\{name}", writable: true);
                    if (entry?.GetValue("ExecutablePath") is not string path) continue;
                    if (!string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;

                    entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                    Directory.CreateDirectory(AppLog.Folder);
                    File.WriteAllText(MarkerFile, exe);
                    AppLog.Write("Asked Windows to always show the tray icon.");
                    return true;
                }
                AppLog.Write("Tray icon not listed by Windows yet (will try again).");
            }
            catch (Exception ex)
            {
                AppLog.Write($"Could not pin tray icon: {ex.Message}");
            }
            return false;
        }
    }
}
