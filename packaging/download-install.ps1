param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
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
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash -ne [string]$entry.sha256) { throw 'SHA-256 do instalador não confere.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $setup
    if ($signature.SignerCertificate.Thumbprint -ne '668BEF56480FCA8EB1B0BC3B102A6A9CEC554CC4' -or $signature.Status -notin @('Valid', 'NotTrusted')) {
        throw 'Assinatura Firawynix do instalador não confere.'
    }
    if ($VerifyOnly) { Write-Output "Pacote $arch verificado."; exit 0 }
    $process = Start-Process -FilePath $setup -ArgumentList '/S' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) { throw "Instalação terminou com erro $($process.ExitCode)." }
    exit 0
} catch {
    [IO.File]::WriteAllText((Join-Path ([IO.Path]::GetTempPath()) 'FirawWorkAssistant-install-error.txt'), $_.Exception.Message)
    exit 1
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolved = [IO.Path]::GetFullPath($temp)
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^FirawWorkAssistant-[a-f0-9]{32}$') { throw 'Pasta temporária inválida.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
