using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class PaletteTests
{
    [Fact]
    public void Palette_Light_MatchesSpec()
    {
        var p = Palette.For(ThemeMode.Light);
        Assert.Equal("#FFFFFF", p.Background);
        Assert.Equal("#F3F4F6", p.Surface);
        Assert.Equal("#E5E7EB", p.Line);
        Assert.Equal("#111827", p.Text);
        Assert.Equal("#6B7280", p.Muted);
        Assert.Equal("#16A34A", p.Green);
        Assert.Equal("#DCFCE7", p.GreenSoft);
        Assert.Equal("#15803D", p.GreenText);
        Assert.Equal("#9CA3AF", p.Gray);
        Assert.Equal("#DC2626", p.Red);
        Assert.Equal("#FEE2E2", p.RedSoft);
        Assert.Equal("#B45309", p.Warning);
        Assert.Equal("#E5E7EB", p.NeutralButton);
    }

    [Fact]
    public void Palette_Dark_MatchesSpec()
    {
        var p = Palette.For(ThemeMode.Dark);
        Assert.Equal("#202020", p.Background);
        Assert.Equal("#2B2B2B", p.Surface);
        Assert.Equal("#3A3A3A", p.Line);
        Assert.Equal("#F3F4F6", p.Text);
        Assert.Equal("#A3A3A3", p.Muted);
        Assert.Equal("#22C55E", p.Green);
        Assert.Equal("#14532D", p.GreenSoft);
        Assert.Equal("#86EFAC", p.GreenText);
        Assert.Equal("#737373", p.Gray);
        Assert.Equal("#EF4444", p.Red);
        Assert.Equal("#450A0A", p.RedSoft);
        Assert.Equal("#FBBF24", p.Warning);
        Assert.Equal("#3A3A3A", p.NeutralButton);
    }

    [Theory]
    [InlineData(ThemeChoice.Auto, true, ThemeMode.Light)]
    [InlineData(ThemeChoice.Auto, false, ThemeMode.Dark)]
    [InlineData(ThemeChoice.Light, false, ThemeMode.Light)]
    [InlineData(ThemeChoice.Dark, true, ThemeMode.Dark)]
    public void Resolve_PicksMode(ThemeChoice choice, bool windowsLight, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeResolver.Resolve(choice, windowsLight));
    }
}
