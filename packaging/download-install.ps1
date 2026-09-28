param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# Use only the Windows inbox PowerShell modules in the installer process.
$env:PSModulePath = Join-Path $PSHOME 'Modules'
$errorLog = Join-Path ([IO.Path]::GetTempPath()) 'FirawWorkAssistant-install-error.txt'
[IO.File]::WriteAllText($errorLog, '')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('FirawWorkAssistant-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    # NSIS runs Windows PowerShell 5.1; use the Windows HTTPS client instead of
    # .NET Framework's TLS negotiation, including from a 32-bit installer.
    $systemDirectory = if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) { 'Sysnative' } else { 'System32' }
    $curl = Join-Path $env:WINDIR "$systemDirectory\curl.exe"
    if (-not (Test-Path -LiteralPath $curl)) { throw 'O cliente HTTPS do Windows não foi encontrado. Atualize o Windows e tente novamente.' }
    function Save-HttpsFile([string]$Url, [string]$Destination, [int]$Timeout) {
        $errorFile = Join-Path $temp 'download-error.txt'
        & $curl --fail --silent --show-error --proto '=https' --tlsv1.2 --connect-timeout 30 --max-time $Timeout --stderr $errorFile --output $Destination $Url
        if ($LASTEXITCODE -ne 0) {
            $detail = if (Test-Path -LiteralPath $errorFile) { [IO.File]::ReadAllText($errorFile).Trim() } else { '' }
            throw "Não foi possível baixar o instalador. $detail"
        }
    }
    $origin = 'https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows'
    $manifestFile = Join-Path $temp 'atualizacao.json'
    Save-HttpsFile "$origin/atualizacao.json" $manifestFile 30
    $manifest = [IO.File]::ReadAllText($manifestFile) | ConvertFrom-Json
    $arch = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
    $entry = $manifest.architectures.$arch
    if (-not $entry) { throw "Pacote $arch não encontrado no manifesto." }
    $uri = [Uri]$entry.url
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'jogos.firawynix.com.br' -or -not $uri.AbsolutePath.StartsWith('/api/games/firaw-work-assistant/windows/')) {
        throw 'Endereço do pacote inválido.'
    }
    $setup = Join-Path $temp 'setup.exe'
    Save-HttpsFile $uri.AbsoluteUri $setup 300
    if ((Get-Item -LiteralPath $setup).Length -ne [long]$entry.size) { throw 'Tamanho do download não confere.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($setup)
    try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($actualHash -ne [string]$entry.sha256) { throw 'SHA-256 do instalador não confere.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $setup
    if ($signature.SignerCertificate.Thumbprint -ne '668BEF56480FCA8EB1B0BC3B102A6A9CEC554CC4' -or $signature.Status -notin @('Valid', 'NotTrusted')) {
        throw 'Assinatura Firawynix do instalador não confere.'
    }
    if ($VerifyOnly) { Write-Output "Pacote $arch verificado."; exit 0 }
    $installDir = [Environment]::GetEnvironmentVariable('FIRAW_WORK_INSTALL_DIR')
    if ([string]::IsNullOrWhiteSpace($installDir) -or $installDir -notmatch '^[a-zA-Z]:\\' -or $installDir -match '["\r\n]') {
        throw 'A pasta de instalação recebida é inválida.'
    }
    $installDir = [IO.Path]::GetFullPath($installDir)
    $process = Start-Process -FilePath $setup -ArgumentList "/S /D=$installDir" -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) { throw "Instalação terminou com erro $($process.ExitCode)." }
    $app = Join-Path $installDir 'Firaw.WorkAssistant.exe'
    if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'O instalador terminou sem colocar o aplicativo na pasta escolhida.' }
    $registry = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant' -ErrorAction Stop
    if ([IO.Path]::GetFullPath($registry.InstallLocation) -ne $installDir) { throw 'A instalação não registrou a pasta escolhida pelo Firaw Center.' }

    # Desktop may be redirected to OneDrive. Repair links after silent installs.
    $shell = New-Object -ComObject WScript.Shell
    $links = @(
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Firaw Work Assistant.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'Firaw\Work Assistant.lnk')
    )
    foreach ($link in $links) {
        if (-not (Test-Path -LiteralPath $link)) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($link)) | Out-Null
            $shortcut = $shell.CreateShortcut($link)
            $shortcut.TargetPath = $app
            $shortcut.WorkingDirectory = $installDir
            $shortcut.IconLocation = "$app,0"
            $shortcut.Save()
        }
        if (-not (Test-Path -LiteralPath $link -PathType Leaf)) { throw "Não foi possível criar o atalho: $link" }
    }
    exit 0
} catch {
    [IO.File]::WriteAllText($errorLog, $_.Exception.Message)
    exit 1
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolved = [IO.Path]::GetFullPath($temp)
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^FirawWorkAssistant-[a-f0-9]{32}$') { throw 'Pasta temporária inválida.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
