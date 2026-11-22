using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using CafeGuard.Card;

namespace CafeGuard.Tests;

public class CardPlacementTests
{
    private static readonly Size Card = new(310, 420);

    [Fact]
    public void BottomTaskbar_BottomRight() =>
        Assert.Equal(new Point(1598, 600), CardPlacement.Compute(new Rect(0, 0, 1920, 1032), new Rect(0, 0, 1920, 1080), Card));

    [Fact]
    public void TopTaskbar_TopRight() =>
        Assert.Equal(new Point(1598, 60), CardPlacement.Compute(new Rect(0, 48, 1920, 1032), new Rect(0, 0, 1920, 1080), Card));

    [Fact]
    public void LeftTaskbar_BottomLeft() =>
        Assert.Equal(new Point(72, 648), CardPlacement.Compute(new Rect(60, 0, 1860, 1080), new Rect(0, 0, 1920, 1080), Card));

    [Fact]
    public void SecondMonitor_BottomRightOfThatMonitor() =>
        Assert.Equal(new Point(4158, 960), CardPlacement.Compute(new Rect(1920, 0, 2560, 1392), new Rect(1920, 0, 2560, 1440), Card));

    [Fact]
    public void TinyWorkArea_StaysInside()
    {
        var work = new Rect(100, 100, 300, 300);
        var p = CardPlacement.Compute(work, new Rect(100, 100, 300, 348), Card);
        Assert.True(p.X >= work.Left && p.Y >= work.Top, $"{p} outside {work}");
    }
}
