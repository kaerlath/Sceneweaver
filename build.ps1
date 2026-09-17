param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not $SkipTests) {
        dotnet run --project tests/CrossFormat.Tests/CrossFormat.Tests.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Conversion tests failed.' }
    }
    dotnet build src/CrossFormat.Plugin/CrossFormat.Plugin.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    dotnet build src/CrossFormat.Cli/CrossFormat.Cli.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
    New-Item -ItemType Directory -Force dist | Out-Null
    $bundleFiles = @(Get-ChildItem src/CrossFormat.Plugin/bin/Release -File | Select-Object -ExpandProperty FullName)
    $bundleFiles += @('LICENSE.md', 'THIRD_PARTY.md', 'README.md')
    Compress-Archive -Path $bundleFiles -DestinationPath dist/Sceneweaver-0.1.0.zip -Force
    Write-Host 'Built dist/Sceneweaver-0.1.0.zip'
} finally { Pop-Location }
