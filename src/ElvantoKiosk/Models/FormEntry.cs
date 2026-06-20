namespace ElvantoKiosk.Models;

/// <summary>Un formulaire Notion présenté sur la page d'accueil.</summary>
public class FormEntry
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Chemin de l'icône (relatif à l'exécutable). Laisser vide en attendant les assets.</summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>Couleur d'accent de la carte (hex, ex. #2563EB). Optionnel.</summary>
    public string AccentColor { get; set; } = string.Empty;
}
