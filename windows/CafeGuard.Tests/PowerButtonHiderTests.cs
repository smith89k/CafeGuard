using CafeGuard.Shared;
using Microsoft.Win32;

namespace CafeGuard.Tests;

public class PowerButtonHiderTests
{
    private static string StateFile() => Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString("N"), "installer-state.json");

    private static InMemoryRegistryStore Registry(int? value)
    {
        var r = new InMemoryRegistryStore();
        if (value != null) r.SetValue(RegistryHive.LocalMachine, LockScreenPowerButton.KeyPath, LockScreenPowerButton.ValueName, value.Value, RegistryValueKind.DWord);
        return r;
    }

    [Fact]
    public void Hide_RecordsPreviousValue_SoUninstallCanRestore()
    {
        var r = Registry(1);
        string file = StateFile();
        var result = PowerButtonHider.Hide(r, file, () => { LockScreenPowerButton.SetHidden(r, true); return true; });
        Assert.Equal(HideResult.Hidden, result);
        Assert.Equal(new InstallerState(true, 1, AppPaths.Version), InstallerState.Load(file));
    }

    [Fact]
    public void Hide_SavesRecordBeforeElevating()
    {
        var r = Registry(1);
        string file = StateFile();
        InstallerState? seenDuringElevation = null;
        PowerButtonHider.Hide(r, file, () => { seenDuringElevation = InstallerState.Load(file); return true; });
        Assert.True(seenDuringElevation?.PowerButtonChangedByUs);
    }

    [Fact]
    public void Hide_Declined_LeavesNoRecord()
    {
        string file = StateFile();
        var result = PowerButtonHider.Hide(Registry(1), file, () => false);
        Assert.Equal(HideResult.Declined, result);
        Assert.False(InstallerState.Load(file)?.PowerButtonChangedByUs ?? false);
    }

    [Fact]
    public void Hide_AlreadyHidden_DoesNothing()
    {
        string file = StateFile();
        bool elevated = false;
        var result = PowerButtonHider.Hide(Registry(0), file, () => elevated = true);
        Assert.Equal(HideResult.AlreadyHidden, result);
        Assert.False(elevated);
        Assert.Null(InstallerState.Load(file));
    }

    // ---------- Show (put the button back from the card; the Store version can't do it on uninstall) ----------

    [Fact]
    public void Show_RestoresRecordedValue_AndClearsRecord()
    {
        var r = Registry(0);
        string file = StateFile();
        new InstallerState(true, 1, AppPaths.Version).Save(file);
        LockScreenPowerCommand? asked = null;
        var result = PowerButtonHider.Show(r, file, cmd => { asked = cmd; cmd.Run(r); return true; });
        Assert.Equal(ShowResult.Shown, result);
        Assert.Equal(new LockScreenPowerCommand(PowerAction.Restore, 1), asked);
        Assert.Equal(1, LockScreenPowerButton.ReadRaw(r));
        Assert.False(InstallerState.Load(file)!.PowerButtonChangedByUs);
    }

    [Fact]
    public void Show_ValueDidNotExistBefore_DeletesIt()
    {
        var r = Registry(0);
        string file = StateFile();
        new InstallerState(true, null, AppPaths.Version).Save(file);
        var result = PowerButtonHider.Show(r, file, cmd => { cmd.Run(r); return true; });
        Assert.Equal(ShowResult.Shown, result);
        Assert.Null(LockScreenPowerButton.ReadRaw(r));
    }

    [Fact]
    public void Show_HiddenBySomeoneElse_DoesNothing()
    {
        string file = StateFile();
        bool elevated = false;
        var result = PowerButtonHider.Show(Registry(0), file, _ => elevated = true);
        Assert.Equal(ShowResult.NotOurs, result);
        Assert.False(elevated);
    }

    [Fact]
    public void Show_Declined_KeepsRecord()
    {
        var r = Registry(0);
        string file = StateFile();
        new InstallerState(true, 1, AppPaths.Version).Save(file);
        var result = PowerButtonHider.Show(r, file, _ => false);
        Assert.Equal(ShowResult.Declined, result);
        Assert.True(InstallerState.Load(file)!.PowerButtonChangedByUs);
        Assert.True(LockScreenPowerButton.IsHidden(r));
    }

    [Fact]
    public void IsHiddenByUs_NeedsBothHiddenAndRecord()
    {
        string file = StateFile();
        Assert.False(PowerButtonHider.IsHiddenByUs(Registry(0), file));
        new InstallerState(true, 1, AppPaths.Version).Save(file);
        Assert.True(PowerButtonHider.IsHiddenByUs(Registry(0), file));
        Assert.False(PowerButtonHider.IsHiddenByUs(Registry(1), file)); // someone showed it again outside CafeGuard
    }

    [Fact]
    public void InstallerState_SaveIsAtomic_CorruptFileLoadsNull()
    {
        string file = StateFile();
        new InstallerState(true, 1, "1.0.0").Save(file);
        Assert.False(File.Exists(file + ".tmp"));
        File.WriteAllText(file, "{\"powerButtonChangedByUs\":");
        Assert.Null(InstallerState.Load(file));
    }
}
