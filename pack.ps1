#requires -Version 5.1
<#
.SYNOPSIS
    Pack HAIRS components into NuGet packages (.nupkg/.snupkg).

.DESCRIPTION
    Builds and packs the standalone HAIRS library (Hairs) and CLI tool (Hairs.Cli).
    Supports preview version scheme: 0.1.0-preview.x.

.PARAMETER Output
    Directory to write packages to. Default: hairs/artifacts/nupkg.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Version
    Exact PackageVersion string (e.g. 0.1.0-preview.1). Overrides -Preview.

.PARAMETER Preview
    Preview iteration number x in 0.1.0-preview.x. Default: 1.
#>
[CmdletBinding()]
param(
    [string] $Output = 'artifacts/nupkg',
    [string] $Configuration = 'Release',
    [string] $Version = '',
    [int] $Preview = 1
)

$ErrorActionPreference = 'Stop'
$hairsRoot = $PSScriptRoot
$outDir = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $hairsRoot $Output }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$pkgVersion = if ($Version) { $Version } else { "0.1.0-preview.$Preview" }
Write-Host "Packing HAIRS packages with version: $pkgVersion"

$solutionPath = Join-Path $hairsRoot "Hairs.slnx"
& dotnet pack $solutionPath -c $Configuration -p:PackageVersion=$pkgVersion -o $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$nupkgs = @(Get-ChildItem (Join-Path $outDir "*.nupkg") -ErrorAction SilentlyContinue)
Write-Host ""
Write-Host "Successfully packed $($nupkgs.Count) package(s) into ${outDir}:"
$nupkgs | ForEach-Object { Write-Host " - $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" }
