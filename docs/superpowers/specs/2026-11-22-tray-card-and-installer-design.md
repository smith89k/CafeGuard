# CafeGuard — Tray Card & Installer Design

Date: 2026-11-22 · **Status: approved by owner**
Visual reference: `.superpowers/design-preview-v2.html` (open in a browser). The mockups there are the source of truth for layout, wording and colors.

## 1. Intent

CafeGuard is a laptop anti-theft alarm for one person working alone in a café: before going to the bathroom they turn it ON, the laptop locks, and unplugging the charger / closing the lid / tampering sets off a 100% siren.

The owner wants it to feel like a polished, real app:
- a **nicer tray pop-up** (replacing today's plain right-click menu), and
- a **nice installer**,
- both with **one consistent look**, **green as the main color**, and **light/dark mode**.

The owner explicitly does **not** want a control-panel window. Everything lives in the tray pop-up.

## 2. Goals, non-goals, anti-goals

**Goals**
- Tray pop-up card ("design B"): one big Turn ON / Turn OFF button, a "ready to leave?" checklist, Test siren and Exit.
- Installer program `CafeGuard-Setup.exe`: welcome & options → installing → done; update and remove when already installed.
- Same visual system in both (colors, fonts, corner radius, shield logo, buttons).
- Light/dark: a three-way switch in the card (**Auto | ☀ | 🌙**, default Auto = follow Windows, live). The installer uses the same saved choice.
- Optional **"🔐 Turn ON when I lock (Win+L)"** mode (switch in the card, default off): locking turns the guard ON.
- **Unlock = OFF, always:** unlocking Windows turns the guard OFF (and stops an ALARM), whether it was turned ON by hand or by Win+L. Only the owner can unlock, so this is safe.
- Installer can hide the lock-screen power button (closes the "shut down from lock screen" hole).
- Uninstall puts back every Windows setting CafeGuard changed.

**Non-goals**
- No control-panel / settings window. No PIN.
- No changes to the Mac version.
- No code signing (SmartScreen "More info → Run anyway" stays, once per new file).
- No changes to how detection works (charger, lid, sleep, shutdown, headphone jack, earbuds behavior stay as they are).

**Anti-goals (would be a failure even if it "works")**
- Anything that makes turning ON slower than today (open card → one click).
- A card that is confusing about whether the laptop is protected right now.
- An installer that needs administrator rights when the power-button option is unticked.
- Leaving the user's Windows settings changed after uninstall.
- Running CafeGuard from the project folder (it carries a Low integrity label → no tray icon). The installed copy must live in `%LOCALAPPDATA%\Programs\CafeGuard`.

## 3. Constraints

- Windows 11 (owner's build 26300), MacBook Pro 16,1 hardware with Boot Camp drivers. Trend Micro Maximum Security is installed: first launch of a brand-new exe is sometimes "Access is denied" once while it scans.
- .NET 8 Desktop Runtime is installed (WPF and WinForms both available). Builds are framework-dependent single-file exes (`win-x64`).
- The project folder `c:\_httdocs\CafeGuard` has an inherited **Low** mandatory label. Anything built there must be installed/copied elsewhere before running.
- Existing single-instance logic (mutex + "show yourself" signal), log at `%LOCALAPPDATA%\CafeGuard\cafeguard.log`, lid/button guard with backup file, crash recovery — all keep working.
- Hiding the lock-screen power button = `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\shutdownwithoutlogon = 0` → needs administrator rights.

## 4. Approaches considered

**Tray card**
1. **WPF window styled as a flyout (chosen).** Built into .NET 8, nothing to install. Good anti-aliased rounded corners, shadows, theming via resource dictionaries, fast to open. Tray icon itself stays a WinForms `NotifyIcon`.
2. Hand-drawn WinForms popup. Same tech as today, but rounded corners/text never look as polished.
3. WebView2 (HTML) card. Easiest to style, but slower to open and ~3× memory for an always-running tray app.

**Installer**
1. **Separate `CafeGuard-Setup.exe` with the app embedded (chosen).** One file to keep or share, same WPF look as the card, per-user install.
2. Inno Setup. Reliable but generic old-style wizard; owner wants "nice" and consistent.
3. Self-installing `CafeGuard.exe`. One file fewer but confusing (behaves differently depending on location).

The owner handed over these technical choices; the picks above are the builder's calls, stated to the owner.

## 5. Visual system (shared by card and installer)

| Token | Light | Dark |
|---|---|---|
| Background | `#FFFFFF` | `#202020` |
| Surface (chips, bars) | `#F3F4F6` | `#2B2B2B` |
| Line | `#E5E7EB` | `#3A3A3A` |
| Text / muted | `#111827` / `#6B7280` | `#F3F4F6` / `#A3A3A3` |
| **Green (main)** | `#16A34A` | `#22C55E` |
| Green soft / green text | `#DCFCE7` / `#15803D` | `#14532D` / `#86EFAC` |
| Gray (OFF shield) | `#9CA3AF` | `#737373` |
| Red / red soft | `#DC2626` / `#FEE2E2` | `#EF4444` / `#450A0A` |
| Warning text | `#B45309` | `#FBBF24` |
| Neutral button | `#E5E7EB` | `#3A3A3A` |

- Font: Segoe UI Variable (fallback Segoe UI). Title 16 px bold (card), 22 px bold (installer hero).
- Corner radius: 10 px windows/cards, 8 px main buttons, 6 px chips and installer buttons.
- Shield logo: same 6-point shield shape everywhere. **Gray = OFF, green = ON, red = ALARM.** Installer logo is green.
- Theme choice: `Auto` / `Light` / `Dark`, saved in `%LOCALAPPDATA%\CafeGuard\settings.json` (`"theme"`). Auto reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (1 light / 0 dark) and re-reads it on `SystemEvents.UserPreferenceChanged`.
- Installer theme: the saved choice if `settings.json` exists, otherwise Auto. No theme switch on the installer.
- Shared code: theme dictionaries, shield drawing and button styles live in one shared folder compiled into both programs, so they cannot drift apart.

## 6. Tray card

**Opening/closing**
- Left-click **or** right-click the tray shield toggles the card.
- Card appears above the notification area, right-aligned to the work area of the screen the taskbar is on, with a ~12 px gap (respect taskbar on other edges: if the taskbar is top/left/right, anchor to that corner instead).
- Closes when it loses focus, on Esc, or after an action that locks the screen.
- Always on top while open; not shown in the taskbar or Alt+Tab.

**Layout** (width ~310 px), top to bottom — see mockups:
1. Shield + title + subtitle; theme switch **Auto | ☀ | 🌙** in the top-right corner (all states).
2. Big button.
3. Checklist chips (not shown in ALARM state).
4. Switch row: **"🔐 Turn ON when I lock (Win+L)"** with small text "Unlocking always turns it OFF" (not shown in ALARM state).
5. Footer: "🔊 Test siren" (left), "Exit" (right).

**States**

| State | Shield | Title / subtitle | Big button | Footer |
|---|---|---|---|---|
| OFF, ready | gray | "CafeGuard is OFF" / "Your laptop is not guarded" | green **"🔒 Turn ON — going to bathroom"** | enabled |
| OFF, charger unplugged | gray | "CafeGuard is OFF" / "Plug in your charger to turn it ON" | disabled **"🔒 Turn ON — plug in charger first"** | enabled |
| ON | green | "CafeGuard is ON" / "Guarding since 3:41 PM" | neutral **"🔓 Turn OFF — I'm back"** | dimmed/disabled |
| ALARM | red (card border red) | "ALARM!" / "Started at 3:43 PM" + red bar with the reason (e.g. "⚠ Charger was unplugged") | red **"🔓 Turn OFF — stop the alarm"** | dimmed/disabled |

The card refreshes live while open (charger plug/unplug updates the chip and button immediately).

**Checklist chips**

| Chip | OK text | Problem text |
|---|---|---|
| 🔌 Charger | OFF: "Plugged in ✓" · ON: "Watching ✓" | "Not plugged in ⚠" |
| 🔊 Sound | "Speakers ✓" (tooltip: device name) | "Muted — will be unmuted ✓" is still OK (alarm forces volume); only show ⚠ "No speakers found" if there is no output device |
| 💻 Lid protection | OFF: "Ready ✓" · ON: "On ✓" | "Not available ⚠" (when the last Apply failed) |
| ⏻ Lock-screen power button | "Hidden ✓" | "Visible — **Hide**" — clicking **Hide** asks for admin (UAC) and hides it |

**Tray icon**: gray shield OFF, green ON, red/green flashing during ALARM. Tooltip "CafeGuard: OFF/ON/ALARM!".

**Auto-lock mode ("🔐 Turn ON when I lock")** — saved in `settings.json` (`"armOnLock"`, default `false`):
- On every Windows lock (`SessionSwitchReason.SessionLock`, including Win+L and idle auto-lock) while OFF: wait ~2 s, then turn ON **only if** the lid is still open, no sleep/hibernate started in that window, and the charger is plugged in. Do **not** call LockWorkStation again (already locked). Skip silently otherwise (log the reason).
- Locks caused by closing the lid or going to sleep must never arm (otherwise putting the laptop in a bag would sound the alarm).
- On unlock (`SessionUnlock`): **always** turn the guard OFF if it is ON or ALARM — regardless of how it was turned ON (owner decision 2026-11-22: "unlock = off"). Replaces the old "still ON, right-click to turn OFF" reminder.
- The manual big button keeps working in both modes.

**Behavior unchanged**: Turn ON still locks the screen immediately and applies lid/button guard; Turn OFF restores; Exit only allowed while OFF; balloon tips for "already running" stay (restyle not required — they are Windows toasts).

## 7. Installer — `CafeGuard-Setup.exe`

Single exe, WPF, same visual system. Contains `CafeGuard.exe` as an embedded resource. Runs as the normal user; elevates only for the power-button step.

**Screen 1 — Welcome & options** (fresh install)
- Green shield, "CafeGuard", one-line description.
- Options:
  - ☑ Start CafeGuard when Windows starts — `HKCU\...\Run\CafeGuard`.
  - ☑ Hide the power button on the lock screen **(Recommended)** — "Windows will ask for permission."
  - ☐ Add a shortcut to the desktop.
- Footer: "Installs for you only · no restart needed" + green **Install**.

**Screen 2 — Installing** (progress bar + step list with ✓ / ● / ○)
1. Close a running CafeGuard if it is OFF (ask it to exit via the existing single-instance channel; fall back to killing it). If it is **ON or ALARM, stop**: "Turn CafeGuard OFF first, then click Try again."
2. Copy `CafeGuard.exe` and the setup exe itself (as the uninstaller) to `%LOCALAPPDATA%\Programs\CafeGuard\`.
3. Start menu shortcut (always); desktop shortcut (if ticked).
4. Register in **Settings → Apps**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\CafeGuard` (DisplayName, DisplayIcon, DisplayVersion, Publisher "CafeGuard", InstallLocation, UninstallString = `"…\CafeGuard-Setup.exe" --uninstall`, NoModify/NoRepair = 1).
5. If ticked: hide lock-screen power button by re-launching the setup exe elevated with `--set-lockscreen-power hide`. Save the previous value first (in `%LOCALAPPDATA%\CafeGuard\installer-state.json`) so uninstall can put it back. If the user says No to the UAC prompt: continue, mark this step "Skipped — you can hide it later from the tray card".
6. If ticked: startup entry.

**Screen 3 — Done**
- Big green check, "CafeGuard is ready", hint pointing at the shield near the clock.
- ☑ Start CafeGuard now + green **Finish**.

**Already installed** → first screen becomes "CafeGuard is installed — what would you like to do?": ◉ Update to this version (keeps settings) / ○ Remove CafeGuard. Then the same progress/done screens.

**Remove (from Setup or Settings → Apps)**
- Refuses while CafeGuard is ON/ALARM (same message as above).
- Closes CafeGuard, restores the lid/button backup if present, puts the lock-screen power button back to its saved previous value (elevated, only if CafeGuard changed it), removes startup entry, shortcuts, Apps registration, `%LOCALAPPDATA%\CafeGuard\` and the program folder (the running uninstaller deletes itself after exit via a short delayed cmd).
- Done screen: "CafeGuard was removed."

**Elevated helper modes** (in the setup exe): `--set-lockscreen-power hide|show|restore:<value>` — writes the HKLM value and exits with 0/1. The tray card's "Hide" link calls the **installed** setup exe with `--set-lockscreen-power hide` (UAC prompt).

## 8. Project layout after the change

```
CafeGuard/
  Build-Installer.bat        builds both programs → dist\CafeGuard-Setup.exe
  README.md
  dist/                      CafeGuard-Setup.exe (the one file to run/share)
  windows/
    CafeGuard/               tray app (existing code moves here)
    CafeGuard.Setup/         installer
    Shared/                  theme colors, shield, button styles (linked into both)
  macos/  archive/  docs/
```
`Launch-CafeGuard.bat` and `app/` are replaced by `Build-Installer.bat` and `dist/`.

## 9. Testing

- Existing `--self-test` and `--test-lid-guard` keep passing (run from the installed copy).
- Installer: fresh install, update while OFF, update refused while ON, remove (check every registry value/file is gone and the power-button value is back), UAC "No" path.
- Card: all states in light and dark; theme switch while open; charger unplug while card is open; Hide link.
- Manual checklist for the owner: install → shield appears near clock → Test siren → Turn ON → unplug charger → alarm → Turn OFF → remove from Settings → Apps.

## 10. Left to the builder

- Exact pixel spacing, animation (a short fade/slide-up like Windows flyouts is welcome but optional).
- Icon file (.ico) generation for exe/shortcuts from the shared shield drawing.
- How the setup exe embeds/extracts the app (embedded resource is the expectation).
- Version numbering (start at 1.0.0).
