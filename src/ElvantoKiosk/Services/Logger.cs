using System;
using System.IO;
using System.Text;

namespace ElvantoKiosk.Services;

/// <summary>
/// Journalisation simple et thread-safe vers logs\application.log
/// (relatif au dossier de l'exécutable).
/// </summary>
public static class Logger
{
    private static readonly object Sync = new();
    private static string _logFile = string.Empty;
    private const long MaxBytes = 5 * 1024 * 1024; // 5 Mo avant rotation

    public static void Init(string baseDirectory)
    {
        try
        {
            var logDir = Path.Combine(baseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            _logFile = Path.Combine(logDir, "application.log");
            Info($"=== Démarrage de l'application (v{typeof(Logger).Assembly.GetName().Version}) ===");
        }
        catch
        {
            // La journalisation ne doit jamais faire planter l'application.
            _logFile = string.Empty;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        var full = ex == null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        Write("ERROR", full);
    }

    public static string GetLogFilePath() => _logFile;

    public static string ReadRecentLines(int maxLines = 200)
    {
        if (string.IsNullOrEmpty(_logFile) || !File.Exists(_logFile))
            return "Aucun journal disponible.";

        lock (Sync)
        {
            try
            {
                var lines = File.ReadAllLines(_logFile, Encoding.UTF8);
                if (lines.Length <= maxLines)
                    return string.Join(Environment.NewLine, lines);

                return string.Join(Environment.NewLine, lines[^maxLines..]);
            }
            catch (Exception ex)
            {
                return $"Impossible de lire le journal : {ex.Message}";
            }
        }
    }

    private static void Write(string level, string message)
    {
        if (string.IsNullOrEmpty(_logFile))
            return;

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

        lock (Sync)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_logFile, line, Encoding.UTF8);
            }
            catch
            {
                // On ignore toute erreur d'écriture du journal.
            }
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(_logFile);
            if (info.Exists && info.Length > MaxBytes)
            {
                var archive = _logFile + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".old";
                File.Move(_logFile, archive);
            }
        }
        catch
        {
            // Ignorer les erreurs de rotation.
        }
    }
}
