# Migração do Apolo para .NET 8 — avaliação e plano

Situação: o Apolo é WinForms em .NET Framework 4.8 (C# 5, csproj antigo com lista explícita de arquivos, 19 projetos, ~370 arquivos .cs). O único compilador que temos é o do GitHub Actions, então cada tentativa custa um ciclo de upload + build.

## Pontos que precisam de atenção
- `System.Web.Extensions` / `JavaScriptSerializer` (4 arquivos): trocar por `System.Text.Json`.
- `ServiceStack.Text` (12 arquivos): existe versão para .NET 8, mas a API de licença/uso mudou; avaliar trocar por System.Text.Json também.
- `MouseKeyboardActivityMonitor` (2 arquivos, biblioteca antiga em DLL): precisa de substituto (hook de teclado próprio via P/Invoke).
- `System.Configuration` (4 arquivos): `ConfigurationManager` existe como pacote NuGet, mas dá para trocar pelas preferências próprias.
- WebView2: já suporta .NET 8 (pacote NuGet em vez de DLL baixada à mão).
- NAudio, NLayer, NVorbis, Concentus, TagLib#: todos têm versões .NET Standard / .NET 6+.
- Recursos (.resx) e designers: o formato é o mesmo, mas o projeto novo (SDK-style) gera recursos de outro jeito; precisa de revisão.
- `Ares.Editor*` (descontinuado): não migrar; sai da solução.

## Plano em fases (cada fase compila sozinha no GitHub Actions)
1. Projetos de base (`Ares.Ipc`, `Ares.Tags`, `Ares.Data`, `Ares.Settings`): converter para SDK-style com `TargetFrameworks` = `net48;net8.0-windows`. O Apolo atual continua usando o net48; o build novo prova que o código compila nos dois.
2. `Ares.Playing`, `Ares.Players`, `Ares.TagsImport`, `Ares.Online`: o mesmo.
3. `Ares.CommonGUI` e `Ares.Player`: converter e trocar as bibliotecas da lista acima; só aqui o executável passa a ser .NET 8.
4. Instalador/CI: publicar autocontido (`dotnet publish -r win-x86 --self-contained`) e ajustar o Inno Setup.

Esta etapa não executou a fase 1: sem poder compilar aqui, converter 19 projetos às cegas quebraria o build atual. Proposta: fazer a fase 1 na próxima etapa, depois que os testes automáticos rodarem no CI.
