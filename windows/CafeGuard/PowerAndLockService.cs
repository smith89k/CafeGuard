using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CafeGuard
{
    public struct PowerStatusInfo
    {
        public bool IsPluggedIn;
        public int BatteryPercent;
        public string StatusText;
    }

    public static class PowerAndLockService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        /// <summary>Reads whether the charger is plugged in, and the battery level. Works on the lock screen.</summary>
        public static PowerStatusInfo GetPowerStatus()
        {
            if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps))
            {
                // 1 = plugged in, 0 = on battery, 255 = unknown (treated as plugged in, to avoid false alarms).
                bool plugged = sps.ACLineStatus != 0;
                int pct = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                return new PowerStatusInfo
                {
                    IsPluggedIn = plugged,
                    BatteryPercent = pct,
                    StatusText = plugged ? $"Charger plugged in ({pct}%)" : $"On battery - unplugged ({pct}%)"
                };
            }

            return new PowerStatusInfo { IsPluggedIn = true, BatteryPercent = 100, StatusText = "Unknown power state" };
        }

        /// <summary>Stops Windows from going to sleep by itself (idle timer) while the guard is on.</summary>
        public static void SetKeepAwakeOnLockScreen(bool enable)
        {
            try
            {
                SetThreadExecutionState(enable
                    ? EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_AWAYMODE_REQUIRED
                    : EXECUTION_STATE.ES_CONTINUOUS);
            }
            catch { }
        }
    }

    /// <summary>
    /// While the guard is on, sets "close the lid", "press the power button" and "press the sleep button"
    /// to Do nothing, so a thief cannot silence the alarm by putting the laptop to sleep.
    ///
    /// The user's own settings are saved to a small file first and put back when the guard is turned off.
    /// If the app ever crashes while on, the next start puts them back too.
    /// </summary>
    public static class LidAndButtonGuard
    {
        private static readonly Guid SubButtons = new("4f971e89-eebd-4455-a8de-9e59040e7347");

        private static readonly (string Name, Guid Id)[] Settings =
        {
            ("Lid", new Guid("5ca83367-6e45-459f-a27b-476b1d01c936")),          // close the lid
            ("PowerButton", new Guid("7648efa3-dd9c-4e3e-b566-50f929386280")),  // press the power button
            ("SleepButton", new Guid("96996bc0-ad50-47ec-923b-6f41874dd9eb")),  // press the sleep button
        };

        private const uint DoNothing = 0;

        private static readonly string BackupFile = Path.Combine(CafeGuard.Shared.AppPaths.DataDir, "power-settings-backup.txt");

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, out uint acValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, out uint dcValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, uint acValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, uint dcValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private static bool TryGetActiveScheme(out Guid scheme)
        {
            scheme = Guid.Empty;
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) != 0 || ptr == IntPtr.Zero) return false;
            try { scheme = Marshal.PtrToStructure<Guid>(ptr); return true; }
            finally { LocalFree(ptr); }
        }

        /// <summary>Current values, for the self-test. 0 = Do nothing, 1 = Sleep, 2 = Hibernate, 3 = Shut down.</summary>
        public static string Describe()
        {
            if (!TryGetActiveScheme(out Guid scheme)) return "(could not read power plan)";
            Guid sub = SubButtons;
            var parts = new List<string>();
            foreach (var (name, id) in Settings)
            {
                Guid setting = id;
                bool okAc = PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac) == 0;
                bool okDc = PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc) == 0;
                parts.Add(okAc && okDc ? $"{name}: plugged={ac} battery={dc}" : $"{name}: (not on this PC)");
            }
            return string.Join(" | ", parts);
        }

        /// <summary>Why the last Apply() failed (for the self-test).</summary>
        public static string LastError { get; private set; } = string.Empty;

        /// <summary>True when the lid / buttons are currently set to Do nothing by CafeGuard.</summary>
        public static bool IsActive => File.Exists(BackupFile);

        /// <summary>Saves the user's settings and switches lid / power button / sleep button to Do nothing.</summary>
        public static bool Apply()
        {
            try
            {
                if (IsActive) return true; // already applied (settings were saved before)
                if (!TryGetActiveScheme(out Guid scheme)) return false;

                var lines = new List<string> { scheme.ToString() };
                Guid sub = SubButtons;
                foreach (var (name, id) in Settings)
                {
                    Guid setting = id;
                    if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac) != 0) continue;
                    if (PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc) != 0) continue;
                    lines.Add($"{name}|{id}|{ac}|{dc}");
                }

                // Save the user's settings BEFORE changing anything.
                Directory.CreateDirectory(Path.GetDirectoryName(BackupFile)!);
                File.WriteAllLines(BackupFile, lines);

                bool anyChanged = false;
                foreach (var line in lines.GetRange(1, lines.Count - 1))
                {
                    Guid setting = Guid.Parse(line.Split('|')[1]);
                    bool okAc = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, DoNothing) == 0;
                    bool okDc = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, DoNothing) == 0;
                    anyChanged |= okAc && okDc;
                }
                PowerSetActiveScheme(IntPtr.Zero, ref scheme); // makes Windows use the new values right away
                if (!anyChanged) LastError = "Windows refused to change the settings.";
                return anyChanged;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        /// <summary>Puts the user's own lid / power button / sleep button settings back.</summary>
        public static void Restore()
        {
            try
            {
                if (!IsActive) return;
                string[] lines = File.ReadAllLines(BackupFile);
                if (lines.Length == 0 || !Guid.TryParse(lines[0], out Guid scheme))
                {
                    File.Delete(BackupFile);
                    return;
                }

                Guid sub = SubButtons;
                bool allOk = true;
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split('|');
                    if (parts.Length != 4) continue;
                    Guid setting = Guid.Parse(parts[1]);
                    allOk &= PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, uint.Parse(parts[2])) == 0;
                    allOk &= PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, uint.Parse(parts[3])) == 0;
                }

                if (TryGetActiveScheme(out Guid active) && active == scheme)
                    PowerSetActiveScheme(IntPtr.Zero, ref scheme);

                if (allOk) File.Delete(BackupFile); // keep the backup if something failed, so we can try again next start
            }
            catch { }
        }
    }
}
