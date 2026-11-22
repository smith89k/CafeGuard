using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafeGuard.Shared;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CafeGuard.Card
{
    /// <summary>The pop-up card above the tray clock. It only shows a <see cref="CardView"/> and reports clicks.</summary>
    public partial class TrayCard : Window
    {
        private CardView? _current;
        private AppSettings? _settings;
        private bool _updating; // true while we set controls from code, so their change events are ignored

        public event Action? TurnOnClicked;
        public event Action? TurnOffClicked;
        public event Action? TestSirenClicked;
        public event Action? ExitClicked;
        public event Action? HidePowerButtonClicked;
        public event Action? ShowPowerButtonClicked;
        public event Action<ThemeChoice>? ThemeChosen;
        public event Action<bool>? ArmOnLockChanged;
        public event Action<AlarmSound>? AlarmSoundChosen;
        public event Action<AlarmPhrase>? PhraseChosen;
        public event Action<VoiceChoice>? VoiceChosen;

        /// <summary>When the card last hid itself because the user clicked elsewhere (used to make the tray click toggle).</summary>
        public DateTime LastAutoHide { get; private set; } = DateTime.MinValue;

        public TrayCard(ThemeMode mode)
        {
            ApplyTheme(mode);
            InitializeComponent();

            BigButton.Click += (_, _) =>
            {
                if (_current?.ButtonKind == ButtonKind.Primary) TurnOnClicked?.Invoke();
                else if (_current != null && _current.ButtonKind != ButtonKind.Disabled) TurnOffClicked?.Invoke();
            };
            TestSirenButton.Click += (_, _) => TestSirenClicked?.Invoke();
            ExitButton.Click += (_, _) => ExitClicked?.Invoke();

            ThemeAuto.Checked += (_, _) => { if (!_updating) ThemeChosen?.Invoke(ThemeChoice.Auto); };
            ThemeLight.Checked += (_, _) => { if (!_updating) ThemeChosen?.Invoke(ThemeChoice.Light); };
            ThemeDark.Checked += (_, _) => { if (!_updating) ThemeChosen?.Invoke(ThemeChoice.Dark); };
            ArmOnLockSwitch.Checked += (_, _) => { if (!_updating) ArmOnLockChanged?.Invoke(true); };
            ArmOnLockSwitch.Unchecked += (_, _) => { if (!_updating) ArmOnLockChanged?.Invoke(false); };

            OnPick(SoundSiren, () => AlarmSoundChosen?.Invoke(AlarmSound.Siren));
            OnPick(SoundVoice, () => AlarmSoundChosen?.Invoke(AlarmSound.Voice));
            OnPick(SoundBoth, () => AlarmSoundChosen?.Invoke(AlarmSound.Both));
            foreach (var phrase in Phrases)
                PhraseBox.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = AlarmPhrases.Text(phrase), TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = AlarmPhrases.Text(phrase) });
            VoiceBox.Items.Add("Man");
            VoiceBox.Items.Add("Woman");
            PhraseBox.SelectionChanged += (_, _) => { if (!_updating && PhraseBox.SelectedIndex >= 0) PhraseChosen?.Invoke(Phrases[PhraseBox.SelectedIndex]); };
            VoiceBox.SelectionChanged += (_, _) => { if (!_updating && VoiceBox.SelectedIndex >= 0) VoiceChosen?.Invoke(VoiceBox.SelectedIndex == 1 ? VoiceChoice.Woman : VoiceChoice.Man); };

            Deactivated += (_, _) =>
            {
                if (!IsVisible || Sticky) return;
                LastAutoHide = DateTime.Now;
                Hide();
            };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) HideByUser(); };

            // Alt+F4 must only hide the card; a closed window could never be shown again.
            Closing += (_, e) =>
            {
                if (_closingForGood) return;
                e.Cancel = true;
                HideByUser();
            };
        }

        private bool _closingForGood;

        /// <summary>During an alarm the card stays up: clicking elsewhere, Esc and Alt+F4 don't hide it.</summary>
        public bool Sticky { get; set; }

        /// <summary>Esc / Alt+F4: hide, unless the alarm card must stay up.</summary>
        public void HideByUser()
        {
            if (!Sticky) Hide();
        }

        private void OnPick(System.Windows.Controls.Primitives.ToggleButton radio, Action report) =>
            radio.Checked += (_, _) => { if (!_updating) report(); };

        private static readonly AlarmPhrase[] Phrases = { AlarmPhrase.DontTouch, AlarmPhrase.PutDown, AlarmPhrase.Protected, AlarmPhrase.PleaseDont };

        /// <summary>Really closes the card (CafeGuard is exiting).</summary>
        public void CloseForGood()
        {
            _closingForGood = true;
            Close();
        }

        public void ApplyTheme(ThemeMode mode) => ThemeResources.Apply(Resources, mode);

        /// <summary>Shows the card with its bottom-right (etc.) corner placed by <paramref name="place"/>.</summary>
        public void ShowAt(Func<Size, Point> place)
        {
            Opacity = 0;
            Show();
            UpdateLayout();
            var p = place(new Size(ActualWidth, ActualHeight));
            Left = p.X;
            Top = p.Y;
            Opacity = 1;
            Activate();
            Focus();
        }

        public void Update(CardView view, AppSettings settings)
        {
            if (_current != null && SameView(_current, view) && settings == _settings) return;
            _current = view;
            _settings = settings;
            var theme = settings.Theme;
            bool armOnLock = settings.ArmOnLock;
            _updating = true;
            try
            {
                TitleText.Text = view.Title;
                SubtitleText.Text = view.Subtitle;
                bool alarm = view.AlarmBarText != null; // the red title and frame are for the alarm only, not for OFF
                TitleText.SetResourceReference(TextBlock.ForegroundProperty, alarm ? "Brush.Red" : "Brush.Text");
                if (view.Shield == ShieldTone.GreenOutline)
                {
                    ShieldIcon.Fill = System.Windows.Media.Brushes.Transparent;
                    ShieldIcon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Brush.Green");
                    ShieldIcon.StrokeThickness = 3;
                }
                else
                {
                    ShieldIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, view.Shield == ShieldTone.Green ? "Brush.Green" : "Brush.Red");
                    ShieldIcon.StrokeThickness = 0;
                }
                Frame.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, alarm ? "Brush.Red" : "Brush.Line");

                AlarmBar.Visibility = view.AlarmBarText == null ? Visibility.Collapsed : Visibility.Visible;
                AlarmBarText.Text = view.AlarmBarText ?? string.Empty;

                BigButton.Content = view.ButtonText;
                BigButton.IsEnabled = view.ButtonEnabled;
                BigButton.SetResourceReference(StyleProperty, view.ButtonKind switch
                {
                    ButtonKind.Neutral => "NeutralButton",
                    ButtonKind.Danger => "DangerButton",
                    _ => "PrimaryButton"
                });

                ChipsPanel.Visibility = view.ShowChips ? Visibility.Visible : Visibility.Collapsed;
                ChipsPanel.Children.Clear();
                foreach (var chip in view.Chips) ChipsPanel.Children.Add(BuildChip(chip));

                ArmOnLockRow.Visibility = view.ShowArmOnLockRow ? Visibility.Visible : Visibility.Collapsed;
                ArmOnLockSwitch.IsChecked = armOnLock;

                SoundRow.Visibility = view.ShowArmOnLockRow ? Visibility.Visible : Visibility.Collapsed;
                SoundSiren.IsChecked = settings.Sound == AlarmSound.Siren;
                SoundVoice.IsChecked = settings.Sound == AlarmSound.Voice;
                SoundBoth.IsChecked = settings.Sound == AlarmSound.Both;
                WordsPanel.Visibility = settings.Sound == AlarmSound.Siren ? Visibility.Collapsed : Visibility.Visible;
                PhraseBox.SelectedIndex = Array.IndexOf(Phrases, settings.Phrase);
                VoiceBox.SelectedIndex = settings.Voice == VoiceChoice.Woman ? 1 : 0;

                TestSirenButton.IsEnabled = view.FooterEnabled;
                ExitButton.IsEnabled = view.FooterEnabled;

                ThemeAuto.IsChecked = theme == ThemeChoice.Auto;
                ThemeLight.IsChecked = theme == ThemeChoice.Light;
                ThemeDark.IsChecked = theme == ThemeChoice.Dark;
            }
            finally
            {
                _updating = false;
            }
        }

        private static bool SameView(CardView a, CardView b) =>
            a with { Chips = Array.Empty<ChipView>() } == b with { Chips = Array.Empty<ChipView>() } && a.Chips.SequenceEqual(b.Chips);

        private UIElement BuildChip(ChipView chip)
        {
            // Icons and values use two colors only: green = normal, red = needs attention.
            var valueBrush = chip.Tone == ChipTone.Ok ? "Brush.GreenText" : "Brush.Red";
            int space = chip.Label.IndexOf(' ');
            var icon = new TextBlock { Text = space > 0 ? chip.Label.Substring(0, space) : string.Empty, FontSize = 12.5, Margin = new Thickness(0, 0, 6, 0) };
            icon.SetResourceReference(StyleProperty, "BodyText");
            icon.SetResourceReference(TextBlock.ForegroundProperty, valueBrush);
            var text = new TextBlock { Text = space > 0 ? chip.Label.Substring(space + 1) : chip.Label, FontSize = 12.5 };
            text.SetResourceReference(StyleProperty, "BodyText");
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(icon);
            label.Children.Add(text);

            var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var value = new TextBlock { Text = chip.Value, FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            value.SetResourceReference(StyleProperty, "BodyText");
            value.SetResourceReference(TextBlock.ForegroundProperty, valueBrush);
            valuePanel.Children.Add(value);
            if (chip.Link != ChipLink.None)
            {
                bool hide = chip.Link == ChipLink.Hide;
                value.Text += " ";
                var link = new Button { Content = new TextBlock { Text = hide ? "Hide" : "Show", TextDecorations = TextDecorations.Underline } };
                link.SetResourceReference(StyleProperty, "GreenLinkButton");
                link.ToolTip = hide
                    ? "Hide the power button on the lock screen (Windows will ask for permission)"
                    : "Put the power button back on the lock screen (Windows will ask for permission)";
                link.Click += (_, _) => (hide ? HidePowerButtonClicked : ShowPowerButtonClicked)?.Invoke();
                valuePanel.Children.Add(link);
            }

            var dock = new DockPanel();
            DockPanel.SetDock(valuePanel, Dock.Right);
            dock.Children.Add(valuePanel);
            dock.Children.Add(label);

            var border = new System.Windows.Controls.Border { Child = dock };
            border.SetResourceReference(StyleProperty, "Chip");
            return border;
        }
    }
}
