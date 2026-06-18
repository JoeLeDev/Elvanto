using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ElvantoKiosk.Controls;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;
using Microsoft.Web.WebView2.Core;

namespace ElvantoKiosk;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly KeyboardHook _keyboardHook = new();
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _submitTimer;
    private readonly DispatcherTimer _submitCheckTimer;

    private FormEntry? _currentForm;
    private bool _inFormMode;
    private bool _submitDetected;
    private bool _allowClose;
    private bool _webViewReady;

    public MainWindow()
    {
        InitializeComponent();

        _config = ConfigService.Load(App.BaseDirectory);

        Home.ApplyConfig(_config, App.BaseDirectory);
        Home.FormSelected += OnFormSelected;

        if (_config.AllowedHosts.Count == 0)
            Logger.Warn("AllowedHosts est vide : la navigation n'est PAS restreinte. Ajoutez les domaines autorisés dans config.json.");

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += IdleTimer_Tick;

        _submitTimer = new DispatcherTimer();
        _submitTimer.Tick += SubmitTimer_Tick;

        _submitCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _submitCheckTimer.Tick += SubmitCheckTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _keyboardHook.Install();
        _idleTimer.Start();
        await InitializeWebViewAsync();
    }

    // ---------------------------------------------------------------------
    //  WebView2
    // ---------------------------------------------------------------------

    private async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(App.BaseDirectory, "WebView2Data");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Web.EnsureCoreWebView2Async(env);

            var settings = Web.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsSwipeNavigationEnabled = false;

            Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Web.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Web.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            Web.CoreWebView2.SourceChanged += OnSourceChanged;
            Web.CoreWebView2.ProcessFailed += OnProcessFailed;
            Web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            _webViewReady = true;
            Logger.Info("WebView2 initialisé.");
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Logger.Error("Runtime WebView2 introuvable. Exécutez le script d'installation.", ex);
            ShowError("Le composant WebView2 n'est pas installé. Contactez l'administrateur (exécutez install.ps1).");
        }
        catch (Exception ex)
        {
            Logger.Error("Échec de l'initialisation de WebView2.", ex);
            ShowError("Erreur d'initialisation du navigateur intégré.");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsUriAllowed(e.Uri))
        {
            e.Cancel = true;
            Logger.Warn($"Navigation bloquée vers une URL non autorisée : {e.Uri}");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        HideLoading();

        if (!e.IsSuccess)
        {
            Logger.Error($"Échec du chargement ({e.WebErrorStatus}) du formulaire « {_currentForm?.Title} » : {Web.Source}");

            var message = e.WebErrorStatus switch
            {
                CoreWebView2WebErrorStatus.HostNameNotResolved or
                CoreWebView2WebErrorStatus.CannotConnect or
                CoreWebView2WebErrorStatus.Disconnected or
                CoreWebView2WebErrorStatus.ConnectionAborted or
                CoreWebView2WebErrorStatus.Timeout =>
                    "Le formulaire n'a pas pu être chargé. Vérifiez la connexion Internet. Réessayez ou revenez à l'accueil.",
                _ => "Une erreur est survenue lors du chargement du formulaire."
            };

            if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                ShowError(message);

            return;
        }

        if (_inFormMode)
        {
            _ = InjectSubmitDetectionScriptAsync();
            _submitCheckTimer.Start();
        }
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (!_inFormMode || _submitDetected)
            return;

        var url = Web.Source?.ToString() ?? string.Empty;
        if (_config.SubmitUrlKeywords.Any(k =>
                !string.IsNullOrWhiteSpace(k) &&
                url.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            TriggerSubmitReturn($"URL : {url}");
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_inFormMode || _submitDetected)
            return;

        var message = e.TryGetWebMessageAsString();
        if (message == "submitDetected")
            TriggerSubmitReturn("page Notion (confirmation)");
    }

    private async System.Threading.Tasks.Task InjectSubmitDetectionScriptAsync()
    {
        if (!_webViewReady || _config.SubmitTextKeywords.Count == 0)
            return;

        try
        {
            var keywordsJson = JsonSerializer.Serialize(_config.SubmitTextKeywords);
            var script = $@"
(function() {{
  const keywords = {keywordsJson};
  function matches(text) {{
    const lower = (text || '').toLowerCase();
    return keywords.some(k => k && lower.includes(String(k).toLowerCase()));
  }}
  function notifyIfSubmitted() {{
    const text = document.body ? document.body.innerText : '';
    if (matches(text)) {{
      window.chrome.webview.postMessage('submitDetected');
      return true;
    }}
    return false;
  }}
  function start() {{
    if (!document.body) return;
    if (notifyIfSubmitted()) return;
    const observer = new MutationObserver(() => {{
      if (notifyIfSubmitted()) observer.disconnect();
    }});
    observer.observe(document.body, {{ childList: true, subtree: true, characterData: true }});
  }}
  if (document.readyState === 'loading') {{
    document.addEventListener('DOMContentLoaded', start);
  }} else {{
    start();
  }}
}})();";

            await Web.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            Logger.Error("Injection du script de détection Notion impossible.", ex);
        }
    }

    private async void SubmitCheckTimer_Tick(object? sender, EventArgs e)
    {
        if (!_inFormMode || _submitDetected || !_webViewReady)
            return;

        try
        {
            var raw = await Web.CoreWebView2.ExecuteScriptAsync(
                "document.body ? document.body.innerText : ''");

            var pageText = JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
            if (ContainsSubmitTextKeyword(pageText))
                TriggerSubmitReturn("texte de confirmation Notion");
        }
        catch
        {
            // Ignorer les erreurs transitoires de lecture DOM.
        }
    }

    private bool ContainsSubmitTextKeyword(string pageText)
    {
        if (string.IsNullOrWhiteSpace(pageText))
            return false;

        return _config.SubmitTextKeywords.Any(k =>
            !string.IsNullOrWhiteSpace(k) &&
            pageText.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private void TriggerSubmitReturn(string source)
    {
        if (_submitDetected)
            return;

        Logger.Info($"Soumission détectée ({source}). Retour à l'accueil dans {_config.ReturnDelayAfterSubmitSeconds}s.");
        _submitDetected = true;
        _submitCheckTimer.Stop();
        _submitTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _config.ReturnDelayAfterSubmitSeconds));
        _submitTimer.Start();
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Aucune nouvelle fenêtre / onglet. On reste dans la même vue.
        e.Handled = true;

        if (IsUriAllowed(e.Uri))
            Web.CoreWebView2.Navigate(e.Uri);
        else
            Logger.Warn($"Ouverture de fenêtre bloquée : {e.Uri}");
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Logger.Error($"Processus WebView2 défaillant : {e.ProcessFailedKind}");
        ShowError("Le navigateur intégré a rencontré un problème. Retour à l'accueil.");
    }

    private bool IsUriAllowed(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        if (uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return true;

        // Pas de liste => aucune restriction (déconseillé, journalisé au démarrage).
        if (_config.AllowedHosts.Count == 0)
            return true;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;

        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            return false;

        var host = parsed.Host;
        return _config.AllowedHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------------
    //  Navigation borne
    // ---------------------------------------------------------------------

    private void OnFormSelected(object? sender, FormEntry form)
    {
        if (!_webViewReady)
        {
            ShowError("Le navigateur intégré n'est pas encore prêt. Réessayez dans un instant.");
            return;
        }

        if (!IsUriAllowed(form.Url))
        {
            Logger.Warn($"URL de formulaire non autorisée (vérifiez AllowedHosts) : {form.Url}");
            ShowError("Ce formulaire pointe vers une adresse non autorisée. Contactez l'administrateur.");
            return;
        }

        _currentForm = form;
        _inFormMode = true;
        _submitDetected = false;
        _submitTimer.Stop();
        _submitCheckTimer.Stop();

        FormTitleText.Text = form.Title;
        Home.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        AdminExitButton.Visibility = Visibility.Collapsed;
        FormContainer.Visibility = Visibility.Visible;

        var showHome = _config.ShowHomeButton;
        FormHomeButton.Visibility = showHome ? Visibility.Visible : Visibility.Collapsed;
        ShowLoading();

        Logger.Info($"Ouverture du formulaire « {form.Title} » : {form.Url}");
        Web.CoreWebView2.Navigate(form.Url);
    }

    private async void ReturnHome(string reason)
    {
        if (!_inFormMode)
            return;

        Logger.Info($"Retour à l'accueil ({reason}).");

        _inFormMode = false;
        _submitDetected = false;
        _submitTimer.Stop();
        _submitCheckTimer.Stop();
        _currentForm = null;

        FormContainer.Visibility = Visibility.Collapsed;
        AdminExitButton.Visibility = Visibility.Visible;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        HideLoading();
        Home.Visibility = Visibility.Visible;

        if (_webViewReady)
        {
            try
            {
                Web.CoreWebView2.Navigate("about:blank");

                if (_config.ClearDataOnReturnHome)
                {
                    await Web.CoreWebView2.Profile.ClearBrowsingDataAsync(
                        CoreWebView2BrowsingDataKinds.Cookies |
                        CoreWebView2BrowsingDataKinds.AllDomStorage |
                        CoreWebView2BrowsingDataKinds.DiskCache);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Erreur lors de la réinitialisation des données de session.", ex);
            }
        }
    }

    // ---------------------------------------------------------------------
    //  Minuteurs
    // ---------------------------------------------------------------------

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        if (!_inFormMode)
            return;

        var idle = IdleTimeService.GetIdleTime().TotalSeconds;
        if (idle >= _config.InactivityTimeoutSeconds)
            ReturnHome($"inactivité {(int)idle}s");
    }

    private void SubmitTimer_Tick(object? sender, EventArgs e)
    {
        _submitTimer.Stop();
        ReturnHome("formulaire soumis");
    }

    // ---------------------------------------------------------------------
    //  Overlays
    // ---------------------------------------------------------------------

    private void ShowLoading()
    {
        FormLoadingPlaceholder.Visibility = Visibility.Visible;
        Web.Visibility = Visibility.Collapsed;
    }

    private void HideLoading()
    {
        FormLoadingPlaceholder.Visibility = Visibility.Collapsed;
        Web.Visibility = Visibility.Visible;
    }

    private void ShowError(string detail)
    {
        ErrorDetailText.Text = detail;
        HideLoading();
        FormContainer.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Visible;
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;

        if (_currentForm != null && _webViewReady)
        {
            FormContainer.Visibility = Visibility.Visible;
            AdminExitButton.Visibility = Visibility.Collapsed;
            ShowLoading();
            Web.CoreWebView2.Navigate(_currentForm.Url);
        }
        else
        {
            ReturnHome("réessayer sans formulaire");
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => ReturnHome("bouton Accueil");

    private void AdminExitButton_Click(object sender, RoutedEventArgs e) => TryAdminExit();

    // ---------------------------------------------------------------------
    //  Sécurité : touches & fermeture
    // ---------------------------------------------------------------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Raccourci administrateur : Ctrl+Maj+Q
        if (e.Key == Key.Q &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            e.Handled = true;
            TryAdminExit();
            return;
        }

        // Alt+F4
        if (e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        // F5 / Ctrl+R (rechargement), F11, Ctrl+L/W/N/T/P/J/O/S, Backspace de navigation
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (e.Key is Key.F5 or Key.F11 or Key.F12 or Key.BrowserBack or Key.BrowserForward or Key.BrowserRefresh)
        {
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key is Key.R or Key.L or Key.W or Key.N or Key.T or Key.P or Key.J or Key.O or Key.S or Key.H)
        {
            e.Handled = true;
        }
    }

    private void TryAdminExit()
    {
        var dialog = new PinDialog(_config.AdminPin) { Owner = this };
        var result = dialog.ShowDialog();

        if (result == true)
        {
            Logger.Info("Sortie du mode kiosque autorisée par l'administrateur.");
            _allowClose = true;
            Close();
        }
        else
        {
            Logger.Warn("Tentative de sortie administrateur annulée ou PIN incorrect.");
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            // Empêche toute fermeture accidentelle (Alt+F4, fermeture système, etc.).
            e.Cancel = true;
            return;
        }

        _idleTimer.Stop();
        _submitTimer.Stop();
        _submitCheckTimer.Stop();
        _keyboardHook.Dispose();
        Web?.Dispose();
    }
}
