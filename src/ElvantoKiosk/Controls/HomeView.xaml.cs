using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;

namespace ElvantoKiosk.Controls;

public partial class HomeView : UserControl
{
    /// <summary>Déclenché lorsqu'un visiteur sélectionne un formulaire.</summary>
    public event EventHandler<FormEntry>? FormSelected;

    public HomeView()
    {
        InitializeComponent();
    }

    public void ApplyConfig(AppConfig config, string baseDirectory)
    {
        OrgNameText.Text = config.OrganizationName;
        WelcomeTitleText.Text = config.WelcomeTitle;
        WelcomeMessageText.Text = config.WelcomeMessage;
        FooterText.Text = config.OrganizationName + " — Borne d'accueil";

        LoadLogo(config.LogoPath, baseDirectory);

        FormsList.ItemsSource = config.Forms;
        var hasForms = config.Forms.Count > 0;
        FormsList.Visibility = hasForms ? Visibility.Visible : Visibility.Collapsed;
        NoFormsText.Visibility = hasForms ? Visibility.Collapsed : Visibility.Visible;
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
                Logger.Warn($"Logo introuvable : {fullPath}. Affichage du nom de l'organisation à la place.");
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.EndInit();

            LogoImage.Source = bitmap;
            LogoImage.Visibility = Visibility.Visible;
            OrgNameText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible de charger le logo.", ex);
        }
    }

    private void FormButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FormEntry form })
        {
            FormSelected?.Invoke(this, form);
        }
    }
}
