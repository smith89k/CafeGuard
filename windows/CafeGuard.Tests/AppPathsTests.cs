using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class AppPathsTests
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    [Fact]
    public void DataDir_FilesLiveInDataDir()
    {
        Assert.Equal(Path.Combine(LocalAppData, "CafeGuard"), AppPaths.DataDir);
        Assert.Equal(Path.Combine(AppPaths.DataDir, "settings.json"), AppPaths.SettingsFile);
        Assert.Equal(Path.Combine(AppPaths.DataDir, "installer-state.json"), AppPaths.InstallerStateFile);
        Assert.Equal(Path.Combine(AppPaths.DataDir, "cafeguard.log"), AppPaths.LogFile);
    }

    [Fact]
    public void Names_AndVersion()
    {
        Assert.Equal("CafeGuard.exe", AppPaths.AppExeName);
        Assert.Equal("1.0.0", AppPaths.Version);
    }
}
