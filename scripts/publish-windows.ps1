param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "BrightScroll\BrightScroll.csproj"
$publishArgs = @(
    $project,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true"
)

if ($Version) {
    $publishArgs += "-p:Version=$Version"
}

dotnet publish @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published self-contained Windows app. Supply the same version to Inno Setup with /DAppVersion=<version>."
