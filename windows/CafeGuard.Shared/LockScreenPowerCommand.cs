namespace CafeGuard.Shared
{
    public enum PowerAction { Hide, Show, Restore }

    /// <summary>
    /// The admin helper command: "--set-lockscreen-power hide|show|restore:N|restore:none".
    /// CafeGuard starts itself with this and "run as administrator"; the helper shows no window.
    /// </summary>
    public sealed record LockScreenPowerCommand(PowerAction Action, int? RestoreValue)
    {
        public const string Switch = "--set-lockscreen-power";

        public static LockScreenPowerCommand? TryParse(string[] args)
        {
            if (args.Length != 2 || args[0] != Switch) return null;
            string a = args[1];
            if (a == "hide") return new LockScreenPowerCommand(PowerAction.Hide, null);
            if (a == "show") return new LockScreenPowerCommand(PowerAction.Show, null);
            if (a == "restore:none") return new LockScreenPowerCommand(PowerAction.Restore, null);
            if (a.StartsWith("restore:") && int.TryParse(a.Substring("restore:".Length), out int v))
                return new LockScreenPowerCommand(PowerAction.Restore, v);
            return null;
        }

        public string ToArguments() => Switch + " " + Action switch
        {
            PowerAction.Hide => "hide",
            PowerAction.Show => "show",
            _ => RestoreValue == null ? "restore:none" : "restore:" + RestoreValue.Value
        };

        public void Run(IRegistryStore registry)
        {
            switch (Action)
            {
                case PowerAction.Hide: LockScreenPowerButton.SetHidden(registry, true); break;
                case PowerAction.Show: LockScreenPowerButton.SetHidden(registry, false); break;
                default: LockScreenPowerButton.Restore(registry, RestoreValue); break;
            }
        }

        /// <summary>Runs the command as the admin helper process. Exit code 0 = done.</summary>
        public int RunAsHelper()
        {
            try { Run(new WindowsRegistryStore()); return 0; }
            catch { return 1; }
        }
    }
}
