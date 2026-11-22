using System;
using System.Collections.Generic;
using CafeGuard.Shared;

namespace CafeGuard.Card
{
    public enum AlarmReason { ChargerUnplugged, LidClosed, Sleep, Shutdown, HeadphoneJack, TaskManager }
    /// <summary>Two colors only (owner's rule): OFF = green outline, ON = solid green, ALARM = red.</summary>
    public enum ShieldTone { GreenOutline, Green, Red }
    public enum ButtonKind { Primary, Neutral, Danger, Disabled }
    public enum ChipTone { Ok, Warn }
    public enum ChipLink { None, Hide, Show }

    /// <summary>Everything the card needs to know right now.</summary>
    public sealed record CardInput(
        GuardState State, bool ChargerPlugged, string SpeakerName, bool LidProtectionAvailable,
        bool PowerButtonHidden, bool PowerButtonHiddenByUs, DateTime? OnSince, DateTime? AlarmAt, AlarmReason? Reason,
        bool ArmOnLock, ThemeChoice Theme);

    public sealed record ChipView(string Label, string Value, ChipTone Tone, ChipLink Link);

    /// <summary>What the card shows: texts, colors and which parts are visible / clickable.</summary>
    public sealed record CardView(
        string Title, string Subtitle, ShieldTone Shield, string ButtonText, ButtonKind ButtonKind,
        bool ButtonEnabled, bool ShowChips, IReadOnlyList<ChipView> Chips, string? AlarmBarText,
        bool ShowArmOnLockRow, bool FooterEnabled);
}
