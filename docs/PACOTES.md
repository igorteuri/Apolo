# Pacotes de conteúdo do Apolo

Um **pacote** é um projeto pronto (pastas, botões, cores, imagens e músicas) guardado num arquivo `.apkg`.
Ele **não** vai dentro do instalador: fica numa *release* do GitHub e o Apolo baixa quando o usuário pede
(menu **Projeto > Pacotes de conteúdo prontos**, e uma oferta única na primeira abertura).

## O que o pacote leva

- Projeto: pastas, botões, playlists, volumes, fades, tipo (música/efeito).
- Subpastas (aninhação das pastas), imagens, emojis e cores dos botões.
- Os arquivos de áudio.
- Créditos (`creditos.txt`): o texto que você escreveu + a lista dos sons com o que está gravado nas tags.

## Passo a passo para criar um pacote

1. **Junte as músicas numa pasta própria dentro da sua pasta de Músicas do Apolo.**
   Ex.: `Músicas\Apolo - Pacote inicial\Kevin MacLeod\...`. O pacote guarda o caminho a partir da pasta de músicas,
   então uma pasta com nome único evita misturar com as músicas de quem baixar.
2. **Monte tudo no Apolo**: projeto novo, pastas, arraste as músicas, ponha imagens, emojis e cores.
3. **Escreva os créditos**: menu **Projeto > Créditos do projeto... > Escrever créditos**. Cole o texto de cada artista
   (modelos abaixo). Salve e confira no Apolo.
4. **Exporte**: **Projeto > Exportar... > Projeto completo, com as músicas e efeitos**. Gera o `.apkg`.
5. **Teste**: abra o `.apkg` em outra conta do Windows (ou apague o projeto e as músicas) e veja se tudo chega certo.
6. **Gere o bloco do catálogo**: no PowerShell, dentro de `tools\pacotes`:
   `.\gerar-entrada.ps1 -Arquivo "C:\caminho\pacote-inicial.apkg" -Titulo "Pacote inicial" -Descricao "Pastas e botões prontos"`
   Copie o bloco que aparece.
7. **Monte o `pacotes.ini`** (um bloco `[nome]` por pacote; linhas começando com `#` são comentários). Exemplo:

```
[pacote-inicial]
titulo = Pacote inicial
descricao = Pastas e botões prontos para começar.\nMúsicas livres de Kevin MacLeod e Vindsvept (veja os créditos).
arquivo = pacote-inicial.apkg
bytes = 123456789
sha256 = 0123abcd...
```

   `\n` na descrição vira quebra de linha. `arquivo` é só o nome, e o `.apkg` precisa estar na mesma release.
8. **Publique no GitHub** (site do repositório > **Releases** > **Draft a new release**):
   - **Tag**: `pacotes` (exatamente isso; é o endereço que o Apolo usa). Título: "Pacotes de conteúdo".
   - Arraste para a caixa de anexos o `pacotes.ini` e o(s) `.apkg` (até 2 GB cada).
   - **Publish release**.
9. **Para atualizar depois**: abra a release, **Edit**, apague o arquivo antigo, anexe o novo (e o `pacotes.ini`
   com `bytes`/`sha256` novos) e salve. Não precisa lançar versão nova do Apolo.

Endereço que o Apolo lê: `https://github.com/igorteuri/Apolo/releases/download/pacotes/pacotes.ini`

Se o tamanho ou o `sha256` do `.apkg` não baterem com o `pacotes.ini`, o Apolo recusa o arquivo (proteção contra
download quebrado). Sempre refaça o passo 6 depois de trocar o `.apkg`.

## Licenças das músicas (conferido em 21/10/2026 nas páginas oficiais)

| Artista | Licença | Pode ir no pacote? |
|---|---|---|
| Kevin MacLeod (incompetech.com) | CC BY 4.0 | Sim, com crédito. A página não fala de pacotes de download; a CC BY permite redistribuir. Um e-mail avisando é boa educação. |
| Vindsvept (vindsvept.se) | CC BY 4.0 (não vale para covers) | Sim, com crédito. A página dele não fala de pacotes de download. Para *jogos* ele pede aviso prévio (não é o caso do Apolo). |
| Tabletop Audio (tabletopaudio.com) | CC BY-NC-ND 4.0 | **Só se o Apolo e o pacote forem 100% gratuitos** e **sem alterar os arquivos** (nem as tags). O "SoundPad" não pode ser usado fora do site. O mais seguro é pedir permissão por e-mail ao autor. |

Modelos de crédito (cole em *Escrever créditos*):

```
Kevin MacLeod (incompetech.com)
Músicas licenciadas sob Creative Commons: By Attribution 4.0
https://creativecommons.org/licenses/by/4.0/

Vindsvept (vindsvept.se)
Músicas licenciadas sob CC BY 4.0
https://creativecommons.org/licenses/by/4.0/

Tabletop Audio (tabletopaudio.com)
Licenciadas sob CC BY-NC-ND 4.0 (uso não comercial, sem alterações)
https://creativecommons.org/licenses/by-nc-nd/4.0/
```

Quem usar a música em vídeo/live precisa creditar de novo no próprio vídeo (descrição do YouTube, por exemplo);
o texto de créditos do Apolo ajuda, mas não substitui isso.
