# Build script for CSharpMajordomo Analyzer package in Release configuration
# This script builds the analyzer package with CI properties for publication

param(
	[string]$Configuration = "Release",
	[string]$OutputDir = ".\artifacts",
	[switch]$ContinuousIntegrationBuild = $true
)

$ErrorActionPreference = "Stop"

# Define paths
$solutionPath = "CSharpMajordomo.sln"
$packageProjectPath = "CSharpMajordomo\CSharpMajordomo.Package\CSharpMajordomo.Package.csproj"
$fullOutputDir = [System.IO.Path]::Combine((Get-location), $OutputDir)

# Ensure we're in the right directory
if (-not (Test-Path $solutionPath)) {
	Write-Error "Solution file not found: $solutionPath"
	exit 1
}

Write-Host "Building CSharpMajordomo Analyzer Package" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Directory: $fullOutputDir" -ForegroundColor Yellow

# Clean output directory if it exists
if (Test-Path $fullOutputDir) {
	Write-Host "Cleaning output directory..." -ForegroundColor Yellow
	Remove-Item -Path $fullOutputDir -Recurse -Force | Out-Null
	Write-Host "Output directory cleaned" -ForegroundColor Green
}

# Create output directory
New-Item -ItemType Directory -Path $fullOutputDir | Out-Null
Write-Host "Created output directory: $fullOutputDir" -ForegroundColor Green

# Build arguments for Release configuration with CI properties
$buildArgs = @(
	"build",
	$packageProjectPath,
	"/p:Configuration=$Configuration",
	"/p:OutDir=$fullOutputDir\build",
	"/p:PackageOutputPath=$fullOutputDir",
	"/p:ContinuousIntegrationBuild=$ContinuousIntegrationBuild",
	"/p:DeterministicSourcePaths=true",
	"/p:SourceLinkCreate=true",
	"/v:m"  # Use minimal verbosity for cleaner output
)

Write-Host "`nRunning dotnet build with the following properties:" -ForegroundColor Cyan
write-Host $buildArgs

Write-Host "`nBuilding...`n" -ForegroundColor Cyan

# Run the build
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
	Write-Error "Build failed with exit code $LASTEXITCODE"
	exit $LASTEXITCODE
}

Write-Host "`nBuild completed successfully!" -ForegroundColor Green

# Find and display the generated NuGet package
$nupkgFiles = Get-ChildItem -Path $fullOutputDir -Filter "*.nupkg" -ErrorAction SilentlyContinue

if ($nupkgFiles) {
	Write-Host "`nGenerated NuGet packages:" -ForegroundColor Green
	foreach ($nupkg in $nupkgFiles) {
		Write-Host "  - $($nupkg.FullName)" -ForegroundColor Yellow
		Write-Host "    Size: $([math]::Round($nupkg.Length / 1MB, 2)) MB" -ForegroundColor Gray
	}
} else {
	Write-Warning "No NuGet packages found in output directory"
}

Write-Host "`nBuild artifacts location: $(Resolve-Path $fullOutputDir)" -ForegroundColor Cyan
