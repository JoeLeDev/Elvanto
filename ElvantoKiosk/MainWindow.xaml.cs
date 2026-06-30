using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ElvantoKiosk.Controls;
using ElvantoKiosk.Models;
using ElvantoKiosk.Services;
using Microsoft.Web.WebView2.Core;

namespace ElvantoKiosk;

public partial class MainWindow : Window
{
    private static readonly string[] TouchKeyboardExecutableCandidates =
    {
        @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe",
        @"C:\Program Files\Tablet PC\TabTip.exe"
    };

    private AppConfig _config;
    private readonly KeyboardHook _keyboardHook = new();
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _submitTimer;
    private readonly DispatcherTimer _submitCheckTimer;
    private readonly DispatcherTimer _thankYouCountdownTimer;
    private readonly List<CoreWebView2Frame> _webFrames = new();

    private FormEntry? _currentForm;
    private bool _inFormMode;
    private bool _submitDetected;
    private bool _allowClose;
    private bool _webViewReady;
    private bool _onScreensaver;
    private string? _submitDetectionScript;
    private int _thankYouSecondsLeft;

    public MainWindow()
    {
        InitializeComponent();

        _config = ConfigService.Load(App.BaseDirectory);

        Home.ApplyConfig(_config, App.BaseDirectory);
        Home.FormSelected += OnFormSelected;

        Screensaver.ApplyConfig(_config, App.BaseDirectory);
        Screensaver.Dismissed += (_, _) => ShowHome();

        if (_config.AllowedHosts.Count == 0)
            Logger.Warn("AllowedHosts est vide : la navigation n'est PAS restreinte. Ajoutez les domaines autorisés dans config.json.");

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += IdleTimer_Tick;

        _submitTimer = new DispatcherTimer();
        _submitTimer.Tick += SubmitTimer_Tick;

        _submitCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _submitCheckTimer.Tick += SubmitCheckTimer_Tick;

        _thankYouCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _thankYouCountdownTimer.Tick += ThankYouCountdownTimer_Tick;

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _keyboardHook.Install();
        _idleTimer.Start();
        await InitializeWebViewAsync();

        if (_config.ScreensaverEnabled)
            ShowScreensaver();
        else
            ShowHome();
    }

    // ---------------------------------------------------------------------
    //  Navigation écrans
    // ---------------------------------------------------------------------

    private void ShowScreensaver()
    {
        _onScreensaver = true;
        _inFormMode = false;
        _keyboardHook.SetFormInputActive(false);

        FormContainer.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        Home.Visibility = Visibility.Collapsed;
        EmailHelperBar.Visibility = Visibility.Collapsed;
        AdminExitButton.Visibility = Visibility.Visible;

        Screensaver.Visibility = Visibility.Visible;
        Screensaver.Start();
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x0A, 0x06, 0x18));
    }

    private void ShowHome()
    {
        _onScreensaver = false;
        _inFormMode = false;
        _keyboardHook.SetFormInputActive(false);
        _submitDetected = false;
        _submitTimer.Stop();
        _submitCheckTimer.Stop();
        _currentForm = null;

        Screensaver.Stop();
        Screensaver.Visibility = Visibility.Collapsed;
        FormContainer.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        AdminExitButton.Visibility = Visibility.Visible;
        EmailHelperBar.Visibility = Visibility.Collapsed;
        Home.Visibility = Visibility.Visible;
        Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
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
            Web.CoreWebView2.FrameCreated += OnFrameCreated;
            Web.PreviewTouchDown += (_, _) =>
            {
                if (_inFormMode)
                    EnsureTouchKeyboardVisible();
            };
            Web.PreviewMouseDown += (_, _) =>
            {
                if (_inFormMode)
                    EnsureTouchKeyboardVisible();
            };

            _submitDetectionScript = BuildSubmitDetectionScript();
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_submitDetectionScript);

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
            FocusWebViewForInput();
            _ = InjectMainFrameDetectionScriptAsync();
            _submitCheckTimer.Start();
        }
    }

    private void FocusWebViewForInput()
    {
        if (!_webViewReady)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            Web.Focus();
            Keyboard.Focus(Web);
            EnsureTouchKeyboardVisible();
        }, DispatcherPriority.Input);
    }

    private void EnsureTouchKeyboardVisible()
    {
        try
        {
            if (Process.GetProcessesByName("TabTip").Length > 0)
                return;

            var path = TouchKeyboardExecutableCandidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Impossible d'ouvrir le clavier tactile Windows: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    //  Aide à la saisie e-mail (contourne le bug AltGr de WebView2 tactile)
    // ---------------------------------------------------------------------

    private void EmailHelper_At_Click(object sender, RoutedEventArgs e)
        => _ = InsertTextInWebViewAsync("@");

    private void EmailHelper_Domain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string domain && !string.IsNullOrEmpty(domain))
            _ = InsertTextInWebViewAsync(domain);
    }

    /// <summary>
    /// Insère le texte donné dans le champ actuellement focalisé de la page Notion,
    /// en simulant une vraie saisie clavier pour que React/Notion enregistre la valeur.
    /// Cible la frame qui possède le focus (champ pouvant être dans une iframe).
    /// </summary>
    private async System.Threading.Tasks.Task InsertTextInWebViewAsync(string text)
    {
        if (!_webViewReady || !_inFormMode)
            return;

        // Garde le focus dans la WebView (le clic bouton ne doit pas le voler durablement).
        var script = BuildInsertTextScript(text);

        try
        {
            // 1) Frame principale.
            await Web.CoreWebView2.ExecuteScriptAsync(script);

            // 2) Iframes (les formulaires Notion s'affichent souvent dans une iframe).
            foreach (var frame in _webFrames.ToList())
            {
                try { await frame.ExecuteScriptAsync(script); }
                catch { /* frame détruite entre-temps : ignorer */ }
            }

            FocusWebViewForInput();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Insertion e-mail impossible : {ex.Message}");
        }
    }

    private static string BuildInsertTextScript(string text)
    {
        // Encode le texte en littéral JSON sûr (gère @, ., guillemets, etc.).
        var literal = JsonSerializer.Serialize(text);

        return $@"
(function() {{
  var value = {literal};
  var el = document.activeElement;
  if (!el) return false;

  // Champs standards <input>/<textarea>
  var tag = (el.tagName || '').toLowerCase();
  if (tag === 'input' || tag === 'textarea') {{
    var start = el.selectionStart, end = el.selectionEnd;
    var current = el.value || '';
    var next;
    if (typeof start === 'number' && typeof end === 'number') {{
      next = current.slice(0, start) + value + current.slice(end);
    }} else {{
      next = current + value;
    }}
    // Utilise le setter natif pour que React détecte le changement.
    try {{
      var proto = tag === 'input' ? window.HTMLInputElement.prototype
                                   : window.HTMLTextAreaElement.prototype;
      var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
      setter.call(el, next);
    }} catch (e) {{
      el.value = next;
    }}
    if (typeof start === 'number') {{
      var pos = start + value.length;
      try {{ el.selectionStart = el.selectionEnd = pos; }} catch (e) {{}}
    }}
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
    return true;
  }}

  // Champs contenteditable (cas Notion)
  if (el.isContentEditable) {{
    var ok = false;
    try {{ ok = document.execCommand('insertText', false, value); }} catch (e) {{}}
    if (!ok) {{
      var sel = window.getSelection();
      if (sel && sel.rangeCount > 0) {{
        var range = sel.getRangeAt(0);
        range.deleteContents();
        range.insertNode(document.createTextNode(value));
        range.collapse(false);
      }}
    }}
    el.dispatchEvent(new InputEvent('input', {{ bubbles: true, data: value, inputType: 'insertText' }}));
    return true;
  }}

  return false;
}})();";
    }

    private void OnFrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        var frame = e.Frame;
        _webFrames.Add(frame);
        frame.Destroyed += (_, _) => _webFrames.Remove(frame);
        frame.NavigationCompleted += async (_, args) =>
        {
            if (!_inFormMode || _submitDetected || !args.IsSuccess)
                return;

            await InjectSubmitDetectionScriptAsync(frame);
        };
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

    private string BuildSubmitDetectionScript()
    {
        var keywordsJson = JsonSerializer.Serialize(_config.SubmitTextKeywords);
        return $@"
(function() {{
  if (window.__elvantoSubmitDetector) return;
  window.__elvantoSubmitDetector = true;
  const keywords = {keywordsJson};
  function collectText(root) {{
    const parts = [];
    function walk(node) {{
      if (!node) return;
      if (node.nodeType === 3) {{
        const t = node.textContent;
        if (t) parts.push(t);
        return;
      }}
      if (node.nodeType !== 1) return;
      if (node.shadowRoot) walk(node.shadowRoot);
      for (let i = 0; i < node.childNodes.length; i++) walk(node.childNodes[i]);
    }}
    walk(root);
    return parts.join(' ');
  }}
  function matches(text) {{
    const lower = (text || '').toLowerCase();
    return keywords.some(k => k && lower.includes(String(k).toLowerCase()));
  }}
  function notifyIfSubmitted() {{
    const root = document.body || document.documentElement;
    const text = collectText(root);
    if (matches(text)) {{
      window.chrome.webview.postMessage('submitDetected');
      return true;
    }}
    return false;
  }}
  function start() {{
    const root = document.body || document.documentElement;
    if (!root) return;
    if (notifyIfSubmitted()) return;
    const observer = new MutationObserver(() => {{
      if (notifyIfSubmitted()) observer.disconnect();
    }});
    observer.observe(root, {{ childList: true, subtree: true, characterData: true }});
  }}
  if (document.readyState === 'loading') {{
    document.addEventListener('DOMContentLoaded', start);
  }} else {{
    start();
  }}
}})();";
    }

    private const string ReadPageTextScript = @"
(function() {
  const parts = [];
  function walk(node) {
    if (!node) return;
    if (node.nodeType === 3) {
      const t = node.textContent;
      if (t) parts.push(t);
      return;
    }
    if (node.nodeType !== 1) return;
    if (node.shadowRoot) walk(node.shadowRoot);
    for (let i = 0; i < node.childNodes.length; i++) walk(node.childNodes[i]);
  }
  walk(document.body || document.documentElement);
  return parts.join(' ');
})();";

    private async System.Threading.Tasks.Task InjectMainFrameDetectionScriptAsync()
    {
        if (!_webViewReady || string.IsNullOrEmpty(_submitDetectionScript))
            return;

        try
        {
            await Web.CoreWebView2.ExecuteScriptAsync(_submitDetectionScript);
        }
        catch (Exception ex)
        {
            Logger.Error("Injection du script de détection Notion (frame principale) impossible.", ex);
        }
    }

    private async System.Threading.Tasks.Task InjectSubmitDetectionScriptAsync(CoreWebView2Frame frame)
    {
        if (!_webViewReady || string.IsNullOrEmpty(_submitDetectionScript))
            return;

        try
        {
            await frame.ExecuteScriptAsync(_submitDetectionScript);
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
            if (await PageTextMatchesSubmitKeywordAsync(s => Web.CoreWebView2.ExecuteScriptAsync(s)))
            {
                TriggerSubmitReturn("texte de confirmation Notion");
                return;
            }

            foreach (var frame in _webFrames)
            {
                if (await PageTextMatchesSubmitKeywordAsync(s => frame.ExecuteScriptAsync(s)))
                {
                    TriggerSubmitReturn("texte de confirmation Notion (sous-frame)");
                    return;
                }
            }
        }
        catch
        {
            // Ignorer les erreurs transitoires de lecture DOM.
        }
    }

    private async System.Threading.Tasks.Task<bool> PageTextMatchesSubmitKeywordAsync(
        Func<string, System.Threading.Tasks.Task<string>> executeScript)
    {
        var raw = await executeScript(ReadPageTextScript);
        var pageText = JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
        return ContainsSubmitTextKeyword(pageText);
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

        var delay = Math.Max(1, _config.ReturnDelayAfterSubmitSeconds);
        ShowThankYouOverlay(delay);
        _submitTimer.Interval = TimeSpan.FromSeconds(delay);
        _submitTimer.Start();
    }

    private void ShowThankYouOverlay(int seconds)
    {
        ThankYouMessageText.Text = string.IsNullOrWhiteSpace(_config.ThankYouMessage)
            ? "Merci ! Retour à l'accueil…"
            : _config.ThankYouMessage;
        _thankYouSecondsLeft = seconds;
        UpdateThankYouCountdownText();
        ThankYouOverlay.Visibility = Visibility.Visible;
        _thankYouCountdownTimer.Start();
    }

    private void ThankYouCountdownTimer_Tick(object? sender, EventArgs e)
    {
        _thankYouSecondsLeft--;
        if (_thankYouSecondsLeft <= 0)
        {
            _thankYouCountdownTimer.Stop();
            return;
        }

        UpdateThankYouCountdownText();
    }

    private void UpdateThankYouCountdownText()
    {
        ThankYouCountdownText.Text = _thankYouSecondsLeft <= 1
            ? "Redirection imminente…"
            : $"Redirection dans {_thankYouSecondsLeft} s…";
    }

    private void HideThankYouOverlay()
    {
        _thankYouCountdownTimer.Stop();
        ThankYouOverlay.Visibility = Visibility.Collapsed;
    }

    public void ReloadConfiguration()
    {
        _config = ConfigService.Load(App.BaseDirectory);
        _submitDetectionScript = BuildSubmitDetectionScript();
        Home.ApplyConfig(_config, App.BaseDirectory);
        Screensaver.ApplyConfig(_config, App.BaseDirectory);
        Logger.Info("Configuration rechargée à chaud.");
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
        _onScreensaver = false;
        _inFormMode = true;
        _keyboardHook.SetFormInputActive(true);
        _submitDetected = false;
        _submitTimer.Stop();
        _submitCheckTimer.Stop();
        _webFrames.Clear();

        Screensaver.Stop();
        Screensaver.Visibility = Visibility.Collapsed;
        Home.Visibility = Visibility.Collapsed;

        FormTitleText.Text = form.Title;
        Home.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        AdminExitButton.Visibility = Visibility.Collapsed;
        FormContainer.Visibility = Visibility.Visible;

        var showHome = _config.ShowHomeButton;
        FormHomeButton.Visibility = showHome ? Visibility.Visible : Visibility.Collapsed;
        EmailHelperBar.Visibility = _config.ShowEmailHelperBar ? Visibility.Visible : Visibility.Collapsed;
        ShowLoading();

        Logger.Info($"Ouverture du formulaire « {form.Title} » : {form.Url}");
        Web.CoreWebView2.Navigate(form.Url);
    }

    private async void ReturnHome(string reason, bool toScreensaver = false)
    {
        if (!_inFormMode && !_onScreensaver && Home.Visibility != Visibility.Visible)
            return;

        if (_onScreensaver)
            return;

        Logger.Info($"Retour à l'accueil ({reason}).");

        HideThankYouOverlay();
        _inFormMode = false;
        _keyboardHook.SetFormInputActive(false);
        _submitDetected = false;
        _submitTimer.Stop();
        _submitCheckTimer.Stop();
        _currentForm = null;

        FormContainer.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        HideLoading();

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

        if (toScreensaver && _config.ScreensaverEnabled)
            ShowScreensaver();
        else
            ShowHome();
    }

    // ---------------------------------------------------------------------
    //  Minuteurs
    // ---------------------------------------------------------------------

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        if (_onScreensaver)
            return;

        var idle = IdleTimeService.GetIdleTime().TotalSeconds;

        if (_inFormMode && idle >= _config.InactivityTimeoutSeconds)
        {
            ReturnHome($"inactivité {(int)idle}s", _config.ReturnToScreensaverOnHome);
            return;
        }

        if (!_inFormMode && _config.ScreensaverEnabled
            && Home.Visibility == Visibility.Visible
            && idle >= _config.ScreensaverIdleTimeoutSeconds)
        {
            ShowScreensaver();
        }
    }

    private void SubmitTimer_Tick(object? sender, EventArgs e)
    {
        _submitTimer.Stop();
        HideThankYouOverlay();
        ReturnHome("formulaire soumis", _config.ReturnToScreensaverOnHome);
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
        // Raccourci administrateur : Ctrl+Maj+Q (toujours actif)
        if (e.Key == Key.Q &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            e.Handled = true;
            TryAdminExit();
            return;
        }

        // Rechargement config à chaud : Ctrl+Maj+R
        if (e.Key == Key.R &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            e.Handled = true;
            ReloadConfiguration();
            return;
        }

        // En mode formulaire (ou AltGr) : laisser WebView2 / clavier tactile gérer la saisie.
        if (_inFormMode || IsAltGrPressed())
            return;

        // Alt+F4
        if (e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            return;
        }

        // F5 / Ctrl+R (rechargement), F11, Ctrl+L/W/N/T/P/J/O/S
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (e.Key is Key.F5 or Key.F11 or Key.F12 or Key.BrowserBack or Key.BrowserForward or Key.BrowserRefresh)
        {
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key is Key.R or Key.L or Key.W or Key.N or Key.T or Key.P or Key.J or Key.O or Key.S or Key.H)
            e.Handled = true;
    }

    private static bool IsAltGrPressed() =>
        (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) ==
        (ModifierKeys.Control | ModifierKeys.Alt);

    private void TryAdminExit()
    {
        var dialog = new PinDialog(_config.AdminPin) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            Logger.Warn("Tentative d'accès administrateur annulée ou PIN incorrect.");
            return;
        }

        var admin = new AdminPanelWindow(_config, App.BaseDirectory) { Owner = this };
        if (admin.ShowDialog() != true)
            return;

        if (admin.ConfigSaved || admin.ConfigReloadRequested)
            ReloadConfiguration();

        if (admin.RequestQuit)
        {
            Logger.Info("Sortie du mode kiosque autorisée par l'administrateur.");
            _allowClose = true;
            Close();
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
        _thankYouCountdownTimer.Stop();
        _keyboardHook.Dispose();
        Web?.Dispose();
    }
}
