using System;
using System.IO;
using System.Text.Json;
using ElvantoKiosk.Models;

namespace ElvantoKiosk.Services;

/// <summary>Chargement / création du fichier de configuration config.json.</summary>
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

    /// <summary>
    /// Charge la configuration. En cas d'absence ou d'erreur de parsing,
    /// renvoie une configuration par défaut (et journalise le problème).
    /// </summary>
    public static AppConfig Load(string baseDirectory)
    {
        var path = ConfigPath(baseDirectory);

        try
        {
            if (!File.Exists(path))
            {
                Logger.Warn($"config.json introuvable ({path}). Création d'une configuration par défaut.");
                var defaults = CreateDefault();
                TrySave(path, defaults);
                return defaults;
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json, Options);

            if (config == null)
            {
                Logger.Error("config.json vide ou invalide. Utilisation des valeurs par défaut.");
                return CreateDefault();
            }

            Logger.Info($"Configuration chargée : {config.Forms.Count} formulaire(s).");
            return config;
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible de lire config.json. Utilisation des valeurs par défaut.", ex);
            return CreateDefault();
        }
    }

    private static void TrySave(string path, AppConfig config)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
        }
        catch (Exception ex)
        {
            Logger.Error("Impossible d'écrire config.json par défaut.", ex);
        }
    }

    private static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            AllowedHosts = { "notion.so", "notion.site", "notion.com" },
            SubmitUrlKeywords = { "thank", "success", "complete", "submitted", "confirmation" },
            SubmitTextKeywords =
            {
                "submission has been received",
                "votre réponse a été enregistrée",
                "formulaire envoyé",
                "merci pour votre envoi"
            },
            Forms =
            {
                new FormEntry
                {
                    Title = "Formulaire d'accueil",
                    Description = "Configurez l'URL dans config.json",
                    Url = "https://VOTRE-SOUS-DOMAINE.elvanto.eu/form/IDENTIFIANT"
                }
            }
        };
    }
}
