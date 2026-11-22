using CafeGuard.Card;
using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class CardViewBuilderTests
{
    private static readonly DateTime At1541 = new(2026, 11, 22, 15, 41, 0);
    private static readonly DateTime At1543 = new(2026, 11, 22, 15, 43, 0);

    private static CardInput Off() => new(GuardState.Off, ChargerPlugged: true, SpeakerName: "Speakers (Apple Audio Device)",
        LidProtectionAvailable: true, PowerButtonHidden: true, PowerButtonHiddenByUs: false, OnSince: null, AlarmAt: null, Reason: null,
        ArmOnLock: false, Theme: ThemeChoice.Auto);

    private static ChipView Chip(CardView v, int i) => v.Chips[i];

    [Fact]
    public void Off_Ready()
    {
        var v = CardViewBuilder.Build(Off());
        Assert.Equal("Cafe Guard is OFF", v.Title);
        Assert.Equal("Your laptop is not guarded", v.Subtitle);
        Assert.Equal(ShieldTone.GreenOutline, v.Shield); // OFF = green outline, ON = solid green, alarm = red
        Assert.Equal("🔒 Turn ON — going to bathroom", v.ButtonText);
        Assert.Equal(ButtonKind.Primary, v.ButtonKind);
        Assert.True(v.ButtonEnabled);
        Assert.True(v.ShowChips);
        Assert.Equal(4, v.Chips.Count);
        Assert.Equal(new ChipView("🔌 Charger", "Plugged in ✓", ChipTone.Ok, ChipLink.None), Chip(v, 0));
        Assert.Equal(new ChipView("🔊 Sound", "Speakers ✓", ChipTone.Ok, ChipLink.None), Chip(v, 1));
        Assert.Equal(new ChipView("💻 Lid protection", "Ready ✓", ChipTone.Ok, ChipLink.None), Chip(v, 2));
        Assert.Equal(new ChipView("⏻ Lock-screen power button", "Hidden ✓", ChipTone.Ok, ChipLink.None), Chip(v, 3));
        Assert.True(v.ShowArmOnLockRow);
        Assert.True(v.FooterEnabled);
        Assert.Null(v.AlarmBarText);
    }

    [Fact]
    public void Off_ChargerUnplugged()
    {
        var v = CardViewBuilder.Build(Off() with { ChargerPlugged = false });
        Assert.Equal("Plug in your charger to turn it ON", v.Subtitle);
        Assert.Equal(ShieldTone.GreenOutline, v.Shield);
        Assert.Equal("🔒 Turn ON — plug in charger first", v.ButtonText);
        Assert.Equal(ButtonKind.Disabled, v.ButtonKind);
        Assert.False(v.ButtonEnabled);
        Assert.Equal(new ChipView("🔌 Charger", "Not plugged in ⚠", ChipTone.Warn, ChipLink.None), Chip(v, 0));
    }

    [Fact]
    public void On()
    {
        var v = CardViewBuilder.Build(Off() with { State = GuardState.On, OnSince = At1541 });
        Assert.Equal("Cafe Guard is ON", v.Title);
        Assert.Equal("Guarding since 3:41 PM", v.Subtitle);
        Assert.Equal(ShieldTone.Green, v.Shield);
        Assert.Equal("🔓 Turn OFF — I'm back", v.ButtonText);
        Assert.Equal(ButtonKind.Neutral, v.ButtonKind);
        Assert.True(v.ButtonEnabled);
        Assert.Equal("Watching ✓", Chip(v, 0).Value);
        Assert.Equal("On ✓", Chip(v, 2).Value);
        Assert.False(v.FooterEnabled);
    }

    [Fact]
    public void Alarm()
    {
        var v = CardViewBuilder.Build(Off() with
        {
            State = GuardState.Alarm, OnSince = At1541, AlarmAt = At1543, Reason = AlarmReason.ChargerUnplugged, ChargerPlugged = false
        });
        Assert.Equal("ALARM!", v.Title);
        Assert.Equal("Started at 3:43 PM", v.Subtitle);
        Assert.Equal(ShieldTone.Red, v.Shield);
        Assert.Equal("⚠ Charger was unplugged", v.AlarmBarText);
        Assert.Equal("🔓 Turn OFF — stop the alarm", v.ButtonText);
        Assert.Equal(ButtonKind.Danger, v.ButtonKind);
        Assert.True(v.ButtonEnabled);
        Assert.False(v.ShowChips);
        Assert.False(v.ShowArmOnLockRow);
        Assert.False(v.FooterEnabled);
    }

    [Fact]
    public void PowerButtonHiddenByUs_OffersShowLink()
    {
        var v = CardViewBuilder.Build(Off() with { PowerButtonHidden = true, PowerButtonHiddenByUs = true });
        Assert.Equal(new ChipView("⏻ Lock-screen power button", "Hidden ✓ —", ChipTone.Ok, ChipLink.Show), Chip(v, 3));
    }

    [Fact]
    public void PackageInfo_TestHostIsNotAStoreApp() => Assert.False(PackageInfo.IsPackaged);

    [Fact]
    public void PowerButtonVisible_OffersHideLink()
    {
        var v = CardViewBuilder.Build(Off() with { PowerButtonHidden = false });
        Assert.Equal(new ChipView("⏻ Lock-screen power button", "Visible —", ChipTone.Warn, ChipLink.Hide), Chip(v, 3));
    }

    [Fact]
    public void LidUnavailable()
    {
        var v = CardViewBuilder.Build(Off() with { LidProtectionAvailable = false });
        Assert.Equal(new ChipView("💻 Lid protection", "Not available ⚠", ChipTone.Warn, ChipLink.None), Chip(v, 2));
    }

    [Fact]
    public void NoSpeakers()
    {
        var v = CardViewBuilder.Build(Off() with { SpeakerName = "" });
        Assert.Equal(new ChipView("🔊 Sound", "No speakers found ⚠", ChipTone.Warn, ChipLink.None), Chip(v, 1));
    }

    [Theory]
    [InlineData(AlarmReason.ChargerUnplugged, "Charger was unplugged")]
    [InlineData(AlarmReason.LidClosed, "Lid was closed")]
    [InlineData(AlarmReason.Sleep, "Someone tried to put it to sleep")]
    [InlineData(AlarmReason.Shutdown, "Someone tried to shut it down")]
    [InlineData(AlarmReason.HeadphoneJack, "Headphones were plugged in")]
    [InlineData(AlarmReason.TaskManager, "Someone opened Task Manager")]
    public void AlarmReasonText_MatchesSpec(AlarmReason reason, string expected)
    {
        Assert.Equal(expected, CardViewBuilder.AlarmReasonText(reason));
    }
}
