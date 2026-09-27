<#
.SYNOPSIS
    Builds a release copy of Zone Champion into .\publish.
.DESCRIPTION
    Nothing needs to be installed first. If this PC has no .NET 10 (or newer) SDK, the script downloads one into
    .\.dotnet inside the repo, without admin rights and without installing anything machine-wide.
    If the .NET 10 Desktop Runtime is installed, the result is a small exe that uses it. Otherwise the exe is
    self-contained: it carries its own copy of .NET.
.PARAMETER Install
    Also copies it to %LOCALAPPDATA%\Programs\ZoneChampion (stopping a running copy first) and starts it.
    Release builds register themselves to start with Windows unless "startWithWindows" is false in settings.
.PARAMETER SelfContained
    Always builds the self-contained exe (about 75 MB), for example to copy to a PC without .NET.
.PARAMETER Runtime
    The Windows architecture to build for, win-x64 or win-arm64. Defaults to this PC's.
#>
param(
    [switch]$Install,
    [switch]$SelfContained,
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'publish'
$localDotnet = Join-Path $root '.dotnet'
$localDotnetExe = Join-Path $localDotnet 'dotnet.exe'

# The machine-level value is the PC's real architecture, even from an emulated x64 PowerShell on ARM.
$arch = switch ([Environment]::GetEnvironmentVariable('PROCESSOR_ARCHITECTURE', 'Machine')) {
    'AMD64' { 'x64' }
    'ARM64' { 'arm64' }
    default { throw 'Zone Champion needs 64-bit Windows (x64 or ARM64).' }
}
if (-not $Runtime) { $Runtime = "win-$arch" }

function Test-Sdk([string]$dotnet) {
    # True if this dotnet.exe has an SDK that can build net10.0.
    if (-not $dotnet -or -not (Test-Path $dotnet)) { return $false }
    $ErrorActionPreference = 'Continue'
    $sdks = & $dotnet --list-sdks
    return [bool]($sdks | Where-Object { [int]($_ -split '\.')[0] -ge 10 })
}

# PATH may list an older or 32-bit dotnet first, so also try the standard location and the repo-local copy.
$dotnet = @(
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
    $localDotnetExe
) | Where-Object { Test-Sdk $_ } | Select-Object -First 1

if (-not $dotnet) {
    Write-Output "No .NET 10 SDK found. Downloading one into $localDotnet (used only for building Zone Champion)..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'  # Windows PowerShell downloads far slower with a progress bar
    & ([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) `
        -Channel 10.0 -Architecture $arch -InstallDir $localDotnet -NoPath
    $dotnet = $localDotnetExe
    if (-not (Test-Sdk $dotnet)) {
        throw 'Downloading the .NET SDK failed. Install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0 and run this again.'
    }
}
if ($dotnet -eq $localDotnetExe) {
    # Tools started during the build look for dotnet on PATH.
    $env:PATH = "$localDotnet;$env:PATH"
}

# The small exe only runs where the .NET 10 Desktop Runtime is installed machine-wide. A build for another
# architecture is for another PC, which may not have it.
$selfContainedBuild = $SelfContained -or $Runtime -ne "win-$arch" -or
    -not (Test-Path (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.WindowsDesktop.App\10.*'))

$publishArgs = @(
    'publish', (Join-Path $root 'src\ZoneChampion\ZoneChampion.csproj'),
    '-c', 'Release', '-r', $Runtime,
    '-p:PublishSingleFile=true', '-p:DebugType=none',
    '--disable-build-servers',  # leave no compiler processes running once the script ends
    '-o', $out
)
if ($selfContainedBuild) {
    $publishArgs += '--self-contained', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true'
} else {
    $publishArgs += '--no-self-contained'
}

# Start clean so -Install never copies leftovers from an earlier build.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$kind = if ($selfContainedBuild) { 'self-contained' } else { 'uses the installed .NET 10 Desktop Runtime' }
Write-Output "Published to $out ($Runtime, $kind)"

if ($Install) {
    $target = Join-Path $env:LOCALAPPDATA 'Programs\ZoneChampion'
    Get-Process -Name ZoneChampion -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $out '*') -Destination $target -Recurse -Force
    Start-Process -FilePath (Join-Path $target 'ZoneChampion.exe')
    Write-Output "Installed to $target and started"
}
