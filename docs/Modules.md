# Módulos do Apolo

O Apolo é dividido em módulos (projetos .NET) para separar responsabilidades. Os nomes internos `Ares.*` vêm do projeto original.

## Base

- **Ares.Data**: modelo de dados dos projetos (pastas, playlists, arquivos), gravado em XML (`.apolo`).
- **Ares.Settings**: configurações do programa, pastas de dados (`%APPDATA%\Apolo`) e extensão de projeto.
- **Ares.Tags**: leitura e gravação das tags de áudio (TagLib#).
- **Ares.CommonGUI**: telas e utilidades comuns ao Player e ao Editor (temas claro/escuro, ícones, janela "Sobre", marca do Apolo, imagens e cores dos botões).
- **Ares.Ipc**: troca de dados entre o Player e o Editor abertos ao mesmo tempo.
- **Ares.ModelChecks** (`Ares.ModelInfo`): verificações que apontam erros no projeto.
- **Ares.Online**: aviso de atualização (Releases do GitHub) e notícias.
- **Ares.WinSecurity**: pequeno executável que ajusta o firewall do Windows para o controle remoto (precisa de permissão de administrador).

## Reprodução

- **Ares.Playing**: toda a lógica de tocar os elementos dos projetos. O motor de áudio próprio do Apolo (pasta `Audio`) usa [NAudio](https://github.com/naudio/NAudio) para a saída, NLayer / NVorbis / Concentus para decodificar e TagLib# para as tags.
- **Ares.Players**: código comum dos players, incluindo o servidor web do controle remoto pelo navegador e pelo celular.
- **Ares.Player**: o programa principal (Player), em Windows Forms. Inclui os renomeador de músicas, verificador de direitos autorais, busca de música e imagens livres, emojis e cores dos botões.
- **Ares.CmdLinePlayer**: player de linha de comando.

## Editor

- **Ares.Editor**: o Editor de projetos, com janelas acopláveis ([DockPanel Suite](http://dockpanelsuite.com/)).
- **Ares.Editor.Actions**: as ações do Editor (padrão *command*, que permite desfazer e refazer).
- **Ares.Editor.Controls**: controles para editar os elementos de um projeto.
- **Ares.TagsImport**: identificação de músicas ([Chromaprint](https://acoustid.org/chromaprint), AcoustID e MusicBrainz).
- **Ares.AudioSource**, **Ares.AudioSource.Freesound** e **Ares.AudioSource.Test**: fontes de áudio do Editor (a busca no Freesound precisa de uma chave de API, veja `ApiKey.example.cs`).

## Fora do .NET

- **StreamDeck**: plugin para o Stream Deck.
- **FoundryModule**: módulo `apolo-audio` para o Foundry VTT.
- **Ares.Player/Discord**: ponte do Discord (Node.js).
- **Branding**: logos do Apolo.
