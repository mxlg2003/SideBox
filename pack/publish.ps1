param(
    [string]$Version = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$versionArgs = @()
if ($Version) {
    $cleanVer = $Version.TrimStart('v').TrimStart('V')
    $versionArgs = @(
        "-p:Version=$cleanVer",
        "-p:AssemblyVersion=$cleanVer.0",
        "-p:FileVersion=$cleanVer.0",
        "-p:InformationalVersion=$cleanVer"
    )
    Write-Host "Publishing SideBox with Version: $cleanVer"
}

dotnet publish src/WinDock.App/WinDock.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    @versionArgs

Write-Host "Published to src/WinDock.App/bin/Release/net10.0-windows10.0.22621.0/win-x64/publish/"
