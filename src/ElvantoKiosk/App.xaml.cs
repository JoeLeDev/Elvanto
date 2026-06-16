using System;
using System.IO;
using System.Threading;
using System.Windows;
using ElvantoKiosk.Services;

namespace ElvantoKiosk;

public partial class App : Application
{
    /// <summary>Dossier de base = dossier de l'exécutable.</summary>
    public static string BaseDirectory { get; } = AppContext.BaseDirectory;

    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Empêcher plusieurs instances simultanées de la borne.
        _singleInstanceMutex = new Mutex(true, "ElvantoKiosk_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Logger.Init(BaseDirectory);

        // Capturer toutes les exceptions non gérées pour les journaliser.
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Exception non gérée (UI).", args.Exception);
            args.Handled = true; // La borne ne doit pas se fermer sur une erreur.
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error("Exception non gérée (domaine).", args.ExceptionObject as Exception);
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("=== Arrêt de l'application ===");
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
