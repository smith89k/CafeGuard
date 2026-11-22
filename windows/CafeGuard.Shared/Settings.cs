using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeGuard.Shared
{
    public enum ThemeChoice { Auto, Light, Dark }
    public enum AlarmSound { Siren, Voice, Both }
    public enum AlarmPhrase { DontTouch, PutDown, Protected, PleaseDont }
    public enum VoiceChoice { Man, Woman }

    /// <summary>The owner's choices: light/dark, "Turn ON when I lock (Win+L)", and the alarm sound.</summary>
    public sealed record AppSettings(ThemeChoice Theme = ThemeChoice.Auto, bool ArmOnLock = false,
        AlarmSound Sound = AlarmSound.Both, AlarmPhrase Phrase = AlarmPhrase.DontTouch, VoiceChoice Voice = VoiceChoice.Man);

    /// <summary>The words the alarm voice can say (owner-approved; no "Thief").</summary>
    public static class AlarmPhrases
    {
        public static string Text(AlarmPhrase phrase) => phrase switch
        {
            AlarmPhrase.PutDown => "Please put the laptop down.",
            AlarmPhrase.Protected => "This laptop is protected. The owner is coming back.",
            AlarmPhrase.PleaseDont => "Please don't touch this laptop.",
            _ => "Don't touch my laptop!"
        };
    }

    /// <summary>Reads and writes settings.json. A missing or broken file always gives the defaults.</summary>
    public static class SettingsStore
    {
        private sealed class Dto
        {
            [JsonPropertyName("theme")] public string? Theme { get; set; }
            [JsonPropertyName("armOnLock")] public bool ArmOnLock { get; set; }
            [JsonPropertyName("alarmSound")] public string? Sound { get; set; }
            [JsonPropertyName("alarmPhrase")] public string? Phrase { get; set; }
            [JsonPropertyName("voice")] public string? Voice { get; set; }
        }

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static AppSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new AppSettings();
                var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path));
                if (dto == null) return new AppSettings();
                var d = new AppSettings();
                return new AppSettings(Parse(dto.Theme, d.Theme), dto.ArmOnLock,
                    Parse(dto.Sound, d.Sound), Parse(dto.Phrase, d.Phrase), Parse(dto.Voice, d.Voice));
            }
            catch
            {
                return new AppSettings();
            }
        }

        private static T Parse<T>(string? text, T fallback) where T : struct, Enum =>
            !int.TryParse(text, out _) && Enum.TryParse(text, ignoreCase: true, out T value) && Enum.IsDefined(value) ? value : fallback;

        /// <summary>Writes to a temporary file first, then swaps it in, so a crash never leaves half a file.</summary>
        public static void Save(string path, AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            var dto = new Dto
            {
                Theme = settings.Theme.ToString(), ArmOnLock = settings.ArmOnLock,
                Sound = settings.Sound.ToString(), Phrase = settings.Phrase.ToString(), Voice = settings.Voice.ToString()
            };
            File.WriteAllText(temp, JsonSerializer.Serialize(dto, Options));
            File.Move(temp, path, overwrite: true);
        }
    }
}
