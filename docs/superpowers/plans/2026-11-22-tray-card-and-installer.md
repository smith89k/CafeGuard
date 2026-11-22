# CafeGuard Tray Card & Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the plain tray menu with a green, light/dark "design B" pop-up card (with Win+L auto-guard and unlock = OFF), and add a matching WPF installer `CafeGuard-Setup.exe`.

**Architecture:** A new `CafeGuard.Shared` class library holds everything both programs must agree on (paths, settings, palette + WPF styles, shield shape, registry seam, lock-screen power button, app↔installer signals). The tray app keeps its WinForms `NotifyIcon`/`ApplicationContext` and shows a WPF `TrayCard` window driven by a pure `CardViewBuilder`. The installer is a separate WPF single-file exe that embeds the published `CafeGuard.exe` and drives a testable `InstallerEngine` through an `IInstallEnvironment` seam.

**Tech Stack:** .NET 8 (`net8.0-windows`, WPF + WinForms), xUnit, System.Text.Json, framework-dependent single-file publish `win-x64`.

**Spec:** `docs/superpowers/specs/2026-11-22-tray-card-and-installer-design.md` (+ mockups `.superpowers/design-preview-v2.html` — source of truth for layout and copy).

**No git repository** in this project and the owner hasn't asked for one: skip commit steps.

## Global Constraints

- Target `net8.0-windows`, `RuntimeIdentifier` `win-x64`, `SelfContained` false, `PublishSingleFile` true. No extra runtime installs.
- Install dir `%LOCALAPPDATA%\Programs\CafeGuard`; data dir `%LOCALAPPDATA%\CafeGuard` (log `cafeguard.log`, `settings.json`, `installer-state.json`, existing `power-settings-backup.txt`).
- Exe names: `CafeGuard.exe`, `CafeGuard-Setup.exe`. Version `1.0.0`. Publisher `CafeGuard`.
- Colors exactly as spec §5 table (light/dark). Font Segoe UI Variable → Segoe UI. Radius 10 (windows/cards), 8 (main buttons), 6 (chips, installer buttons).
- Shield: gray = OFF, green = ON, red = ALARM; installer logo green.
- Card/installer copy exactly as spec §6–§7 and the v2 mockup (including emoji: 🔒 🔓 🔌 🔊 💻 ⏻ 🔐 ⚠ ✓ ☀ 🌙).
- Admin rights only for the lock-screen power button (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`, value `shutdownwithoutlogon`, 0 = hidden).
- The project folder has an inherited **Low** integrity label. Any exe the owner or a test runs for real must come from a folder labelled Medium (`dist\`, via `icacls <dir> /setintegritylevel (OI)(CI)M`) or from the install dir. Unit tests must not touch the real registry or `%LOCALAPPDATA%`; they write only under `AppContext.BaseDirectory`.
- Existing behavior (detection, lid guard, crash recovery, single instance, log) must keep working; `--self-test` and `--test-lid-guard` must keep passing from the installed copy.

## Review Focus

- **Corrupt or half-written `settings.json`** → app starts with defaults (Auto, armOnLock false), never crashes. Test in Task 2.
- **Owner runs Setup (update/remove) while guard is ON/ALARM** → refused with "Turn CafeGuard OFF first, then click Try again.", nothing changed. Test in Task 7.
- **UAC prompt declined** during install → install still succeeds, power-button step = Skipped, `powerButtonChangedByUs` false. Test in Task 7.
- **Power button was already hidden before install** → uninstall must leave it hidden (only restore what CafeGuard changed). Test in Task 7.
- **Taskbar not at the bottom / second monitor** → card stays fully inside the work area next to the taskbar. Tests in Task 5.

---

### Task 1: Restructure into solution, shared library and tests; Medium-labelled build output

**Files:**
- Create: `windows/CafeGuard.sln`, `windows/CafeGuard.Shared/CafeGuard.Shared.csproj`, `windows/CafeGuard.Tests/CafeGuard.Tests.csproj`, `Build-Installer.bat`
- Move: `windows/*.cs` + `windows/CafeGuard.csproj` → `windows/CafeGuard/`
- Move: `AppSupport.cs`'s `AppLog` folder constant → `windows/CafeGuard.Shared/AppPaths.cs`
- Delete: `Launch-CafeGuard.bat`, `app/`

**Interfaces:**
- Produces: `CafeGuard.Shared.AppPaths` — `static string InstallDir`, `DataDir`, `SettingsFile`, `InstallerStateFile`, `LogFile`; `const string AppExeName = "CafeGuard.exe"`, `SetupExeName = "CafeGuard-Setup.exe"`, `Version = "1.0.0"`. `AppLog` uses `AppPaths.LogFile`.
- Produces: `Build-Installer.bat` (this task: publishes the app to `dist\payload\CafeGuard.exe` after labelling `dist` Medium; Task 8 extends it).

- [ ] **Step 1:** Create the class library `CafeGuard.Shared` (`net8.0-windows`, `UseWPF` true, `UseWindowsForms` true, Nullable enable) and the xUnit test project `CafeGuard.Tests` (`net8.0-windows`, `UseWPF` true) referencing Shared and CafeGuard. Add all three to `windows/CafeGuard.sln`. App csproj references Shared and gets `UseWPF` true.
- [ ] **Step 2: Write the test** `AppPathsTests.InstallDir_IsUnderLocalAppDataPrograms`: `Assert.Equal(Path.Combine(Environment.GetFolderPath(LocalApplicationData), "Programs", "CafeGuard"), AppPaths.InstallDir)`; and `DataDir_FilesLiveInDataDir`: `SettingsFile`, `InstallerStateFile`, `LogFile` all start with `AppPaths.DataDir` and end with `settings.json`, `installer-state.json`, `cafeguard.log`.
- [ ] **Step 3:** Run `dotnet test windows/CafeGuard.sln` → FAIL (AppPaths missing). Implement `AppPaths`; point `AppLog`, `LidAndButtonGuard.BackupFile` and `TrayIconPinner` marker at `AppPaths.DataDir`. Run again → PASS.
- [ ] **Step 4:** Write `Build-Installer.bat`: `mkdir dist` if missing; `icacls dist /setintegritylevel (OI)(CI)M`; `dotnet publish windows\CafeGuard -c Release -o dist\payload`. Run it.
- [ ] **Step 5: Verify** `icacls dist\payload\CafeGuard.exe` shows `Medium Mandatory Level` (or no label), then copy it to `AppPaths.InstallDir`, run `--self-test` and `--test-lid-guard` from there → both print PASSED.

### Task 2: Settings, palette and theme resolution

**Files:**
- Create: `windows/CafeGuard.Shared/Settings.cs`, `windows/CafeGuard.Shared/Palette.cs`, `windows/CafeGuard.Shared/ThemeResolver.cs`
- Test: `windows/CafeGuard.Tests/SettingsTests.cs`, `PaletteTests.cs`

**Interfaces:**
- Produces: `enum ThemeChoice { Auto, Light, Dark }`; `sealed record AppSettings(ThemeChoice Theme = ThemeChoice.Auto, bool ArmOnLock = false)`; `static class SettingsStore { AppSettings Load(string path); void Save(string path, AppSettings settings); }` — JSON property names `theme` (string "Auto"/"Light"/"Dark") and `armOnLock`.
- Produces: `enum ThemeMode { Light, Dark }`; `sealed record Palette(string Background, string Surface, string Line, string Text, string Muted, string Green, string GreenSoft, string GreenText, string Gray, string Red, string RedSoft, string Warning, string NeutralButton)` with `static Palette For(ThemeMode mode)`; `static class ThemeResolver { ThemeMode Resolve(ThemeChoice choice, bool windowsUsesLight); bool WindowsUsesLightTheme(); }` (reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`, missing → true).

- [ ] **Step 1: Write the tests**
  - `Load_MissingFile_ReturnsDefaults` → `new AppSettings()`.
  - `Load_CorruptJson_ReturnsDefaults` (file content `{"theme":` ) → defaults, no throw.
  - `Load_UnknownTheme_FallsBackToAuto` (`{"theme":"Purple","armOnLock":true}`) → `Theme == Auto`, `ArmOnLock == true`.
  - `SaveThenLoad_RoundTrips` (`new AppSettings(ThemeChoice.Dark, true)`), saving into a not-yet-existing subfolder of `AppContext.BaseDirectory`.
  - `Palette_Light_MatchesSpec`: Background `#FFFFFF`, Green `#16A34A`, Gray `#9CA3AF`, Red `#DC2626`, Warning `#B45309`; `Palette_Dark_MatchesSpec`: Background `#202020`, Green `#22C55E`, Gray `#737373`, Red `#EF4444`, Warning `#FBBF24`.
  - `Resolve`: (Auto, true)→Light, (Auto, false)→Dark, (Light, false)→Light, (Dark, true)→Dark.
- [ ] **Step 2:** Run `dotnet test windows/CafeGuard.sln --filter "SettingsTests|PaletteTests"` → FAIL.
- [ ] **Step 3:** Implement. `Save` writes to a temp file then `File.Move(overwrite: true)` so a crash can't leave half a file. All other palette values from spec §5.
- [ ] **Step 4:** Run tests → PASS.

### Task 3: Card view builder (states, copy, chips)

**Files:**
- Create: `windows/CafeGuard/Card/CardModel.cs` (records/enums), `windows/CafeGuard/Card/CardViewBuilder.cs`
- Test: `windows/CafeGuard.Tests/CardViewBuilderTests.cs`

**Interfaces:**
- Consumes: `GuardState` (existing: `Off`, `On`, `Alarm`), `ThemeChoice`.
- Produces:
  - `enum AlarmReason { ChargerUnplugged, LidClosed, Sleep, Shutdown, HeadphoneJack }`
  - `enum ShieldTone { Gray, Green, Red }`, `enum ButtonKind { Primary, Neutral, Danger, Disabled }`, `enum ChipTone { Ok, Warn }`
  - `sealed record CardInput(GuardState State, bool ChargerPlugged, string SpeakerName, bool LidProtectionAvailable, bool PowerButtonHidden, DateTime? OnSince, DateTime? AlarmAt, AlarmReason? Reason, bool ArmOnLock, ThemeChoice Theme)`
  - `sealed record ChipView(string Label, string Value, ChipTone Tone, bool HasHideLink)`
  - `sealed record CardView(string Title, string Subtitle, ShieldTone Shield, string ButtonText, ButtonKind ButtonKind, bool ButtonEnabled, bool ShowChips, IReadOnlyList<ChipView> Chips, string? AlarmBarText, bool ShowArmOnLockRow, bool FooterEnabled)`
  - `static class CardViewBuilder { CardView Build(CardInput input); }`
  - `static string AlarmReasonText(AlarmReason r)`: ChargerUnplugged → "Charger was unplugged", LidClosed → "Lid was closed", Sleep → "Someone tried to put it to sleep", Shutdown → "Someone tried to shut it down", HeadphoneJack → "Headphones were plugged in".

- [ ] **Step 1: Write the tests** (times formatted `h:mm tt`, e.g. 15:41 → "3:41 PM", invariant `en-US`):
  - `Off_Ready`: Title "CafeGuard is OFF", Subtitle "Your laptop is not guarded", Gray, Button "🔒 Turn ON — going to bathroom", Primary, enabled, chips in order Charger "Plugged in ✓" / Sound "Speakers ✓" / Lid protection "Ready ✓" / Lock-screen power button "Hidden ✓", ShowArmOnLockRow true, FooterEnabled true, AlarmBarText null.
  - `Off_ChargerUnplugged`: Subtitle "Plug in your charger to turn it ON", Button "🔒 Turn ON — plug in charger first", Disabled, not enabled, Charger chip "Not plugged in ⚠" Warn.
  - `On`: Title "CafeGuard is ON", Subtitle "Guarding since 3:41 PM", Green, Button "🔓 Turn OFF — I'm back", Neutral, Charger "Watching ✓", Lid "On ✓", FooterEnabled false.
  - `Alarm`: Title "ALARM!", Subtitle "Started at 3:43 PM", Red, AlarmBarText "⚠ Charger was unplugged", Button "🔓 Turn OFF — stop the alarm", Danger, ShowChips false, ShowArmOnLockRow false, FooterEnabled false.
  - `PowerButtonVisible`: chip value "Visible — Hide", Warn, HasHideLink true.
  - `LidUnavailable`: Lid chip "Not available ⚠" Warn.
  - `NoSpeakers` (SpeakerName ""): Sound chip "No speakers found ⚠" Warn.
  - Chip labels exactly "🔌 Charger", "🔊 Sound", "💻 Lid protection", "⏻ Lock-screen power button".
- [ ] **Step 2:** Run `dotnet test windows/CafeGuard.sln --filter CardViewBuilderTests` → FAIL.
- [ ] **Step 3:** Implement `CardViewBuilder.Build` as a straight mapping from the spec §6 tables.
- [ ] **Step 4:** Run → PASS.

### Task 4: Win+L auto-guard

**Files:**
- Create: `windows/CafeGuard/AutoLockPolicy.cs`
- Modify: `windows/CafeGuard/TrayApp.cs` (session switch handling, arm source, settings)
- Test: `windows/CafeGuard.Tests/AutoLockPolicyTests.cs`

**Interfaces:**
- Consumes: `AppSettings.ArmOnLock`, `SettingsStore`, `SystemWatcher.GoingToSleep/LidClosed`, `PowerAndLockService.GetPowerStatus()`.
- Produces: `enum ArmSource { Manual, AutoLock }`; `static class AutoLockPolicy { bool ShouldArmAfterLock(bool armOnLock, GuardState state, bool lidOpen, bool sleepStartedSinceLock, bool chargerPlugged); bool ShouldDisarmOnUnlock(GuardState state); }` (unlock = OFF always, spec §6); `TrayApp.TurnOn(ArmSource source)` (manual button passes `Manual`, and only `Manual` calls `LockWorkStation`).

- [ ] **Step 1: Write the tests**
  - `Arms_WhenEnabledOffLidOpenNoSleepCharger` → true.
  - false when: armOnLock false; state On; state Alarm; lidOpen false; sleepStartedSinceLock true; chargerPlugged false (one test each).
  - `Disarms_OnUnlock` for On → true and Alarm → true; Off → false.
- [ ] **Step 2:** Run `--filter AutoLockPolicyTests` → FAIL. Implement policy → PASS.
- [ ] **Step 3: Wire into TrayApp.** On `SessionLock`: record `_lockedAt`, clear `_sleepSinceLock`; start a one-shot 2000 ms timer; on tick evaluate `ShouldArmAfterLock` with `lidOpen` from a new `SystemWatcher.IsLidOpen` (last lid notification value, default true) and `_sleepSinceLock` (set by `GoingToSleep`/`LidClosed`); if true `TurnOn(ArmSource.AutoLock)`; log the decision either way. On `SessionUnlock`: if `ShouldDisarmOnUnlock(_state)` → `TurnOff()` and log "Auto-OFF on unlock". Remove the old "still ON" reminder tip. `ArmSource` remains only to decide whether `TurnOn` calls `LockWorkStation` and for the log.
- [ ] **Step 4: Verify manually** (installed copy, armOnLock set to true in `settings.json`): Win+L with charger in → after unlock the log shows "Turned ON" then "Auto-OFF on unlock"; also manual Turn ON → unlock → "Auto-OFF on unlock"; close the lid (Windows locks/sleeps) → log shows a skip reason and no ALARM.

### Task 5: Shared WPF look + TrayCard window

**Files:**
- Create: `windows/CafeGuard.Shared/Styles.xaml`, `windows/CafeGuard.Shared/ThemeResources.cs`, `windows/CafeGuard.Shared/Shield.cs`
- Create: `windows/CafeGuard/Card/TrayCard.xaml(.cs)`, `windows/CafeGuard/Card/CardPlacement.cs`
- Modify: `windows/CafeGuard/TrayApp.cs` (remove `ContextMenuStrip`; open card on left/right click; tray icon tones)
- Test: `windows/CafeGuard.Tests/CardPlacementTests.cs`

**Interfaces:**
- Consumes: `Palette.For`, `ThemeResolver`, `CardViewBuilder`, `CardView`, `AppSettings`, `SettingsStore`.
- Produces:
  - `static class ThemeResources { void Apply(ResourceDictionary target, ThemeMode mode); }` — adds brushes under keys `Brush.Background`, `Brush.Surface`, `Brush.Line`, `Brush.Text`, `Brush.Muted`, `Brush.Green`, `Brush.GreenSoft`, `Brush.GreenText`, `Brush.Gray`, `Brush.Red`, `Brush.RedSoft`, `Brush.Warning`, `Brush.NeutralButton`, and merges `/CafeGuard.Shared;component/Styles.xaml` once. Styles (all via `DynamicResource` brushes): `CardBorder`, `PrimaryButton`, `NeutralButton`, `DangerButton`, `Chip`, `ThemeSwitch`, `ToggleSwitch` (green when on), `FooterLink`, `TitleText`, `SubtitleText`.
  - `static class Shield { Geometry CreateGeometry(double width, double height); }` — points (50%,0) (100%,18%) (92%,62%) (50%,100%) (8%,62%) (0,18%).
  - `static class CardPlacement { Point Compute(Rect workArea, Rect screenBounds, Size card, double gap = 12); }` — taskbar edge = the side where `workArea` is smaller than `screenBounds`; card goes in that edge's right/bottom-most corner (bottom → bottom-right, top → top-right, left → bottom-left, right → bottom-right), `gap` from both edges, clamped inside `workArea`.
  - `TrayCard : Window` — `void Show(CardView view, ThemeMode mode, Point position)`, `void Update(CardView view)`, `void ApplyTheme(ThemeMode mode)`; events `TurnOnClicked`, `TurnOffClicked`, `TestSirenClicked`, `ExitClicked`, `HidePowerButtonClicked`, `ThemeChosen(ThemeChoice)`, `ArmOnLockChanged(bool)`.

- [ ] **Step 1: Write the tests** `CardPlacementTests` (card 310×420, gap 12; every `Rect` below is `(x, y, width, height)`):
  - bottom taskbar: screen (0,0,1920,1080), work (0,0,1920,1032) → (1598, 600).
  - top taskbar: work (0,48,1920,1032) → (1598, 60).
  - left taskbar: work (60,0,1860,1080) → (72, 648).
  - second monitor right of the first: screen (1920,0,2560,1440), work (1920,0,2560,1392) → (4158, 960).
  - tiny work area (300×300) → result fully inside work area (x ≥ left, y ≥ top).
- [ ] **Step 2:** Run `--filter CardPlacementTests` → FAIL. Implement `CardPlacement` → PASS.
- [ ] **Step 3: Build the look.** `Styles.xaml` + `ThemeResources` + `Shield`; `TrayCard.xaml` laid out exactly like the v2 mockup (width 310, `WindowStyle=None`, `AllowsTransparency=True`, `ShowInTaskbar=False`, `Topmost=True`, 10 px corners, soft shadow; theme switch "Auto | ☀ | 🌙" top-right; arm-on-lock row "🔐 Turn ON when I lock (Win+L)" / "Unlocking always turns it OFF"; footer "🔊 Test siren" / "Exit"). Closes on `Deactivated` and Esc. Call `ElementHost.EnableModelessKeyboardInterop(card)` once after creating it (WinForms message loop hosts it).
- [ ] **Step 4: Wire TrayApp.** Delete the `ContextMenuStrip`. `NotifyIcon.MouseUp` (left or right) toggles the card at `CardPlacement.Compute(Screen.FromPoint(Cursor.Position).WorkingArea, …Bounds, card size)` — `Screen` gives physical pixels, WPF positions in DIPs: divide by the monitor's DPI scale (`VisualTreeHelper.GetDpi(card)` after the window handle exists; the owner's display is scaled). Build `CardInput` from live state each 150 ms tick while the card is visible (`OnSince`, `AlarmAt`, `Reason` stored when turning ON / alarming; `PowerButtonHidden` via `LockScreenPowerButton.IsHidden` from Task 6 — until then pass true). Theme: resolve from settings; re-apply on `SystemEvents.UserPreferenceChanged` and on `ThemeChosen` (save settings). `ArmOnLockChanged` saves settings. Tray icon tones: OFF gray `#9CA3AF`, ON green `#16A34A`, ALARM flashing red `#DC2626`/green. `TriggerAlarm` now takes `AlarmReason`.
- [ ] **Step 5: Verify manually** from the installed copy: left- and right-click open the card above the clock; all four states look like the mockup in light and dark; switching Windows theme while open re-colors it; Auto/☀/🌙 persists across restart; unplugging the charger while the card is open flips the chip and disables the button within a second.

### Task 6: Lock-screen power button + app↔installer signals

**Files:**
- Create: `windows/CafeGuard.Shared/RegistryStore.cs`, `windows/CafeGuard.Shared/LockScreenPowerButton.cs`, `windows/CafeGuard.Shared/GuardSignals.cs`, `windows/CafeGuard.Shared/Elevation.cs`
- Create (test helper): `windows/CafeGuard.Tests/InMemoryRegistryStore.cs`
- Modify: `windows/CafeGuard/TrayApp.cs` (signals, Hide link)
- Test: `windows/CafeGuard.Tests/LockScreenPowerButtonTests.cs`, `GuardSignalsTests.cs`

**Interfaces:**
- Produces:
  - `interface IRegistryStore { object? GetValue(RegistryHive hive, string keyPath, string name); void SetValue(RegistryHive hive, string keyPath, string name, object value, RegistryValueKind kind); void DeleteValue(RegistryHive hive, string keyPath, string name); void DeleteKey(RegistryHive hive, string keyPath); bool KeyExists(RegistryHive hive, string keyPath); }` + `sealed class WindowsRegistryStore : IRegistryStore` (64-bit view).
  - `static class LockScreenPowerButton { const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"; const string ValueName = "shutdownwithoutlogon"; int? ReadRaw(IRegistryStore r); bool IsHidden(IRegistryStore r); void SetHidden(IRegistryStore r, bool hidden); void Restore(IRegistryStore r, int? previous); }` — hidden ⇔ value == 0; `Restore(null)` deletes the value.
  - `static class GuardSignals { void SetGuarding(bool on); bool IsGuarding(); void RequestExit(); void ListenForExitRequest(SynchronizationContext ui, Action onExit); }` — named events `CafeGuard_GuardOn` (manual reset) and `CafeGuard_ExitRequest` (auto reset); `IsGuarding()` is false when the event doesn't exist.
  - `static class Elevation { bool RunElevated(string exePath, string arguments, int timeoutMs = 60000); }` — `Verb = "runas"`; returns false on cancel (Win32 error 1223), timeout, or non-zero exit.
- [ ] **Step 1: Write the tests**
  - `IsHidden_TrueOnlyWhenZero` (missing → false, 1 → false, 0 → true).
  - `SetHidden_True_WritesZeroDword`, `SetHidden_False_WritesOne`.
  - `Restore_Null_DeletesValue`, `Restore_Value_WritesIt`.
  - `GuardSignals_IsGuarding_FollowsSetGuarding` (use a unique event-name suffix per test via an internal `GuardSignals.NameSuffix` hook so parallel tests/real app don't collide).
- [ ] **Step 2:** Run `--filter "LockScreenPowerButtonTests|GuardSignalsTests"` → FAIL. Implement → PASS.
- [ ] **Step 3: Wire TrayApp.** `SetGuarding(true)` in `TurnOn`, `SetGuarding(false)` in `TurnOff`; `ListenForExitRequest` → exit only when OFF (otherwise ignore and log). Card `HidePowerButtonClicked` → `Elevation.RunElevated(Path.Combine(AppPaths.InstallDir, AppPaths.SetupExeName), "--set-lockscreen-power hide")`, then refresh the card. `CardInput.PowerButtonHidden` = `LockScreenPowerButton.IsHidden(new WindowsRegistryStore())` (read once per card open + after Hide).

### Task 7: Installer engine

**Files:**
- Create: `windows/CafeGuard.Setup/CafeGuard.Setup.csproj` (WinExe, WPF, `AssemblyName` `CafeGuard-Setup`, references Shared), `windows/CafeGuard.Setup/Engine/InstallerEngine.cs`, `Engine/InstallEnvironment.cs`, `Engine/InstallerState.cs`
- Test: `windows/CafeGuard.Tests/InstallerEngineTests.cs` (+ `FakeInstallEnvironment`)

**Interfaces:**
- Consumes: `IRegistryStore`, `LockScreenPowerButton`, `AppPaths`, `GuardSignals`, `Elevation`.
- Produces:
  - `sealed record InstallOptions(bool StartWithWindows = true, bool HidePowerButton = true, bool DesktopShortcut = false)`
  - `enum InstallStep { CloseRunning, CopyFiles, StartMenu, RegisterApp, HidePowerButton, StartWithWindows, RemoveFiles, RestoreSettings }`, `enum StepResult { Done, Skipped, Failed }`
  - `sealed record InstallResult(bool Success, string? BlockedMessage, IReadOnlyDictionary<InstallStep, StepResult> Steps)`
  - `interface IInstallEnvironment { IRegistryStore Registry { get; } string InstallDir { get; } string DataDir { get; } string StartMenuDir { get; } string DesktopDir { get; } string SetupExePath { get; } bool IsGuarding(); bool CloseRunningApp(); bool RunElevated(string arguments); Stream OpenPayload(); void CreateShortcut(string lnkPath, string targetPath, string workingDir); bool PowerSettingsBackupExists(); void RestorePowerSettingsBackup(); }`
  - `sealed record InstallerState(bool PowerButtonChangedByUs, int? PreviousPowerValue, string Version)` saved as `installer-state.json` in `DataDir`.
  - `sealed class InstallerEngine(IInstallEnvironment env) { bool IsInstalled(); InstallOptions CurrentOptions(); InstallResult Install(InstallOptions options, IProgress<(InstallStep Step, StepResult Result)>? progress = null); InstallResult Uninstall(IProgress<(InstallStep, StepResult)>? progress = null); }`
  - Blocked message exactly `"Turn CafeGuard OFF first, then click Try again."`
  - Registry (HKCU): `Software\Microsoft\Windows\CurrentVersion\Uninstall\CafeGuard` → `DisplayName`="CafeGuard", `DisplayVersion`="1.0.0", `Publisher`="CafeGuard", `DisplayIcon`=`<InstallDir>\CafeGuard.exe`, `InstallLocation`=`<InstallDir>`, `UninstallString`=`"<InstallDir>\CafeGuard-Setup.exe" --uninstall`, `NoModify`=1, `NoRepair`=1 (DWORD). `Software\Microsoft\Windows\CurrentVersion\Run` → `CafeGuard`=`"<InstallDir>\CafeGuard.exe"`.
  - Elevated args: `--set-lockscreen-power hide`, `--set-lockscreen-power restore:<n>`, `--set-lockscreen-power restore:none`.
- [ ] **Step 1: Write the tests** (fake env: in-memory registry, dirs under `AppContext.BaseDirectory/<guid>`, payload = 3 bytes, recorded shortcut calls, scripted `IsGuarding`/`RunElevated` results):
  - `Install_Fresh_CopiesBothExes_RegistersApp_StartMenuShortcut` — `CafeGuard.exe` bytes equal payload; `CafeGuard-Setup.exe` copied; all Uninstall values above; Start menu `CafeGuard.lnk` created; no desktop shortcut by default.
  - `Install_StartWithWindows_WritesRunValue`; `Install_StartWithWindowsOff_RemovesRunValue`.
  - `Install_HidePowerButton_SavesPreviousValueAndElevates` — previous raw 1 → state `PowerButtonChangedByUs` true, `PreviousPowerValue` 1, elevated args `--set-lockscreen-power hide`.
  - `Install_AlreadyHidden_DoesNotElevate` — raw 0 → step Skipped, `PowerButtonChangedByUs` false.
  - **Review focus:** `Install_UacDeclined_StillSucceeds` — RunElevated false → Success true, HidePowerButton Skipped, `PowerButtonChangedByUs` false.
  - **Review focus:** `Install_WhileGuarding_IsBlocked` — Success false, BlockedMessage exact, no files written.
  - `Uninstall_RemovesEverything` — Uninstall key, Run value, shortcuts, `DataDir` gone; install dir removal scheduled (engine deletes `CafeGuard.exe`; setup exe self-delete is the UI's job).
  - `Uninstall_RestoresPowerButton_OnlyIfChangedByUs` — state changed=true prev=1 → elevated `restore:1`; prev null → `restore:none`.
  - **Review focus:** `Uninstall_PowerButtonWasAlreadyHidden_LeavesIt` — state changed=false → no elevation.
  - `Uninstall_RestoresLidBackup_IfPresent`; **Review focus:** `Uninstall_WhileGuarding_IsBlocked`.
  - `IsInstalled_TrueWhenUninstallKeyAndExeExist`.
  - `CurrentOptions_ReflectsExistingInstall` — Run value present + desktop `CafeGuard.lnk` present → `InstallOptions(StartWithWindows: true, HidePowerButton: false, DesktopShortcut: true)` (update never re-asks about the power button; the existing state file is kept).
- [ ] **Step 2:** Run `--filter InstallerEngineTests` → FAIL.
- [ ] **Step 3:** Implement `InstallerEngine` (step order per spec §7 screen 2) and the real `InstallEnvironment` (paths from `AppPaths`/`Environment.SpecialFolder.Programs`/`DesktopDirectory`; `CloseRunningApp` = `GuardSignals.RequestExit()` then wait 5 s for `CafeGuard` processes, kill remaining only if `!IsGuarding()`; shortcuts via `WScript.Shell` COM; `OpenPayload` = embedded resource `CafeGuard.exe`; `RunElevated` = `Elevation.RunElevated(SetupExePath-in-InstallDir, args)`; lid backup via `LidAndButtonGuard`-compatible file in `DataDir` — restore by launching installed `CafeGuard.exe --restore-power-settings`, add that arg to the app's `Program`).
- [ ] **Step 4:** Run → PASS.

### Task 8: Installer UI, packaging, docs

**Files:**
- Create: `windows/CafeGuard.Setup/App.xaml(.cs)`, `SetupWindow.xaml(.cs)`, `Program.cs` (arg routing)
- Create: `tools/make-icon.ps1` → `windows/CafeGuard.Shared/cafeguard.ico` (used as `ApplicationIcon` by both exes)
- Modify: `Build-Installer.bat`, `README.md`, `windows/CafeGuard.Setup/CafeGuard.Setup.csproj` (`<EmbeddedResource Include="..\..\dist\payload\CafeGuard.exe" LogicalName="CafeGuard.exe" />`)

**Interfaces:**
- Consumes: `InstallerEngine`, `InstallEnvironment`, `ThemeResources`, `Shield`, `SettingsStore`, `ThemeResolver`, `LockScreenPowerButton`, `WindowsRegistryStore`.
- Produces: `CafeGuard-Setup.exe` modes — no args: install/update/remove UI; `--uninstall`: remove UI; `--set-lockscreen-power hide|show|restore:<n>|restore:none`: writes HKLM via `LockScreenPowerButton`, exit 0/1, no UI.

- [ ] **Step 1: Write the test** `SetupArgsTests.Parse` for a `SetupArgs.Parse(string[] args)` record (`Mode` = Ui/Uninstall/SetPower, `PowerAction` = Hide/Show/Restore, `RestoreValue` int?): `[]`→Ui; `["--uninstall"]`→Uninstall; `["--set-lockscreen-power","hide"]`→SetPower/Hide; `["--set-lockscreen-power","restore:1"]`→Restore/1; `["--set-lockscreen-power","restore:none"]`→Restore/null; unknown → Ui. Run → FAIL; implement → PASS.
- [ ] **Step 2: Build the screens** in `SetupWindow` exactly per mockup v2 / spec §7 (460 px wide, same `ThemeResources`; theme = saved `settings.json` choice, else Auto): Welcome & options (3 checkboxes, "Installs for you only · no restart needed", green Install) → Installing (progress bar + step list ✓/●/○, mapped from `IProgress`, run on a background task) → Done ("CafeGuard is ready", tray hint, ☑ Start CafeGuard now, Finish). Already-installed start screen with "Update to this version" / "Remove CafeGuard" + Continue; Update calls `Install(engine.CurrentOptions())`. Blocked → show the exact blocked message with a "Try again" button. Remove done → "CafeGuard was removed." then self-delete the setup exe and install dir via `cmd /c ping 127.0.0.1 -n 3 >nul & rmdir /s /q "<InstallDir>"` started hidden on exit.
- [ ] **Step 3:** `tools/make-icon.ps1` draws the green shield (16/32/48/256) into `cafeguard.ico`; run it; set `ApplicationIcon` in both exe projects.
- [ ] **Step 4:** Extend `Build-Installer.bat`: label `dist` Medium → publish app to `dist\payload` → publish Setup to `dist` → delete `windows\*\bin` and `obj`. Update README (install = run `dist\CafeGuard-Setup.exe`; card; Win+L option; remove via Settings → Apps).
- [ ] **Step 5: Verify** `Build-Installer.bat` ends with `dist\CafeGuard-Setup.exe`; `dotnet test windows/CafeGuard.sln` all green; then the owner runs the manual checklist (spec §9): install (UAC yes) → shield near clock → card light/dark → Test siren → Turn ON → unplug → alarm → Turn OFF → Win+L option → run Setup again → Remove → Settings → Apps no longer lists CafeGuard, `shutdownwithoutlogon` back to 1.
