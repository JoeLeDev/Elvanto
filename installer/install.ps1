<#
.SYNOPSIS
    Installe la borne Elvanto Kiosk sur un poste Windows 10 / 11.

.DESCRIPTION
    - Installe le runtime Microsoft Edge WebView2 s'il est absent.
    - Copie l'application (depuis .\publish) vers le dossier d'installation.
    - Copie / conserve le fichier de configuration config.json.
    - Configure le démarrage automatique après connexion (raccourci Startup
      pour tous les utilisateurs, ou tâche planifiée selon -StartupMethod).
    - Crée un raccourci d'administration sur le bureau public.

.PARAMETER SourceDir
    Dossier contenant l'exécutable publié (défaut : ..\publish).

.PARAMETER InstallDir
    Dossier d'installation (défaut : C:\Program Files\ElvantoKiosk).

.PARAMETER StartupMethod
    "Startup" (raccourci dans le dossier Démarrage) ou "ScheduledTask".

.PARAMETER Force
    Écrase config.json existant (sinon la configuration en place est conservée).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install.ps1
#>

[CmdletBinding()]
param(
    [string]$SourceDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "publish"),
    [string]$InstallDir = "C:\Program Files\ElvantoKiosk",
    [ValidateSet("Startup", "ScheduledTask")]
    [string]$StartupMethod = "Startup",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$AppName = "ElvantoKiosk"
$ExeName = "ElvantoKiosk.exe"

# --- Élévation administrateur -------------------------------------------------
function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host "Élévation des privilèges administrateur..." -ForegroundColor Yellow
    $argList = @("-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"",
                 "-SourceDir", "`"$SourceDir`"", "-InstallDir", "`"$InstallDir`"",
                 "-StartupMethod", $StartupMethod)
    if ($Force) { $argList += "-Force" }
    Start-Process powershell -Verb RunAs -ArgumentList $argList
    return
}

Write-Host "=== Installation de Elvanto Kiosk ===" -ForegroundColor Cyan

# --- 1. Vérifier les fichiers source ----------------------------------------
$sourceExe = Join-Path $SourceDir $ExeName
if (-not (Test-Path $sourceExe)) {
    throw "Exécutable introuvable : $sourceExe`nExécutez d'abord build.ps1 pour générer le dossier 'publish'."
}

# --- 2. Installer WebView2 si nécessaire -------------------------------------
function Test-WebView2Installed {
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    )
    foreach ($k in $keys) {
        $pv = (Get-ItemProperty -Path $k -Name pv -ErrorAction SilentlyContinue).pv
        if ($pv -and $pv -ne "0.0.0.0") { return $true }
    }
    return $false
}

if (Test-WebView2Installed) {
    Write-Host "[OK] Runtime WebView2 déjà présent." -ForegroundColor Green
} else {
    Write-Host "[..] Téléchargement et installation du runtime WebView2..." -ForegroundColor Yellow
    $bootstrapper = Join-Path $env:TEMP "MicrosoftEdgeWebview2Setup.exe"
    try {
        Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" `
                          -OutFile $bootstrapper -UseBasicParsing
        Start-Process -FilePath $bootstrapper -ArgumentList "/silent", "/install" -Wait
        Write-Host "[OK] Runtime WebView2 installé." -ForegroundColor Green
    } catch {
        Write-Warning "Impossible d'installer WebView2 automatiquement : $($_.Exception.Message)"
        Write-Warning "Installez-le manuellement : https://developer.microsoft.com/microsoft-edge/webview2/"
    } finally {
        if (Test-Path $bootstrapper) { Remove-Item $bootstrapper -Force -ErrorAction SilentlyContinue }
    }
}

# --- 3. Copier l'application -------------------------------------------------
Write-Host "[..] Copie des fichiers vers $InstallDir ..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$existingConfig = Join-Path $InstallDir "config.json"
$backupConfig = $null
if ((Test-Path $existingConfig) -and -not $Force) {
    $backupConfig = Join-Path $env:TEMP "config.backup.json"
    Copy-Item $existingConfig $backupConfig -Force
    Write-Host "    Configuration existante conservée." -ForegroundColor DarkGray
}

Copy-Item -Path (Join-Path $SourceDir "*") -Destination $InstallDir -Recurse -Force

if ($backupConfig) {
    Copy-Item $backupConfig $existingConfig -Force
    Remove-Item $backupConfig -Force -ErrorAction SilentlyContinue
}
Write-Host "[OK] Fichiers copiés." -ForegroundColor Green

$installedExe = Join-Path $InstallDir $ExeName

# --- 4. Démarrage automatique ------------------------------------------------
function New-Shortcut {
    param([string]$Path, [string]$Target, [string]$WorkDir, [string]$Description)
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($Path)
    $sc.TargetPath = $Target
    $sc.WorkingDirectory = $WorkDir
    $sc.Description = $Description
    $sc.Save()
}

if ($StartupMethod -eq "Startup") {
    $startupDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup"
    $lnk = Join-Path $startupDir "$AppName.lnk"
    New-Shortcut -Path $lnk -Target $installedExe -WorkDir $InstallDir -Description "Borne d'accueil Elvanto"
    Write-Host "[OK] Démarrage automatique configuré (Startup) : $lnk" -ForegroundColor Green
} else {
    $taskName = "ElvantoKioskAutostart"
    schtasks /Create /TN $taskName /TR "`"$installedExe`"" /SC ONLOGON /RL HIGHEST /F | Out-Null
    Write-Host "[OK] Démarrage automatique configuré (Tâche planifiée : $taskName)." -ForegroundColor Green
}

# --- 5. Raccourci d'administration sur le bureau public ----------------------
$publicDesktop = "$env:Public\Desktop"
$adminLnk = Join-Path $publicDesktop "Elvanto Kiosk - Administration.lnk"
# Ouvre le dossier d'installation (config.json + logs) pour l'administrateur.
New-Shortcut -Path $adminLnk -Target $InstallDir -WorkDir $InstallDir `
             -Description "Configuration et journaux de la borne Elvanto"
Write-Host "[OK] Raccourci d'administration créé sur le bureau." -ForegroundColor Green

Write-Host ""
Write-Host "=== Installation terminée ===" -ForegroundColor Cyan
Write-Host "  Application : $installedExe"
Write-Host "  Config      : $existingConfig"
Write-Host "  Journaux    : $(Join-Path $InstallDir 'logs\application.log')"
Write-Host ""
Write-Host "  Éditez config.json pour renseigner les URLs des formulaires Elvanto," -ForegroundColor Yellow
Write-Host "  puis redémarrez la borne (ou lancez l'application)." -ForegroundColor Yellow
Write-Host ""
Write-Host "  Pour quitter le mode kiosque : bouton Fermer (coin inf. droit) ou Ctrl+Maj+Q, puis PIN (config.json)." -ForegroundColor Yellow
