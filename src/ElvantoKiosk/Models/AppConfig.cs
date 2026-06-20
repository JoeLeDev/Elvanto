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

    /// <summary>Affiche un écran de veille (horloge + logo) au démarrage et après inactivité.</summary>
    public bool ScreensaverEnabled { get; set; } = true;

    /// <summary>Sous-titre affiché sur l'écran de veille.</summary>
    public string ScreensaverSubtitle { get; set; } = "KIOSQUE DE PRISE DE RENDEZ-VOUS";

    /// <summary>Texte d'invitation tactile sur l'écran de veille.</summary>
    public string ScreensaverPrompt { get; set; } = "TOUCHEZ L'ÉCRAN POUR COMMENCER";

    /// <summary>Délai d'inactivité sur la page d'accueil avant retour à l'écran de veille (secondes).</summary>
    public int ScreensaverIdleTimeoutSeconds { get; set; } = 120;

    /// <summary>Retourne à l'écran de veille (au lieu de la page d'accueil) après un formulaire.</summary>
    public bool ReturnToScreensaverOnHome { get; set; } = true;

    /// <summary>Message affiché après soumission d'un formulaire, avant retour à l'accueil.</summary>
    public string ThankYouMessage { get; set; } = "Merci ! Retour à l'accueil…";

    /// <summary>Couleurs d'accent des cartes (hex). Une par formulaire, ou répétées cycliquement.</summary>
    public List<string> FormCardAccentColors { get; set; } = new();

    /// <summary>Domaines autorisés à la navigation. Tout le reste est bloqué.</summary>
    public List<string> AllowedHosts { get; set; } = new();

    /// <summary>Mots-clés d'URL indiquant qu'un formulaire a été soumis (page de remerciement).</summary>
    public List<string> SubmitUrlKeywords { get; set; } = new();

    /// <summary>
    /// Mots-clés recherchés dans le texte affiché par Notion après soumission
    /// (l'URL ne change souvent pas — détection via le message de confirmation).
    /// Astuce : mettez un mot unique dans le titre de confirmation Notion (ex. « ENVOYE »).
    /// </summary>
    public List<string> SubmitTextKeywords { get; set; } = new();

    /// <summary>Liste des formulaires affichés sur la page d'accueil.</summary>
    public List<FormEntry> Forms { get; set; } = new();
}
