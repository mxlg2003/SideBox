$ErrorActionPreference = "Stop"

function Find-RepoRoot([string]$start) {
    $root = $start
    while ($root) {
        $csproj = Join-Path $root "src\WinDock.App\WinDock.App.csproj"
        if (Test-Path $csproj) {
            return $root
        }
        $parent = Split-Path $root
        if ($parent -eq $root) {
            break
        }
        $root = $parent
    }
    throw "WinDock repo root not found from $start"
}

$repo = Find-RepoRoot $PSScriptRoot
$project = Join-Path $repo "src\WinDock.App\WinDock.App.csproj"
$exe = Join-Path $repo "src\WinDock.App\bin\Debug\net10.0-windows10.0.22621.0\WinDock.exe"

$running = @(Get-Process -Name WinDock -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $running | Stop-Process -Force
}

$deadline = (Get-Date).AddSeconds(15)
while (Get-Process -Name WinDock -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) {
        throw "WinDock did not exit after Stop-Process -Force"
    }
    Start-Sleep -Milliseconds 200
}

dotnet build $project --nologo
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path $exe)) {
    throw "WinDock.exe not found at $exe"
}

Start-Process $exe
Write-Host "WinDock restarted."
