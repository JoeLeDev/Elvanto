using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;

namespace ElvantoKiosk.Controls;

public partial class ScreensaverView : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public event EventHandler? Dismissed;

    public ScreensaverView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    public void ApplyConfig(AppConfig config, string baseDirectory)
    {
        OrgNameText.Text = config.OrganizationName;
        SubtitleText.Text = config.ScreensaverSubtitle;
        PromptText.Text = config.ScreensaverPrompt;
        LoadLogo(config.LogoPath, baseDirectory);
    }

    public void Start()
    {
        UpdateClock();
        _clockTimer.Start();
        StartPulseAnimation();
    }

    public void Stop()
    {
        _clockTimer.Stop();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        HoursText.Text = now.ToString("HH", French);
        MinutesText.Text = now.ToString("mm", French);
        SecondsText.Text = now.ToString("ss", French);
        DateText.Text = now.ToString("dddd d MMMM yyyy", French).ToUpper(French);
    }

    private void StartPulseAnimation()
    {
        var animation = new DoubleAnimation(0.35, 1.0, TimeSpan.FromSeconds(1.2))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        PulseDot.BeginAnimation(OpacityProperty, animation);
    }

    private void LoadLogo(string logoPath, string baseDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logoPath))
                return;

            var fullPath = Path.IsPathRooted(logoPath)
                ? logoPath
                : Path.Combine(baseDirectory, logoPath);

            if (!File.Exists(fullPath))
            {
                Logger.Warn($"Logo introuvable pour l'écran de veille : {fullPath}");
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.EndInit();

            LogoImage.Source = bitmap;
            LogoFrame.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible de charger le logo (écran de veille).", ex);
        }
    }

    private void OnScreenTouched(object sender, InputEventArgs e)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
