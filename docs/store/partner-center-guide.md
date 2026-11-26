# Putting CafeGuard in the Microsoft Store

Everything here is done by you, in your own Microsoft account. The build files are ready.

## 1. One-time setup (about 15 minutes)

1. Open **Partner Center** (partner.microsoft.com/dashboard) and sign up as an **individual** Windows app developer. For individuals it's free.
2. Choose **Apps and games → New product → MSIX or PWA app**, and reserve the name **CafeGuard**. If it's taken, try "CafeGuard Laptop Alarm".
3. Open the new app, then **Product management → Product identity**. Copy these three values into `windows\CafeGuard.Store\store-identity.json`:

   | Partner Center shows        | Put it in            |
   |-----------------------------|----------------------|
   | Package/Identity/Name       | `identityName`         |
   | Package/Identity/Publisher  | `publisher` (starts with `CN=`) |
   | Package/Properties/PublisherDisplayName | `publisherDisplayName` |

4. Double-click **Build-Store.bat**. Upload this file: `dist\store\CafeGuard_1.0.0.0_x64.msix`. You don't need to sign it, because the Store signs it for you.

## 2. The submission pages

| Page | What to choose |
|---|---|
| Pricing and availability | Free, all markets |
| Properties | Category: **Security**. Privacy policy: not needed, because CafeGuard collects no data and never uses the internet. Add the text below if Microsoft asks for it anyway. |
| Age ratings | Answer "no" to everything, which gives the rating "Everyone". |
| Packages | Upload the .msix file. |
| Store listing | Use the description below. You need at least 1 screenshot: open the card from the tray and press Win+Shift+S. |
| Submission options | Paste the two explanations below under **restricted capabilities**. |

## 3. Text to paste

### Why CafeGuard needs `runFullTrust`

> CafeGuard is a classic Windows desktop app (WinForms/WPF) and an anti-theft alarm for laptops. It needs full trust for these desktop features, which have no sandboxed equivalent:
>
> - A notification-area (tray) icon, which is how the user turns the guard ON and OFF.
> - Reading the charger, lid and sleep/shutdown events, so it knows when someone tampers with the laptop.
> - Setting the system volume and the default speaker through Core Audio, so the siren plays at full volume and plugged-in headphones can't silence it.
> - While the guard is ON, temporarily setting the lid-close and power-button actions to "Do nothing" and blocking shutdown, so a thief can't silence the alarm.
> - Noticing when Task Manager opens, and refusing a normal "End task" on its own process only. An administrator can still end it.
> - Asking for Windows Hello (PIN, fingerprint or face) before the guard turns OFF.
>
> All of these settings are put back when the guard is turned OFF. CafeGuard uses no internet connection and collects no data.

### Why CafeGuard needs `allowElevation`

> CafeGuard is an anti-theft alarm for people working alone in public places such as cafés. While it guards, Windows shows the lock screen. The power button on the lock screen lets anyone shut the laptop down without a password, which silences the alarm. CafeGuard therefore offers one optional link, "Hide". It sets the standard Windows policy value HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\shutdownwithoutlogon to 0. Writing this machine-wide policy requires administrator rights.
>
> When the user clicks the link, CafeGuard starts one copy of itself with the "runas" verb, so Windows shows the UAC consent prompt every time. Nothing changes unless the user approves. The elevated process writes only that single value and exits immediately. CafeGuard never elevates at launch, never elevates silently, and never elevates for anything else.
>
> CafeGuard records the previous value before changing it. A "Show" link in the app puts it back, again with a UAC prompt. The feature cannot work without this capability, because the setting is machine-wide policy.

### Store description

> **Going to the bathroom at the café? CafeGuard watches your laptop.**
>
> Turn CafeGuard ON from the shield icon near the clock and walk away. If someone unplugs the charger, closes the lid, or tries to put the laptop to sleep or shut it down, CafeGuard sounds a loud siren at full volume. The siren also sounds if they plug headphones in to silence it. When you come back, unlock Windows and the guard turns OFF.
>
> • One click to turn ON and OFF
> • Optional: turn ON automatically when you lock your laptop (Win+L)
> • Optional: hide the lock-screen power button, so nobody can shut down without your password
> • Light and dark look
> • No account, no internet, no data collected
>
> Tip: if you hid the lock-screen power button, click "Show" in CafeGuard before you uninstall it. Windows doesn't let Store apps undo changes when they're uninstalled.

### Privacy statement (only if asked)

> CafeGuard does not collect, store, or send any personal information. It does not connect to the internet. Its settings and log stay on your computer.

## 4a. Asking Microsoft again for `allowElevation`

Microsoft denied it on 2026-11-26 (report: "10.6.3 Capabilities") and asked for new, longer reasons. The restricted-capabilities box only takes 500 characters, so the short text goes there and the long one goes in **Additional Testing Information** (Notes for certification).

Short text (restricted capabilities box):

```text
Reconsideration, new info in Notes for certification. Anti-theft alarm: the lock-screen power button lets a thief shut down a locked laptop and silence it. Windows only hides it via HKLM policy shutdownwithoutlogon; no per-user setting or API exists. Elevates only when the user clicks optional Hide/Show, UAC each time; writes or restores only that value, then exits. We assure it is used only for this and writes only that path. Support: https://github.com/smith89k/CafeGuard/issues
```

Long text (Additional Testing Information):

```text
Cafe Guard - request to reconsider the restricted capability allowElevation (new and expanded information)

1. WHY THE PRODUCT NEEDS IT
Cafe Guard is an anti-theft alarm for people working alone in a cafe who step away from their laptop for a moment. While the guard is ON, the laptop is locked. The Windows lock screen shows a power button that lets anyone shut the laptop down without a password. Shutting down ends the alarm at once, which defeats the whole product. Hiding that button is the most important extra protection a user can add.

What we explored before choosing elevation:
- A per-user setting (HKCU): none exists. Windows reads this only from the machine-wide policy value HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\shutdownwithoutlogon.
- A Windows API or a Settings page: none exists for hiding the lock-screen power button.
- Blocking shutdown from inside the app: Cafe Guard already asks Windows to block shutdown while the guard is ON, but that request can be overridden, and the lock-screen button still gives a stranger a one-click way to try.
- Asking users to change Group Policy themselves: the Group Policy Editor is not available on Windows Home, and editing the registry by hand is risky for non-technical users.
Writing this one policy value is the only supported way, and it requires administrator rights.

2. THE EXACT FEATURE THAT USES IT
On the app's tray card there is a row "Lock-screen power button: Visible - Hide". The "Hide" link is optional and off by default. Everything else in the app works without it.

3. LEAST PRIVILEGE
- The app never runs elevated at launch. It always runs as a normal user.
- Elevation happens only when the user clicks "Hide" or "Show". The app uses ShellExecute with "runas", so Windows shows the UAC prompt every single time. If the user clicks No, nothing changes.
- The elevated process is Cafe Guard itself, started with one fixed command: --set-lockscreen-power hide | show | restore:N | restore:none. It shows no window, writes or restores that single value, and exits immediately.
- It touches no other registry key, file, service, driver or setting.

4. SAFEGUARDS AGAINST MISUSE OR HARM
- Before hiding the button, the app saves the previous value in the user's own app data folder. "Show" puts back exactly that value, or removes the value if it did not exist before.
- If the button was already hidden by someone else (for example an IT administrator), the app does not change it and never offers to undo it.
- The Store description tells users to click "Show" before uninstalling, because MSIX apps cannot run code on uninstall.
- No network access, no data collected, no background service.
- The full source code is public, so anyone can check this: https://github.com/smith89k/CafeGuard

5. SUPPORT CONTACT / DEVELOPER WEBSITE
https://github.com/smith89k/CafeGuard (support: https://github.com/smith89k/CafeGuard/issues)

6. STATEMENT OF ASSURANCE
We confirm that allowElevation is used only for the purpose described above, and that the elevated process writes only to HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System, value shutdownwithoutlogon, and nowhere else.

HOW TO TEST
Click the shield icon near the clock to open the card. Next to "Lock-screen power button", click "Hide" and approve the UAC prompt. Press Win+L: the power button is gone from the lock screen. Unlock, click "Show", approve UAC, and the button is back.
```

## 4. If Microsoft says no to `allowElevation`

That is the most likely reason for a rejection. To resubmit without it:

1. In `windows\CafeGuard.Store\AppxManifest.template.xml`, delete the line `<rescap:Capability Name="allowElevation" />`.
2. Raise the version in `windows\CafeGuard.Shared\AppPaths.cs`, for example from 1.0.0 to 1.0.1.
3. Run Build-Store.bat again and upload the new file.

Everything else keeps working. Only the "Hide" link will no longer be able to hide the button.

## 5. Updates later

Raise the version in `AppPaths.cs`, run Build-Store.bat, and in Partner Center create a **new submission** with the new .msix file.
