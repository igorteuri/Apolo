# Banco de imagens

Gera as imagens WebP (256x256) que o Apolo oferece para os botões, em `BancoDeImagens/` na raiz do repositório.
São ilustrações feitas por código (nenhuma imagem de terceiros).

Precisa de Python 3 com Pillow e numpy:

    pip install pillow numpy
    python tools/banco-de-imagens/gerar.py            # gera BancoDeImagens/*.webp e indice.txt
    python tools/banco-de-imagens/gerar.py --folha    # só uma folha de contato PNG para olhar o resultado

- `cenas.py`: uma função por imagem; o decorador `@cena('id', 'palavras pt,palavras en')` define o nome do arquivo e as
  palavras que fazem o Apolo escolher a imagem (o programa casa o nome do botão com essas palavras, sem acento).
- `subjects.py`, `criaturas.py`, `objetos.py`: os elementos desenhados (castelo, dragão, espada...).
- `base.py`: a base de desenho (degradês, textura de pintura, acabamento).

Para acrescentar uma imagem: escreva uma cena nova em `cenas.py`, rode `gerar.py` e envie o resultado.
O build copia `BancoDeImagens/` para a pasta do programa.
