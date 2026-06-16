<#
.SYNOPSIS
    Désinstalle la borne Elvanto Kiosk.

.DESCRIPTION
    Supprime le démarrage automatique (raccourci Startup + tâche planifiée),
    le raccourci d'administration, puis le dossier d'installation.
    Ne désinstalle pas le runtime WebView2 (partagé par le système).
#>

[CmdletBinding()]
param(
    [string]$InstallDir = "C:\Program Files\ElvantoKiosk"
)

$ErrorActionPreference = "Stop"
$AppName = "ElvantoKiosk"

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Start-Process powershell -Verb RunAs -ArgumentList @(
        "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"", "-InstallDir", "`"$InstallDir`"")
    return
}

Write-Host "=== Désinstallation de Elvanto Kiosk ===" -ForegroundColor Cyan

# Arrêter l'application si elle tourne.
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Raccourci Startup
$startupLnk = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup\$AppName.lnk"
if (Test-Path $startupLnk) { Remove-Item $startupLnk -Force; Write-Host "[OK] Raccourci de démarrage supprimé." }

# Tâche planifiée
schtasks /Delete /TN "ElvantoKioskAutostart" /F 2>$null | Out-Null

# Raccourci d'administration
$adminLnk = "$env:Public\Desktop\Elvanto Kiosk - Administration.lnk"
if (Test-Path $adminLnk) { Remove-Item $adminLnk -Force; Write-Host "[OK] Raccourci d'administration supprimé." }

# Dossier d'installation
if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "[OK] Dossier d'installation supprimé : $InstallDir"
}

Write-Host "=== Désinstallation terminée ===" -ForegroundColor Cyan
