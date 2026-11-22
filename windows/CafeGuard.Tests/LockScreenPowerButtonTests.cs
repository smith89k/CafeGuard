using CafeGuard.Shared;
using Microsoft.Win32;

namespace CafeGuard.Tests;

public class LockScreenPowerButtonTests
{
    private const RegistryHive HKLM = RegistryHive.LocalMachine;
    private static readonly string Key = LockScreenPowerButton.KeyPath;
    private static readonly string Name = LockScreenPowerButton.ValueName;

    [Fact]
    public void Constants_MatchSpec()
    {
        Assert.Equal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", Key);
        Assert.Equal("shutdownwithoutlogon", Name);
    }

    [Fact]
    public void IsHidden_TrueOnlyWhenZero()
    {
        var r = new InMemoryRegistryStore();
        Assert.False(LockScreenPowerButton.IsHidden(r));
        r.SetValue(HKLM, Key, Name, 1, RegistryValueKind.DWord);
        Assert.False(LockScreenPowerButton.IsHidden(r));
        r.SetValue(HKLM, Key, Name, 0, RegistryValueKind.DWord);
        Assert.True(LockScreenPowerButton.IsHidden(r));
    }

    [Fact]
    public void SetHidden_True_WritesZero()
    {
        var r = new InMemoryRegistryStore();
        LockScreenPowerButton.SetHidden(r, true);
        Assert.Equal(0, r.GetValue(HKLM, Key, Name));
    }

    [Fact]
    public void SetHidden_False_WritesOne()
    {
        var r = new InMemoryRegistryStore();
        LockScreenPowerButton.SetHidden(r, false);
        Assert.Equal(1, r.GetValue(HKLM, Key, Name));
    }

    [Fact]
    public void ReadRaw_ReturnsValueOrNull()
    {
        var r = new InMemoryRegistryStore();
        Assert.Null(LockScreenPowerButton.ReadRaw(r));
        r.SetValue(HKLM, Key, Name, 1, RegistryValueKind.DWord);
        Assert.Equal(1, LockScreenPowerButton.ReadRaw(r));
    }

    [Fact]
    public void Restore_Null_DeletesValue()
    {
        var r = new InMemoryRegistryStore();
        r.SetValue(HKLM, Key, Name, 0, RegistryValueKind.DWord);
        LockScreenPowerButton.Restore(r, null);
        Assert.Null(r.GetValue(HKLM, Key, Name));
    }

    [Fact]
    public void Restore_Value_WritesIt()
    {
        var r = new InMemoryRegistryStore();
        r.SetValue(HKLM, Key, Name, 0, RegistryValueKind.DWord);
        LockScreenPowerButton.Restore(r, 1);
        Assert.Equal(1, r.GetValue(HKLM, Key, Name));
    }
}
