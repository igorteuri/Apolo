# Apolo - documento de passagem (HANDOFF)

Use este arquivo para retomar o projeto em uma nova sessao do Claude (inclusive em outra conta).
Cole no inicio da conversa: "Leia docs/HANDOFF.md do repositorio igorteuri/Apolo e continue de onde paramos."

## Quem e o que
- Igor, leigo em programacao, fala portugues. Repositorio: https://github.com/igorteuri/Apolo (clone local H:\Repositorios\Apolo, usa GitHub Desktop).
- Apolo e uma modernizacao do player de audio RPG Ares (Jorg Ruedenauer, GPL v2+). Creditos mantidos; namespaces internos `Ares.*` mantidos.
- C# WinForms, .NET Framework 4.8, csproj antigo com `<Compile Include>` explicito, **C# 5** (sem `?.`, `out var`, interpolacao), x86.
- O Editor foi descontinuado: tudo essencial vive no Player.

## Regras de trabalho combinadas
1. O Claude **nao faz push**. Igor sobe tudo manualmente. Commits sao locais, com os trailers de coautoria da sessao.
2. Entregas em zip (`apolo-etapaNN.zip`) com os arquivos alterados; Igor copia para o repositorio, roda o workflow `.github/workflows/build.yml` no GitHub Actions, testa e reporta.
3. Nao ha compilador no ambiente do Claude: o build do CI e o teste de compilacao. Cuidado com ambiguidades de nomes com `Ares.Data` (ex.: `Keys` -> `System.Windows.Forms.Keys`; `File` -> `System.IO.File`).
4. Responder em portugues, linguagem simples, sem jargao.
5. Atualizacoes do Apolo para apoiadores saem pelo Cofre dos Clubistas (itch.io): apoio https://cutt.ly/clubecritico , cofre https://igorteuri.itch.io/contedo-exclusivo-dados-criticos (senha mensal no Discord).

## Mapa do codigo (src/Ares.Player)
- `Player.cs` (janela principal), `Player.Edit.cs` (modo edicao, `ApplyProjectEdit`, arrastar), `Player.Undo.cs` (desfazer/refazer por snapshots), `Player.Info.cs` (menu Extras/ajuda), `Player.Header.cs` (cabecalho), `Player.Status.cs`, `Player.Tracks.cs` + `Player.TrackColumns.cs` + `TrackListView.cs` (lista de musicas em colunas), `NowPlayingStrip.cs` (faixa "Tocando agora"), `AppTabBar.cs` + `Player.Web.cs` (abas e abas web), `Player.Remote.cs`/`Player.Deck.cs` (servidor web/JSON para Stream Deck, Foundry e controle web), `PlaylistProperties.cs`, `ElementButton.cs` (botoes em cartao, selo de tipo), `Player.Library.cs` (importar arrastando pastas), `Ares.CommonGUI/DropOptionsDialog.cs` (dialogo de adicionar).
- Stream Deck: `StreamDeck/com.igorteuri.apolo.sdPlugin/bin/plugin.js`. Foundry: `FoundryModule/apolo-audio` (v0.5.1).
- Instalador: `Setup/Apolo.iss` (Inno Setup, passo no build.yml, artefato `Apolo-installer`).

## O que foi feito (resumo por etapa)
- Etapas 46-50: propriedades da playlist; icones/cores nos botoes do Stream Deck e do controle web; biblioteca do Foundry inline; desfazer/refazer (Ctrl+Z/Y); a pasta permanece ao editar/arrastar (`m_PreferredModeTitle`); janela de Avisos centralizada; Editor removido do build/instalador; janelas de ajuda do Foundry e de atualizacao.
- Etapa 51 (esta): abas 50% mais largas com logo a esquerda; faixa "Tocando agora" (chips de efeitos com progresso/pausa/parar/repetir e chips de abas web com mutar) no lugar da coluna de status (agora recolhida por padrao, preferencia `LeftOpenV2`); lista de musicas em colunas (#, Titulo, Artista, Album, Duracao, Arquivo) com ordenacao por clique no cabecalho, colunas reordenaveis, arrastar linhas para reordenar playlists em sequencia, "Gravar esta ordem na playlist", "Editar informacoes" (titulo no projeto; artista/album nas tags); selo de tipo nos botoes (musica / efeito); atalhos no dialogo de adicionar; "Novo painel de efeitos" no menu de pastas.
- Checkpoint: tag/branch local `checkpoint-etapa50` (commit b619101) e zip `apolo-checkpoint-etapa50-fonte-completo.zip`.

## Pendencias / ideias
- Confirmar com Igor os resultados da etapa 50 (pasta nao pula mais; Avisos centralizado; instalador depois de aplicar o build.yml) e da etapa 51 (compilou? visual da faixa e da lista).
- Barra de titulo do Windows realmente removida (janela sem borda) foi adiada: arriscada (mover, redimensionar, snap, DPI).
- Coluna "atalho" por faixa: nao existe no modelo (atalhos sao por botao).
- Preparacao de distribuicao para apoiadores: revisao de licencas (GPL, AcoustID), empacotamento, itch.io.
- Multiplataforma (Mac/Linux): so depois do Windows pronto; caminho recomendado: .NET 8 + Avalonia. Microfone virtual, captura de audio por app e atalhos globais sao especificos de cada sistema.
- Exemplos de vocabulario de emojis, polimento de tema/alinhamento.

## Licoes tecnicas
- `Ares.Playing.Player.SetProject` volta para a primeira pasta; por isso a UI reaplica a pasta preferida apos recarregar.
- Evitar chaves duplicadas em inicializadores de dicionario; `sed` pode alterar modificadores de acesso; compilar "no escuro" exige revisao cuidadosa.
