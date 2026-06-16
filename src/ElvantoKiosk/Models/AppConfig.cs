using System.Collections.Generic;

namespace ElvantoKiosk.Models;

/// <summary>
/// Configuration de la borne, chargée depuis config.json (modifiable sans recompiler).
/// </summary>
public class AppConfig
{
    public string OrganizationName { get; set; } = "Mon Organisation";

    public string WelcomeTitle { get; set; } = "Bienvenue";

    public string WelcomeMessage { get; set; } = "Merci de compléter l'un des formulaires ci-dessous.";

    /// <summary>Chemin du logo, relatif au dossier de l'exécutable (ou absolu).</summary>
    public string LogoPath { get; set; } = "assets/logo.png";

    /// <summary>Code PIN demandé pour quitter le mode kiosque (Ctrl+Shift+Q).</summary>
    public string AdminPin { get; set; } = "1234";

    /// <summary>Délai d'inactivité (en secondes) avant retour automatique à l'accueil.</summary>
    public int InactivityTimeoutSeconds { get; set; } = 60;

    /// <summary>Délai (en secondes) après détection d'une soumission avant retour à l'accueil.</summary>
    public int ReturnDelayAfterSubmitSeconds { get; set; } = 5;

    /// <summary>Affiche un bouton « Accueil » discret pendant l'affichage d'un formulaire.</summary>
    public bool ShowHomeButton { get; set; } = true;

    /// <summary>Efface cookies / données de session au retour à l'accueil (confidentialité entre visiteurs).</summary>
    public bool ClearDataOnReturnHome { get; set; } = true;

    /// <summary>Domaines autorisés à la navigation. Tout le reste est bloqué.</summary>
    public List<string> AllowedHosts { get; set; } = new();

    /// <summary>Mots-clés d'URL indiquant qu'un formulaire a été soumis (page de remerciement).</summary>
    public List<string> SubmitUrlKeywords { get; set; } = new();

    /// <summary>Liste des formulaires affichés sur la page d'accueil.</summary>
    public List<FormEntry> Forms { get; set; } = new();
}
