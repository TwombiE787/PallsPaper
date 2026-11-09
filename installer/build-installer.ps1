$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'PallsPaper.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$outputDir = Join-Path $repoRoot 'artifacts\installer'
$script = Join-Path $PSScriptRoot 'PallsPaper.iss'
$appVersion = '1.0.1'
$innoCompilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$innoCompiler = $innoCompilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $innoCompiler) {
    throw 'Inno Setup 6 ist nicht installiert. Installiere JRSoftware.InnoSetup und starte das Skript erneut.'
}

$bootstrapper = Join-Path $PSScriptRoot 'MicrosoftEdgeWebView2Setup.exe'
if (-not (Test-Path $bootstrapper)) {
    throw "Der Microsoft-WebView2-Bootstrapper fehlt: $bootstrapper"
}

$signature = Get-AuthenticodeSignature $bootstrapper
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
    throw 'Der Microsoft-WebView2-Bootstrapper besitzt keine gültige Microsoft-Signatur.'
}

New-Item -ItemType Directory -Force -Path $publishDir, $outputDir | Out-Null

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish ist mit Exitcode $LASTEXITCODE fehlgeschlagen."
}

& $innoCompiler "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" $script
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup ist mit Exitcode $LASTEXITCODE fehlgeschlagen."
}

$installer = Join-Path $outputDir "PallsPaper-Setup-$appVersion-x64.exe"
if (-not (Test-Path $installer)) {
    throw "Die Installer-Datei wurde nicht erzeugt: $installer"
}

Write-Output "Installer erstellt: $installer"
