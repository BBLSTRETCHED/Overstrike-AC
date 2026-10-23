$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project CityStore.Tests/CityStore.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed' }
    if (Test-Path artifacts/ArkhamCity) { Remove-Item artifacts/ArkhamCity -Recurse -Force }
    dotnet publish Overstrike/Overstrike.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Platform=x64 -o artifacts/ArkhamCity
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Move-Item artifacts/ArkhamCity/Overstrike.exe 'artifacts/ArkhamCity/Overstrike AC.exe' -Force
    New-Item -ItemType Directory -Force artifacts/ArkhamCity/Support | Out-Null
    Copy-Item LICENSE.txt, ARKHAM_CITY_README.md, UPSTREAM_COMMIT.txt, QUICK_START.txt, VALIDATION_REPORT.md, BUTTON_AUDIT.md artifacts/ArkhamCity/Support/
    Get-ChildItem artifacts/ArkhamCity -Filter *.pdb | Remove-Item
    Write-Host 'Run artifacts/ArkhamCity/Overstrike AC.exe'
} finally { Pop-Location }
