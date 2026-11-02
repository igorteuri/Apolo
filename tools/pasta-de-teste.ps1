# Gera uma pasta com o Apolo pronto para abrir (sem instalador), para testar rápido nesta máquina.
#   powershell -ExecutionPolicy Bypass -File tools/pasta-de-teste.ps1 [-Destino <pasta>]
# Padrão: a pasta "Teste" ao lado do clone (fora do repositório).
# O que não é compilado aqui (túnel cloudflared, ponte do Discord, ajudante de firewall, Stream Deck,
# módulo do Foundry) é copiado do Apolo instalado em "C:\Program Files\Apolo", se existir.
param([string]$Destino)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not $Destino) { $Destino = Join-Path (Split-Path -Parent (Split-Path -Parent $root)) 'Teste' }
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet).Source }

$exe = Join-Path $Destino 'Apolo.exe'
$running = Get-Process -Name Apolo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running) { Write-Host 'FALHOU: o Apolo da pasta de teste está aberto. Feche e rode de novo.'; exit 1 }

$watch = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host "==> compilando o Apolo (x64) em $Destino"
& $dotnet publish src/Ares.Player/Ares.Player.csproj -c Release -r win-x64 --self-contained true --nologo -v:q -clp:ErrorsOnly -o $Destino
if ($LASTEXITCODE -ne 0) { Write-Host 'FALHOU: o Apolo não compila.'; exit 1 }

Remove-Item (Join-Path $Destino 'bass*.dll'), (Join-Path $Destino 'Bass*.dll') -Force -ErrorAction SilentlyContinue
Write-Host '==> banco de imagens'
Copy-Item BancoDeImagens (Join-Path $Destino 'BancoDeImagens') -Recurse -Force

$installed = 'C:\Program Files\Apolo'
foreach ($item in 'cloudflared.exe', 'discord', 'WinSecurity', 'StreamDeck', 'FoundryModule') {
    $from = Join-Path $installed $item
    $to = Join-Path $Destino $item
    if ((Test-Path $from) -and -not (Test-Path $to)) {
        Write-Host "==> copiando $item do Apolo instalado"
        Copy-Item $from $to -Recurse -Force
    }
}

Write-Host ("OK: pasta de teste pronta ({0:N0} s): {1}" -f $watch.Elapsed.TotalSeconds, $exe)
exit 0
