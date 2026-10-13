# Apolo

**Apolo** é um player de áudio para mesas de RPG (*pen & paper*): ele toca a música de fundo, os efeitos sonoros e as ambiências da sua sessão, tudo organizado em **pastas** e **playlists** que você controla com um clique, pelo teclado, pelo celular ou pelo Stream Deck.

O Apolo é uma modernização do **[Ares](http://aresrpg.sourceforge.net/)** (*Aural RPG Experience System*), criado por **Jörg Ruedenauer**. O Ares ficou abandonado por anos; o Apolo continua esse trabalho com uma interface nova e recursos pensados para quem mestra RPG online e presencial.

> *Apolo is a modernization of Ares, an audio player for tabletop RPG sessions. See the end of this file for an English summary.*

## Créditos

- **Apolo** — criado e mantido por **Igor Teuri**, do canal **Dados Críticos** ([YouTube](https://youtube.com/dadoscriticos?sub_confirmation=1) · [itch.io](https://igorteuri.itch.io/)).
- **Ares** — o programa original é de **Jörg Ruedenauer** e colaboradores. Todo o crédito pela base do programa (motor de áudio, projetos, Editor, Player e controladores) pertence a eles. O Apolo é um *fork* e mantém os nomes internos `Ares.*` no código. Os projetos agora são `.apolo` e os dados ficam em `%APPDATA%\Apolo`; projetos `.ares` e a pasta `%APPDATA%\Ares` antigos são copiados automaticamente na primeira abertura (os originais não são apagados).
- **Áudio:** motor de áudio próprio do Apolo, feito com [NAudio](https://github.com/naudio/NAudio), [NLayer](https://github.com/naudio/NLayer) e [NVorbis](https://github.com/NVorbis/NVorbis) (licença MIT), [Concentus](https://github.com/lostromb/concentus) (licença BSD) e [TagLib#](https://github.com/mono/taglib-sharp) (licença LGPL). Todas livres, inclusive para uso comercial.
- **Ícones:** [Lucide](https://lucide.dev/) (licença ISC).
- **Emojis dos botões:** [Noto Emoji](https://github.com/googlefonts/noto-emoji), do Google (licença Apache 2.0), com o [Twemoji](https://github.com/jdecked/twemoji) (CC-BY 4.0) como segunda fonte. Os desenhos são baixados na primeira vez que um emoji é escolhido.
- **Palavras dos emojis:** [Unicode CLDR](https://github.com/unicode-org/cldr-json) (nomes e palavras-chave em português e inglês, licença Unicode), baixadas uma vez para o reconhecimento automático e a busca de emojis.
- **Páginas web embutidas:** Microsoft WebView2.
- **Túnel do controle remoto:** [cloudflared](https://github.com/cloudflare/cloudflared), da Cloudflare.
- **Discord:** [discord.js/voice](https://github.com/discordjs/discord.js) e [@snazzah/davey](https://github.com/Snazzah/davey), rodando em um processo Node.js que acompanha o programa.
- Bibliotecas do projeto original: WeifenLuo DockPanel Suite, ServiceStack, RestSharp, Chromaprint, FFmpeg, HtmlRenderer, Mouse/Keyboard Hooks, jCIFS e outras, cada uma com a sua licença (veja os arquivos `*Licence*` na raiz).

## Licença

Assim como o Ares, o Apolo é distribuído sob a **GNU GPL (versão 2 ou posterior)**. Veja [LICENSE.txt](LICENSE.txt) e [license-gplv3.txt](license-gplv3.txt).

## O que o Apolo faz

- **Pastas e playlists.** As pastas (antes chamadas de "modos") guardam playlists de música ou de efeitos sonoros, e podem ter subpastas.
- **Importar do computador.** Escolha uma pasta do PC e o Apolo cria as pastas e playlists automaticamente. Também é possível arrastar pastas e arquivos para a janela, escolhendo o tipo de playlist, volume, fades e repetição.
- **Modo de edição dentro do Player.** Adicionar, renomear, reordenar e excluir (inclusive vários de uma vez com Ctrl/Shift), com imagens e ícones nos botões.
- **Lista de músicas** da playlist que está tocando, com busca e reordenação.
- **Abas no estilo Kenku FM.** A aba "Apolo" é a tela inicial; as outras são páginas web (YouTube, bancos de sons etc.) que continuam tocando em segundo plano, mais uma aba para enviar o áudio por **VDO.Ninja**.
- **Discord.** O Apolo pode entrar em um canal de voz e transmitir o que está tocando.
- **Controle remoto pelo navegador** (inclusive pelo celular, com túnel da Cloudflare) e **plugin para Stream Deck**.
- **Overlay para OBS** mostrando a música que está tocando.
- **Atalhos de teclado** configuráveis, para o programa e para cada pasta e playlist.
- **Localizador de arquivos ausentes**, que ajuda a reencontrar músicas movidas de lugar.
- **Renomeador de músicas** (menu Extras): limpa nomes de arquivo, busca título/artista/álbum na internet ([AcoustID](https://acoustid.org/) + [MusicBrainz](https://musicbrainz.org/)) e renomeia por um padrão que você escolhe, com prévia e desfazer.
- **Verificador de direitos autorais** (menu Extras): identifica a música e mostra um semáforo de risco para lives e vídeos. É só um indicativo, não uma garantia.
- **Música e efeitos livres** (menu Extras) e **imagens para os botões**: busca em bancos de licença livre ([Openverse](https://openverse.org/), Wikimedia Commons, Internet Archive e, opcionalmente, Freesound), sempre guardando autor e licença.
- **Aparência:** emoji (escolhido ou sugerido automaticamente pelo nome do botão) e cor de destaque para cada botão e cada pasta.
- **Aviso de atualização** pelos Releases do GitHub e **mostrar na pasta** para qualquer música da lista.
- Temas claro e escuro, ícone na bandeja do Windows, português e inglês.

## Como compilar

Veja [HowToBuild.md](HowToBuild.md) e [Modules.md](Modules.md). O repositório também tem um fluxo do GitHub Actions (`.github/workflows/build.yml`) que compila o Apolo no Windows e empacota o Discord e o plugin do Stream Deck.

## Roadmap

Veja [ROADMAP.md](ROADMAP.md).

---

## English summary

**Apolo** is an audio player for tabletop RPG sessions: background music, sound effects and ambience organized in folders and playlists, controlled by mouse, keyboard, phone, Stream Deck, or sent to Discord and VDO.Ninja. It is a modernization of **[Ares](http://aresrpg.sourceforge.net/)** by **Jörg Ruedenauer**, who deserves all credit for the foundation of the program. Apolo is created and maintained by **Igor Teuri** (Dados Críticos channel). Like Ares, it is released under the GNU GPL v2 or later. Audio is played by Apolo's own engine, built on NAudio, NLayer, NVorbis (MIT), Concentus (BSD) and TagLib# (LGPL), all free for commercial use. See "Créditos" above for the full list of third-party components.
