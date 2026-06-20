using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;

namespace ElvantoKiosk.Controls;

public partial class AdminPanelWindow : Window
{
    private readonly string _baseDirectory;

    public AppConfig Config { get; private set; }
    public bool RequestQuit { get; private set; }
    public bool ConfigSaved { get; private set; }
    public bool ConfigReloadRequested { get; private set; }

    public AdminPanelWindow(AppConfig config, string baseDirectory)
    {
        InitializeComponent();
        Config = CloneConfig(config);
        _baseDirectory = baseDirectory;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Elvanto Kiosk v{version} — {ConfigService.ConfigPath(_baseDirectory)}";

        BuildConfigEditor();
        RefreshLogs();
        ConnectionStatusText.Text = "Cliquez sur « Tester la connexion ».";
    }

    private static AppConfig CloneConfig(AppConfig source) =>
        System.Text.Json.JsonSerializer.Deserialize<AppConfig>(
            System.Text.Json.JsonSerializer.Serialize(source)) ?? new AppConfig();

    private void BuildConfigEditor()
    {
        ConfigEditorPanel.Children.Clear();

        AddSection("Organisation & accueil");
        AddField("OrganizationName", "Nom affiché si le logo est absent.", Config.OrganizationName);
        AddField("WelcomeTitle", "Titre principal sur la page des liens.", Config.WelcomeTitle);
        AddField("WelcomeMessage", "Message de présentation sous le titre.", Config.WelcomeMessage);
        AddField("LogoPath", "Chemin du logo (ex. assets/LOGO-ICC.png), relatif au dossier de l'app.", Config.LogoPath);

        AddSection("Sécurité & comportement");
        AddField("AdminPin", "Code PIN pour ouvrir ce panneau (Ctrl+Maj+Q ou cadenas).", Config.AdminPin);
        AddField("InactivityTimeoutSeconds", "Secondes d'inactivité sur un formulaire avant retour auto.", Config.InactivityTimeoutSeconds.ToString());
        AddField("ReturnDelayAfterSubmitSeconds", "Délai (secondes) après envoi avant retour à l'accueil.", Config.ReturnDelayAfterSubmitSeconds.ToString());
        AddField("ThankYouMessage", "Message affiché au visiteur après envoi du formulaire.", Config.ThankYouMessage);
        AddCheckField("ShowHomeButton", "Affiche le bouton « Revenir à l'accueil » sur les formulaires.", Config.ShowHomeButton);
        AddCheckField("ClearDataOnReturnHome", "Efface cookies/session entre chaque visiteur.", Config.ClearDataOnReturnHome);

        AddSection("Écran de veille");
        AddCheckField("ScreensaverEnabled", "Active l'écran de veille au démarrage et après inactivité.", Config.ScreensaverEnabled);
        AddField("ScreensaverSubtitle", "Sous-titre sous le nom (ex. KIOSQUE DE PRISE DE RENDEZ-VOUS).", Config.ScreensaverSubtitle);
        AddField("ScreensaverPrompt", "Texte d'invitation tactile sur l'écran de veille.", Config.ScreensaverPrompt);
        AddField("ScreensaverIdleTimeoutSeconds", "Inactivité sur la page d'accueil avant retour veille (s).", Config.ScreensaverIdleTimeoutSeconds.ToString());
        AddCheckField("ReturnToScreensaverOnHome", "Retour à la veille (au lieu de l'accueil) après un formulaire.", Config.ReturnToScreensaverOnHome);

        AddSection("Couleurs des cartes");
        AddField("FormCardAccentColors", "Couleurs hex séparées par des virgules (#DC2626, #CA8A04…). Une par carte ou répétées.", string.Join(", ", Config.FormCardAccentColors));

        AddSection("Détection de soumission Notion");
        AddField("SubmitTextKeywords", "Mots-clés dans la page de confirmation (un par ligne ou séparés par des virgules). Mettez FORMULAIRE_ENVOYÉ dans le titre Notion.", string.Join(", ", Config.SubmitTextKeywords));
        AddField("AllowedHosts", "Domaines autorisés pour la navigation (notion.so, notion.site…).", string.Join(", ", Config.AllowedHosts));

        AddSection("Formulaires");
        AddHelp("Chaque formulaire = une carte sur la page d'accueil. IconPath : laisser vide en attendant les icônes fournies par ICC.");

        for (var i = 0; i < Config.Forms.Count; i++)
        {
            var form = Config.Forms[i];
            var idx = i;
            AddSubSection($"Formulaire {i + 1}");
            AddFormField(idx, "Title", "Titre de la carte.", form.Title);
            AddFormField(idx, "Description", "Texte descriptif sous le titre.", form.Description);
            AddFormField(idx, "Url", "Lien Notion public du formulaire.", form.Url);
            AddFormField(idx, "IconPath", "Chemin icône (ex. assets/icon-visite.png). Vide = initiale du titre.", form.IconPath);
            AddFormField(idx, "AccentColor", "Couleur hex optionnelle (#2563EB). Sinon couleur globale.", form.AccentColor);
        }
    }

    private void AddSection(string title)
    {
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC)),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 20, 0, 8)
        });
    }

    private void AddSubSection(string title)
    {
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x93, 0xC5, 0xFD)),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6)
        });
    }

    private void AddHelp(string text)
    {
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddField(string key, string description, string value)
    {
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = key,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 2)
        });
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });
        ConfigEditorPanel.Children.Add(new TextBox
        {
            Text = value,
            Tag = key,
            MinHeight = 36,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55))
        });
    }

    private void AddCheckField(string key, string description, bool value)
    {
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 4)
        });
        ConfigEditorPanel.Children.Add(new CheckBox
        {
            Content = key,
            IsChecked = value,
            Tag = key,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC)),
            Margin = new Thickness(0, 0, 0, 4)
        });
    }

    private void AddFormField(int formIndex, string property, string description, string value)
    {
        var tag = $"Form.{formIndex}.{property}";
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = property,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCB, 0xD5, 0xE1)),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 2)
        });
        ConfigEditorPanel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        });
        ConfigEditorPanel.Children.Add(new TextBox
        {
            Text = value,
            Tag = tag,
            MinHeight = 32,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55))
        });
    }

    private void ReadConfigFromEditor()
    {
        foreach (var child in ConfigEditorPanel.Children)
        {
            if (child is TextBox { Tag: string key } box)
            {
                if (key.StartsWith("Form.", StringComparison.Ordinal))
                {
                    var parts = key.Split('.');
                    if (parts.Length == 3 && int.TryParse(parts[1], out var idx) && idx < Config.Forms.Count)
                    {
                        var form = Config.Forms[idx];
                        switch (parts[2])
                        {
                            case "Title": form.Title = box.Text; break;
                            case "Description": form.Description = box.Text; break;
                            case "Url": form.Url = box.Text; break;
                            case "IconPath": form.IconPath = box.Text; break;
                            case "AccentColor": form.AccentColor = box.Text; break;
                        }
                    }
                    continue;
                }

                ApplyConfigField(key, box.Text);
            }
            else if (child is CheckBox { Tag: string checkKey } check)
            {
                ApplyConfigBoolField(checkKey, check.IsChecked == true);
            }
        }
    }

    private void ApplyConfigField(string key, string value)
    {
        switch (key)
        {
            case "OrganizationName": Config.OrganizationName = value; break;
            case "WelcomeTitle": Config.WelcomeTitle = value; break;
            case "WelcomeMessage": Config.WelcomeMessage = value; break;
            case "LogoPath": Config.LogoPath = value; break;
            case "AdminPin": Config.AdminPin = value; break;
            case "ThankYouMessage": Config.ThankYouMessage = value; break;
            case "ScreensaverSubtitle": Config.ScreensaverSubtitle = value; break;
            case "ScreensaverPrompt": Config.ScreensaverPrompt = value; break;
            case "InactivityTimeoutSeconds":
                if (int.TryParse(value, out var idle)) Config.InactivityTimeoutSeconds = idle;
                break;
            case "ReturnDelayAfterSubmitSeconds":
                if (int.TryParse(value, out var delay)) Config.ReturnDelayAfterSubmitSeconds = delay;
                break;
            case "ScreensaverIdleTimeoutSeconds":
                if (int.TryParse(value, out var ssIdle)) Config.ScreensaverIdleTimeoutSeconds = ssIdle;
                break;
            case "FormCardAccentColors":
                Config.FormCardAccentColors = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                break;
            case "SubmitTextKeywords":
                Config.SubmitTextKeywords = value.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                break;
            case "AllowedHosts":
                Config.AllowedHosts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                break;
        }
    }

    private void ApplyConfigBoolField(string key, bool value)
    {
        switch (key)
        {
            case "ShowHomeButton": Config.ShowHomeButton = value; break;
            case "ClearDataOnReturnHome": Config.ClearDataOnReturnHome = value; break;
            case "ScreensaverEnabled": Config.ScreensaverEnabled = value; break;
            case "ReturnToScreensaverOnHome": Config.ReturnToScreensaverOnHome = value; break;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        ConnectionStatusText.Text = "Test en cours…";
        ConnectionStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCB, 0xD5, 0xE1));

        var url = Config.Forms.FirstOrDefault()?.Url ?? "https://www.notion.so";
        var (success, message) = await ConnectionTestService.TestAsync(url);

        ConnectionStatusText.Text = message;
        ConnectionStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            success ? System.Windows.Media.Color.FromRgb(0x86, 0xEF, 0xAC) : System.Windows.Media.Color.FromRgb(0xFC, 0xA5, 0xA5));
    }

    private void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        ReadConfigFromEditor();
        var (ok, msg) = ConfigService.Save(_baseDirectory, Config);
        if (!ok)
        {
            MessageBox.Show(msg, "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfigSaved = true;
        ConfigReloadRequested = true;
        DialogResult = true;
        Close();
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        ReadConfigFromEditor();
        var (ok, msg) = ConfigService.Save(_baseDirectory, Config);
        SaveStatusText.Text = msg;
        SaveStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            ok ? System.Windows.Media.Color.FromRgb(0x86, 0xEF, 0xAC) : System.Windows.Media.Color.FromRgb(0xFC, 0xA5, 0xA5));
        SaveStatusText.Visibility = Visibility.Visible;
        if (ok) ConfigSaved = true;
    }

    private void RefreshLogs_Click(object sender, RoutedEventArgs e) => RefreshLogs();

    private void RefreshLogs()
    {
        LogTextBox.Text = Logger.ReadRecentLines(250);
        LogTextBox.ScrollToEnd();
    }

    private void QuitApp_Click(object sender, RoutedEventArgs e)
    {
        RequestQuit = true;
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = ConfigSaved || ConfigReloadRequested;
        Close();
    }
}
