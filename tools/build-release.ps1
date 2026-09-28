param([switch]$Sign)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$version = '0.1.0'
$makensis = Join-Path $env:LOCALAPPDATA 'tauri\NSIS\makensis.exe'
if (-not (Test-Path -LiteralPath $makensis)) { throw 'NSIS não encontrado.' }
New-Item -ItemType Directory -Force release,site\public\downloads | Out-Null
foreach ($arch in @('x64', 'x86')) {
    $rid = "win-$arch"
    $out = Join-Path $root "release\app-$arch"
    dotnet publish .\Firaw.WorkAssistant.csproj -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $out
    if ($LASTEXITCODE -ne 0) { throw "Falha na publicação $arch." }
    if ($Sign) { & "$PSScriptRoot\sign-windows.ps1" -File (Join-Path $out 'Firaw.WorkAssistant.exe') }
    & $makensis /INPUTCHARSET UTF8 "/DARCH=$arch" "/DVERSION=$version" packaging\offline.nsi
    if ($LASTEXITCODE -ne 0) { throw "Falha no instalador $arch." }
    if ($Sign) { & "$PSScriptRoot\sign-windows.ps1" -File (Join-Path $root "release\Firaw-Work-Assistant-$version-$arch-Setup.exe") }
}
& $makensis /INPUTCHARSET UTF8 packaging\online.nsi
if ($LASTEXITCODE -ne 0) { throw 'Falha no instalador online.' }
if ($Sign) { & "$PSScriptRoot\sign-windows.ps1" -File (Join-Path $root 'release\Firaw-Work-Assistant-Instalador-Online.exe') }
$manifest = [ordered]@{ version = $version }
foreach ($arch in @('x64', 'x86')) {
    $name = "Firaw-Work-Assistant-$version-$arch-Setup.exe"
    $file = Join-Path $root "release\$name"
    $manifest[$arch] = [ordered]@{
        url = "https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows/$arch/arquivo"
        size = (Get-Item -LiteralPath $file).Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
        signerThumbprint = (Get-AuthenticodeSignature -LiteralPath $file).SignerCertificate.Thumbprint
    }
}
foreach ($file in Get-ChildItem release -Filter '*.exe' -File) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $root 'site\public\downloads') -Force
}
$json = $manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $root 'site\public\downloads\release.json'), $json, (New-Object Text.UTF8Encoding $false))
$checks = Get-ChildItem site\public\downloads -File | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $_.Name
}
[IO.File]::WriteAllLines((Join-Path $root 'site\public\downloads\SHA256SUMS.txt'), $checks)
Write-Output $json
