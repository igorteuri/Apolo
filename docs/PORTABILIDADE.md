# Apolo para Mac e Linux — estado do código e plano

Documento de preparação (23/10/2026). Resume o que já é portátil, o que é só de Windows e em que ordem atacar.

## 1. Como o código está dividido hoje

| Projeto (pasta) | O que é | Linhas | Roda em Mac/Linux? |
|---|---|---:|---|
| `Ares.Data` | modelo do projeto (pastas, botões, playlists), leitura/gravação do `.apolo` | 6,2 mil | **Sim** (`net8.0`) |
| `Ares.Settings` | configurações, pastas do Apolo, projetos recentes | 1,7 mil | **Sim** |
| `Ares.Tags` | banco de tags/músicas (SQLite), leitura de tags (TagLib) | 6,5 mil | Quase: o SQLite é o pacote para Windows |
| `Ares.ModelChecks` (`Ares.ModelInfo`) | verificações, importar/exportar `.apkg` | 2,5 mil | **Sim** |
| `Ares.Playing` | motor de áudio (mixagem, nivelador, decodificação MP3/Ogg/Opus/WAV) | 5,7 mil | Quase: a saída de som é WASAPI |
| `Ares.Players` | lógica de tocar playlists, volumes, fades | 3 mil | **Sim** |
| `Ares.Ipc` | instância única do programa (memória compartilhada, mutex) | 2,1 mil | Não: usa `kernel32` |
| `Ares.TagsImport` | consulta de músicas para a checagem de direitos | 0,9 mil | Não: usa `chromaprint.dll` |
| `Ares.CommonGUI` | tema, diálogos, ícones, importação por arrastar | 6,8 mil | **Não** (WinForms) |
| `Ares.Player` | a janela principal e tudo o que aparece nela | 29,8 mil | **Não** (WinForms) |
| `Ares.WinSecurity` | ajudante do Firewall do Windows (.NET Framework 4.8) | 0,5 mil | Não, e nem precisa |

**Resultado do teste:** os 7 primeiros projetos foram compilados como `net8.0` puro (sem `-windows`) e compilaram sem nenhum erro. Ou seja, o "cérebro" do Apolo (cerca de 25 mil linhas) já é portátil na compilação. O que falta é o que ele chama no sistema e, principalmente, a **interface**, que é WinForms.

Desde a reorganização de 23/10/2026 os projetos estão assim: bibliotecas portáteis com `TargetFramework = net8.0` e só `Ares.CommonGUI` e `Ares.Player` com `net8.0-windows`. Isso impede, na compilação, que alguém use WinForms por engano numa parte portátil.

## 2. O que é só de Windows hoje

### Motor de áudio (`Ares.Playing`)
- **Saída de som:** `AudioEngine.cs` usa WASAPI (NAudio) por trás de uma classe `Sink` (já existe a separação), e lista as placas de som com `MMDeviceEnumerator`. Falta um segundo `Sink` e uma listagem de dispositivos para Mac/Linux.
- **Decodificação:** MP3 (NLayer), Ogg (NVorbis) e Opus (Concentus) são código .NET puro e **funcionam em qualquer sistema**. WAV e "tudo que o Media Foundation lê" (AAC, M4A, WMA) usam o decodificador do Windows: no Mac/Linux esses formatos precisam de outra biblioteca (ver §4).
- **Captura de áudio:** capturar o som de um aplicativo (`AppAudioCapture`, `ProcessLoopbackCapture`) e o microfone virtual (`VirtualMic`) são recursos do Windows 10/11. No Mac/Linux ficam desligados no começo.

### Interface (`Ares.Player`, `Ares.CommonGUI`)
- Toda a janela é WinForms, desenhada à mão em boa parte (botões de playlist, barras, abas, tema). Não existe WinForms no Mac/Linux.
- Chamadas diretas ao Windows: `user32` (janela, atalho global do pad, mover janela sem foco), `dwmapi` e `uxtheme` (barra de título escura), `shell32`, `winmm`, `avrt`.
- **Atalhos globais:** `RegisterHotKey` (pad) e o gancho de teclado (`MouseKeyboardActivityMonitor`). No Mac/Linux são outras APIs, e no Wayland não existe atalho global.
- **Bandeja do sistema:** `NotifyIcon` (existe em Avalonia, mas com diferenças).
- **Abas de web, VDO.Ninja e envio de áudio:** usam **WebView2**, que é do Windows. No Mac/Linux precisa de outro navegador embutido.
- Fonte de símbolos `Segoe UI Symbol` usada em alguns botões (vários símbolos precisam ser trocados por ícones).

### Resto
- **Ajudante de firewall** (`Ares.WinSecurity`) e o **instalador** (Inno Setup): só existem no Windows. No Mac/Linux o controle remoto não pede essa liberação.
- **Banco SQLite:** o pacote `System.Data.SQLite.Core` traz o binário só do Windows. Trocar por `Microsoft.Data.Sqlite` resolve nos três sistemas.
- **SkiaSharp:** só o pacote `NativeAssets.Win32` está referenciado; para Mac/Linux entram `NativeAssets.macOS` e `NativeAssets.Linux`.
- **Checagem de direitos (chromaprint + ffmpeg antigo):** as DLLs de 2013 (`avcodec-54`) só existem para Windows. No Mac/Linux, usar a biblioteca `libchromaprint` do sistema.
- **Túnel do controle remoto:** o `cloudflared` tem versão para Mac e Linux; basta baixar o binário certo.
- **Ponte do Discord:** é Node.js, funciona em qualquer sistema.
- **Controle remoto web, módulo do Foundry, plugin do Stream Deck:** o servidor do controle remoto usa `HttpListener` (serve nos três sistemas) e o resto é web; **já funciona** em Mac/Linux assim que o motor tocar som.

## 3. O que já está pronto para ser reaproveitado
- A separação **motor × interface** já existe: o motor toca e a janela só pede. Isso torna possível trocar só a interface.
- O **controle remoto web** (celular, Stream Deck, Foundry) é uma interface completa que não depende do Windows.
- Muita lógica de tela já foi isolada em arquivos sem WinForms e coberta por testes (`PackCatalog`, `ProjectCredits`, `DropPlan`, `TrackText`, `JsonText`, `LevelCompressor`, `ImageBankIndex`, `ImageUrlParser`). São candidatos a ir direto para um projeto portátil.

## 4. Decisões que precisam ser tomadas

### 4.1 A interface (a maior decisão)
| Caminho | Prós | Contras |
|---|---|---|
| **Avalonia UI** (C#/.NET 8, Windows, Mac e Linux) | uma só base de código, mesma linguagem, visual sob controle total | reescrever as ~30 mil linhas de interface, tela por tela |
| Só interface web (o motor vira um "servidor" local e o navegador é a janela) | rápido de ter funcionando em Mac/Linux; já temos o controle remoto | perde o modo de edição rico, arrastar-e-soltar do Explorer, pad por cima da tela, bandeja |
| Manter WinForms só no Windows e fazer uma segunda interface mais simples nos outros | menos risco | duas interfaces para manter |

**Recomendação:** Avalonia, em etapas. Antes da reescrita, tirar a lógica de dentro das janelas (hoje há muita regra misturada em `Player.*.cs`) e passá-la para projetos portáteis. Assim a nova interface vira "só tela".

### 4.2 A saída de áudio
Para Mac/Linux é preciso um motor de saída nativo. Opções:
- **miniaudio** (um arquivo C, domínio público; fala com CoreAudio, PulseAudio/PipeWire e ALSA; existem ligações para .NET): é a escolha mais simples, e também serviria no Windows.
- OpenAL Soft (via Silk.NET), PortAudio ou SDL2: funcionam, mas têm mais peças.

O desenho é o mesmo do `WasapiSink`: o motor entrega blocos de amostras em `float`, a saída só as consome. Precisa também de uma interface para "listar placas de som" que hoje é só WASAPI.

### 4.3 Formatos de áudio sem Media Foundation
AAC/M4A/WMA/ALAC não têm decodificador .NET puro. Opções: ffmpeg do sistema (via processo ou biblioteca) ou aceitar, no começo, só MP3/Ogg/Opus/WAV/FLAC (FLAC já é possível com biblioteca .NET).

### 4.4 Distribuição
- **Mac:** pacote `.app` assinado e notarizado (exige conta de desenvolvedor da Apple) em `.dmg`.
- **Linux:** AppImage ou Flatpak, e talvez `.deb`.
- **CI:** um workflow por sistema (`windows-latest`, `macos-latest`, `ubuntu-latest`).

## 5. Ordem sugerida
1. **Já feito:** projetos portáteis separados dos de Windows; limpeza do código morto; projetos simples (sem lista de arquivos).
2. Trocar `System.Data.SQLite.Core` por `Microsoft.Data.Sqlite`; trocar `chromaprint` por chamada que funcione nos três sistemas; adicionar os binários do SkiaSharp.
3. Criar `Ares.Audio.Abstractions` (saída de som e lista de dispositivos como interfaces) e deixar o WASAPI como a implementação do Windows.
4. Implementar a saída com miniaudio e testar o motor **sem interface**, por um programa de linha de comando que toca um projeto `.apolo`. É o primeiro marco: **o Apolo tocando som no Mac/Linux**.
5. Extrair a lógica de dentro de `Ares.Player` (regras de importação, pacotes, edição, desfazer, atalhos, créditos) para um projeto portátil com testes.
6. Interface Avalonia: começar pela tela principal (pastas + botões + transporte), depois lista de faixas, edição, pad flutuante, configurações.
7. Itens sem equivalente (captura de app, microfone virtual, WebView2, atalho global no Wayland): decidir um a um se serão cortados, adaptados ou adiados.
8. Empacotar e criar os fluxos de CI para Mac e Linux.

## 6. Riscos
- **Perda de recursos no começo:** captura de aplicativos e microfone virtual não terão versão Mac/Linux logo de saída.
- **Teste:** só é possível conferir o Mac/Linux com máquinas (ou CI) desses sistemas; a conferência do som precisa de gente ouvindo.
- **Mac:** assinatura e notarização têm custo e burocracia.
- **Tamanho do trabalho:** a interface é de longe a maior parte (§1). Vale decidir §4.1 antes de começar.
