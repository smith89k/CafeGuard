using System;
using System.IO;
using System.Media;
using System.Text;

namespace CafeGuard
{
    public sealed class SirenPlayer : IDisposable
    {
        private SoundPlayer? _player;
        private MemoryStream? _wavStream;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        public SirenPlayer()
        {
            byte[] wavBytes = GeneratePiercingSirenWav(durationSeconds: 4.0);
            _wavStream = new MemoryStream(wavBytes);
            _player = new SoundPlayer(_wavStream);
            _player.Load();
        }

        /// <summary>Swaps in a new alarm sound (UI thread). If it is playing, it carries on with the new sound.</summary>
        public void SetSound(byte[] wavBytes)
        {
            bool wasPlaying = _isPlaying;
            Stop();
            _player?.Dispose();
            _wavStream?.Dispose();
            _wavStream = new MemoryStream(wavBytes);
            _player = new SoundPlayer(_wavStream);
            try { _player.Load(); } catch { }
            if (wasPlaying) StartLooping();
        }

        public void StartLooping()
        {
            if (_player == null) return;
            try
            {
                _wavStream?.Seek(0, SeekOrigin.Begin);
                _player.PlayLooping();
                _isPlaying = true;
            }
            catch { }
        }

        public void Stop()
        {
            if (_player == null) return;
            try
            {
                _player.Stop();
                _isPlaying = false;
            }
            catch { }
        }

        /// <summary>
        /// Generates a 16-bit 44.1kHz mono PCM WAV containing a high-decibel ear-piercing
        /// European/Police dual-resonance anti-theft siren (1800Hz - 3500Hz harmonic sweep).
        /// </summary>
        public static byte[] GeneratePiercingSirenWav(double durationSeconds = 4.0)
        {
            const int sampleRate = 44100;
            const short bitsPerSample = 16;
            const short channels = 1;
            int totalSamples = (int)(sampleRate * durationSeconds);
            int dataChunkSize = totalSamples * channels * (bitsPerSample / 8);

            using var ms = new MemoryStream(44 + dataChunkSize);
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            // RIFF Header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataChunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt subchunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // PCM chunk size
            bw.Write((short)1); // AudioFormat = 1 (PCM)
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
            bw.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
            bw.Write(bitsPerSample);

            // data subchunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataChunkSize);

            double phase1 = 0.0;
            double phase2 = 0.0;

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;

                // Alternate every 2 seconds between fast urgency warble (6 Hz sweep) and piercing two-tone (Hi-Lo)
                double freq1;
                double freq2;
                if ((int)(t * 2) % 2 == 0)
                {
                    // Fast warble sweep between 2100 Hz and 3400 Hz (peak human ear & laptop speaker resonance)
                    double sweep = 0.5 * (1.0 + Math.Sin(2.0 * Math.PI * 6.5 * t));
                    freq1 = 2100.0 + (1300.0 * sweep);
                    freq2 = freq1 * 0.5; // sub-harmonic body
                }
                else
                {
                    // Rapid urgent Hi-Lo strobe (2800 Hz / 1950 Hz at 8 Hz rate)
                    bool hi = Math.Sin(2.0 * Math.PI * 8.0 * t) >= 0;
                    freq1 = hi ? 2850.0 : 1950.0;
                    freq2 = hi ? 1425.0 : 975.0;
                }

                phase1 += 2.0 * Math.PI * freq1 / sampleRate;
                phase2 += 2.0 * Math.PI * freq2 / sampleRate;

                // Rich clipped harmonic waveform (maximizes RMS acoustic power without speaker damage)
                double raw = 0.70 * Math.Sin(phase1)
                           + 0.20 * Math.Sign(Math.Sin(phase1))
                           + 0.25 * Math.Sin(phase2);

                double clamped = Math.Clamp(raw, -0.98, 0.98);
                short sample = (short)(clamped * short.MaxValue);
                bw.Write(sample);
            }

            bw.Flush();
            return ms.ToArray();
        }

        public void Dispose()
        {
            Stop();
            _player?.Dispose();
            _wavStream?.Dispose();
        }
    }
}
