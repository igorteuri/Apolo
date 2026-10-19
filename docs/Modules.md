# Módulos do Apolo

O Apolo é dividido em módulos (projetos .NET) para separar responsabilidades. Os nomes internos `Ares.*` vêm do projeto original e foram mantidos. Os projetos ficam na pasta `src`.

## Base

- **Ares.Data**: modelo de dados dos projetos (pastas, playlists, arquivos), gravado em XML (`.apolo`).
- **Ares.Settings**: configurações do programa, pastas de dados (`%APPDATA%\Apolo`) e extensão de projeto.
- **Ares.Tags**: leitura e gravação das tags de áudio (TagLib#).
- **Ares.CommonGUI**: telas e utilidades comuns (temas claro/escuro, ícones, janela "Sobre", marca do Apolo, imagens e cores dos botões, diálogo de adicionar arquivos e pastas).
- **Ares.Ipc**: reconhecimento de uma segunda cópia do programa aberta (abre o projeto na janela que já existe).
- **Ares.ModelChecks** (`Ares.ModelInfo`): verificações que apontam erros no projeto.
- **Ares.WinSecurity**: pequeno executável que ajusta o firewall do Windows para o controle remoto (precisa de permissão de administrador; é o único projeto em .NET Framework 4.8).

## Reprodução

- **Ares.Playing**: toda a lógica de tocar os elementos dos projetos. O motor de áudio próprio do Apolo (pasta `Audio`) usa [NAudio](https://github.com/naudio/NAudio) para a saída, NLayer / NVorbis / Concentus para decodificar e TagLib# para as tags.
- **Ares.Players**: código comum dos players (controle da reprodução, mensagens).
- **Ares.Player**: o programa principal (Player), em Windows Forms. Inclui o modo de edição, o renomeador de músicas, o verificador de direitos autorais, a busca de música e imagens livres, emojis, imagens por link e cores dos botões, o controle remoto (`Player.Remote.cs`) e a ponte do Discord.
- **Ares.TagsImport**: identificação de músicas ([Chromaprint](https://acoustid.org/chromaprint), AcoustID e MusicBrainz), usada na verificação de direitos autorais.

## Fora do .NET

- **StreamDeck**: plugin para o Stream Deck.
- **FoundryModule**: módulo `apolo-audio` para o Foundry VTT.
- **src/Ares.Player/Discord**: ponte do Discord (Node.js).
- **Branding**: logos do Apolo.
- **Setup**: instalador (Inno Setup).
- **tests**: testes automáticos (xunit); rodam no build e, se algum falhar, o instalador não é gerado.

## O que foi removido

O Editor do Ares original (`Ares.Editor`, `Ares.Editor.Actions`, `Ares.Editor.Controls`), o player de linha de comando, as fontes de áudio do Editor (`Ares.AudioSource*`), as janelas de novidades do Ares antigo (`Ares.Online`) e os restos do Android foram retirados do repositório porque o Apolo não os usa. Eles continuam no histórico do Git (o ponto de partida é o commit anterior à limpeza) e no repositório original do Ares.
