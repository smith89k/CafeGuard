# 🛡️ CafeGuard — Laptop Anti-Theft Alarm for Coffee Shops

Alone at a café and need the bathroom? Turn Cafe Guard **ON** (or press **Win+L** if you switched that on).
If anyone **unplugs the charger**, **closes the lid**, tries to **sleep / shut down**, or **plugs headphones into the jack**,
a loud siren plays at **100% volume** (mute and volume keys don't work).
When you come back, click **Turn OFF** — Windows asks for your PIN or fingerprint, so only you can stop it. (If you locked the screen, unlocking turns it OFF.)

---

## Install (Windows)

Get **CafeGuard** from the **Microsoft Store** (Store ID `9N8FHQ4X67LN`, once Microsoft has approved it).
It starts with Windows by itself (switch that off in **Settings → Apps → Startup**). Look for the shield near the clock;
the first time, click **^** and drag the shield onto the taskbar so it's always one click away.

Before it's in the Store: run `Build-Store.bat`, then `Store-Test.bat install` (needs Developer Mode).
Run `Store-Test.bat remove` before installing the real one from the Store.

To remove: **Settings → Apps → CafeGuard → Uninstall**. If CafeGuard hid the lock-screen power button,
click **Show** on the card first — Windows doesn't let Store apps undo changes when they are uninstalled.

## Use it

Click the shield (left or right click) to open the card:

| | |
|---|---|
| **🔒 Turn ON — going to bathroom** | Starts guarding (the screen stays open, so you see the green shield). Needs the charger plugged in. |
| **🔓 Turn OFF** | Asks Windows "Verify it's you" (PIN / fingerprint). If that can't open, the screen locks and unlocking turns it OFF. |
| **Checklist** | Charger · Sound · Lid protection · Lock-screen power button (with a **Hide** link if it's still visible, or **Show** to put it back if CafeGuard hid it). |
| **🔐 Turn ON when I lock (Win+L)** | Switch: locking Windows turns the guard ON by itself. Closing the lid or sleep never does. |
| **🔊 Alarm sound** | Siren, Voice or Both. Pick the words and a man's or woman's voice (Windows' built-in voices). Plain siren if there is no English voice. |
| **Auto · ☀ · 🌙** | Light or dark (Auto follows Windows). |
| **🔊 Test siren / Exit** | Only while OFF. |

Two colors only: **green = normal** (OFF or ON), **blinking red = ALARM**. Card icons are green when fine, red when something needs attention.

### What it does while ON
- **Charger unplugged**, **lid closed**, **sleep or shutdown attempt**, **headphones in the laptop's jack** → alarm.
- The lid, power button and sleep button are set to *Do nothing* while ON (your own settings are saved and put back).
- **Bluetooth / USB earbuds connecting by themselves** → no alarm; sound is moved back to the speakers.
- **Alarm** → the red card pops up and stays on top until you turn it OFF with your PIN.
- **Unlocking Windows** → OFF (also stops the alarm).

### Limits (no app can beat these)
- Holding the power button for ~5 seconds forces the laptop off.
- While ON, **opening Task Manager sounds the alarm**, and a normal **End task** gets "Access denied". On an administrator account Task Manager runs with full rights and can still close Cafe Guard (loudly) — lock the screen (Win+L) for the strongest protection.
- If Cafe Guard is not running, nothing is guarded.

---

## Folder layout

| Folder / file | What it is |
|---|---|
| `Build-Store.bat` | Builds `dist\store\CafeGuard_<version>_x64.msix` — the file you upload to Partner Center (needs the .NET 8 SDK and Windows SDK). |
| `Store-Test.bat install\|remove` | Puts the Store version on this laptop from that file (Developer Mode), or takes it off. |
| `windows/CafeGuard/` | The tray app. |
| `windows/CafeGuard.Shared/` | Shared look (green palette, styles, shield), paths, settings, registry helpers. |
| `windows/CafeGuard.Store/` | Store package description, pictures, and your Partner Center identity. |
| `windows/CafeGuard.Tests/` | Automatic tests — run `windows
un-tests.bat`. |
| `tools/` | Build scripts; `make-icon.ps1` / `make-store-assets.ps1` re-draw the icons. |
| `docs/store/partner-center-guide.md` | Step-by-step Store submission, with the text to paste. |
| `macos/` | Mac version (`cafeguard_mac.py`, separate). |
| `docs/superpowers/` | Design and build plans. |
| `archive/` | Old versions, for reference only. |

### Developer checks
```powershell
windows
un-tests.bat     # automatic tests
```
Log: `%LOCALAPPDATA%\CafeGuard\cafeguard.log` · Settings: `%LOCALAPPDATA%\CafeGuard\settings.json`

---

## Mac

```bash
python3 macos/cafeguard_mac.py
```
Small window with one big **Turn ON / Turn OFF** button: charger unplugged, lid closed, or headphones in the jack → siren at 100%.
Turning ON asks for your Mac password (so closing the lid doesn't sleep). Not yet tested on a real Mac.
