param([switch]$Sign)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$version = '0.1.3'
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
foreach ($name in @('Firaw-Work-Assistant-Instalador-Online.exe', "Firaw-Work-Assistant-$version-x64-Setup.exe", "Firaw-Work-Assistant-$version-x86-Setup.exe")) {
    Copy-Item -LiteralPath (Join-Path $root "release\$name") -Destination (Join-Path $root 'site\public\downloads') -Force
}
$json = $manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $root 'site\public\downloads\release.json'), $json, (New-Object Text.UTF8Encoding $false))
$checks = @('Firaw-Work-Assistant-Instalador-Online.exe', "Firaw-Work-Assistant-$version-x64-Setup.exe", "Firaw-Work-Assistant-$version-x86-Setup.exe", 'release.json') | ForEach-Object {
    $file = Join-Path $root "site\public\downloads\$_"
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant(), $_
}
[IO.File]::WriteAllLines((Join-Path $root 'site\public\downloads\SHA256SUMS.txt'), $checks)
Write-Output $json
