using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class SettingsTests
{
    private static string TempFile(string name = "settings.json")
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "testdata", Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, name);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        Assert.Equal(new AppSettings(), SettingsStore.Load(TempFile()));
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        string path = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"theme\":");
        Assert.Equal(new AppSettings(), SettingsStore.Load(path));
    }

    [Fact]
    public void Load_UnknownTheme_FallsBackToAuto()
    {
        string path = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"theme\":\"Purple\",\"armOnLock\":true}");
        var s = SettingsStore.Load(path);
        Assert.Equal(ThemeChoice.Auto, s.Theme);
        Assert.True(s.ArmOnLock);
    }

    [Fact]
    public void AlarmSound_RoundTrips_AndBadValuesUseDefaults()
    {
        string path = TempFile();
        var settings = new AppSettings(ThemeChoice.Auto, false, AlarmSound.Voice, AlarmPhrase.Protected, VoiceChoice.Woman);
        SettingsStore.Save(path, settings);
        Assert.Equal(settings, SettingsStore.Load(path));

        File.WriteAllText(path, "{\"alarmSound\":\"Loud\",\"alarmPhrase\":\"Thief\",\"voice\":\"Robot\"}");
        Assert.Equal(new AppSettings(), SettingsStore.Load(path));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        string path = TempFile(); // folder does not exist yet
        var settings = new AppSettings(ThemeChoice.Dark, true);
        SettingsStore.Save(path, settings);
        Assert.Equal(settings, SettingsStore.Load(path));
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(path));
        Assert.Contains("\"armOnLock\": true", File.ReadAllText(path));
    }
}
