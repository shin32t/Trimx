# Builds the release files: a portable zip, and the installer if Inno Setup is installed.
# Run from anywhere:  powershell -ExecutionPolicy Bypass -File installer\build.ps1
# Output goes to installer\output\.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'src\Trimx\bin\publish\win-x64'
$outputDir = Join-Path $PSScriptRoot 'output'

# The version comes from the project file, so it is written in one place only.
$project = Join-Path $root 'src\Trimx\Trimx.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'Version was not found in Trimx.csproj.' }
Write-Host "Trimx $version"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -p:PublishProfile=Release-win-x64
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

New-Item -ItemType Directory -Force $outputDir | Out-Null

# Portable zip: the published folder plus the license files, inside a folder named Trimx.
$staging = Join-Path $outputDir 'Trimx'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
Copy-Item $publishDir $staging -Recurse
Copy-Item (Join-Path $root 'LICENSE') $staging
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $staging
$zip = Join-Path $outputDir "Trimx-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $staging -DestinationPath $zip
Remove-Item $staging -Recurse -Force
Write-Host "Created $zip"

# Installer, when Inno Setup 6 or later is available. The newest version wins.
$roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
$iscc = foreach ($major in 7, 6) {
    foreach ($root in $roots) {
        $candidate = Join-Path $root "Inno Setup $major\ISCC.exe"
        if (Test-Path $candidate) { $candidate }
    }
}
$iscc = $iscc | Select-Object -First 1

if ($iscc) {
    & $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'Trimx.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
    Write-Host "Created the installer in $outputDir"
}
else {
    Write-Warning 'Inno Setup 6 or later was not found, so only the zip was created. Install it from https://jrsoftware.org/isinfo.php'
}
