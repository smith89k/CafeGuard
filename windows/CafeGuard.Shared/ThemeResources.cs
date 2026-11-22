using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CafeGuard.Shared
{
    /// <summary>Puts CafeGuard's colors (light or dark) and shared styles into a window's resources.</summary>
    public static class ThemeResources
    {
        public static void Apply(ResourceDictionary target, ThemeMode mode)
        {
            if (!target.MergedDictionaries.OfType<CafeGuardStyles>().Any())
                target.MergedDictionaries.Add(new CafeGuardStyles());

            var p = Palette.For(mode);
            Set(target, "Brush.Background", p.Background);
            Set(target, "Brush.Surface", p.Surface);
            Set(target, "Brush.Line", p.Line);
            Set(target, "Brush.Text", p.Text);
            Set(target, "Brush.Muted", p.Muted);
            Set(target, "Brush.Green", p.Green);
            Set(target, "Brush.GreenSoft", p.GreenSoft);
            Set(target, "Brush.GreenText", p.GreenText);
            Set(target, "Brush.Gray", p.Gray);
            Set(target, "Brush.Red", p.Red);
            Set(target, "Brush.RedSoft", p.RedSoft);
            Set(target, "Brush.Warning", p.Warning);
            Set(target, "Brush.NeutralButton", p.NeutralButton);
        }

        private static void Set(ResourceDictionary target, string key, string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            target[key] = brush;
        }
    }
}
