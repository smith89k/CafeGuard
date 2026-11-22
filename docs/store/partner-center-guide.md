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

> CafeGuard is a classic Windows desktop app (WinForms/WPF). It shows a notification-area (tray) icon and reads the charger/battery state. To sound the alarm, it sets the system volume through Core Audio. While the guard is ON, it temporarily sets the lid-close and power-button actions to "Do nothing" and blocks shutdown, so a thief can't silence the alarm. These settings are restored when the guard is turned OFF.

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

## 4. If Microsoft says no to `allowElevation`

That is the most likely reason for a rejection. To resubmit without it:

1. In `windows\CafeGuard.Store\AppxManifest.template.xml`, delete the line `<rescap:Capability Name="allowElevation" />`.
2. Raise the version in `windows\CafeGuard.Shared\AppPaths.cs`, for example from 1.0.0 to 1.0.1.
3. Run Build-Store.bat again and upload the new file.

Everything else keeps working. Only the "Hide" link will no longer be able to hide the button.

## 5. Updates later

Raise the version in `AppPaths.cs`, run Build-Store.bat, and in Partner Center create a **new submission** with the new .msix file.
