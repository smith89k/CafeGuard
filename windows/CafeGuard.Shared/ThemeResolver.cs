using Microsoft.Win32;

namespace CafeGuard.Shared
{
    public static class ThemeResolver
    {
        public static ThemeMode Resolve(ThemeChoice choice, bool windowsUsesLight) => choice switch
        {
            ThemeChoice.Light => ThemeMode.Light,
            ThemeChoice.Dark => ThemeMode.Dark,
            _ => windowsUsesLight ? ThemeMode.Light : ThemeMode.Dark
        };

        /// <summary>Windows' "app mode" setting. Missing value = light (the Windows default).</summary>
        public static bool WindowsUsesLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
            }
            catch
            {
                return true;
            }
        }

        public static ThemeMode Current(ThemeChoice choice) => Resolve(choice, WindowsUsesLightTheme());
    }
}
