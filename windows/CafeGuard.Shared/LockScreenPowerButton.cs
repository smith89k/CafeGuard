using Microsoft.Win32;

namespace CafeGuard.Shared
{
    /// <summary>
    /// The power button on the Windows lock screen (Sleep / Shut down / Restart).
    /// Hidden when HKLM\...\Policies\System\shutdownwithoutlogon = 0. Writing needs administrator rights.
    /// </summary>
    public static class LockScreenPowerButton
    {
        public const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        public const string ValueName = "shutdownwithoutlogon";

        public static int? ReadRaw(IRegistryStore registry) =>
            registry.GetValue(RegistryHive.LocalMachine, KeyPath, ValueName) is int v ? v : null;

        public static bool IsHidden(IRegistryStore registry) => ReadRaw(registry) == 0;

        public static void SetHidden(IRegistryStore registry, bool hidden) =>
            registry.SetValue(RegistryHive.LocalMachine, KeyPath, ValueName, hidden ? 0 : 1, RegistryValueKind.DWord);

        /// <summary>Puts back a previously saved value; null means "the value did not exist".</summary>
        public static void Restore(IRegistryStore registry, int? previous)
        {
            if (previous == null) registry.DeleteValue(RegistryHive.LocalMachine, KeyPath, ValueName);
            else registry.SetValue(RegistryHive.LocalMachine, KeyPath, ValueName, previous.Value, RegistryValueKind.DWord);
        }
    }
}
