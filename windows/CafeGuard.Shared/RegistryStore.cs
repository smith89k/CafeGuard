using Microsoft.Win32;

namespace CafeGuard.Shared
{
    /// <summary>The registry, behind a small seam so installer logic can be tested with a fake.</summary>
    public interface IRegistryStore
    {
        object? GetValue(RegistryHive hive, string keyPath, string name);
        void SetValue(RegistryHive hive, string keyPath, string name, object value, RegistryValueKind kind);
        void DeleteValue(RegistryHive hive, string keyPath, string name);
        void DeleteKey(RegistryHive hive, string keyPath);
        bool KeyExists(RegistryHive hive, string keyPath);
    }

    /// <summary>The real Windows registry (64-bit view).</summary>
    public sealed class WindowsRegistryStore : IRegistryStore
    {
        private static RegistryKey Root(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

        public object? GetValue(RegistryHive hive, string keyPath, string name)
        {
            using var root = Root(hive);
            using var key = root.OpenSubKey(keyPath);
            return key?.GetValue(name);
        }

        public void SetValue(RegistryHive hive, string keyPath, string name, object value, RegistryValueKind kind)
        {
            using var root = Root(hive);
            using var key = root.CreateSubKey(keyPath, writable: true);
            key.SetValue(name, value, kind);
        }

        public void DeleteValue(RegistryHive hive, string keyPath, string name)
        {
            using var root = Root(hive);
            using var key = root.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }

        public void DeleteKey(RegistryHive hive, string keyPath)
        {
            using var root = Root(hive);
            root.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }

        public bool KeyExists(RegistryHive hive, string keyPath)
        {
            using var root = Root(hive);
            using var key = root.OpenSubKey(keyPath);
            return key != null;
        }
    }
}
