using System;
using System.Threading;
using System.Windows.Forms;

namespace CafeGuard
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Admin helper: CafeGuard started itself "as administrator" to hide / show the lock-screen power button.
            var powerCommand = CafeGuard.Shared.LockScreenPowerCommand.TryParse(args);
            if (powerCommand != null) return powerCommand.RunAsHelper();

            // Optional CLI self-test mode to verify hardware sensors & audio synthesis
            if (args.Length > 0 && args[0] == "--self-test")
            {
                return RunSelfTest();
            }
            if (args.Length > 0 && args[0] == "--restore-power-settings")
            {
                // Used by CafeGuard Setup when removing CafeGuard: put the lid / power button settings back.
                LidAndButtonGuard.Restore();
                return LidAndButtonGuard.IsActive ? 1 : 0;
            }
            if (args.Length == 4 && args[0] == "--render-card")
            {
                return CafeGuard.Card.CardSnapshot.Render(args[1], args[2], args[3]);
            }
            if (args.Length > 0 && args[0] == "--test-lid-guard")
            {
                return RunLidGuardTest();
            }

            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ShowCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowCrash(e.ExceptionObject as Exception);

            AppLog.Write($"Starting CafeGuard from {Environment.ProcessPath}");

            if (!SingleInstance.TryBecomeTheOnlyCopy())
            {
                // Another copy holds the lock. Ask it to show itself.
                if (SingleInstance.AskRunningCopyToShowItself())
                {
                    AppLog.Write("Another copy is running and answered; it is showing itself. Exiting.");
                    return 0;
                }

                AppLog.Write("Another copy is running but did not answer (stuck).");
                var answer = MessageBox.Show(
                    "An earlier copy of Cafe Guard is running but not responding.\n" +
                    "(Antivirus sometimes pauses new programs while it checks them.)\n\n" +
                    "Close it and start Cafe Guard fresh?",
                    "Cafe Guard",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return 0;

                SingleInstance.CloseOtherCopies();
                if (!SingleInstance.TryBecomeTheOnlyCopy(5000))
                {
                    MessageBox.Show("The old copy could not be closed. Please restart your computer, then start Cafe Guard again.",
                        "Cafe Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }

            try
            {
                using var app = new TrayApp();
                Application.Run(app);
            }
            finally
            {
                AppLog.Write("CafeGuard closed.");
                SingleInstance.Release();
            }
            return 0;
        }

        private static void ShowCrash(Exception? ex)
        {
            AppLog.Write($"ERROR: {ex}");
            try
            {
                MessageBox.Show($"Cafe Guard hit a problem and may not be guarding:\n\n{ex?.Message}\n\nDetails are saved in:\n{AppLog.Folder}\\cafeguard.log",
                    "Cafe Guard problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        /// <summary>Turns lid / button protection on, then off again, and checks the user's settings come back.</summary>
        private static int RunLidGuardTest()
        {
            string before = LidAndButtonGuard.Describe();
            Console.WriteLine($"Before : {before}");
            bool applied = LidAndButtonGuard.Apply();
            Console.WriteLine($"During : {LidAndButtonGuard.Describe()} (applied={applied}) {LidAndButtonGuard.LastError}");
            LidAndButtonGuard.Restore();
            string after = LidAndButtonGuard.Describe();
            Console.WriteLine($"After  : {after}");
            bool ok = applied && before == after && !LidAndButtonGuard.IsActive;
            Console.WriteLine(ok ? "=== LID GUARD TEST PASSED ===" : "=== LID GUARD TEST FAILED ===");
            return ok ? 0 : 1;
        }

        private static int RunSelfTest()
        {
            try
            {
                var power = PowerAndLockService.GetPowerStatus();
                var snap = CoreAudioController.TakeSnapshot();
                var audio = CoreAudioController.GetCurrentAudioStatus();
                byte[] wav = SirenPlayer.GeneratePiercingSirenWav(1.0);

                Console.WriteLine("=== CafeGuard Hardware & CoreAudio Self-Test ===");
                Console.WriteLine($"[PASS] Power Sensor      : {power.StatusText} (PluggedIn={power.IsPluggedIn}, Battery={power.BatteryPercent}%)");
                Console.WriteLine($"[PASS] CoreAudio Endpoint: {snap.DefaultDeviceName} (ID={snap.DefaultDeviceId})");
                Console.WriteLine($"[PASS] Active Outputs    : {snap.ActiveDeviceCount} device(s), Volume={audio.VolumePercent}%, Muted={audio.IsMuted}");
                Console.WriteLine($"[PASS] Siren Synthesizer : Generated {wav.Length} bytes PCM WAV (1800Hz-3500Hz dual sweep)");
                // Memory check: run the guard's sound check 3000 times (about 7 minutes of guarding).
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                for (int i = 0; i < 300; i++) CoreAudioController.CheckAudio(snap); // warm up
                GC.Collect(); GC.WaitForPendingFinalizers(); proc.Refresh();
                long memBefore = proc.PrivateMemorySize64; int handlesBefore = proc.HandleCount;
                for (int i = 0; i < 3000; i++)
                {
                    var r = CoreAudioController.CheckAudio(snap);
                    if (r != AudioCheckResult.Ok) throw new Exception($"Sound check gave {r} with nothing changed");
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); proc.Refresh();
                long growKb = (proc.PrivateMemorySize64 - memBefore) / 1024;
                int growHandles = proc.HandleCount - handlesBefore;
                Console.WriteLine($"[{(growKb < 2048 && growHandles < 50 ? "PASS" : "FAIL")}] Memory leak test: +{growKb} KB, +{growHandles} handles after 3000 checks");
                Console.WriteLine($"[INFO] Lid/buttons now  : {LidAndButtonGuard.Describe()}");
                Console.WriteLine("=== ALL CHECKS PASSED ===");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FAIL] Self-test error: {ex}");
                return 1;
            }
        }
    }
}
