# Apolo para Linux e macOS

Esta é a versão do Apolo que roda em **Linux, macOS e Windows** com a mesma janela (feita com Avalonia) e o **mesmo motor de áudio** do Apolo para Windows. Ela fica na pasta `src/Apolo.Desktop`. O Apolo para Windows de sempre (WinForms) continua sendo o principal enquanto a versão nova não tiver tudo.

> **Estado:** primeira etapa. Toca projetos prontos, mas ainda não edita. Veja a tabela no fim.

## Como baixar

A cada envio ao GitHub, a aba **Actions** monta os pacotes (em *Artifacts* da execução "Apolo Desktop"):

| Sistema | Arquivo | Como usar |
|---|---|---|
| Linux (64 bits) | `Apolo-linux-x64.tar.gz` | Extraia e rode `Apolo/Apolo.Desktop` (ou use o `apolo.desktop`) |
| macOS (Apple Silicon: M1, M2, M3...) | `Apolo-osx-arm64.zip` | Descompacte e arraste `Apolo.app` para Aplicativos |
| macOS (Intel) | `Apolo-osx-x64.zip` | Idem |
| Windows | `Apolo-win-x64.zip` | Extraia e rode `Apolo.Desktop.exe` |

Não precisa instalar o .NET: tudo vai junto.

### macOS: o aviso de segurança
O aplicativo ainda não é assinado pela Apple. Na primeira vez, clique com o botão direito em `Apolo.app` > **Abrir** > **Abrir**. Se o macOS disser que o app está "danificado", rode no Terminal: `xattr -dr com.apple.quarantine /Applications/Apolo.app`.

### Linux: o som
O Apolo usa o OpenAL Soft (vem junto), que conversa com o PulseAudio, o PipeWire ou o ALSA. Se o sistema não tiver nenhum desses, o Apolo abre sem som e avisa.

### Formatos de áudio
- **Funcionam sem nada extra:** MP3, OGG (Vorbis), Opus e WAV.
- **Precisam do [ffmpeg](https://ffmpeg.org) instalado** (o Apolo o encontra sozinho no `PATH`): M4A/AAC, FLAC, WMA, AIFF e outros. No Linux: `sudo apt install ffmpeg`. No macOS: `brew install ffmpeg`.

## Onde ficam os dados
- Linux: `~/.config/Apolo`
- macOS: `~/Library/Application Support/Apolo`
- Windows: `%APPDATA%\Apolo`

Os projetos `.apolo` e os pacotes `.apkg` são os mesmos do Windows: dá para montar o projeto no Windows e abrir no Linux ou no Mac. Para os caminhos das músicas funcionarem, use a mesma pasta de músicas nas **Configurações** ou projetos com pacotes (`.apkg`), que levam as músicas junto.

## O que já funciona e o que falta

| Recurso | Windows (WinForms) | Linux / macOS (esta versão) |
|---|:--:|:--:|
| Abrir projetos `.apolo` e pacotes `.apkg` | sim | **sim** |
| Pastas (com subpastas), botões, cores, ícones e imagens | sim | **sim** |
| Tocar música e efeitos juntos, parar, pausar, anterior, próxima, repetir | sim | **sim** |
| Volumes geral, música e sons; nivelador | sim | **sim** |
| Efeitos que repetem com crossfade | sim | **sim** (mesmo motor) |
| Barra de progresso com pulo, lista de efeitos tocando | sim | **sim** |
| Escolher pasta de músicas e placa de som | sim | **sim** |
| Modo de edição (criar/mudar pastas e botões), arrastar arquivos | sim | falta |
| Lista de faixas, importar pastas, desfazer | sim | falta |
| Controle remoto pelo celular, Stream Deck, Foundry | sim | falta |
| Discord, envio de áudio (VDO.Ninja), overlay do OBS | sim | falta |
| Pad flutuante com atalho global | sim | falta |
| Tour guiado, tutorial, baixar pacotes prontos | sim | falta |
| Abas de web, captura de áudio de outros programas | sim | só no Windows |

Ver também `docs/PORTABILIDADE.md` (a ordem planejada).
