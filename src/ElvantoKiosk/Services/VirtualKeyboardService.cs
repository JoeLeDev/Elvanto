using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ElvantoKiosk.Services;

/// <summary>
/// Gère le clavier virtuel en mode formulaire.
/// TabTip (clavier tactile tablette) ne transmet pas correctement certains caractères
/// AltGr comme « @ » dans WebView2 (bug connu Microsoft). Le clavier visuel Windows
/// (osk.exe) fonctionne de manière fiable sur les bornes ICC.
/// </summary>
public sealed class VirtualKeyboardService
{
    public const string ModeOsk = "Osk";
    public const string ModeTabTip = "TabTip";
    public const string ModeNone = "None";

    private static readonly string OskPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "osk.exe");

    private static readonly string[] TabTipPaths =
    {
        @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe",
        @"C:\Program Files\Tablet PC\TabTip.exe"
    };

    private bool _useOsk = true;
    private bool _disabled;
    private bool _startedOsk;

    public void ApplyConfig(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) ||
            string.Equals(mode, ModeNone, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "false", StringComparison.OrdinalIgnoreCase))
        {
            _disabled = true;
            _useOsk = false;
            return;
        }

        _disabled = false;
        _useOsk = !string.Equals(mode, ModeTabTip, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ouvre le clavier configuré et ferme TabTip si on utilise OSK.</summary>
    public void EnsureVisible()
    {
        if (_disabled)
            return;

        try
        {
            if (_useOsk)
            {
                DismissTabTip();
                EnsureOskVisible();
                return;
            }

            EnsureTabTipVisible();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Impossible d'afficher le clavier virtuel : {ex.Message}");
        }
    }

    /// <summary>Ferme TabTip s'il s'est rouvert automatiquement (mode OSK).</summary>
    public void SuppressTabletKeyboard()
    {
        if (_disabled || !_useOsk)
            return;

        try
        {
            if (Process.GetProcessesByName("TabTip").Length > 0)
                DismissTabTip();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Impossible de fermer le clavier tactile : {ex.Message}");
        }
    }

    public void OnFormClosed()
    {
        if (!_startedOsk)
            return;

        try
        {
            foreach (var process in Process.GetProcessesByName("osk"))
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(500))
                        process.Kill();
                }
                catch
                {
                    // Ignorer si le clavier a déjà été fermé manuellement.
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Impossible de fermer le clavier visuel : {ex.Message}");
        }
        finally
        {
            _startedOsk = false;
        }
    }

    private void EnsureOskVisible()
    {
        if (!File.Exists(OskPath))
        {
            Logger.Warn($"Clavier visuel Windows introuvable : {OskPath}");
            return;
        }

        if (Process.GetProcessesByName("osk").Length > 0)
            return;

        Process.Start(new ProcessStartInfo(OskPath) { UseShellExecute = true });
        _startedOsk = true;
        Logger.Info("Clavier visuel Windows (OSK) ouvert pour la saisie formulaire.");
    }

    private static void EnsureTabTipVisible()
    {
        if (Process.GetProcessesByName("TabTip").Length > 0)
            return;

        var path = TabTipPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(path))
        {
            Logger.Warn("Clavier tactile TabTip introuvable sur ce poste.");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Logger.Info("Clavier tactile TabTip ouvert pour la saisie formulaire.");
    }

    private static void DismissTabTip()
    {
        foreach (var process in Process.GetProcessesByName("TabTip"))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // TabTip peut se fermer entre-temps.
            }
        }
    }
}
