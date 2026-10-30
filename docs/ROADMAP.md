# Apolo (fork do Ares) - Roadmap

O Apolo é um controlador de músicas e efeitos para mesas de RPG (Windows; versão Linux/macOS em andamento).
Não vem com músicas: use as suas ou baixe os pacotes prontos.

## Pronto
**Tocar**
- Música e efeitos juntos, pausa, anterior/próxima, barra de progresso com pulo, volumes geral/música/sons com curva fina, % e mudo.
- Repetir em 3 modos (desligado, playlist, faixa); crossfade entre músicas; efeitos que repetem sem estalo (crossfade consigo mesmo).
- Nivelador de volume (presets e ajuste fino); seletor de saída de áudio; atalhos de teclado configuráveis e atalho global; pad flutuante.

**Organizar**
- Projetos `.apolo` (abre também `.ares`), pastas e subpastas, botões de música (playlist) e de efeito (SFX).
- Arrastar arquivos e pastas com importação inteligente; soltar em uma playlist existente; modo de edição; desfazer/refazer.
- Salvamento automático e seguro (cópia `.bak` e recuperação); registro de erros (`errors.log`, `crash.log`).
- Cores, emojis, ícones e imagens nos botões; banco de imagens; imagem por link.
- Lista de músicas com colunas, busca, ordenação, execuções e direitos; projetos enormes sem travar.

**Conteúdo**
- Instalador 64 bits; pacotes `.apkg` com músicas, ícones, subpastas e créditos; pacotes prontos para baixar.
- Tour guiado e tutorial; ferramentas: renomeador, verificador de direitos, arquivos ausentes, baixar áudio livre, exportação.

**Conexões**
- Controle remoto pelo celular (link seguro pela Cloudflare), plugin do Stream Deck, módulo do Foundry VTT.
- Bot do Discord, envio de áudio por VDO.Ninja, overlay personalizável para o OBS, abas web com captura de áudio.

**Interface**
- Tema escuro, claro e personalizado; ícones Lucide; Configurações numa aba com barra lateral; janela lembra tamanho e posição.

## Em andamento
- Ajustes de interface para monitores menores e revisão das páginas de Configurações.
- Linux e macOS (`src/Apolo.Desktop`, Avalonia): já toca, importa, edita e baixa pacotes. Ver `docs/LINUX-MAC.md`.

## Próximos passos
- Perfis e melhorias na criação de pastas.
- Organização interna do código (arquivos grandes, código antigo do Ares que não é mais usado).
- Mais pacotes de conteúdo.
- Foundry VTT, fase 2: painel do Apolo dentro do Foundry e playlists do Foundry criadas a partir de um projeto.

## Futuro (baixa prioridade)
- Gravação / entrada de áudio dentro do Apolo.
- Modo de alto contraste do Windows.

## Fora dos planos
- Editor separado (removido: tudo está no Player), aviso automático de versão nova, painel de efeitos (os botões de efeito cumprem o papel).
