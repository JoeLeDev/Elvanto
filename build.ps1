<#
.SYNOPSIS
    Compile Elvanto Kiosk en exécutable autonome (.exe) pour Windows x64.

.DESCRIPTION
    Génère un exécutable self-contained (le runtime .NET 8 est embarqué,
    aucune installation de .NET requise sur la borne). Le résultat est placé
    dans le dossier .\publish.

.NOTES
    Nécessite le SDK .NET 8 (https://dotnet.microsoft.com/download/dotnet/8.0).
    À exécuter sous Windows (l'application est une appli WPF Windows).
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\publish"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\ElvantoKiosk\ElvantoKiosk.csproj"

Write-Host "==> Vérification du SDK .NET..." -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Le SDK .NET 8 est introuvable. Installez-le depuis https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "==> Nettoyage de $OutputDir ..." -ForegroundColor Cyan
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }

Write-Host "==> Publication (self-contained, win-x64)..." -ForegroundColor Cyan
dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation (code $LASTEXITCODE)." }

Write-Host ""
Write-Host "==> Build terminé." -ForegroundColor Green
Write-Host "    Exécutable : $OutputDir\ElvantoKiosk.exe"
Write-Host "    Pensez à éditer $OutputDir\config.json avant le déploiement."
