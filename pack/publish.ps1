$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet publish src/WinDock.App/WinDock.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false

Write-Host "Published to src/WinDock.App/bin/Release/net10.0-windows10.0.22621.0/win-x64/publish/"
