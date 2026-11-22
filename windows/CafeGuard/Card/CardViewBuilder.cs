using System;
using System.Collections.Generic;
using System.Globalization;

namespace CafeGuard.Card
{
    /// <summary>Turns the guard's current situation into the card's words and colors (spec §6).</summary>
    public static class CardViewBuilder
    {
        private static readonly CultureInfo Clock = CultureInfo.GetCultureInfo("en-US");

        private static string Time(DateTime? t) => (t ?? DateTime.Now).ToString("h:mm tt", Clock);

        public static string AlarmReasonText(AlarmReason reason) => reason switch
        {
            AlarmReason.ChargerUnplugged => "Charger was unplugged",
            AlarmReason.LidClosed => "Lid was closed",
            AlarmReason.Sleep => "Someone tried to put it to sleep",
            AlarmReason.Shutdown => "Someone tried to shut it down",
            AlarmReason.HeadphoneJack => "Headphones were plugged in",
            AlarmReason.TaskManager => "Someone opened Task Manager",
            _ => "Something was tampered with"
        };

        public static CardView Build(CardInput input)
        {
            bool on = input.State == GuardState.On;
            var chips = BuildChips(input, on);

            return input.State switch
            {
                GuardState.On => new CardView(
                    "Cafe Guard is ON", $"Guarding since {Time(input.OnSince)}", ShieldTone.Green,
                    "🔓 Turn OFF — I'm back", ButtonKind.Neutral, true,
                    ShowChips: true, chips, AlarmBarText: null, ShowArmOnLockRow: true, FooterEnabled: false),

                GuardState.Alarm => new CardView(
                    "ALARM!", $"Started at {Time(input.AlarmAt)}", ShieldTone.Red,
                    "🔓 Turn OFF — stop the alarm", ButtonKind.Danger, true,
                    ShowChips: false, chips,
                    AlarmBarText: "⚠ " + AlarmReasonText(input.Reason ?? AlarmReason.ChargerUnplugged),
                    ShowArmOnLockRow: false, FooterEnabled: false),

                _ => input.ChargerPlugged
                    ? new CardView(
                        "Cafe Guard is OFF", "Your laptop is not guarded", ShieldTone.GreenOutline,
                        "🔒 Turn ON — going to bathroom", ButtonKind.Primary, true,
                        ShowChips: true, chips, AlarmBarText: null, ShowArmOnLockRow: true, FooterEnabled: true)
                    : new CardView(
                        "Cafe Guard is OFF", "Plug in your charger to turn it ON", ShieldTone.GreenOutline,
                        "🔒 Turn ON — plug in charger first", ButtonKind.Disabled, false,
                        ShowChips: true, chips, AlarmBarText: null, ShowArmOnLockRow: true, FooterEnabled: true)
            };
        }

        private static IReadOnlyList<ChipView> BuildChips(CardInput input, bool on)
        {
            return new List<ChipView>
            {
                input.ChargerPlugged
                    ? new ChipView("🔌 Charger", on ? "Watching ✓" : "Plugged in ✓", ChipTone.Ok, ChipLink.None)
                    : new ChipView("🔌 Charger", "Not plugged in ⚠", ChipTone.Warn, ChipLink.None),

                string.IsNullOrEmpty(input.SpeakerName)
                    ? new ChipView("🔊 Sound", "No speakers found ⚠", ChipTone.Warn, ChipLink.None)
                    : new ChipView("🔊 Sound", "Speakers ✓", ChipTone.Ok, ChipLink.None),

                input.LidProtectionAvailable
                    ? new ChipView("💻 Lid protection", on ? "On ✓" : "Ready ✓", ChipTone.Ok, ChipLink.None)
                    : new ChipView("💻 Lid protection", "Not available ⚠", ChipTone.Warn, ChipLink.None),

                !input.PowerButtonHidden
                    ? new ChipView("⏻ Lock-screen power button", "Visible —", ChipTone.Warn, ChipLink.Hide)
                    : input.PowerButtonHiddenByUs
                        ? new ChipView("⏻ Lock-screen power button", "Hidden ✓ —", ChipTone.Ok, ChipLink.Show)
                        : new ChipView("⏻ Lock-screen power button", "Hidden ✓", ChipTone.Ok, ChipLink.None),
            };
        }
    }
}
