using System;
using System.IO;

namespace CafeGuard.Shared
{
    /// <summary>Where CafeGuard keeps its files.</summary>
    public static class AppPaths
    {
        public const string AppExeName = "CafeGuard.exe";
        public const string Version = "1.0.0";

        private static readonly string LocalAppData =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        /// <summary>%LOCALAPPDATA%\CafeGuard — settings, log, backups.</summary>
        public static string DataDir { get; } = Path.Combine(LocalAppData, "CafeGuard");

        public static string SettingsFile { get; } = Path.Combine(DataDir, "settings.json");
        public static string InstallerStateFile { get; } = Path.Combine(DataDir, "installer-state.json");
        public static string LogFile { get; } = Path.Combine(DataDir, "cafeguard.log");
    }
}
