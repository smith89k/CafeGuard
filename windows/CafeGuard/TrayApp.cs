using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using CafeGuard.Card;
using CafeGuard.Shared;
using Microsoft.Win32;

namespace CafeGuard
{
    public enum GuardState
    {
        Off,
        On,
        Alarm
    }

    /// <summary>
    /// Tray CafeGuard. Click the shield (left or right) to open the card.
    /// Turning ON locks Windows; unlocking Windows turns it OFF (only the owner can unlock).
    /// </summary>
    public sealed class TrayApp : ApplicationContext
    {
        // Tray icon colors (spec §5): gray = OFF, green = ON, red = ALARM.
        private static readonly Color GreenTone = ColorTranslator.FromHtml("#16A34A");
        private static readonly Color RedTone = ColorTranslator.FromHtml("#DC2626");

        private GuardState _state = GuardState.Off;
        private AudioDeviceSnapshot? _armedAudioSnapshot;
        private bool _flashToggle;
        private AppSettings _settings = SettingsStore.Load(AppPaths.SettingsFile);
        private ArmSource _armSource = ArmSource.Manual;
        private DateTime _lockedAt;
        private DateTime? _lastLidClose, _lastSuspend, _lastResume;
        private DateTime? _onSince;
        private DateTime? _alarmAt;
        private AlarmReason? _alarmReason;
        private bool _lidProtectionAvailable = true;
        private bool _powerButtonHidden = true;
        private bool _powerButtonHiddenByUs;
        private string _speakerName = string.Empty;

        private readonly SirenPlayer _siren = new SirenPlayer();
        private readonly SystemWatcher _watcher = new SystemWatcher();
        private readonly System.Windows.Forms.Timer _monitorTimer;
        private readonly System.Windows.Forms.Timer _testSirenTimer;
        private readonly System.Windows.Forms.Timer _pinTimer;
        private readonly System.Windows.Forms.Timer _autoArmTimer = new() { Interval = 2000 };
        private int _pinTries;

        private readonly NotifyIcon _trayIcon;
        private TrayCard? _card;

        // Two colors only: OFF = green outline, ON = solid green, ALARM = red (blinks red / red outline).
        private readonly Icon _iconOff = DrawShieldIcon(GreenTone, filled: false);
        private readonly Icon _iconOn = DrawShieldIcon(GreenTone);
        private readonly Icon _iconAlarm = DrawShieldIcon(RedTone);
        private readonly Icon _iconAlarmBlink = DrawShieldIcon(RedTone, filled: false);

        public TrayApp()
        {
            // WPF windows (the card) live inside this WinForms app; give WPF its application object.
            if (System.Windows.Application.Current == null)
                _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };

            _trayIcon = new NotifyIcon { Icon = _iconOff, Visible = true };
            _trayIcon.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) ToggleCard();
            };
            AppLog.Write("Tray icon shown.");

            // A second launch of CafeGuard asks this copy to show itself.
            var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            SingleInstance.ListenForShowMe(ui, ShowMe);

            // Another program (e.g. Store-Test.bat) can ask us to exit — only allowed while OFF.
            GuardSignals.SetGuarding(false);
            GuardSignals.ListenForExitRequest(ui, () =>
            {
                if (_state == GuardState.Off) { AppLog.Write("Installer asked CafeGuard to exit."); ExitApp(); }
                else AppLog.Write("Installer asked CafeGuard to exit, but the guard is ON — ignored.");
            });

            // Windows 11 hides new tray icons. Ask it to always show ours (first run only).
            // The Store version can't: Windows gives Store apps a private copy of that setting.
            _pinTimer = new System.Windows.Forms.Timer { Interval = 3000, Enabled = false };
            _pinTimer.Tick += (_, _) =>
            {
                _pinTries++;
                if (TrayIconPinner.PinOnce()) RefreshTrayIcon();
                if (_pinTries >= 5 || File.Exists(Path.Combine(AppLog.Folder, "tray-icon-pinned.txt")))
                    _pinTimer.Stop();
            };
            if (!PackageInfo.IsPackaged) _pinTimer.Start();

            _monitorTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();

            _testSirenTimer = new System.Windows.Forms.Timer { Interval = 7000 }; // long enough for the words + siren once
            _testSirenTimer.Tick += (_, _) =>
            {
                _testSirenTimer.Stop();
                if (_state == GuardState.Off)
                {
                    _siren.Stop();
                    CoreAudioController.RestoreVolume(_armedAudioSnapshot);
                }
            };

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            _autoArmTimer.Tick += AutoArmTimer_Tick;

            // Lid closed / sleep / shutdown while ON = alarm.
            _watcher.LidClosed += () => { _lastLidClose = DateTime.Now; _autoArmTimer.Stop(); if (_state == GuardState.On) TriggerAlarm(AlarmReason.LidClosed); };
            _watcher.GoingToSleep += () => { _lastSuspend = DateTime.Now; _autoArmTimer.Stop(); if (_state == GuardState.On) TriggerAlarm(AlarmReason.Sleep); };
            _watcher.WokeUp += () =>
            {
                _lastResume = DateTime.Now;
                _autoArmTimer.Stop(); // a lock reported around sleep/wake is never a Win+L
                // Sound can stop during sleep. If the alarm was going, start it again right away.
                if (_state == GuardState.Alarm)
                {
                    _siren.Stop();
                    CoreAudioController.EnforceMaxVolumeAndAntiHijack(_armedAudioSnapshot);
                    _siren.StartLooping();
                }
            };
            _watcher.ShutdownRequested += () =>
            {
                if (_state == GuardState.Off) return false;
                TriggerAlarm(AlarmReason.Shutdown);
                return true;
            };

            // If CafeGuard crashed last time while ON, put the user's lid / power button settings back.
            LidAndButtonGuard.Restore();

            UpdateTrayLook();
            PrepareAlarmSound();
            if (PackageInfo.IsPackaged && FirstTime("store-tray-tip-shown.txt"))
                ShowTip("Keep the shield next to the clock",
                    "Click ^ near the clock and drag the shield onto the taskbar, so it's always one click away.");
            else
                ShowTip("Cafe Guard is ready", "Click the shield icon near the clock to turn it ON before you leave.");
        }

        /// <summary>True the first time it's called for this marker (then remembers it).</summary>
        private static bool FirstTime(string marker)
        {
            try
            {
                string path = Path.Combine(AppLog.Folder, marker);
                if (File.Exists(path)) return false;
                Directory.CreateDirectory(AppLog.Folder);
                File.WriteAllText(path, DateTime.Now.ToString("o"));
                return true;
            }
            catch { return false; }
        }

        // ---------- On / Off ----------

        /// <summary>Turns the guard ON. Only the manual button locks the screen (Win+L mode is already locked).</summary>
        private void TurnOn(ArmSource source)
        {
            if (_state != GuardState.Off) return;

            if (!PowerAndLockService.GetPowerStatus().IsPluggedIn)
            {
                ShowTip("Plug in your charger first", "Cafe Guard watches the charger. Plug it in, then turn it ON.", ToolTipIcon.Warning);
                return;
            }
            if (TaskManagerDetector.IsOpen())
            {
                // Opening Task Manager while ON sounds the alarm, so it must be closed first.
                ShowTip("Close Task Manager first", "Opening Task Manager while Cafe Guard is ON sounds the alarm.", ToolTipIcon.Warning);
                return;
            }

            _testSirenTimer.Stop();
            _siren.Stop();
            _card?.Hide();

            _armedAudioSnapshot = CoreAudioController.TakeSnapshot();
            PowerAndLockService.SetKeepAwakeOnLockScreen(true);
            _lidProtectionAvailable = LidAndButtonGuard.Apply();
            _armSource = source;
            _onSince = DateTime.Now;
            // A normal Task Manager "End task" now gets "Access denied" (admins can still force it).
            AppLog.Write(ProcessGuard.DenyTerminate(ProcessGuard.Self) ? "End task blocked while ON." : "Could not block End task.");
            _alarmAt = null;
            _alarmReason = null;
            AppLog.Write($"Turned ON ({source}). Lid protection: {(_lidProtectionAvailable ? "on" : "FAILED - " + LidAndButtonGuard.LastError)}");
            if (!_lidProtectionAvailable)
            {
                ShowTip("Lid protection not available",
                    "Windows did not allow Cafe Guard to change the lid setting. Closing the lid will still sound the alarm.",
                    ToolTipIcon.Warning);
            }
            SetState(GuardState.On);

        }

        private void TurnOff()
        {
            if (_state == GuardState.Off) return;

            _siren.Stop();
            PowerAndLockService.SetKeepAwakeOnLockScreen(false);
            LidAndButtonGuard.Restore();
            _watcher.AllowShutdown();
            CoreAudioController.RestoreVolume(_armedAudioSnapshot);
            _onSince = null;
            ProcessGuard.AllowTerminate(ProcessGuard.Self);
            SetState(GuardState.Off);
            AppLog.Write("Turned OFF.");
        }

        private bool _askingOwner;

        /// <summary>
        /// Turn OFF button: Windows asks "Verify it's you" (PIN / fingerprint). Verified → OFF. Cancelled → stays ON.
        /// If this laptop can't do that check, the screen locks and unlocking turns CafeGuard OFF (unlock = OFF).
        /// The guard keeps watching (and the alarm keeps sounding) while Windows asks.
        /// </summary>
        private void AskOwnerThenTurnOff()
        {
            if (_state == GuardState.Off || _askingOwner) return;
            _askingOwner = true;
            // Windows centres its PIN window on the window we give it: an invisible 1x1 window in the middle of the screen.
            var center = ProcessGuard.CenterOf(Screen.FromPoint(Cursor.Position).WorkingArea);
            var anchor = new Form
            {
                FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Size = new Size(1, 1), Location = center, Opacity = 0.01, TopMost = true
            };
            anchor.Show();
            anchor.Activate();
            IntPtr window = anchor.Handle;
            var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            System.Threading.Tasks.Task.Run(() => OwnerCheck.AskWindows(window, "Turn Cafe Guard OFF", TimeSpan.FromMinutes(2)))
                .ContinueWith(t => ui.Post(_ =>
                {
                    anchor.Close();
                    anchor.Dispose();
                    _askingOwner = false;
                    var outcome = t.IsFaulted ? VerifyOutcome.Unavailable : t.Result;
                    var decision = OwnerCheck.Decide(outcome);
                    AppLog.Write($"Turn OFF asked: Windows check {outcome} -> {decision}.");
                    if (_state == GuardState.Off) return;
                    if (decision == OffDecision.TurnOff) TurnOff();
                    else if (decision == OffDecision.LockInstead)
                    {
                        ShowTip("Unlock to turn OFF", "Windows can't ask for your PIN here, so the screen locks. Unlocking turns Cafe Guard OFF.");
                        PowerAndLockService.LockWorkStation();
                    }
                }, null));
        }

        private void TriggerAlarm(AlarmReason reason)
        {
            if (_state == GuardState.Alarm) return;
            AppLog.Write($"ALARM: {CardViewBuilder.AlarmReasonText(reason)}");

            _alarmAt = DateTime.Now;
            _alarmReason = reason;
            SetState(GuardState.Alarm);
            CoreAudioController.EnforceMaxVolumeAndAntiHijack(_armedAudioSnapshot);
            _siren.StartLooping();
            ShowAlarmCard();
        }

        /// <summary>The red ALARM card pops up by itself and stays up until the owner turns CafeGuard OFF.</summary>
        private void ShowAlarmCard()
        {
            var card = EnsureCard();
            card.Sticky = true;
            if (card.IsVisible) { RefreshCard(); card.Activate(); return; }
            card.ApplyTheme(ThemeResolver.Current(_settings.Theme));
            RefreshCard();
            card.ShowAt(size =>
            {
                var (work, bounds) = ScreenInfo.AtCursor();
                return CardPlacement.Compute(work, bounds, size);
            });
        }

        private void RunTestSiren()
        {
            if (_state != GuardState.Off) return;
            _armedAudioSnapshot = CoreAudioController.TakeSnapshot();
            CoreAudioController.EnforceMaxVolumeAndAntiHijack(_armedAudioSnapshot);
            _siren.StartLooping();
            _testSirenTimer.Stop();
            _testSirenTimer.Start();
        }

        // ---------- Watching ----------

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (_state == GuardState.On)
            {
                if (!PowerAndLockService.GetPowerStatus().IsPluggedIn)
                {
                    TriggerAlarm(AlarmReason.ChargerUnplugged);
                }
                // Task Manager (runs with full rights on admin accounts) could close Cafe Guard: sound the alarm first.
                else if (TaskManagerDetector.IsOpen())
                {
                    TriggerAlarm(AlarmReason.TaskManager);
                }
                // Earbuds that connect by themselves: sound is quietly moved back to the speakers (no alarm).
                // Headphones plugged into the laptop's own jack (speakers cut off): alarm.
                else if (_armedAudioSnapshot != null &&
                    CoreAudioController.CheckAudio(_armedAudioSnapshot) == AudioCheckResult.SpeakersCutOff)
                {
                    TriggerAlarm(AlarmReason.HeadphoneJack);
                }
            }
            else if (_state == GuardState.Alarm)
            {
                // Keep volume at 100%, unmuted, on the speakers, and keep the siren going.
                CoreAudioController.EnforceMaxVolumeAndAntiHijack(_armedAudioSnapshot);
                if (!_siren.IsPlaying) _siren.StartLooping();

                // Blink the tray icon: red shield / red outline.
                _flashToggle = !_flashToggle;
                _trayIcon.Icon = _flashToggle ? _iconAlarm : _iconAlarmBlink;
            }

            if (_card?.IsVisible == true) RefreshCard();
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                // Win+L mode: decide ~2 s later, once we know whether this lock came with the lid closing / sleep.
                _lockedAt = DateTime.Now;
                _autoArmTimer.Stop();
                if (_settings.ArmOnLock) _autoArmTimer.Start();
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _autoArmTimer.Stop();
                // Unlock = OFF: only the owner can unlock Windows.
                if (AutoLockPolicy.ShouldDisarmOnUnlock(_state))
                {
                    AppLog.Write("Auto-OFF on unlock.");
                    TurnOff();
                }
            }
        }

        private void AutoArmTimer_Tick(object? sender, EventArgs e)
        {
            _autoArmTimer.Stop();
            bool charger = PowerAndLockService.GetPowerStatus().IsPluggedIn;
            if (AutoLockPolicy.ShouldArmAfterLock(_settings.ArmOnLock, _state, _watcher.IsLidOpen, charger,
                    _lockedAt, DateTime.Now, _lastLidClose, _lastSuspend, _lastResume))
            {
                TurnOn(ArmSource.AutoLock);
            }
            else
            {
                AppLog.Write($"Win+L mode: not turning ON (state={_state}, lidOpen={_watcher.IsLidOpen}, charger={charger}, lidClosed={_lastLidClose:T}, sleep={_lastSuspend:T}, wake={_lastResume:T}).");
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Windows switched light/dark: re-color the card if it follows Windows.
            if (_settings.Theme == ThemeChoice.Auto) _card?.ApplyTheme(ThemeResolver.Current(ThemeChoice.Auto));
        }

        // ---------- Card ----------

        private void ToggleCard()
        {
            var card = EnsureCard();
            // Clicking the shield while the card is open first hides it (it loses focus); don't reopen it.
            if (card.IsVisible || (DateTime.Now - card.LastAutoHide).TotalMilliseconds < 400)
            {
                card.Hide();
                return;
            }

            _speakerName = CoreAudioController.TakeSnapshot().DefaultDeviceId.Length > 0
                ? CoreAudioController.GetCurrentAudioStatus().DeviceName
                : string.Empty;
            ReadPowerButtonState();
            card.ApplyTheme(ThemeResolver.Current(_settings.Theme));
            RefreshCard();
            card.ShowAt(size =>
            {
                var (work, bounds) = ScreenInfo.AtCursor();
                return CardPlacement.Compute(work, bounds, size);
            });
        }

        private TrayCard EnsureCard()
        {
            if (_card != null) return _card;

            _card = new TrayCard(ThemeResolver.Current(_settings.Theme));
            ElementHost.EnableModelessKeyboardInterop(_card);
            _card.TurnOnClicked += () => TurnOn(ArmSource.Manual);
            _card.TurnOffClicked += AskOwnerThenTurnOff;
            _card.TestSirenClicked += RunTestSiren;
            _card.ExitClicked += ExitApp;
            _card.HidePowerButtonClicked += HidePowerButton;
            _card.ShowPowerButtonClicked += ShowPowerButton;
            _card.ThemeChosen += choice =>
            {
                SaveSettings(_settings with { Theme = choice });
                _card.ApplyTheme(ThemeResolver.Current(choice));
                RefreshCard();
            };
            _card.AlarmSoundChosen += sound => ChangeAlarmSound(_settings with { Sound = sound });
            _card.PhraseChosen += phrase => ChangeAlarmSound(_settings with { Phrase = phrase });
            _card.VoiceChosen += voice => ChangeAlarmSound(_settings with { Voice = voice });
            _card.ArmOnLockChanged += on =>
            {
                SaveSettings(_settings with { ArmOnLock = on });
                AppLog.Write($"Win+L mode {(on ? "enabled" : "disabled")}.");
            };
            return _card;
        }

        private void RefreshCard()
        {
            if (_card == null) return;
            var input = new CardInput(_state, PowerAndLockService.GetPowerStatus().IsPluggedIn, _speakerName,
                _lidProtectionAvailable, _powerButtonHidden, _powerButtonHiddenByUs, _onSince, _alarmAt, _alarmReason,
                _settings.ArmOnLock, _settings.Theme);
            _card.Update(CardViewBuilder.Build(input), _settings);
        }

        private void ChangeAlarmSound(AppSettings settings)
        {
            SaveSettings(settings);
            RefreshCard();
            PrepareAlarmSound();
        }

        private int _soundVersion;

        /// <summary>Makes the chosen alarm sound in the background (speech takes a moment) and hands it to the siren player.</summary>
        private void PrepareAlarmSound()
        {
            var settings = _settings;
            int version = ++_soundVersion;
            var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            System.Threading.Tasks.Task.Run(() => AlarmSoundMaker.Build(settings))
                .ContinueWith(t => ui.Post(_ =>
                {
                    if (version != _soundVersion) return; // a newer choice is on its way
                    if (t.IsFaulted) { AppLog.Write($"Could not make the alarm sound: {t.Exception?.GetBaseException().Message}"); return; }
                    _siren.SetSound(t.Result);
                    AppLog.Write($"Alarm sound ready: {settings.Sound}, \"{AlarmPhrases.Text(settings.Phrase)}\", {settings.Voice}.");
                }, null));
        }

        private void SaveSettings(AppSettings settings)
        {
            _settings = settings;
            try { SettingsStore.Save(AppPaths.SettingsFile, settings); }
            catch (Exception ex) { AppLog.Write($"Could not save settings: {ex.Message}"); }
        }

        /// <summary>Is the power button on the lock screen hidden, and by us? (Reading HKLM needs no admin rights.)</summary>
        private void ReadPowerButtonState()
        {
            try
            {
                var registry = new WindowsRegistryStore();
                _powerButtonHidden = LockScreenPowerButton.IsHidden(registry);
                _powerButtonHiddenByUs = PowerButtonHider.IsHiddenByUs(registry, AppPaths.InstallerStateFile);
            }
            catch
            {
                _powerButtonHidden = false;
                _powerButtonHiddenByUs = false;
            }
        }

        /// <summary>
        /// "Hide" link on the card: CafeGuard starts a copy of itself with administrator rights to hide the button
        /// (Windows asks for permission). The old value is recorded first, so it can be put back. Runs in the
        /// background so the guard keeps watching while the permission prompt is open.
        /// </summary>
        private void HidePowerButton() =>
            RunPowerButtonJob("Hide", () => PowerButtonHider.Hide(new WindowsRegistryStore(), AppPaths.InstallerStateFile,
                () => RunAdminHelper(new LockScreenPowerCommand(PowerAction.Hide, null))).ToString());

        /// <summary>"Show" link: put the button back the way it was before CafeGuard hid it.</summary>
        private void ShowPowerButton() =>
            RunPowerButtonJob("Show", () => PowerButtonHider.Show(new WindowsRegistryStore(), AppPaths.InstallerStateFile,
                RunAdminHelper).ToString());

        private static bool RunAdminHelper(LockScreenPowerCommand command)
        {
            string? self = Environment.ProcessPath;
            return self != null && Elevation.RunElevated(self, command.ToArguments());
        }

        private void RunPowerButtonJob(string name, Func<string> job)
        {
            var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            System.Threading.Tasks.Task.Run(job)
                .ContinueWith(t => ui.Post(_ =>
                {
                    AppLog.Write($"{name} lock-screen power button: {(t.IsFaulted ? "error " + t.Exception?.GetBaseException().Message : t.Result)}.");
                    ReadPowerButtonState();
                    RefreshCard();
                }, null));
        }

        // ---------- Tray look ----------

        /// <summary>Called when CafeGuard is started a second time: make sure the icon is there and say hello.</summary>
        private void ShowMe()
        {
            AppLog.Write("Asked to show myself.");
            RefreshTrayIcon();
            ShowTip("Cafe Guard is already running",
                _state == GuardState.Off
                    ? "Click the shield icon near the clock to turn it ON."
                    : "Cafe Guard is ON. Turning it OFF asks for your Windows PIN.");
        }

        /// <summary>Removes and re-adds the tray icon, so Windows picks up the latest settings.</summary>
        private void RefreshTrayIcon()
        {
            _trayIcon.Visible = false;
            _trayIcon.Visible = true;
        }

        private void SetState(GuardState state)
        {
            _state = state;
            GuardSignals.SetGuarding(state != GuardState.Off);
            if (_card != null) _card.Sticky = state == GuardState.Alarm;
            UpdateTrayLook();
            if (_card?.IsVisible == true) RefreshCard();
        }

        private void UpdateTrayLook()
        {
            _trayIcon.Icon = _state switch
            {
                GuardState.On => _iconOn,
                GuardState.Alarm => _iconAlarm,
                _ => _iconOff
            };
            _trayIcon.Text = _state switch
            {
                GuardState.On => "Cafe Guard: ON",
                GuardState.Alarm => "Cafe Guard: ALARM!",
                _ => "Cafe Guard: OFF"
            };
        }

        private void ShowTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _trayIcon.ShowBalloonTip(3000, title, text, icon);
        }

        private static Icon DrawShieldIcon(Color fillColor, bool filled = true)
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var pts = new PointF[Shield.Points.Length];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new PointF(3 + (float)Shield.Points[i].X * 26, 2 + (float)Shield.Points[i].Y * 28);

            if (filled)
            {
                using var brush = new SolidBrush(fillColor);
                g.FillPolygon(brush, pts);
            }
            else
            {
                using var pen = new Pen(fillColor, 3f);
                g.DrawPolygon(pen, pts);
            }

            return Icon.FromHandle(bmp.GetHicon());
        }

        // ---------- Exit ----------

        private void ExitApp()
        {
            if (_state != GuardState.Off) return;
            _card?.CloseForGood();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
                SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
                _monitorTimer.Dispose();
                _autoArmTimer.Dispose();
                _testSirenTimer.Dispose();
                _pinTimer.Dispose();
                _siren.Dispose();
                _watcher.Dispose();
                PowerAndLockService.SetKeepAwakeOnLockScreen(false);
                LidAndButtonGuard.Restore();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
