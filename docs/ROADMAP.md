# Apolo (fork do Ares) - Roadmap

## Concluído
Tema escuro/claro, barra de progresso, pausa, seletor de saída de áudio, arrastar pastas/arquivos, Biblioteca,
ícones modernos (Lucide), abas com páginas web, importação de pastas do disco, edição simples dentro do Player
(seleção múltipla, apagar, opções ao soltar pastas), atalhos configuráveis, ferramenta de arquivos ausentes,
controle remoto + plugin Stream Deck com árvore de pastas, bandeja do Windows, bot do Discord, overlay para OBS,
Editor modernizado (tema, painéis acopláveis, ícones), formato de projeto .apolo, exportação em 3 modos, novos logos, renomeador de músicas, verificador de direitos autorais, "Mostrar na pasta", Foundry VTT (controles) e controle web, imagens sugeridas para os botões e baixador de áudio livre.

## Próximos passos
- Instalador do Apolo (Inno Setup, gerado pelo build) — em teste
- Polimento do motor de áudio (pular dentro de arquivos Opus longos, já com mudança de velocidade sem alterar o tom) — em teste
- Player: pausa por som, ícones/emojis/cores também nas pastas, menu de contexto reorganizado, ícones guardados na pasta do Apolo e dentro do pacote — em teste
- **Editor descontinuado**: tudo o que importa está no Player (modo de edição, "Propriedades da playlist", desfazer/refazer). O código do Editor continua no repositório, mas não entra mais no build nem no instalador — em teste
- Atualizações: o Apolo não procura versões sozinho; a janela "Como atualizar" leva ao Cofre de Conteúdo dos Clubistas (itch.io)
- Stream Deck e controle web mostram o emoji e a cor dos botões; módulo do Foundry com a biblioteca dentro da interface — em teste
- Ajustes finais de tema e de alinhamento da interface

## Ideias em estudo
- **Foundry VTT 14**: fase 1 pronta (módulo `FoundryModule/apolo-audio`: os jogadores ouvem o áudio do Apolo, enviado por VDO.Ninja, dentro do Foundry). Próximas fases: painel de controle do Apolo dentro do Foundry e playlists nativas do Foundry criadas a partir de um projeto.

## Futuro (baixa prioridade)
- Entrada de áudio / gravação dentro do Apolo para usar como música
