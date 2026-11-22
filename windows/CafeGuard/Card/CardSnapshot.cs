using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CafeGuard.Shared;

namespace CafeGuard.Card
{
    /// <summary>
    /// Developer check: draws the card in a given state/theme into a PNG without touching the tray.
    /// Usage: CafeGuard.exe --render-card out.png off|off-unplugged|on|alarm|power-visible|power-ours light|dark
    /// </summary>
    internal static class CardSnapshot
    {
        public static int Render(string outPath, string state, string theme)
        {
            if (System.Windows.Application.Current == null)
                _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            var mode = theme == "dark" ? ThemeMode.Dark : ThemeMode.Light;
            var t = DateTime.Today.AddHours(15).AddMinutes(41);
            var input = new CardInput(GuardState.Off, true, "Speakers (Apple Audio Device)", true, true, false, null, null, null, false, ThemeChoice.Auto);
            input = state switch
            {
                "off-unplugged" => input with { ChargerPlugged = false },
                "on" => input with { State = GuardState.On, OnSince = t, ArmOnLock = true },
                "alarm" => input with { State = GuardState.Alarm, OnSince = t, AlarmAt = t.AddMinutes(2), Reason = AlarmReason.ChargerUnplugged },
                "power-visible" => input with { PowerButtonHidden = false },
                "power-ours" => input with { PowerButtonHiddenByUs = true },
                _ => input
            };

            var card = new TrayCard(mode) { Left = -3000, Top = -3000, ShowActivated = false };
            card.Update(CardViewBuilder.Build(input), new AppSettings(input.Theme, input.ArmOnLock));
            card.Show();
            card.UpdateLayout();

            var root = (FrameworkElement)card.Content;
            double scale = 2.0;
            var bmp = new RenderTargetBitmap((int)(card.ActualWidth * scale), (int)(card.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var backdrop = new DrawingVisual();
            using (var dc = backdrop.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(mode == ThemeMode.Dark ? Color.FromRgb(16, 24, 40) : Color.FromRgb(200, 210, 225)), null,
                    new Rect(0, 0, card.ActualWidth, card.ActualHeight));
            }
            bmp.Render(backdrop);
            bmp.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            using (var fs = File.Create(outPath)) encoder.Save(fs);
            card.Close();
            return 0;
        }
    }
}
