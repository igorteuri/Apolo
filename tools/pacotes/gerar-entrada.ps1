# Gera o bloco do pacote para colar no arquivo pacotes.ini (tamanho e assinatura sha256 calculados do .apkg).
# Uso (PowerShell):  .\gerar-entrada.ps1 -Arquivo "C:\...\pacote-inicial.apkg" -Titulo "Pacote inicial" -Descricao "Pastas e botões prontos"
param(
    [Parameter(Mandatory = $true)][string]$Arquivo,
    [Parameter(Mandatory = $true)][string]$Titulo,
    [string]$Descricao = ""
)
$f = Get-Item -LiteralPath $Arquivo
$id = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
$hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLower()
Write-Output ""
Write-Output "[$id]"
Write-Output "titulo = $Titulo"
Write-Output "descricao = $Descricao"
Write-Output "arquivo = $($f.Name)"
Write-Output "bytes = $($f.Length)"
Write-Output "sha256 = $hash"
Write-Output ""
