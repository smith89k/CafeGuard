using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CafeGuard.Shared;

namespace CafeGuard
{
    /// <summary>
    /// Makes the looping alarm sound the owner picked: siren, a spoken warning, or both
    /// (design: docs/superpowers/specs/2026-11-22-alarm-sound-choice-design.md).
    /// The voice comes from Windows' own built-in voices. If there is none, the plain siren is used — never silence.
    /// </summary>
    public static class AlarmSoundMaker
    {
        public const int SampleRate = 44100; // same format as the siren: 16-bit mono
        public const double GapAfterVoiceBeforeSiren = 0.3;
        public const double PauseAfterVoice = 0.7;

        /// <summary>The full WAV for these settings. Slow-ish (speech): call it off the UI thread.</summary>
        public static byte[] Build(AppSettings settings) =>
            Compose(settings.Sound, settings.Sound == AlarmSound.Siren ? null : Speak(AlarmPhrases.Text(settings.Phrase), settings.Voice));

        /// <summary>Joins the parts into one sound that loops: Siren = 4 s siren; Voice = words + pause; Both = words + gap + 3 s siren.</summary>
        public static byte[] Compose(AlarmSound sound, short[]? voice)
        {
            if (sound == AlarmSound.Siren || voice == null || voice.Length == 0)
                return SirenPlayer.GeneratePiercingSirenWav(4.0);

            if (sound == AlarmSound.Voice)
                return ToWav(voice.Concat(Silence(PauseAfterVoice)).ToArray());

            var siren = ReadPcm(SirenPlayer.GeneratePiercingSirenWav(3.0));
            return ToWav(voice.Concat(Silence(GapAfterVoiceBeforeSiren)).Concat(siren).ToArray());
        }

        /// <summary>
        /// Speaks the words with an English Windows voice (man or woman) through Windows' built-in speech (SAPI),
        /// so nothing extra is installed. Null if no English voice exists or speech fails.
        /// </summary>
        public static short[]? Speak(string text, VoiceChoice voice)
        {
            object? spVoice = null, stream = null;
            try
            {
                Type? voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                Type? streamType = Type.GetTypeFromProgID("SAPI.SpMemoryStream");
                if (voiceType == null || streamType == null) return null;
                spVoice = Activator.CreateInstance(voiceType);
                stream = Activator.CreateInstance(streamType);
                dynamic v = spVoice!, ms = stream!;

                string wanted = voice == VoiceChoice.Woman ? "Female" : "Male";
                dynamic? english = null, match = null;
                dynamic tokens = v.GetVoices("", "");
                for (int i = 0; i < (int)tokens.Count; i++)
                {
                    dynamic token = tokens.Item(i);
                    if (!IsEnglish((string)token.GetAttribute("Language"))) continue;
                    english ??= token;
                    if (string.Equals((string)token.GetAttribute("Gender"), wanted, StringComparison.OrdinalIgnoreCase)) { match = token; break; }
                }
                if (english == null) return null;

                v.Voice = match ?? english;
                ms.Format.Type = 34; // SAFT44kHz16BitMono (same format as the siren). 39 would be 48 kHz stereo = slow, deep voice.
                v.AudioOutputStream = ms;
                v.Volume = 100;
                v.Rate = 0;
                v.Speak(text, 0); // 0 = wait until finished

                byte[] bytes = (byte[])ms.GetData();
                var pcm = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, pcm, 0, pcm.Length * 2);
                return pcm.Length == 0 ? null : Normalize(pcm);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (stream != null) Marshal.FinalReleaseComObject(stream);
                if (spVoice != null) Marshal.FinalReleaseComObject(spVoice);
            }
        }

        /// <summary>SAPI lists languages as hex ids like "409;9"; English ids end in 09.</summary>
        private static bool IsEnglish(string? languages) =>
            !string.IsNullOrEmpty(languages) && languages.Split(';').Any(l =>
                int.TryParse(l.Trim(), System.Globalization.NumberStyles.HexNumber, null, out int id) && (id & 0x3FF) == 0x09);

        /// <summary>Makes the loudest point 95% of full scale, so the voice is as loud as the siren.</summary>
        public static short[] Normalize(short[] pcm)
        {
            int peak = pcm.Length == 0 ? 0 : pcm.Max(s => Math.Abs((int)s));
            if (peak == 0) return (short[])pcm.Clone();
            double gain = 0.95 * short.MaxValue / peak;
            return pcm.Select(s => (short)Math.Clamp(s * gain, short.MinValue, short.MaxValue)).ToArray();
        }

        /// <summary>The samples inside a 16-bit mono WAV (finds the "data" chunk).</summary>
        public static short[] ReadPcm(byte[] wav)
        {
            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "data")
                {
                    size = Math.Min(size, wav.Length - pos - 8);
                    var pcm = new short[size / 2];
                    Buffer.BlockCopy(wav, pos + 8, pcm, 0, pcm.Length * 2);
                    return pcm;
                }
                pos += 8 + size + (size & 1);
            }
            return Array.Empty<short>();
        }

        public static byte[] ToWav(short[] pcm)
        {
            int dataSize = pcm.Length * 2;
            using var ms = new MemoryStream(44 + dataSize);
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
            bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataSize); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(SampleRate); bw.Write(SampleRate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(dataSize);
            var bytes = new byte[dataSize];
            Buffer.BlockCopy(pcm, 0, bytes, 0, dataSize);
            bw.Write(bytes);
            bw.Flush();
            return ms.ToArray();
        }

        private static short[] Silence(double seconds) => new short[(int)(seconds * SampleRate)];
    }
}
