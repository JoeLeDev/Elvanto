using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;

namespace ElvantoKiosk.Controls;

public partial class HomeView : UserControl
{
    private static readonly Color[] DefaultAccents =
    {
        Color.FromRgb(0xDC, 0x26, 0x26),
        Color.FromRgb(0xCA, 0x8A, 0x04),
        Color.FromRgb(0x25, 0x63, 0xEB),
        Color.FromRgb(0x7C, 0x3A, 0xED)
    };

    private string _baseDirectory = string.Empty;

    public event EventHandler<FormEntry>? FormSelected;

    public HomeView()
    {
        InitializeComponent();
    }

    public void ApplyConfig(AppConfig config, string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        OrgNameText.Text = config.OrganizationName;
        WelcomeTitleText.Text = config.WelcomeTitle;
        WelcomeMessageText.Text = config.WelcomeMessage;

        LoadLogo(config.LogoPath, baseDirectory);
        BuildFormButtons(config);
    }

    private Color ResolveAccent(AppConfig config, FormEntry form, int index)
    {
        if (ColorHelper.TryParseHex(form.AccentColor, out var formColor))
            return formColor;

        if (config.FormCardAccentColors.Count > 0)
        {
            var hex = config.FormCardAccentColors[index % config.FormCardAccentColors.Count];
            if (ColorHelper.TryParseHex(hex, out var listColor))
                return listColor;
        }

        return DefaultAccents[index % DefaultAccents.Length];
    }

    private void BuildFormButtons(AppConfig config)
    {
        FormsPanel.Children.Clear();

        var count = config.Forms.Count;
        var hasForms = count > 0;
        FormsPanel.Visibility = hasForms ? Visibility.Visible : Visibility.Collapsed;
        NoFormsText.Visibility = hasForms ? Visibility.Collapsed : Visibility.Visible;

        if (!hasForms)
            return;

        FormsPanel.Columns = count;

        for (var i = 0; i < config.Forms.Count; i++)
        {
            var form = config.Forms[i];
            var accent = ResolveAccent(config, form, i);

            var button = new Button
            {
                Style = (Style)Application.Current.FindResource("FormCardButtonLight"),
                Tag = form,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = BuildCardContent(form, accent)
            };
            button.Click += FormButton_Click;
            FormsPanel.Children.Add(button);
        }
    }

    private UIElement BuildCardContent(FormEntry form, Color accent)
    {
        var titleColor = (Color)Application.Current.FindResource("HomeTextColor");
        var mutedColor = (Color)Application.Current.FindResource("HomeMutedColor");
        var accentBrush = new SolidColorBrush(accent);
        var accentSoft = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var accentBar = new Border
        {
            Height = 5,
            CornerRadius = new CornerRadius(3, 3, 0, 0),
            Background = accentBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(-24, -28, -24, 0)
        };
        Grid.SetRow(accentBar, 0);
        root.Children.Add(accentBar);

        var body = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(body, 1);
        body.Children.Add(BuildCardIcon(form, accent, accentBrush, accentSoft));

        body.Children.Add(new TextBlock
        {
            Text = form.Title,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(titleColor),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 240
        });

        if (!string.IsNullOrWhiteSpace(form.Description))
        {
            body.Children.Add(new TextBlock
            {
                Text = form.Description,
                FontSize = 15,
                Foreground = new SolidColorBrush(mutedColor),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                LineHeight = 22,
                Margin = new Thickness(16, 12, 16, 0),
                MaxWidth = 240
            });
        }

        root.Children.Add(body);

        var footer = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = accentSoft,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            Child = new TextBlock
            {
                Text = "\u2192",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private UIElement BuildCardIcon(FormEntry form, Color accent, SolidColorBrush accentBrush, SolidColorBrush accentSoft)
    {
        var iconHost = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            Background = accentSoft,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 18),
            ClipToBounds = true
        };

        if (!string.IsNullOrWhiteSpace(form.IconPath))
        {
            var fullPath = Path.IsPathRooted(form.IconPath)
                ? form.IconPath
                : Path.Combine(_baseDirectory, form.IconPath);

            if (File.Exists(fullPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.EndInit();

                    iconHost.Child = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(10)
                    };
                    RenderOptions.SetBitmapScalingMode(iconHost.Child as Image, BitmapScalingMode.HighQuality);
                    return iconHost;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Icône introuvable pour « {form.Title} » : {ex.Message}");
                }
            }
        }

        var initial = string.IsNullOrWhiteSpace(form.Title)
            ? "?"
            : form.Title.Trim()[0].ToString().ToUpperInvariant();

        iconHost.Child = new TextBlock
        {
            Text = initial,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = accentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return iconHost;
    }

    private void FormButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            PlayPressFeedback(button);
            if (button.Tag is FormEntry form)
                FormSelected?.Invoke(this, form);
        }
    }

    private static void PlayPressFeedback(Button button)
    {
        if (button.Template?.FindName("border", button) is not FrameworkElement border)
            return;

        border.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1);
        border.RenderTransform = scale;

        var animation = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
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
            LogoFrame.Visibility = Visibility.Visible;
            OrgNameText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible de charger le logo.", ex);
        }
    }
}
