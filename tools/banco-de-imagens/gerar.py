"""Gera o banco de imagens do Apolo: um arquivo WebP (256x256) por cena e o índice de palavras.

Uso (precisa de Python 3 com Pillow e numpy):
    python gerar.py [caminho de saída]      (padrão: ../../BancoDeImagens)
    python gerar.py --folha                 (só uma folha de contato em PNG, para olhar o resultado)
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import cenas
from base import W


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    folha = '--folha' in sys.argv
    out = os.path.abspath(args[0]) if args else os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'BancoDeImagens'))
    ids = sorted(cenas.CENAS)
    if folha:
        cols = 8
        rows = (len(ids) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * 128, rows * 128), (0, 0, 0))
        for i, ident in enumerate(ids):
            try:
                im = cenas.render(ident).resize((128, 128), Image.LANCZOS)
            except Exception as ex:  # noqa
                print('ERRO', ident, ex)
                continue
            sheet.paste(im, ((i % cols) * 128, (i // cols) * 128))
        path = os.path.join(os.environ.get('TEMP', '.'), 'banco_folha.png')
        sheet.save(path)
        print(path, len(ids), 'cenas')
        return
    os.makedirs(out, exist_ok=True)
    total = 0
    lines = ['# Banco de imagens do Apolo: arquivo | palavras em português | palavras em inglês (geradas por tools/banco-de-imagens/gerar.py)']
    for ident in ids:
        im = cenas.render(ident)
        path = os.path.join(out, ident + '.webp')
        im.save(path, 'WEBP', quality=82, method=6)
        total += os.path.getsize(path)
        pt, en = cenas.PALAVRAS[ident]
        lines.append('%s|%s' % (ident, pt))
    with open(os.path.join(out, 'indice.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print(len(ids), 'imagens,', total // 1024, 'KB no total,', total // max(1, len(ids)), 'bytes por imagem em média ->', out)


if __name__ == '__main__':
    main()
