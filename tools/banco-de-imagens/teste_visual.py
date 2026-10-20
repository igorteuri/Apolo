"""Gera uma folha de contato com algumas cenas, para avaliar o estilo antes de gerar o banco inteiro."""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from base import Canvas, rgb, W
import subjects as sj
import cenas


def main():
    names = sys.argv[1:] or ['carvalho', 'piramide', 'castelo', 'ponte', 'caveira', 'gema', 'taverna', 'dragao']
    sheet = Image.new('RGB', (W * 4, W * ((len(names) + 3) // 4)), (0, 0, 0))
    for i, n in enumerate(names):
        im = cenas.render(n)
        sheet.paste(im, ((i % 4) * W, (i // 4) * W))
    out = os.path.join(os.environ.get('TEMP', '.'), 'banco_teste.png')
    sheet.save(out)
    print(out)


if __name__ == '__main__':
    main()
