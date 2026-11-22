namespace CafeGuard.Shared
{
    public enum ThemeMode { Light, Dark }

    /// <summary>CafeGuard's colors (spec §5). Green is the main color.</summary>
    public sealed record Palette(
        string Background, string Surface, string Line, string Text, string Muted,
        string Green, string GreenSoft, string GreenText, string Gray,
        string Red, string RedSoft, string Warning, string NeutralButton)
    {
        public static readonly Palette Light = new(
            "#FFFFFF", "#F3F4F6", "#E5E7EB", "#111827", "#6B7280",
            "#16A34A", "#DCFCE7", "#15803D", "#9CA3AF",
            "#DC2626", "#FEE2E2", "#B45309", "#E5E7EB");

        public static readonly Palette Dark = new(
            "#202020", "#2B2B2B", "#3A3A3A", "#F3F4F6", "#A3A3A3",
            "#22C55E", "#14532D", "#86EFAC", "#737373",
            "#EF4444", "#450A0A", "#FBBF24", "#3A3A3A");

        public static Palette For(ThemeMode mode) => mode == ThemeMode.Dark ? Dark : Light;
    }
}
