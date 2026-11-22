using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class AlarmSoundTests
{
    private const int Rate = AlarmSoundMaker.SampleRate;
    private static short[] FakeVoice(int samples) => Enumerable.Repeat((short)1000, samples).ToArray();

    [Fact]
    public void Settings_DefaultsAreBoth_DontTouch_Man()
    {
        var s = new AppSettings();
        Assert.Equal(AlarmSound.Both, s.Sound);
        Assert.Equal(AlarmPhrase.DontTouch, s.Phrase);
        Assert.Equal(VoiceChoice.Man, s.Voice);
    }

    [Fact]
    public void Phrases_ExactWords()
    {
        Assert.Equal("Don't touch my laptop!", AlarmPhrases.Text(AlarmPhrase.DontTouch));
        Assert.Equal("Please put the laptop down.", AlarmPhrases.Text(AlarmPhrase.PutDown));
        Assert.Equal("This laptop is protected. The owner is coming back.", AlarmPhrases.Text(AlarmPhrase.Protected));
        Assert.Equal("Please don't touch this laptop.", AlarmPhrases.Text(AlarmPhrase.PleaseDont));
    }

    [Fact]
    public void Compose_Siren_IsFourSecondsOfSiren()
    {
        var pcm = AlarmSoundMaker.ReadPcm(AlarmSoundMaker.Compose(AlarmSound.Siren, FakeVoice(Rate)));
        Assert.Equal(4 * Rate, pcm.Length);
    }

    [Fact]
    public void Compose_Both_IsVoiceThenGapThenThreeSecondsOfSiren()
    {
        var pcm = AlarmSoundMaker.ReadPcm(AlarmSoundMaker.Compose(AlarmSound.Both, FakeVoice(Rate)));
        int gap = (int)(AlarmSoundMaker.GapAfterVoiceBeforeSiren * Rate);
        Assert.Equal(Rate + gap + 3 * Rate, pcm.Length);
        Assert.NotEqual(0, pcm[10]);            // starts with the voice
        Assert.Equal(0, pcm[Rate + gap / 2]);   // short quiet gap
    }

    [Fact]
    public void Compose_Voice_IsVoiceThenPause()
    {
        var pcm = AlarmSoundMaker.ReadPcm(AlarmSoundMaker.Compose(AlarmSound.Voice, FakeVoice(Rate)));
        Assert.Equal(Rate + (int)(AlarmSoundMaker.PauseAfterVoice * Rate), pcm.Length);
    }

    [Theory]
    [InlineData(AlarmSound.Voice)]
    [InlineData(AlarmSound.Both)]
    public void Compose_NoVoiceAvailable_FallsBackToSiren(AlarmSound sound)
    {
        var pcm = AlarmSoundMaker.ReadPcm(AlarmSoundMaker.Compose(sound, null));
        Assert.Equal(4 * Rate, pcm.Length); // never silent
    }

    [Fact]
    public void Normalize_MakesQuietVoiceLoud()
    {
        var loud = AlarmSoundMaker.Normalize(new short[] { 100, -200, 50 });
        Assert.InRange(loud.Max(s => Math.Abs((int)s)), 31000, 32767);
        Assert.True(loud[1] < 0);
    }

    [Fact]
    public void Speak_OnThisLaptop_GivesSoundOrNothing()
    {
        // Machines without an English voice return null (then the siren is used); with one, real sound must come back.
        bool hasEnglishVoice;
        using (var tokens = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Speech\Voices\Tokens"))
            hasEnglishVoice = tokens?.GetSubKeyNames().Any(n => n.Contains("EN-US", StringComparison.OrdinalIgnoreCase)) == true;

        foreach (var voice in new[] { VoiceChoice.Man, VoiceChoice.Woman })
        {
            var pcm = AlarmSoundMaker.Speak("Don't touch my laptop!", voice);
            if (!hasEnglishVoice) continue;
            Assert.NotNull(pcm);
            // "Don't touch my laptop!" takes about 1-2 s at normal speed. Much longer = wrong format (plays slow and deep).
            Assert.InRange(pcm!.Length, Rate / 2, (int)(Rate * 2.2));
            Assert.InRange(pcm.Max(x => Math.Abs((int)x)), 30000, 32767);        // made loud
        }
    }
}
