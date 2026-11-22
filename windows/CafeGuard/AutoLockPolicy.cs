using System;

namespace CafeGuard
{
    /// <summary>How the guard was turned ON: by the big button, or by locking Windows (Win+L mode).</summary>
    public enum ArmSource { Manual, AutoLock }

    /// <summary>Rules for "Turn ON when I lock (Win+L)" and "unlock = OFF" (spec §6).</summary>
    public static class AutoLockPolicy
    {
        /// <summary>A lid-close / sleep / wake this close to the lock means the lock came from it, not from Win+L.</summary>
        public static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Called ~2 seconds after Windows locked. Arms only for a real "walk away" lock. Windows may report the lock
        /// before OR after the lid-close / sleep that caused it, so any of those near the lock (or during the wait) blocks arming.
        /// </summary>
        public static bool ShouldArmAfterLock(bool armOnLock, GuardState state, bool lidOpen, bool chargerPlugged,
            DateTime lockedAt, DateTime now, DateTime? lastLidClose, DateTime? lastSuspend, DateTime? lastResume)
        {
            if (!armOnLock || state != GuardState.Off || !lidOpen || !chargerPlugged) return false;
            DateTime from = lockedAt - Window;
            bool Near(DateTime? t) => t != null && t.Value >= from && t.Value <= now;
            return !Near(lastLidClose) && !Near(lastSuspend) && !Near(lastResume);
        }

        /// <summary>Unlocking proves it's the owner, so it always turns the guard OFF (and stops an alarm).</summary>
        public static bool ShouldDisarmOnUnlock(GuardState state) => state != GuardState.Off;
    }
}
