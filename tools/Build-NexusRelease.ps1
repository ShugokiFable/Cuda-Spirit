param(
    [string]$Output = ".\\artifacts\\Cuda-Spirit-Nexus-Release"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src\CudaSpirit.App\CudaSpirit.App.csproj'
$Out = [IO.Path]::GetFullPath((Join-Path $Root $Output))
$Dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$Compatible = @(& $Dotnet --list-sdks) | Where-Object { $_ -match '^(8\.|9\.|10\.)' } | Select-Object -First 1
if (!$Compatible) { throw 'Install any stable .NET 8 or newer SDK to build the release.' }
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
& $Dotnet restore (Join-Path $Root 'CudaSpirit.sln') --nologo
if ($LASTEXITCODE) { throw 'Restore failed.' }
& $Dotnet build (Join-Path $Root 'CudaSpirit.sln') -c Release --no-restore -warnaserror --nologo
if ($LASTEXITCODE) { throw 'Build failed.' }
& $Dotnet publish $Project -c Release -r win-x64 --self-contained true -warnaserror `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded -p:DebugSymbols=false -o $Out --nologo
if ($LASTEXITCODE) { throw 'Publish failed.' }
Get-ChildItem $Out -File | Where-Object Extension -in '.pdb','.xml' | Remove-Item -Force
$Exe = Join-Path $Out 'CudaSpirit.exe'
if (!(Test-Path $Exe)) { throw 'Publish produced no CudaSpirit.exe.' }
Copy-Item (Join-Path $Root 'NEXUS_RELEASE_NOTES.md') $Out -Force
Copy-Item (Join-Path $Root 'PRIVACY.md') $Out -Force
Copy-Item (Join-Path $Root 'DATA_SOURCES.md') $Out -Force
@'
@echo off
start "" "%~dp0CudaSpirit.exe"
'@ | Set-Content (Join-Path $Out 'RUN CUDA SPIRIT.bat') -Encoding Ascii
Write-Host "Self-contained Nexus release created at $Out" -ForegroundColor Green
Write-Host 'End users need no .NET runtime or SDK.' -ForegroundColor Green
