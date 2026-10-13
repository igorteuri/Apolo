"""Draws the icons of the Apolo Stream Deck plugin (run once; the PNGs are kept in the repository)."""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), '..', 'com.igorteuri.apolo.sdPlugin', 'assets')
ACCENT = (255, 182, 39, 255)
DIM = (150, 140, 120, 255)
BG = (34, 30, 25, 255)
WHITE = (255, 255, 255, 255)
S = 8  # supersampling


def glyph(name, d, size, color):
    """Draws a symbol in a square of `size` pixels."""
    u = size / 100.0
    P = lambda *pts: [(x * u, y * u) for x, y in zip(pts[0::2], pts[1::2])]
    if name == 'play':
        d.polygon(P(32, 20, 32, 80, 80, 50), fill=color)
    elif name == 'pause':
        d.rounded_rectangle([x * u for x in (26, 20, 43, 80)], radius=4 * u, fill=color)
        d.rounded_rectangle([x * u for x in (57, 20, 74, 80)], radius=4 * u, fill=color)
    elif name == 'stop':
        d.rounded_rectangle([x * u for x in (25, 25, 75, 75)], radius=7 * u, fill=color)
    elif name == 'prev':
        d.rectangle([x * u for x in (22, 22, 30, 78)], fill=color)
        d.polygon(P(78, 22, 78, 78, 34, 50), fill=color)
    elif name == 'next':
        d.rectangle([x * u for x in (70, 22, 78, 78)], fill=color)
        d.polygon(P(22, 22, 22, 78, 66, 50), fill=color)
    elif name == 'repeat':
        w = int(7 * u)
        d.arc([x * u for x in (20, 28, 80, 72)], 200, 340, fill=color, width=w)
        d.arc([x * u for x in (20, 28, 80, 72)], 20, 160, fill=color, width=w)
        d.polygon(P(76, 22, 92, 40, 66, 44), fill=color)
        d.polygon(P(24, 78, 8, 60, 34, 56), fill=color)
    elif name == 'volume':
        d.polygon(P(14, 38, 30, 38, 52, 18, 52, 82, 30, 62, 14, 62), fill=color)
        w = int(6 * u)
        d.arc([x * u for x in (50, 32, 72, 68)], -50, 50, fill=color, width=w)
        d.arc([x * u for x in (50, 20, 86, 80)], -50, 50, fill=color, width=w)
    elif name == 'mode':
        for (x, y) in ((22, 22), (54, 22), (22, 54), (54, 54)):
            d.rounded_rectangle([x * u, y * u, (x + 24) * u, (y + 24) * u], radius=5 * u, fill=color)
    elif name == 'element':
        d.ellipse([x * u for x in (22, 56, 48, 82)], fill=color)
        d.rectangle([x * u for x in (44, 20, 51, 70)], fill=color)
        d.polygon(P(44, 20, 51, 20, 76, 34, 76, 48, 51, 36), fill=color)


def make(name, size, kind, color=None):
    """kind: 'state' (dark rounded tile + coloured symbol), 'white' (monochrome on transparent)"""
    big = size * S
    img = Image.new('RGBA', (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if kind == 'state':
        d.rounded_rectangle([0, 0, big - 1, big - 1], radius=big * 0.14, fill=BG)
        glyph(name.split('-')[0], d, big, color or ACCENT)
    else:
        glyph(name, d, big, WHITE)
    return img.resize((size, size), Image.LANCZOS)


def save(base, name, size, kind, color=None):
    make(name, size, kind, color).save(os.path.join(OUT, base + '.png'))
    make(name, size * 2, kind, color).save(os.path.join(OUT, base + '@2x.png'))


os.makedirs(OUT, exist_ok=True)
# action list icons (white, 20 px)
for n in ('stop', 'prev', 'next', 'repeat', 'volume', 'mode', 'element'):
    save(n, n, 20, 'white')
save('playpause', 'play', 20, 'white')
# key images (72 px)
save('play', 'play', 72, 'state')
save('pause', 'pause', 72, 'state')
save('stop', 'stop', 72, 'state')
save('prev', 'prev', 72, 'state')
save('next', 'next', 72, 'state')
save('repeat-off', 'repeat', 72, 'state', DIM)
save('repeat-on', 'repeat', 72, 'state', ACCENT)
save('volume-key', 'volume', 72, 'state')
save('volume-dial', 'volume', 72, 'white')
save('mode-off', 'mode', 72, 'state', DIM)
save('mode-on', 'mode', 72, 'state', ACCENT)
save('element-off', 'element', 72, 'state', DIM)
save('element-on', 'element', 72, 'state', ACCENT)
# category icon and plugin icon (the Apolo logo)
logo = Image.open(os.path.join(os.path.dirname(__file__), '..', '..', 'Branding', 'apolo-256.png')).convert('RGBA')
logo.resize((28, 28), Image.LANCZOS).save(os.path.join(OUT, 'category-icon.png'))
logo.resize((56, 56), Image.LANCZOS).save(os.path.join(OUT, 'category-icon@2x.png'))
logo.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, 'plugin-icon.png'))
logo.resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, 'plugin-icon@2x.png'))
print(len(os.listdir(OUT)), 'files')
