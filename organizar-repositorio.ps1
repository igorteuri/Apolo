# Organizar-repositorio.ps1  (Apolo)
#
# Limpa o repositorio: remove o que veio do Ares e nao faz parte do Apolo (Android, Linux/Mono, MediaPortal,
# banco online de tags, controle em Java, scripts de build antigos) e organiza licencas e documentos.
#
# Como usar (na pasta do repositorio, no PowerShell):
#   .\organizar-repositorio.ps1              -> so mostra o que seria feito (nada muda)
#   .\organizar-repositorio.ps1 -Executar    -> faz de verdade (pede confirmacao e cria uma copia de seguranca)
#
# O script NAO faz commit nem push. Depois de rodar, voce confere e envia do jeito que preferir.
# Nada se perde: o historico do Git guarda tudo, e o script cria um branch de seguranca antes.

param([switch]$Executar)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

function Titulo($t) { Write-Host ""; Write-Host $t -ForegroundColor Cyan }

# ---------- verificacoes ----------
if (-not (Test-Path 'Ares.sln')) {
    throw "Rode este script na pasta do repositorio (onde esta o Ares.sln)."
}
# O Git na linha de comando e opcional: sem ele o script apaga e move os arquivos direto, e voce
# confere e envia as mudancas pelo GitHub Desktop (ou outro programa de Git).
$temGit = [bool](Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path '.git')
if ($temGit) { Write-Host "Git encontrado: o script usa o Git (e cria um branch de seguranca)." -ForegroundColor DarkGray }
else { Write-Host "Git nao encontrado na linha de comando: o script mexe direto nos arquivos. Use o GitHub Desktop para conferir e enviar." -ForegroundColor DarkGray }

# ---------- listas ----------
# Pastas inteiras que saem
$pastas = @(
    'Ares.Controller',            # controle em Java / app Android (inclui uma chave de assinatura do Android)
    'Ares.Controllers',           # base dos controles antigos
    'Ares.MGPlugin',              # plugin do MeisterGeister
    'MPControllerTest',           # teste do plugin do MediaPortal
    'Ares.Plugin',                # plugin do MediaPortal
    'Ares.Player_Android',        # player para Android
    'Ares.GlobalDB.Service',      # banco online de tags (servidor)
    'Ares.GlobalDB.StandaloneHost',
    'Ares.GlobalDb.Web',
    'Ares.GlobalDbImportTest',
    'Ares.GlobalDbUploadTest',
    'Ares.TagsImportTest',        # testes antigos do importador de tags
    'Ares_Portable',              # versao portatil para Linux
    'Libraries_Android',
    'Libraries_Linux',
    'Libraries_Linux_x64',
    'Libraries_Linux_arm_hardfp',
    'Libraries_Linux_arm_softfp',
    'Libraries_MP'
)

# Arquivos soltos que saem
$arquivos = @(
    'Ares.AndroidPlayer.sln', 'Ares.Controllers.sln', 'Ares.GlobalDB.sln', 'Ares.MonoPlayer.sln',
    'Ares_MediaPortal.sln', 'Ares_Test.sln',
    'Ares.build', 'build.xml', 'Build_All.cmd', 'Prepare_Setup.cmd', 'Ares.FxCop', 'Tags.uxf',
    'Ares.png', 'Ares_wide.png',
    'JcifsLicense.txt', 'Xamarin.Android.jCIFSLicense.md',
    # documentos antigos (as versoes novas ficam na pasta docs)
    'HowToBuild.md', 'Modules.md', 'ROADMAP.md'
)

# Projetos de Mono e Android dentro das pastas que ficam (nao fazem parte do Ares.sln)
if ($temGit) {
    $achados = @(git ls-files "*_Mono.csproj" "*_Android.csproj")
} else {
    $achados = @(Get-ChildItem -Recurse -File -Include "*_Mono.csproj", "*_Android.csproj" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/') })
}
$extras = @($achados | Where-Object {
    $p = $_
    -not ($pastas | Where-Object { $p -like "$_/*" })
})

# Licencas que vao para a pasta Licencas
$licencas = @(
    'ChromaprintLicence.txt', 'FfmpegLicence.txt', 'HtmlRendererLicence.txt', 'MouseKeyboardHooksLicence.txt',
    'RestSharpLicence.txt', 'ServiceStackLicence.txt', 'WeifenLuo.WinFormsUI.License.txt',
    'license-gplv3.txt', 'lizenz-deutsch.txt'
)

# ---------- previa ----------
function Tamanho($caminho) {
    if (Test-Path -LiteralPath $caminho) {
        $s = (Get-ChildItem -LiteralPath $caminho -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        if ($s) { return [math]::Round($s / 1MB, 1) }
    }
    return 0
}

Titulo "Pastas que serao removidas"
foreach ($p in $pastas) {
    if (Test-Path -LiteralPath $p) { Write-Host ("  {0,-34} {1,7} MB" -f $p, (Tamanho $p)) }
    else { Write-Host ("  {0,-34} (nao existe)" -f $p) -ForegroundColor DarkGray }
}
Titulo "Arquivos que serao removidos"
foreach ($f in $arquivos) {
    if (Test-Path -LiteralPath $f) { Write-Host "  $f" } else { Write-Host "  $f (nao existe)" -ForegroundColor DarkGray }
}
Titulo "Projetos de Mono / Android que serao removidos ($($extras.Count))"
$extras | ForEach-Object { Write-Host "  $_" }
Titulo "Licencas que vao para a pasta Licencas"
foreach ($l in $licencas) {
    if (Test-Path -LiteralPath $l) { Write-Host "  $l" } else { Write-Host "  $l (nao existe)" -ForegroundColor DarkGray }
}

if (-not $Executar) {
    Write-Host ""
    Write-Host "Nada foi alterado. Se a lista estiver certa, rode:  .\organizar-repositorio.ps1 -Executar" -ForegroundColor Yellow
    return
}

# ---------- execucao ----------
$sujo = $null
if ($temGit) { $sujo = git status --porcelain }
if ($sujo) {
    Write-Host ""
    Write-Host "Atencao: ha alteracoes ainda nao salvas no Git:" -ForegroundColor Yellow
    $sujo | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
    $r = Read-Host "Continuar mesmo assim? (S/N)"
    if ($r -notin @('S', 's')) { Write-Host "Cancelado."; return }
}

Write-Host ""
$r = Read-Host "Digite SIM para remover os itens acima e organizar as licencas"
if ($r -ne 'SIM') { Write-Host "Cancelado. Nada foi alterado."; return }

$backup = "backup-antes-da-limpeza-" + (Get-Date -Format 'yyyyMMdd-HHmm')
if ($temGit) {
    git branch $backup
    Write-Host "Copia de seguranca criada no branch: $backup" -ForegroundColor Green
} else {
    Write-Host "Sem Git na linha de comando, nao ha branch de seguranca. Tudo continua no historico do GitHub." -ForegroundColor Yellow
}

Titulo "Removendo..."
foreach ($p in $pastas) {
    if ($temGit) { git rm -r -q --ignore-unmatch -- $p }
    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }   # sobras que o Git nao rastreava (bin, obj...)
}
foreach ($f in $arquivos) {
    if ($temGit) { git rm -q -f --ignore-unmatch -- $f }
    if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force }
}
foreach ($e in $extras) {
    if ($temGit) { git rm -q -f --ignore-unmatch -- $e }
    if (Test-Path -LiteralPath $e) { Remove-Item -LiteralPath $e -Force }
}
# sobras desta limpeza e da etapa 37
foreach ($f in @('APAGAR.txt')) {
    if ($temGit) { git rm -q -f --ignore-unmatch -- $f }
    if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force }
}

Titulo "Organizando as licencas..."
New-Item -ItemType Directory -Force -Path 'Licencas' | Out-Null
foreach ($l in $licencas) {
    if (Test-Path -LiteralPath $l) {
        if ($temGit) { git mv -f -- $l "Licencas/$l" } else { Move-Item -LiteralPath $l -Destination "Licencas/$l" -Force }
    }
}

if ($temGit) { git add -A }

Titulo "Pronto"
if (-not (Test-Path 'docs/HowToBuild.md')) {
    Write-Host "Aviso: a pasta docs nao foi encontrada. Extraia primeiro o zip da etapa com os documentos novos." -ForegroundColor Yellow
}
Write-Host "Ainda NAO foi feito commit nem push."
if ($temGit) {
    Write-Host "1) Faca o commit, por exemplo:  git commit -m 'Limpeza do repositorio: remove legado do Ares e organiza docs e licencas'"
    Write-Host "2) Para desfazer tudo antes do commit:  git reset --hard $backup"
} else {
    Write-Host "1) Abra o GitHub Desktop: as remocoes e movimentacoes aparecem como alteracoes. Escreva uma mensagem, faca o commit e o push."
    Write-Host "2) Para desfazer antes do commit: no GitHub Desktop, menu Branch > Discard all changes (descartar todas as alteracoes)."
}
Write-Host "3) Depois de conferir o build no GitHub, apague este script (organizar-repositorio.ps1)."
