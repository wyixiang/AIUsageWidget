$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputPath = Join-Path $projectRoot 'dist'
dotnet publish (Join-Path $projectRoot 'src/AIUsageWidget.csproj') -c Release -r win-x64 --self-contained false -o $outputPath --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'scripts') -Filter '*.vbs' -File | Copy-Item -Destination $outputPath -Force
