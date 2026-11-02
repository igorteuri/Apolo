# Verificação local do Apolo: compila o programa e roda os testes automáticos.
# Para no primeiro erro. É o mesmo que o GitHub Actions confere (build.yml), em versão rápida.
#   powershell -ExecutionPolicy Bypass -File tools/ci.ps1
#   (rodado sozinho antes de cada push pelo .githooks/pre-push)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# o dotnet do PATH pode ser o x86 sem SDK: prefere o x64
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnet = $cmd.Source } else { $dotnet = $null }
}
if (-not $dotnet -or -not (& $dotnet --list-sdks 2>$null)) {
    Write-Host 'AVISO: .NET SDK não encontrado nesta máquina; a verificação fica só no GitHub Actions.'
    exit 0
}

$watch = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host '==> compilando o Apolo'
& $dotnet build src/Ares.Player/Ares.Player.csproj -c Debug --nologo -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { Write-Host 'FALHOU: o Apolo não compila.'; exit 1 }

Write-Host '==> testes automáticos'
& $dotnet test tests/Apolo.Tests/Apolo.Tests.csproj -c Debug --nologo -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { Write-Host 'FALHOU: há teste vermelho.'; exit 1 }

Write-Host ("OK: tudo verde ({0:N0} s)" -f $watch.Elapsed.TotalSeconds)
exit 0
