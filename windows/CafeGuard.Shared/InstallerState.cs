using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeGuard.Shared
{
    /// <summary>
    /// What CafeGuard changed in Windows, so removing CafeGuard can put it back (installer-state.json).
    /// Written by the installer AND by the tray card's "Hide" link.
    /// </summary>
    public sealed record InstallerState(
        [property: JsonPropertyName("powerButtonChangedByUs")] bool PowerButtonChangedByUs,
        [property: JsonPropertyName("previousPowerValue")] int? PreviousPowerValue,
        [property: JsonPropertyName("version")] string Version)
    {
        public static InstallerState? Load(string path)
        {
            try { return File.Exists(path) ? JsonSerializer.Deserialize<InstallerState>(File.ReadAllText(path)) : null; }
            catch { return null; }
        }

        /// <summary>Writes a temporary file first, then swaps it in, so a crash never leaves half a file.</summary>
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
    }
}
