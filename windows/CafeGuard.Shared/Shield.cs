using System.Windows;
using System.Windows.Media;

namespace CafeGuard.Shared
{
    /// <summary>The CafeGuard shield shape, shared by the card, the installer, the tray icon and the .ico.</summary>
    public static class Shield
    {
        /// <summary>Corner points as fractions of width/height.</summary>
        public static readonly (double X, double Y)[] Points =
        {
            (0.50, 0.00), (1.00, 0.18), (0.92, 0.62), (0.50, 1.00), (0.08, 0.62), (0.00, 0.18)
        };

        public static Geometry CreateGeometry(double width, double height)
        {
            var figure = new PathFigure { StartPoint = new Point(Points[0].X * width, Points[0].Y * height), IsClosed = true, IsFilled = true };
            for (int i = 1; i < Points.Length; i++)
                figure.Segments.Add(new LineSegment(new Point(Points[i].X * width, Points[i].Y * height), true));
            var geometry = new PathGeometry(new[] { figure });
            geometry.Freeze();
            return geometry;
        }
    }
}
