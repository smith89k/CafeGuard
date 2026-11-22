using System;

namespace CafeGuard.Shared
{
    public enum HideResult { AlreadyHidden, Hidden, Declined }

    /// <summary>
    /// Hides the lock-screen power button and records the old value, so removing CafeGuard can put it back.
    /// Used by the installer and the tray card's "Hide" link. Runs in the normal (not elevated) process,
    /// so the record lands in this user's folder.
    /// </summary>
    public static class PowerButtonHider
    {
        /// <param name="elevate">Runs the admin helper that actually hides it; true = done.</param>
        public static HideResult Hide(IRegistryStore registry, string stateFile, Func<bool> elevate)
        {
            int? previous = LockScreenPowerButton.ReadRaw(registry);
            if (previous == 0) return HideResult.AlreadyHidden; // hidden by someone else: not ours to restore

            var existing = InstallerState.Load(stateFile);
            bool alreadyOurs = existing?.PowerButtonChangedByUs == true;

            // Save the record FIRST (spec §7): if anything dies mid-way, uninstall still knows what to put back.
            if (!alreadyOurs)
                new InstallerState(true, previous, AppPaths.Version).Save(stateFile);

            if (elevate()) return HideResult.Hidden;

            // Permission refused: nothing changed, so take the record back.
            if (!alreadyOurs)
            {
                if (existing != null) existing.Save(stateFile);
                else new InstallerState(false, null, AppPaths.Version).Save(stateFile);
            }
            return HideResult.Declined;
        }

        /// <summary>Hidden, and by CafeGuard (so CafeGuard may put it back)?</summary>
        public static bool IsHiddenByUs(IRegistryStore registry, string stateFile) =>
            LockScreenPowerButton.IsHidden(registry) && InstallerState.Load(stateFile)?.PowerButtonChangedByUs == true;

        /// <summary>
        /// The card's "Show" link: puts back the value CafeGuard recorded before hiding it. Needed because the
        /// Microsoft Store version can't run anything when it is uninstalled.
        /// </summary>
        /// <param name="elevate">Runs the admin helper with this command; true = done.</param>
        public static ShowResult Show(IRegistryStore registry, string stateFile, Func<LockScreenPowerCommand, bool> elevate)
        {
            if (!IsHiddenByUs(registry, stateFile)) return ShowResult.NotOurs;

            var record = InstallerState.Load(stateFile)!;
            if (!elevate(new LockScreenPowerCommand(PowerAction.Restore, record.PreviousPowerValue)))
                return ShowResult.Declined; // nothing changed: keep the record so it can still be put back later

            new InstallerState(false, null, AppPaths.Version).Save(stateFile);
            return ShowResult.Shown;
        }
    }

    public enum ShowResult { NotOurs, Shown, Declined }
}
