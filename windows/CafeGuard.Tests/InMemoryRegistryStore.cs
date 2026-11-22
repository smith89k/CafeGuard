using CafeGuard.Shared;
using Microsoft.Win32;

namespace CafeGuard.Tests;

/// <summary>Fake registry for tests: hive + key path (case-insensitive) → values.</summary>
public sealed class InMemoryRegistryStore : IRegistryStore
{
    private readonly Dictionary<string, Dictionary<string, object>> _keys = new(StringComparer.OrdinalIgnoreCase);

    private static string K(RegistryHive hive, string keyPath) => hive + "|" + keyPath.Trim('\\');

    public object? GetValue(RegistryHive hive, string keyPath, string name) =>
        _keys.TryGetValue(K(hive, keyPath), out var v) && v.TryGetValue(name, out var o) ? o : null;

    public void SetValue(RegistryHive hive, string keyPath, string name, object value, RegistryValueKind kind)
    {
        if (!_keys.TryGetValue(K(hive, keyPath), out var v)) _keys[K(hive, keyPath)] = v = new(StringComparer.OrdinalIgnoreCase);
        v[name] = value;
    }

    public void DeleteValue(RegistryHive hive, string keyPath, string name)
    {
        if (_keys.TryGetValue(K(hive, keyPath), out var v)) v.Remove(name);
    }

    public void DeleteKey(RegistryHive hive, string keyPath) => _keys.Remove(K(hive, keyPath));

    public bool KeyExists(RegistryHive hive, string keyPath) => _keys.ContainsKey(K(hive, keyPath));
}
