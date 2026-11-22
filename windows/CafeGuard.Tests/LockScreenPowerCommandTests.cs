using CafeGuard.Shared;
using Microsoft.Win32;

namespace CafeGuard.Tests;

/// <summary>The admin helper command line ("--set-lockscreen-power ...") that CafeGuard.exe runs itself with.</summary>
public class LockScreenPowerCommandTests
{
    [Fact] public void Hide() => Assert.Equal(new LockScreenPowerCommand(PowerAction.Hide, null), LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", "hide" }));
    [Fact] public void Show() => Assert.Equal(new LockScreenPowerCommand(PowerAction.Show, null), LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", "show" }));
    [Fact] public void RestoreValue() => Assert.Equal(new LockScreenPowerCommand(PowerAction.Restore, 1), LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", "restore:1" }));
    [Fact] public void RestoreNone() => Assert.Equal(new LockScreenPowerCommand(PowerAction.Restore, null), LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", "restore:none" }));
    [Fact] public void NotACommand_IsNull() => Assert.Null(LockScreenPowerCommand.TryParse(new[] { "--self-test" }));
    [Fact] public void BadValue_IsNull() => Assert.Null(LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", "restore:abc" }));
    [Fact] public void NoArgs_IsNull() => Assert.Null(LockScreenPowerCommand.TryParse(Array.Empty<string>()));

    [Theory]
    [InlineData("hide")]
    [InlineData("show")]
    [InlineData("restore:0")]
    [InlineData("restore:1")]
    [InlineData("restore:none")]
    public void ToArguments_RoundTrips(string action)
    {
        var cmd = LockScreenPowerCommand.TryParse(new[] { "--set-lockscreen-power", action })!;
        Assert.Equal("--set-lockscreen-power " + action, cmd.ToArguments());
        Assert.Equal(cmd, LockScreenPowerCommand.TryParse(cmd.ToArguments().Split(' ')));
    }

    [Fact]
    public void Run_Hide_WritesZero()
    {
        var r = new InMemoryRegistryStore();
        new LockScreenPowerCommand(PowerAction.Hide, null).Run(r);
        Assert.Equal(0, LockScreenPowerButton.ReadRaw(r));
    }

    [Fact]
    public void Run_RestoreNone_DeletesValue()
    {
        var r = new InMemoryRegistryStore();
        r.SetValue(RegistryHive.LocalMachine, LockScreenPowerButton.KeyPath, LockScreenPowerButton.ValueName, 0, RegistryValueKind.DWord);
        new LockScreenPowerCommand(PowerAction.Restore, null).Run(r);
        Assert.Null(LockScreenPowerButton.ReadRaw(r));
    }
}
