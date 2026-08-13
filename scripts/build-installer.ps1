param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "LoginWindow\FamilyTheater.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerScript = Join-Path $repoRoot "installer\FamilyTheater.iss"
$installerOutputDir = Join-Path $repoRoot "artifacts\installer"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path $installerOutputDir) {
    Remove-Item -LiteralPath $installerOutputDir -Recurse -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    --self-contained false `
    /p:PublishSingleFile=false `
    /p:PublishTrimmed=false `
    /p:PublishReadyToRun=false `
    /p:Version=$Version `
    -o $publishDir

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Inno Setup 6\Inno Setup Compiler.lnk"
if (Test-Path $shortcutPath) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath) {
        $isccCandidates += $shortcut.TargetPath
    }
}

$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccPath) {
    throw "Inno Setup compiler ISCC.exe was not found."
}

$isccVersionArgument = "/DMyAppVersion=$Version"
& $isccPath $isccVersionArgument $installerScript
