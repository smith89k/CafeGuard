using System;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace CafeGuard.Card
{
    /// <summary>Where the card pops up: next to the taskbar's clock corner, inside the screen's work area.</summary>
    public static class CardPlacement
    {
        /// <param name="workArea">Screen area not covered by the taskbar (same units as the result).</param>
        /// <param name="screenBounds">The whole screen.</param>
        public static Point Compute(Rect workArea, Rect screenBounds, Size card, double gap = 12)
        {
            double left = workArea.Left - screenBounds.Left;
            double top = workArea.Top - screenBounds.Top;
            double right = screenBounds.Right - workArea.Right;
            double bottom = screenBounds.Bottom - workArea.Bottom;

            double x = workArea.Right - gap - card.Width;   // right side by default
            double y = workArea.Bottom - gap - card.Height; // bottom by default

            double biggest = Math.Max(Math.Max(left, top), Math.Max(right, bottom));
            if (biggest > 0 && biggest == top) y = workArea.Top + gap;
            else if (biggest > 0 && biggest == left) x = workArea.Left + gap;

            x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - card.Width));
            y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - card.Height));
            return new Point(x, y);
        }
    }
}
