using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;

namespace ElvantoKiosk.Controls;

public partial class ScreensaverView : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private bool _dismissInProgress;

    public event EventHandler? Dismissed;

    public ScreensaverView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    public void ApplyConfig(AppConfig config, string baseDirectory)
    {
        PromptText.Text = config.ScreensaverPrompt;
        LoadBackground(config.ScreensaverBackgroundPath, baseDirectory);
        LoadLogo(config.ScreensaverLogoPath, config.OrganizationName, baseDirectory);
        LoadTitle(config.ScreensaverTitlePath, baseDirectory);
    }

    public void Start()
    {
        _dismissInProgress = false;
        UpdateClock();
        _clockTimer.Start();
    }

    public void Stop() => _clockTimer.Stop();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        DateText.Text = now.ToString("dddd d MMMM yyyy", French).ToUpper(French);
        TimeText.Text = now.ToString("HH:mm", French);
    }

    private void LoadBackground(string path, string baseDirectory)
    {
        if (!TryLoadImage(path, baseDirectory, out var bitmap))
        {
            BackgroundFallback.Visibility = Visibility.Visible;
            BackgroundImage.Visibility = Visibility.Collapsed;
            return;
        }

        BackgroundImage.Source = bitmap;
        BackgroundImage.Visibility = Visibility.Visible;
        BackgroundFallback.Visibility = Visibility.Collapsed;
    }

    private void LoadLogo(string path, string organizationName, string baseDirectory)
    {
        if (TryLoadImage(path, baseDirectory, out var bitmap))
        {
            LogoImage.Source = bitmap;
            LogoImage.Visibility = Visibility.Visible;
            OrgNameText.Visibility = Visibility.Collapsed;
            return;
        }

        if (!string.IsNullOrWhiteSpace(organizationName))
        {
            OrgNameText.Text = organizationName;
            OrgNameText.Visibility = Visibility.Visible;
        }
    }

    private void LoadTitle(string path, string baseDirectory)
    {
        if (TryLoadImage(path, baseDirectory, out var bitmap))
        {
            TitleImage.Source = bitmap;
            TitleImage.Visibility = Visibility.Visible;
            TitleFallbackText.Visibility = Visibility.Collapsed;
            return;
        }

        TitleFallbackText.Visibility = Visibility.Visible;
    }

    private static bool TryLoadImage(string imagePath, string baseDirectory, out BitmapImage bitmap)
    {
        bitmap = new BitmapImage();

        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return false;

            var fullPath = Path.IsPathRooted(imagePath)
                ? imagePath
                : Path.Combine(baseDirectory, imagePath);

            if (!File.Exists(fullPath))
                return false;

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.EndInit();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Image écran de veille introuvable ou invalide ({imagePath}) : {ex.Message}");
            return false;
        }
    }

    private void OnScreenTouched(object sender, InputEventArgs e)
    {
        e.Handled = true;
        if (_dismissInProgress)
            return;

        _dismissInProgress = true;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
