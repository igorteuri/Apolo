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

## Andamento
- Etapa 55 (fase 1, em andamento): pasta `net8/` com cópias "paralelas" dos projetos `Ares.Ipc`, `Ares.Settings` e `Ares.Data` para .NET 8. Elas compilam os MESMOS arquivos de `src/` (gerado por `tools/gen_net8.py`), então o Apolo atual não muda. O passo "Migration test (.NET 8)" do build mostra quais já compilam; ele não bloqueia o instalador.
- Próximas fases: acrescentar `Ares.Tags` (trocar ServiceStack.Text e SQLite por pacotes NuGet), `Ares.Playing`, `Ares.Players`, `Ares.CommonGUI` e por fim `Ares.Player`; quando todos compilarem em .NET 8, o executável novo substitui o antigo.
- Etapa 56: `Ares.Ipc`, `Ares.Settings` e `Ares.Data` compilam em .NET 8 (confirmado no GitHub Actions).
- Etapa 57 (fase 2): entram `Ares.Tags` (SQLite e TagLib# por NuGet; o ServiceStack.Text antigo foi trocado por um substituto em JSON, `net8/shims`), `Ares.ModelInfo`, `Ares.Playing` (NAudio, NLayer, NVorbis, Concentus por NuGet) e `Ares.CommonGUI`. Ficam para depois: `Ares.TagsImport` (RestSharp), `Ares.Players` (servidor web do controle remoto, feito com ServiceStack 4 + Razor: precisa ser reescrito), `Ares.Online` e `Ares.Player`.
- Etapa 59: os 7 projetos acima compilam em .NET 8 (confirmado no GitHub Actions). A causa dos erros anteriores era a pasta `obj` compartilhada.
- Etapa 60 (fase 3): entram `Ares.TagsImport` (RestSharp 106), `Ares.Players` (sem o servidor web antigo do ServiceStack: um substituto vazio avisa que ele não existe; o controle remoto do próprio Apolo, em `Player.Remote.cs`, não é afetado) e `Ares.Online` (HtmlRenderer). Falta o executável `Ares.Player` (WebView2 e monitor de teclado por pacotes/código novo) e o plugin Freesound.
- Etapa 60: `Ares.Players` compilou. Falharam `Ares.TagsImport` e `Ares.Online`.
- Etapa 61 (poda de legado + janela principal): `Ares.Online` (janelas de novidades e nova versão do Ares antigo) não é usado pelo Player, então NÃO será migrado e deve sair do projeto no fim. `Ares.TagsImport` passou a compilar só o necessário (`MusicLookup`, usado na verificação de direitos autorais; sai o banco online de tags antigo). `Ares.Tags` e a parte de rede antiga de `Ares.Players` seguem por enquanto porque o `Player.cs` ainda as chama; remover depois da migração, como limpeza separada. Entra `Ares.Player` (a janela principal) com dois substitutos em `net8/shims`: monitor de teclado (hotkeys) e `JavaScriptSerializer`; o registro de licença do ServiceStack no `Program.cs` fica só para o build antigo.

## Progresso etapas 62-66
- Etapas 62-65: corrigidos os últimos erros (ambiguidade de `MethodInvoker`, `HookProc` público no substituto do monitor de teclado); os 10 projetos, incluindo o Apolo.exe, compilam no .NET 8.
- Etapa 66: `net8.yml` agora publica o Apolo (self-contained, win-x86) e envia o artefato `Apolo-net8` para testes de execução.
- Próximo: testar a execução (WebView2, atalhos globais, captura de áudio, verificação de direitos), instalador e limpeza do legado; só então o merge na master.
- Etapa 67 (primeiros testes do Apolo .NET 8): abriu, toca, renomeia, verifica direitos e abre a web. Ajustes: pacote do Discord/Stream Deck/Foundry no net8.yml, grade suave nas listas, botões da janela de direitos, painel de pastas redimensionável com dica de texto completo, busca livre com Wikimedia Commons e Freesound em música e efeitos.
- Menus sem ícones (só alguns itens tinham, deixava coluna vazia); marca de seleção mantida onde há itens marcáveis.
- Conhecido: editar o projeto (mover botão) recarrega o projeto e para o som (comportamento antigo, a melhorar depois).
- Etapa 69 (2º teste): `Process.Start` de arquivo/URL precisa de `UseShellExecute = true` no .NET 8 (corrigido: plugin do Stream Deck, links, páginas de música livre); renomeador com grade suave (inclui caixas de marcar); cabeçalhos de lista escuros; listas suspensas com as cores do tema; painel de Estado redimensionável; F2 renomeia pasta/botão; fade nas bordas das imagens dos botões; tocadas não reordenam a lista na hora; janela de Configurações reorganizada; só os idiomas pt, pt-BR, de, en no pacote.
- Ideias para depois: banco de imagens leves (webp) de locais/temas de RPG para preencher botões (manual ou automático); barras de rolagem no tema.
- Etapa 70: cabeçalho das listas desenhado com cores do tema; `ThemedComboBox` (lista suspensa no tema) no tipo da playlist; setas ▲▼ da lista de músicas com aviso de erro. Barras de rolagem do tema ainda pendentes.

- Etapa 71: build.yml agora publica .NET 8 self-contained + instalador Inno Setup (WinSecurity em subpasta); net8.yml só manual; testes em net8; correções: flash branco na abertura, ícone de pausa, barra de rolagem, abas/espaçamentos de Configurações, reordenar botões sem parar a música.
- Etapa 72: corrige build do WinSecurity (solution filter tools/WinSecurity.slnf).
- Etapa 73: importador de .apkg tolerante (projeto identificado pela extensão, comparações ordinais, entradas desconhecidas ignoradas).
- Etapa 74: importar .apkg ao abrir o programa: monitor de progresso não usa mais a rede quando ela ainda não existe (NullReference).
- Etapa 75: log de diagnóstico da captura de áudio (%AppData%\Apolo\audio-capture.log) para investigar áudio das abas web que não sai no Discord/VDO.
- Etapa 76: captura de áudio refeita (motor do Apolo sempre + processos WebView2 via process loopback, mixados); botão Extensões nas abas web (pasta, .crx, .zip).
- Etapa 77: com 'não ouvir pelo PC' as abas web continuam sendo enviadas (Discord/VDO) mas ficam mudas no PC (sessões de áudio do WebView2 mutadas no Windows).
