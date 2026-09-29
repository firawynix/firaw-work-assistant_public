$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = '0.1.3'
$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $sdk) { throw 'MakeAppx não encontrado.' }
Add-Type -AssemblyName System.Drawing
$bundleDir = Join-Path $root "release\store-packages-$version"
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
foreach ($arch in @('x64','x86')) {
    $stage = Join-Path $root "release\store-stage-$version-$arch"
    $app = Join-Path $stage 'app'
    $assets = Join-Path $stage 'Assets'
    New-Item -ItemType Directory -Force -Path $app,$assets | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "release\app-$arch\Firaw.WorkAssistant.exe") -Destination $app -Force
    $image = [Drawing.Image]::FromFile((Join-Path $root 'assets\huginn-muninn.png'))
    try {
        foreach ($size in @(50,44,150)) {
            $bitmap = [Drawing.Bitmap]::new($size,$size)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($image,0,0,$size,$size)
                $name = switch ($size) { 50 { 'StoreLogo.png' }; 44 { 'Square44x44Logo.png' }; 150 { 'Square150x150Logo.png' } }
                $bitmap.Save((Join-Path $assets $name),[Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $image.Dispose() }
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="Firawynix.FirawWorkAssistant" Publisher="CN=1FDE3668-C222-4506-AFE6-E2E425EAECD8" Version="$version.0" ProcessorArchitecture="$arch" />
  <Properties>
    <DisplayName>Firaw Work Assistant</DisplayName>
    <PublisherDisplayName>Firawynix</PublisherDisplayName>
    <Description>Assistente de trabalho com tarefas, notas e checklists</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources><Resource Language="pt-BR" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications>
    <Application Id="WorkAssistant" Executable="app\Firaw.WorkAssistant.exe" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="Firaw Work Assistant" Description="Assistente de trabalho com tarefas, notas e checklists" BackgroundColor="transparent" Square44x44Logo="Assets\Square44x44Logo.png" Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
    [IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
    & $sdk pack /o /d $stage /p (Join-Path $bundleDir "Firaw-Work-Assistant-$version-$arch.msix")
    if ($LASTEXITCODE -ne 0) { throw "Falha no MSIX $arch." }
}
& $sdk bundle /o /bv "$version.0" /d $bundleDir /p (Join-Path $root "release\Firaw-Work-Assistant-$version.msixbundle")
if ($LASTEXITCODE -ne 0) { throw 'Falha no bundle Store.' }
