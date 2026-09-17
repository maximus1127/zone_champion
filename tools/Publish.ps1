<#
.SYNOPSIS
    Builds a release copy of Zone Champion into .\publish.
.PARAMETER Install
    Also copies it to %LOCALAPPDATA%\Programs\ZoneChampion (stopping a running copy first) and starts it.
    Release builds register themselves to start with Windows unless "startWithWindows" is false in settings.
#>
param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'publish'

dotnet publish (Join-Path $root 'src\ZoneChampion\ZoneChampion.csproj') `
    -c Release -r win-x64 --no-self-contained `
    -p:PublishSingleFile=true -p:DebugType=none `
    -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Output "Published to $out"

if ($Install) {
    $target = Join-Path $env:LOCALAPPDATA 'Programs\ZoneChampion'
    Get-Process -Name ZoneChampion -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $out '*') -Destination $target -Recurse -Force
    Start-Process -FilePath (Join-Path $target 'ZoneChampion.exe')
    Write-Output "Installed to $target and started"
}
