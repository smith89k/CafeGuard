using CafeGuard.Card;
using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class TrayCardTests
{
    [Fact]
    public void AltF4_HidesCard_SoItCanOpenAgain()
    {
        UiThread.Run(() =>
        {
            var card = new TrayCard(ThemeMode.Light) { Left = -3000, Top = -3000, ShowActivated = false };
            card.Show();
            card.Close();          // what Alt+F4 does
            Assert.False(card.IsVisible);
            card.Show();           // the next tray click must still work
            Assert.True(card.IsVisible);
            card.CloseForGood();
            Assert.False(card.IsVisible);
        });
    }

    [Fact]
    public void Sticky_AlarmCardCannotBeClosedOrHidden()
    {
        UiThread.Run(() =>
        {
            var card = new TrayCard(ThemeMode.Light) { Left = -3000, Top = -3000, ShowActivated = false };
            card.Show();
            card.Sticky = true;
            card.Close();              // Alt+F4
            card.HideByUser();         // click elsewhere / Esc
            Assert.True(card.IsVisible);

            card.Sticky = false;
            card.HideByUser();
            Assert.False(card.IsVisible);
            card.CloseForGood();
        });
    }

    // Owner rule: icons use two colors only — green for normal, red for alert.
    [Fact]
    public void ChipIcons_GreenWhenOk_RedWhenAlert()
    {
        UiThread.Run(() =>
        {
            var card = new TrayCard(ThemeMode.Light) { Left = -3000, Top = -3000, ShowActivated = false };
            var input = new CardInput(GuardState.Off, ChargerPlugged: false, SpeakerName: "Speakers", LidProtectionAvailable: true,
                PowerButtonHidden: false, PowerButtonHiddenByUs: false, OnSince: null, AlarmAt: null, Reason: null,
                ArmOnLock: false, Theme: ThemeChoice.Auto);
            card.Update(CardViewBuilder.Build(input), new AppSettings());

            Assert.Same(card.Resources["Brush.Red"], Find(card, "🔌").Foreground);         // charger unplugged
            Assert.Same(card.Resources["Brush.Red"], Find(card, "Not plugged in ⚠").Foreground);
            Assert.Same(card.Resources["Brush.GreenText"], Find(card, "🔊").Foreground);  // speakers fine
            Assert.Same(card.Resources["Brush.Red"], Find(card, "⏻").Foreground);          // power button visible
            Assert.Same(card.Resources["Brush.GreenText"], Find(card, "🔐").Foreground);  // Win+L row icon
            card.CloseForGood();
        });
    }

    [Fact]
    public void SoundRow_WordsShowOnlyWhenVoiceIsUsed_AndClicksReportChoice()
    {
        UiThread.Run(() =>
        {
            var card = new TrayCard(ThemeMode.Light) { Left = -3000, Top = -3000, ShowActivated = false };
            var view = CardViewBuilder.Build(new CardInput(GuardState.Off, true, "Speakers", true, true, false, null, null, null, false, ThemeChoice.Auto));
            var words = (System.Windows.FrameworkElement)card.FindName("WordsPanel");

            card.Update(view, new AppSettings(Sound: AlarmSound.Siren));
            Assert.Equal(System.Windows.Visibility.Collapsed, words.Visibility);

            card.Update(view, new AppSettings(Sound: AlarmSound.Both));
            Assert.Equal(System.Windows.Visibility.Visible, words.Visibility);

            AlarmSound? chosen = null;
            card.AlarmSoundChosen += s => chosen = s;
            ((System.Windows.Controls.RadioButton)card.FindName("SoundVoice")).IsChecked = true;
            Assert.Equal(AlarmSound.Voice, chosen);

            // Words and voice are select boxes.
            var words2 = (System.Windows.Controls.ComboBox)card.FindName("PhraseBox");
            var voices = (System.Windows.Controls.ComboBox)card.FindName("VoiceBox");
            Assert.Equal(4, words2.Items.Count);
            Assert.Equal(0, words2.SelectedIndex);          // default "Don't touch my laptop!"
            Assert.Equal(new[] { "Man", "Woman" }, voices.Items.Cast<object>().Select(i => i.ToString()));
            AlarmPhrase? phrase = null; VoiceChoice? voice = null;
            card.PhraseChosen += p => phrase = p;
            card.VoiceChosen += v => voice = v;
            words2.SelectedIndex = 2;
            voices.SelectedIndex = 1;
            Assert.Equal(AlarmPhrase.Protected, phrase);
            Assert.Equal(VoiceChoice.Woman, voice);
            card.CloseForGood();
        });
    }

    private static System.Windows.Controls.TextBlock Find(System.Windows.DependencyObject root, string text)
    {
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root))
        {
            if (child is System.Windows.Controls.TextBlock tb && tb.Text.Trim() == text) return tb;
            if (child is System.Windows.DependencyObject d)
            {
                try { return Find(d, text); } catch (Xunit.Sdk.XunitException) { }
            }
        }
        throw new Xunit.Sdk.XunitException($"No text '{text}' on the card");
    }
}
