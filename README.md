# Elvanto Kiosk

Borne d'accueil Windows en **mode kiosque** pour formulaires **Elvanto**.

Au démarrage du poste, l'application s'ouvre automatiquement en plein écran et
affiche une **page d'accueil** (logo + message de bienvenue) proposant plusieurs
**boutons vers différents formulaires Elvanto**. Le visiteur choisit un
formulaire, le remplit dans un navigateur intégré, puis la borne revient
automatiquement à l'accueil après soumission ou après une période d'inactivité.

- **Technologies** : C# / .NET 8, WPF, Microsoft Edge WebView2
- **Compatibilité** : Windows 10 et Windows 11 (x64)
- **Exécutable autonome** : le runtime .NET est embarqué (rien à installer côté .NET)

---

## Sommaire

1. [Fonctionnement](#fonctionnement)
2. [Structure du projet](#structure-du-projet)
3. [Configuration (`config.json`)](#configuration-configjson)
4. [Compilation](#compilation)
5. [Installation sur la borne](#installation-sur-la-borne)
6. [Mode administrateur / sortie du kiosque](#mode-administrateur--sortie-du-kiosque)
7. [Journalisation](#journalisation)
8. [Sécurité & verrouillage complet de Windows](#sécurité--verrouillage-complet-de-windows)
9. [Dépannage](#dépannage)

---

## Fonctionnement

- **Plein écran** sans bordure, sans barre d'adresse ni menus, toujours au premier plan.
- **Page d'accueil maison** : logo de l'organisation, titre et message de bienvenue,
  et une carte cliquable par formulaire défini dans la configuration.
- **Affichage du formulaire** dans un WebView2 embarqué.
- **Retour automatique à l'accueil** :
  - après **soumission** détectée (URL de remerciement) — délai configurable ;
  - après **60 secondes d'inactivité** (configurable) ;
  - via un bouton **« Accueil »** discret.
- **Réinitialisation des données** : cookies / stockage de session effacés au retour
  à l'accueil, pour la confidentialité entre visiteurs.
- **Message d'erreur convivial** si Internet est indisponible, avec bouton *Réessayer*.
- **Sécurité** : touches système neutralisées (touche Windows, Alt+Tab, Alt+F4,
  Ctrl+Échap, F11, F5, Ctrl+L/W/N/T…), navigation limitée aux domaines autorisés,
  blocage des nouvelles fenêtres/onglets, fermeture accidentelle empêchée.

---

## Structure du projet

```
Elvanto/
├─ ElvantoKiosk.sln
├─ build.ps1                      # Compile l'exécutable autonome -> .\publish
├─ installer/
│  ├─ install.ps1                # Installe WebView2, copie, démarrage auto, raccourcis
│  └─ uninstall.ps1              # Désinstalle proprement
├─ src/ElvantoKiosk/
│  ├─ ElvantoKiosk.csproj
│  ├─ app.manifest               # DPI awareness, Windows 10/11
│  ├─ App.xaml(.cs)              # Démarrage, instance unique, gestion d'erreurs
│  ├─ MainWindow.xaml(.cs)       # Fenêtre kiosque + WebView2 + sécurité
│  ├─ config.json                # Configuration (URLs, PIN, délais…) — MODIFIABLE
│  ├─ assets/                    # logo.png (à fournir)
│  ├─ Controls/
│  │  ├─ HomeView.xaml(.cs)      # Page d'accueil (logo, message, boutons)
│  │  └─ PinDialog.xaml(.cs)     # Saisie du code PIN administrateur
│  ├─ Models/                    # AppConfig, FormEntry
│  └─ Services/                  # ConfigService, Logger, IdleTimeService, KeyboardHook
└─ README.md
```

---

## Configuration (`config.json`)

L'URL des formulaires (et tous les réglages) se modifient **sans recompiler**.
Le fichier se trouve à côté de l'exécutable (après installation :
`C:\Program Files\ElvantoKiosk\config.json`).

```json
{
  "OrganizationName": "Mon Organisation",
  "WelcomeTitle": "Bienvenue",
  "WelcomeMessage": "Merci de compléter l'un des formulaires ci-dessous.",
  "LogoPath": "assets/logo.png",
  "AdminPin": "1234",
  "InactivityTimeoutSeconds": 60,
  "ReturnDelayAfterSubmitSeconds": 5,
  "ShowHomeButton": true,
  "ClearDataOnReturnHome": true,
  "AllowedHosts": ["elvanto.eu", "elvanto.com", "elvanto.com.au"],
  "SubmitUrlKeywords": ["thank", "merci", "success", "complete", "submitted", "confirmation"],
  "Forms": [
    {
      "Title": "Première visite",
      "Description": "Inscrivez-vous si c'est votre première venue.",
      "Url": "https://VOTRE-SOUS-DOMAINE.elvanto.eu/form/IDENTIFIANT"
    }
  ]
}
```

| Clé | Rôle |
|---|---|
| `OrganizationName` | Nom affiché (et utilisé si aucun logo). |
| `WelcomeTitle` / `WelcomeMessage` | Textes d'accueil. |
| `LogoPath` | Chemin du logo (relatif à l'exe ou absolu). |
| `AdminPin` | Code PIN pour quitter le kiosque (**à changer !**). |
| `InactivityTimeoutSeconds` | Inactivité avant retour à l'accueil (défaut 60). |
| `ReturnDelayAfterSubmitSeconds` | Délai après soumission avant retour à l'accueil. |
| `ShowHomeButton` | Affiche le bouton « Accueil » pendant un formulaire. |
| `ClearDataOnReturnHome` | Efface cookies/session entre visiteurs. |
| `AllowedHosts` | Domaines autorisés à la navigation (sécurité). |
| `SubmitUrlKeywords` | Mots-clés d'URL signalant une soumission réussie. |
| `Forms[]` | Liste des formulaires (titre, description, URL). |

> **Remplacez les URLs `VOTRE-SOUS-DOMAINE` / `IDENTIFIANT`** par vos vrais
> formulaires Elvanto. Ajoutez autant d'entrées que nécessaire dans `Forms`.

---

## Compilation

Sous **Windows**, avec le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) :

```powershell
# Depuis la racine du projet
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Résultat : un exécutable autonome dans `.\publish\ElvantoKiosk.exe`
(le runtime .NET 8 est embarqué — aucune dépendance .NET à installer).

---

## Installation sur la borne

1. Copiez le dossier `publish\` **et** le dossier `installer\` sur la borne.
2. Déposez votre logo dans `publish\assets\logo.png` (facultatif).
3. Lancez l'installation (clic droit → *Exécuter avec PowerShell*, ou) :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\install.ps1
```

Le script :
- installe le **runtime WebView2** s'il est absent ;
- copie l'application dans `C:\Program Files\ElvantoKiosk` ;
- conserve un `config.json` déjà présent (sauf `-Force`) ;
- configure le **démarrage automatique après connexion** (raccourci dans
  `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup`) ;
- crée un **raccourci d'administration** sur le bureau (ouvre le dossier
  contenant `config.json` et les journaux).

Variante avec tâche planifiée :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\install.ps1 -StartupMethod ScheduledTask
```

Désinstallation :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\uninstall.ps1
```

---

## Mode administrateur / sortie du kiosque

L'utilisateur ne peut **pas** fermer l'application (Alt+F4, croix, etc. sont bloqués).

Pour quitter (administrateur), deux méthodes :

1. **Bouton « Fermer »** (coin inférieur droit, icône cadenas) — adapté aux écrans tactiles.
2. Raccourci clavier **Ctrl + Maj + Q** (si un clavier est branché).

Dans les deux cas, un **clavier numérique à l'écran** demande le code PIN défini
dans `config.json` (`AdminPin`).

> Changez impérativement le PIN par défaut (`1234`) avant le déploiement.

### Matériel testé

Compatible avec les mini-PC tactiles Windows 11 (ex. MSI Cubi 5, écran 10 points).
Les boutons et le clavier PIN sont dimensionnés pour une utilisation au doigt.

---

## Journalisation

Les événements et erreurs (connexion, chargement, démarrage WebView2…) sont
enregistrés dans :

```
<dossier d'installation>\logs\application.log
```

Rotation automatique au-delà de 5 Mo.

---

## Sécurité & verrouillage complet de Windows

L'application bloque déjà la plupart des raccourcis (touche Windows, Alt+Tab,
Alt+Échap, Ctrl+Échap, Alt+F4, F11, F5, raccourcis navigateur…) via un hook
clavier bas niveau, restreint la navigation aux `AllowedHosts` et empêche
l'ouverture de nouvelles fenêtres.

**Limite technique :** `Ctrl+Alt+Suppr` et `Ctrl+Maj+Échap` (Gestionnaire des
tâches) ne peuvent **pas** être interceptés par une application. Pour un
verrouillage total d'une borne en libre accès, complétez par une configuration
Windows dédiée :

- **Compte kiosque dédié** (utilisateur standard, sans droits admin).
- **Accès attribué / Shell Launcher** (Windows 10/11 Pro/Entreprise) pour
  remplacer l'explorateur par l'application.
- Stratégies de groupe : masquer le Gestionnaire des tâches, désactiver le
  changement rapide d'utilisateur, etc.

Ces réglages relèvent de l'administration système Windows et viennent en
complément de l'application.

---

## Dépannage

| Symptôme | Piste |
|---|---|
| Page d'erreur « Connexion indisponible » | Vérifier la connexion Internet ; consulter `logs\application.log`. |
| « WebView2 n'est pas installé » | Relancer `install.ps1` (installe le runtime) ou installer manuellement. |
| Un bouton de formulaire ne charge rien | Vérifier l'URL dans `config.json` et que son domaine est dans `AllowedHosts`. |
| Le logo ne s'affiche pas | Vérifier `LogoPath` et la présence du fichier ; sinon le nom de l'organisation s'affiche. |
| Impossible de quitter | Ctrl+Maj+Q puis PIN (`AdminPin` dans `config.json`). |
```
# Elvanto
