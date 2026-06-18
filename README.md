# ICC Kiosk (ElvantoKiosk)

Borne d'accueil Windows en **mode kiosque** pour formulaires **Notion**.

Au démarrage du poste, l'application s'ouvre automatiquement en plein écran et affiche une **page d'accueil** (logo ICC, message de bienvenue) avec des **cartes cliquables** vers différents formulaires Notion. Le visiteur choisit un formulaire, le remplit dans un navigateur intégré, puis la borne revient automatiquement à l'accueil après soumission ou inactivité.

- **Technologies** : C# / .NET 8, WPF, Microsoft Edge WebView2
- **Compatibilité** : Windows 10 et Windows 11 (x64), écrans tactiles
- **Exécutable autonome** : le runtime .NET est embarqué (aucune installation .NET requise sur la borne)
- **Build automatisé** : GitHub Actions génère l'exe à chaque push sur `main`

---

## Sommaire

1. [Fonctionnement](#fonctionnement)
2. [Déploiement rapide (GitHub Actions)](#déploiement-rapide-github-actions)
3. [Installation sur la borne](#installation-sur-la-borne)
4. [Configuration (`config.json`)](#configuration-configjson)
5. [Mises à jour](#mises-à-jour)
6. [Mode administrateur](#mode-administrateur)
7. [Checklist avant livraison](#checklist-avant-livraison)
8. [Compilation locale (optionnel)](#compilation-locale-optionnel)
9. [Aperçu design sur Mac](#aperçu-design-sur-mac)
10. [Structure du projet](#structure-du-projet)
11. [Journalisation](#journalisation)
12. [Sécurité](#sécurité)
13. [Dépannage](#dépannage)

---

## Fonctionnement

- **Plein écran** sans bordure, barre d'adresse ni menus, toujours au premier plan.
- **Page d'accueil** : logo ICC, titre, message de bienvenue, cartes de formulaires (taille uniforme).
- **Formulaires Notion** chargés en direct via WebView2 (mise à jour automatique des champs côté Notion).
- **Retour automatique à l'accueil** :
  - après **soumission** détectée (mots-clés d'URL configurables) ;
  - après **60 secondes d'inactivité** (configurable) ;
  - via le bouton **« Accueil »**.
- **Confidentialité** : cookies et session effacés entre visiteurs.
- **Erreur réseau** : message convivial avec bouton *Réessayer*.
- **Sécurité kiosque** : raccourcis système bloqués, navigation limitée aux domaines autorisés, fermeture protégée par PIN.

---

## Déploiement rapide (GitHub Actions)

Méthode recommandée : **pas besoin du SDK .NET sur la borne**.

1. Pousser le code sur `main` (repo : [JoeLeDev/Elvanto](https://github.com/JoeLeDev/Elvanto)).
2. Aller dans **Actions** → workflow **Build Elvanto Kiosk** → attendre le statut **Success**.
3. Télécharger l'artifact **`ElvantoKiosk-win-x64`** (fichier zip, ~60 Mo).
4. Copier le zip sur le PC kiosque, dézipper (ex. `C:\Temp\ElvantoKiosk`).
5. Vérifier `config.json` (URLs Notion, PIN).
6. Installer (voir section suivante).

Le workflow compile sur `windows-latest`, publie un exe self-contained win-x64 et inclut les scripts `installer/`.

---

## Installation sur la borne

### Prérequis

- Windows 10 ou 11 (x64)
- Connexion Internet
- Droits administrateur pour l'installation

### Étapes

1. Dézipper l'artifact `ElvantoKiosk-win-x64` sur la borne.
2. Éditer `config.json` si nécessaire (PIN, titres des cartes).
3. Ouvrir **PowerShell en administrateur** dans le dossier dézippé :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\install.ps1
```

Le script :
- installe le **runtime WebView2** s'il est absent ;
- copie l'application dans `C:\Program Files\ElvantoKiosk` ;
- conserve un `config.json` déjà présent (sauf `-Force`) ;
- configure le **démarrage automatique** (raccourci Startup) ;
- crée un **raccourci d'administration** sur le bureau public.

### Fichiers après installation

| Élément | Chemin |
|---|---|
| Application | `C:\Program Files\ElvantoKiosk\ElvantoKiosk.exe` |
| Configuration | `C:\Program Files\ElvantoKiosk\config.json` |
| Logo | `C:\Program Files\ElvantoKiosk\assets\LOGO-ICC.png` |
| Journaux | `C:\Program Files\ElvantoKiosk\logs\application.log` |

### Variantes

Démarrage via tâche planifiée :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\install.ps1 -StartupMethod ScheduledTask
```

Désinstallation :

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\uninstall.ps1
```

---

## Configuration (`config.json`)

Tous les réglages se modifient **sans recompiler**. Éditez le fichier puis relancez l'application.

```json
{
  "OrganizationName": "ICC",
  "WelcomeTitle": "Bienvenue",
  "WelcomeMessage": "Merci de compléter l'un des formulaires ci-dessous.",
  "LogoPath": "assets/LOGO-ICC.png",
  "AdminPin": "1234",
  "InactivityTimeoutSeconds": 60,
  "ReturnDelayAfterSubmitSeconds": 5,
  "ShowHomeButton": true,
  "ClearDataOnReturnHome": true,
  "AllowedHosts": ["notion.so", "notion.site", "notion.com"],
  "SubmitUrlKeywords": ["thank", "merci", "success", "complete", "submitted", "confirmation"],
  "Forms": [
    {
      "Title": "Première visite",
      "Description": "Inscrivez-vous si c'est votre première venue.",
      "Url": "https://votre-espace.notion.site/form/xxxxxxxx"
    }
  ]
}
```

| Clé | Rôle |
|---|---|
| `OrganizationName` | Nom affiché (fallback si pas de logo). |
| `WelcomeTitle` / `WelcomeMessage` | Textes d'accueil. |
| `LogoPath` | Chemin du logo (relatif à l'exe). |
| `AdminPin` | Code PIN pour quitter le kiosque (**à changer !**). |
| `InactivityTimeoutSeconds` | Inactivité avant retour à l'accueil (défaut 60 s). |
| `ReturnDelayAfterSubmitSeconds` | Délai après soumission avant retour à l'accueil. |
| `ShowHomeButton` | Affiche le bouton « Accueil » pendant un formulaire. |
| `ClearDataOnReturnHome` | Efface cookies/session entre visiteurs. |
| `AllowedHosts` | Domaines autorisés (sécurité navigation). |
| `SubmitUrlKeywords` | Mots-clés d'URL indiquant une soumission réussie. |
| `Forms[]` | Cartes de l'accueil : titre, description, URL Notion. |

### Préparer un formulaire Notion

1. Créer le formulaire dans Notion.
2. **Partager le formulaire** → **Toute personne sur le web disposant du lien**.
3. Copier l'URL (`notion.site` ou `notion.so`).
4. Coller dans `Forms[].Url`.

Les modifications de champs dans Notion (libellés, champs ajoutés/supprimés) se reflètent **automatiquement** dans la borne, sans rebuild.

> `C:\Program Files\...` peut exiger des droits admin pour enregistrer. Quittez l'app (PIN), éditez en admin, puis relancez.

---

## Mises à jour

### Contenu Notion (champs, libellés)

Automatique — rien à faire côté borne.

### Cartes d'accueil, PIN, logo, délais

Éditer `config.json` sur la borne → relancer l'app. Pas de rebuild.

### Nouvelle version de l'application

1. Push sur `main` → télécharger le nouvel artifact GitHub Actions.
2. Quitter l'app (PIN).
3. Relancer `installer\install.ps1` (conserve la config existante).

---

## Mode administrateur

L'utilisateur ne peut pas fermer l'application (Alt+F4, croix, etc. bloqués).

Pour quitter :

1. **Bouton « Fermer »** (coin inférieur droit, icône cadenas) — adapté au tactile.
2. Raccourci **Ctrl + Maj + Q** (si clavier branché).

Un clavier numérique à l'écran demande le PIN (`AdminPin` dans `config.json`).

> Changer le PIN par défaut (`1234`) avant la mise en production.

**Matériel** : compatible mini-PC tactiles Windows 11 (ex. MSI Cubi 5, 10 points de contact).

---

## Checklist avant livraison

- [ ] `config.json` : URLs Notion OK, `AllowedHosts` corrects, PIN changé
- [ ] Chaque carte ouvre le bon formulaire Notion
- [ ] Saisie et envoi du formulaire fonctionnels
- [ ] Retour auto après inactivité (60 s)
- [ ] Bouton **Fermer** + PIN opérationnels
- [ ] Redémarrage PC → lancement automatique de la borne
- [ ] Logs sans erreurs répétées (`logs\application.log`)
- [ ] Test coupure/rétablissement Internet

---

## Compilation locale (optionnel)

Si vous préférez compiler sur un PC Windows plutôt que via GitHub Actions, installez le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) puis :

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Résultat : `.\publish\ElvantoKiosk.exe` (runtime .NET embarqué).

---

## Aperçu design sur Mac

Le fichier `preview/index.html` permet de visualiser la page d'accueil dans Safari/Chrome (logo, cartes, dialog PIN). Ce n'est **pas** l'application réelle — les formulaires Notion ne s'affichent pas dans cette maquette.

```bash
open preview/index.html
```

---

## Structure du projet

```
Elvanto/
├─ .github/workflows/build.yml   # Build GitHub Actions → artifact win-x64
├─ ElvantoKiosk.sln
├─ build.ps1                     # Compilation locale (optionnel)
├─ installer/
│  ├─ install.ps1
│  └─ uninstall.ps1
├─ preview/
│  └─ index.html                 # Maquette design (Mac)
├─ src/ElvantoKiosk/
│  ├─ config.json                # Configuration — MODIFIABLE sans rebuild
│  ├─ assets/LOGO-ICC.png
│  ├─ MainWindow.xaml(.cs)       # Kiosque + WebView2
│  ├─ Controls/                  # HomeView, PinDialog
│  ├─ Models/                    # AppConfig, FormEntry
│  └─ Services/                  # Config, Logger, IdleTime, KeyboardHook
└─ README.md
```

---

## Journalisation

Événements et erreurs enregistrés dans :

```
<dossier d'installation>\logs\application.log
```

Rotation automatique au-delà de 5 Mo.

---

## Sécurité

L'application bloque la plupart des raccourcis (touche Windows, Alt+Tab, Alt+F4, F11, F5, Ctrl+L/W/N/T…), restreint la navigation aux `AllowedHosts` et empêche l'ouverture de nouvelles fenêtres.

**Limite** : `Ctrl+Alt+Suppr` et `Ctrl+Maj+Échap` ne peuvent pas être interceptés. Pour un verrouillage total, compléter avec un compte kiosque dédié et les stratégies Windows (Assigned Access, GPO).

---

## Dépannage

| Symptôme | Piste |
|---|---|
| « Connexion indisponible » | Vérifier Internet ; consulter `logs\application.log`. |
| « WebView2 n'est pas installé » | Relancer `install.ps1`. |
| Formulaire ne charge pas | URL dans `config.json` ; domaine dans `AllowedHosts` ; formulaire Notion en mode public. |
| Logo absent | Vérifier `LogoPath` et `assets/LOGO-ICC.png`. |
| Impossible de quitter | Bouton Fermer ou Ctrl+Maj+Q + PIN. |
| `config.json` ne s'enregistre pas | Éditer en administrateur (`C:\Program Files\...`). |
