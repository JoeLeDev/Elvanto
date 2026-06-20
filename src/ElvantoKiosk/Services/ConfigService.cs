using System;
using System.IO;
using System.Text.Json;
using ElvantoKiosk.Models;

namespace ElvantoKiosk.Services;

/// <summary>Chargement / sauvegarde du fichier de configuration config.json.</summary>
public static class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static string ConfigPath(string baseDirectory) =>
        Path.Combine(baseDirectory, "config.json");

    public static AppConfig Load(string baseDirectory)
    {
        var path = ConfigPath(baseDirectory);

        try
        {
            if (!File.Exists(path))
            {
                Logger.Warn($"config.json introuvable ({path}). Création d'une configuration par défaut.");
                var defaults = CreateDefault();
                TrySave(baseDirectory, defaults);
                return defaults;
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json, Options);

            if (config == null)
            {
                Logger.Error("config.json vide ou invalide. Utilisation des valeurs par défaut.");
                return CreateDefault();
            }

            EnsureDefaults(config);
            Logger.Info($"Configuration chargée : {config.Forms.Count} formulaire(s).");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible de lire config.json. Utilisation des valeurs par défaut.", ex);
            return CreateDefault();
        }
    }

    public static (bool Success, string Message) Save(string baseDirectory, AppConfig config)
    {
        try
        {
            EnsureDefaults(config);
            var path = ConfigPath(baseDirectory);
            var json = JsonSerializer.Serialize(config, Options);
            File.WriteAllText(path, json);
            Logger.Info("Configuration enregistrée depuis le panneau admin.");
            return (true, "Configuration enregistrée.");
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible d'enregistrer config.json.", ex);
            return (false, $"Erreur : {ex.Message}");
        }
    }

    private static void EnsureDefaults(AppConfig config)
    {
        if (config.FormCardAccentColors.Count == 0)
        {
            config.FormCardAccentColors =
            [
                "#DC2626",
                "#CA8A04",
                "#2563EB",
                "#7C3AED"
            ];
        }
    }

    private static void TrySave(string baseDirectory, AppConfig config)
    {
        Save(baseDirectory, config);
    }

    private static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            ThankYouMessage = "Merci ! Retour à l'accueil…",
            ScreensaverEnabled = true,
            ScreensaverSubtitle = "KIOSQUE DE PRISE DE RENDEZ-VOUS",
            ScreensaverPrompt = "TOUCHEZ L'ÉCRAN POUR COMMENCER",
            FormCardAccentColors =
            [
                "#DC2626",
                "#CA8A04",
                "#2563EB",
                "#7C3AED"
            ],
            AllowedHosts = { "notion.so", "notion.site", "notion.com" },
            SubmitUrlKeywords = { "thank", "success", "complete", "submitted", "confirmation" },
            SubmitTextKeywords =
            {
                "nous avons bien reçu votre réponse",
                "nous avons bien reçu",
                "envoyer une copie",
                "FORMULAIRE_ENVOYÉ"
            },
            Forms =
            {
                new FormEntry
                {
                    Title = "Formulaire d'accueil",
                    Description = "Configurez l'URL dans config.json",
                    Url = "https://notion.so"
                }
            }
        };
    }
}
