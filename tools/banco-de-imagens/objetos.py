"""Objetos, instrumentos, veículos e símbolos do banco de imagens. (x, y) = chão no centro; s = escala."""
import math

from base import blob, dark, light, mix, rgb, smooth
import subjects as sj
from subjects import GOLD, STEEL, STONE, WOOD, RED, P


def anchor(c, x, y, s=1.0):
    col = (150, 160, 176)
    c.line([(x, y - 118 * s), (x, y - 10 * s)], dark(col, 0.2), 6 * s)
    c.line([(x, y - 118 * s), (x, y - 10 * s)], col, 4 * s)
    c.ellipse(x, y - 126 * s, 9 * s, 9 * s, col, dark(col, 0.3))
    c.ellipse(x, y - 126 * s, 4 * s, 4 * s, (30, 40, 56))
    c.line([(x - 22 * s, y - 100 * s), (x + 22 * s, y - 100 * s)], col, 5 * s)
    c.curve([(x - 52 * s, y - 40 * s), (x - 40 * s, y - 8 * s), (x, y + 2 * s), (x + 40 * s, y - 8 * s), (x + 52 * s, y - 40 * s)], col, 6 * s)
    c.poly(P(x, y, s, [(-62, -46), (-44, -38), (-54, -28)]), col, dark(col, 0.3))
    c.poly(P(x, y, s, [(62, -46), (44, -38), (54, -28)]), col, dark(col, 0.3))


def helm_wheel(c, x, y, s=1.0):
    for i in range(8):
        a = math.radians(i * 45)
        c.line([(x, y - 60 * s), (x + math.cos(a) * 52 * s, y - 60 * s + math.sin(a) * 52 * s)], WOOD[1], 4.4 * s)
        c.ellipse(x + math.cos(a) * 54 * s, y - 60 * s + math.sin(a) * 54 * s, 4.6 * s, 4.6 * s, WOOD[2], WOOD[0])
    c.ellipse(x, y - 60 * s, 40 * s, 40 * s, WOOD[2], WOOD[0], alpha=0.0)
    c.curve([(x + math.cos(math.radians(a)) * 36 * s, y - 60 * s + math.sin(math.radians(a)) * 36 * s) for a in range(0, 361, 30)], WOOD[2], 5.4 * s)
    c.ellipse(x, y - 60 * s, 10 * s, 10 * s, GOLD[2], GOLD[0])


def flag_pirate(c, x, y, s=1.0):
    c.line([(x - 40 * s, y), (x - 40 * s, y - 140 * s)], WOOD[1], 3.6 * s)
    pts = [(x - 40 * s + i * 8 * s, y - 130 * s + math.sin(i * 0.9) * 5 * s) for i in range(0, 13)]
    c.poly(pts + [(px, py + 70 * s) for px, py in reversed(pts)], (30, 30, 40), (10, 10, 16))
    sj.skull(c, x + 14 * s, y - 70 * s, s * 0.7, (240, 240, 232))
    for ang in (30, -30):
        a = math.radians(ang)
        c.line([(x + 14 * s - math.cos(a) * 30 * s, y - 40 * s + math.sin(a) * 18 * s), (x + 14 * s + math.cos(a) * 30 * s, y - 40 * s - math.sin(a) * 18 * s)], (240, 240, 232), 3.0 * s)


def amphora(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-18, -112), (18, -112), (14, -96), (34, -68), (36, -34), (22, -6), (10, 0), (-10, 0), (-22, -6), (-36, -34), (-34, -68), (-14, -96)]), (204, 106, 60), (120, 54, 30), vertical=False)
    c.poly(P(x, y, s, [(-30, -62), (30, -62), (32, -52), (-32, -52)]), (30, 20, 20), None, alpha=0.7)
    c.poly(P(x, y, s, [(-34, -40), (34, -40), (34, -34), (-34, -34)]), (30, 20, 20), None, alpha=0.7)
    c.curve([(x - 34 * s, y - 84 * s), (x - 52 * s, y - 70 * s), (x - 40 * s, y - 56 * s)], (150, 76, 40), 4 * s)
    c.curve([(x + 34 * s, y - 84 * s), (x + 52 * s, y - 70 * s), (x + 40 * s, y - 56 * s)], (150, 76, 40), 4 * s)


def column(c, x, y, s=1.0, broken=False):
    h = 70 if broken else 110
    c.poly(P(x, y, s, [(-16, 0), (-16, -8), (-12, -8), (-12, -h), (12, -h), (12, -8), (16, -8), (16, 0)]), STONE[3], STONE[1], vertical=False)
    for dx in (-6, 0, 6):
        c.line([(x + dx * s, y - 10 * s), (x + dx * s, y - (h - 2) * s)], STONE[1], 0.9, alpha=0.5)
    if not broken:
        c.rect(x - 18 * s, y - 118 * s, x + 18 * s, y - 110 * s, STONE[3], STONE[2])
    else:
        c.poly(P(x, y, s, [(-12, -h), (-4, -h - 8), (4, -h - 2), (12, -h - 10), (12, -h)]), STONE[3], STONE[2])


def statue(c, x, y, s=1.0):
    c.rect(x - 24 * s, y - 24 * s, x + 24 * s, y, STONE[3], STONE[1])
    c.poly(P(x, y, s, [(-16, -24), (-10, -86), (10, -86), (16, -24)]), STONE[3], STONE[1], vertical=False)
    c.ellipse(x, y - 98 * s, 11 * s, 13 * s, STONE[3], STONE[1])
    c.poly(P(x, y, s, [(-10, -86), (-34, -70), (-30, -64), (-8, -74)]), STONE[3], STONE[1])
    sj.sword(c, x + 26 * s, y - 28 * s, s * 0.8, 8, STONE)


def trumpet(c, x, y, s=1.0):
    c.line([(x - 70 * s, y - 50 * s), (x + 20 * s, y - 50 * s)], GOLD[2], 6 * s)
    c.curve([(x + 20 * s, y - 50 * s), (x + 40 * s, y - 50 * s), (x + 40 * s, y - 24 * s), (x - 40 * s, y - 24 * s), (x - 40 * s, y - 38 * s), (x + 20 * s, y - 38 * s)], GOLD[1], 5 * s)
    c.poly(P(x, y, s, [(20, -50), (72, -86), (72, -14), (20, -50)]), GOLD[3], GOLD[0])
    c.ellipse(x - 72 * s, y - 50 * s, 5 * s, 6 * s, GOLD[3], GOLD[1])
    for dx in (-26, -10, 6):
        c.rect(x + dx * s - 3 * s, y - 68 * s, x + dx * s + 3 * s, y - 52 * s, GOLD[3], GOLD[1])


def guitar(c, x, y, s=1.0, color=(190, 60, 40)):
    c.line([(x, y - 90 * s), (x, y - 170 * s)], WOOD[1], 8 * s)
    c.poly(P(x, y, s, [(-8, -174), (8, -174), (8, -160), (-8, -160)]), WOOD[0], None)
    c.poly(blob(x, y - 30 * s, 36 * s, 30 * s, c.rng, 0.02, 14), light(color, 0.2), dark(color, 0.4))
    c.poly(blob(x, y - 66 * s, 26 * s, 24 * s, c.rng, 0.02, 14), light(color, 0.2), dark(color, 0.4))
    c.ellipse(x, y - 46 * s, 10 * s, 10 * s, (24, 18, 20))
    for dx in (-3, -1, 1, 3):
        c.line([(x + dx * s, y - 160 * s), (x + dx * s, y - 22 * s)], (240, 240, 230), 0.5)


def violin(c, x, y, s=1.0):
    col = (170, 90, 40)
    c.line([(x, y - 76 * s), (x, y - 150 * s)], (60, 36, 24), 6 * s)
    c.ellipse(x, y - 156 * s, 6 * s, 6 * s, col, dark(col, 0.4))
    c.poly(blob(x, y - 34 * s, 26 * s, 26 * s, c.rng, 0.02, 14), light(col, 0.2), dark(col, 0.4))
    c.poly(blob(x, y - 66 * s, 20 * s, 18 * s, c.rng, 0.02, 14), light(col, 0.2), dark(col, 0.4))
    c.line([(x - 6 * s, y - 52 * s), (x - 6 * s, y - 40 * s)], (30, 20, 16), 2.0)
    c.line([(x + 6 * s, y - 52 * s), (x + 6 * s, y - 40 * s)], (30, 20, 16), 2.0)
    c.line([(x + 50 * s, y - 20 * s), (x - 60 * s, y - 100 * s)], (190, 140, 70), 2.6 * s)
    c.line([(x + 50 * s, y - 20 * s), (x - 60 * s, y - 100 * s)], (240, 236, 220), 0.8)


def drum(c, x, y, s=1.0):
    c.rect(x - 48 * s, y - 70 * s, x + 48 * s, y - 14 * s, (180, 70, 44), (110, 34, 24), vertical=False)
    c.ellipse(x, y - 14 * s, 48 * s, 12 * s, (150, 54, 36), (110, 34, 24))
    c.ellipse(x, y - 70 * s, 48 * s, 12 * s, (246, 232, 200), (214, 196, 160))
    for i in range(-3, 4):
        c.line([(x + i * 12 * s, y - 62 * s), (x + (i + 0.5) * 12 * s, y - 20 * s)], (230, 210, 170), 1.2, alpha=0.7)
    for ang, dx in ((-30, -50), (30, 50)):
        a = math.radians(ang)
        c.line([(x + dx * s, y - 120 * s), (x + (dx + math.sin(a) * -30) * s, y - 76 * s)], (210, 170, 110), 3.4 * s)


def piano(c, x, y, s=1.0):
    c.rect(x - 70 * s, y - 56 * s, x + 70 * s, y - 20 * s, (28, 24, 30), (50, 44, 52))
    c.rect(x - 70 * s, y - 40 * s, x + 70 * s, y, (248, 246, 240), (206, 202, 194))
    for i in range(-6, 7):
        c.line([(x + i * 10 * s + 5 * s, y - 40 * s), (x + i * 10 * s + 5 * s, y)], (150, 146, 140), 0.8)
    for i in range(-6, 7):
        if i % 7 not in (2, 6, -1, -5):
            c.rect(x + i * 10 * s + 1 * s, y - 40 * s, x + i * 10 * s + 9 * s, y - 16 * s, (24, 22, 28), (60, 56, 64))
    c.rect(x - 66 * s, y - 70 * s, x - 6 * s, y - 56 * s, (34, 28, 36), (60, 50, 62))


def note(c, x, y, s=1.0, color=(250, 230, 160)):
    c.glow(x, y - 50 * s, 50 * s, (255, 200, 100), alpha=0.35)
    c.ellipse(x - 14 * s, y - 14 * s, 15 * s, 11 * s, light(color, 0.2), dark(color, 0.3))
    c.rect(x - 1 * s, y - 100 * s, x + 4 * s, y - 14 * s, color, dark(color, 0.2))
    c.poly(P(x, y, s, [(4, -100), (34, -86), (34, -68), (4, -82)]), color, dark(color, 0.2))


def speaker(c, x, y, s=1.0):
    c.rect(x - 38 * s, y - 100 * s, x + 38 * s, y, (40, 40, 52), (18, 18, 26), radius=6)
    c.ellipse(x, y - 70 * s, 16 * s, 16 * s, (80, 84, 100), (40, 44, 58))
    c.ellipse(x, y - 70 * s, 6 * s, 6 * s, (150, 156, 176))
    c.ellipse(x, y - 28 * s, 22 * s, 22 * s, (80, 84, 100), (40, 44, 58))
    c.ellipse(x, y - 28 * s, 9 * s, 9 * s, (150, 156, 176))
    for r in (46, 62, 78):
        c.curve([(x + r * s * math.cos(math.radians(a)) + 40 * s, y - 52 * s + r * s * 0.8 * math.sin(math.radians(a))) for a in range(-50, 51, 20)], (150, 220, 255), 2.2 * s, alpha=0.7)


def masks(c, x, y, s=1.0):
    c.poly(blob(x - 26 * s, y - 56 * s, 34 * s, 38 * s, c.rng, 0.02, 14), (250, 230, 160), (210, 170, 80))
    c.poly(blob(x + 28 * s, y - 50 * s, 34 * s, 38 * s, c.rng, 0.02, 14), (110, 130, 210), (50, 60, 130))
    c.poly(P(x, y, s, [(-44, -70), (-30, -66), (-38, -62)]), (40, 30, 30))
    c.poly(P(x, y, s, [(-20, -70), (-8, -74), (-14, -62)]), (40, 30, 30))
    c.curve([(x - 40 * s, y - 40 * s), (x - 26 * s, y - 28 * s), (x - 12 * s, y - 40 * s)], (40, 30, 30), 2.4 * s)
    c.poly(P(x, y, s, [(14, -62), (26, -56), (18, -54)]), (20, 20, 40))
    c.poly(P(x, y, s, [(40, -62), (50, -66), (46, -54)]), (20, 20, 40))
    c.curve([(x + 14 * s, y - 28 * s), (x + 28 * s, y - 40 * s), (x + 42 * s, y - 28 * s)], (20, 20, 40), 2.4 * s)


def clapper(c, x, y, s=1.0):
    c.rect(x - 56 * s, y - 80 * s, x + 56 * s, y, (34, 34, 42), (14, 14, 22), radius=4)
    c.poly(P(x, y, s, [(-58, -110), (58, -130), (62, -100), (-54, -82)]), (230, 230, 236), (150, 150, 160))
    for i in range(6):
        c.poly(P(x, y, s, [(-58 + i * 20, -110 - i * 3.3), (-44 + i * 20, -112 - i * 3.3), (-36 + i * 20, -92 - i * 3), (-50 + i * 20, -90 - i * 3)]), (30, 30, 36))
    c.rect(x - 56 * s, y - 84 * s, x + 56 * s, y - 70 * s, (230, 230, 236), (170, 170, 180))
    c.line([(x - 40 * s, y - 40 * s), (x + 40 * s, y - 40 * s)], (200, 200, 210), 1.2, alpha=0.6)


def bed(c, x, y, s=1.0):
    c.rect(x - 70 * s, y - 40 * s, x + 70 * s, y - 10 * s, (110, 70, 140), (70, 40, 100), radius=4)
    c.rect(x - 70 * s, y - 56 * s, x - 56 * s, y, WOOD[2], WOOD[0])
    c.rect(x + 56 * s, y - 44 * s, x + 70 * s, y, WOOD[2], WOOD[0])
    c.rect(x - 64 * s, y - 52 * s, x - 22 * s, y - 38 * s, (250, 248, 240), (214, 210, 200), radius=6)
    c.rect(x - 70 * s, y - 14 * s, x + 70 * s, y - 6 * s, WOOD[2], WOOD[0])
    for i in range(3):
        c.line([(x + (30 + i * 12) * s, y - 70 * s - i * 10 * s), (x + (42 + i * 12) * s, y - 70 * s - i * 10 * s), (x + (30 + i * 12) * s, y - 62 * s - i * 10 * s), (x + (42 + i * 12) * s, y - 62 * s - i * 10 * s)], (230, 230, 255), 1.6, alpha=0.9)


def trophy(c, x, y, s=1.0):
    c.glow(x, y - 70 * s, 80 * s, (255, 220, 120), alpha=0.5)
    c.poly(P(x, y, s, [(-30, -120), (30, -120), (26, -70), (14, -48), (6, -40), (6, -24), (20, -20), (20, 0), (-20, 0), (-20, -20), (-6, -24), (-6, -40), (-14, -48), (-26, -70)]), GOLD[3], GOLD[0], vertical=False)
    c.curve([(x - 30 * s, y - 110 * s), (x - 52 * s, y - 100 * s), (x - 44 * s, y - 76 * s), (x - 24 * s, y - 70 * s)], GOLD[2], 4.4 * s)
    c.curve([(x + 30 * s, y - 110 * s), (x + 52 * s, y - 100 * s), (x + 44 * s, y - 76 * s), (x + 24 * s, y - 70 * s)], GOLD[2], 4.4 * s)
    c.poly(blob(x, y - 86 * s, 5 * s, 5 * s, c.rng, 0.0, 8), (255, 250, 220), None)
    c.rect(x - 24 * s, y - 8 * s, x + 24 * s, y, WOOD[2], WOOD[0])


def speech(c, x, y, s=1.0):
    c.poly(blob(x - 22 * s, y - 80 * s, 46 * s, 30 * s, c.rng, 0.02, 14) + [(x - 30 * s, y - 60 * s)], (250, 250, 255), (206, 214, 236))
    c.poly(P(x, y, s, [(-44, -64), (-54, -40), (-26, -58)]), (250, 250, 255), (206, 214, 236))
    for dx in (-42, -22, -2):
        c.ellipse(x + dx * s, y - 82 * s, 4.4 * s, 4.4 * s, (110, 130, 180))
    c.poly(blob(x + 34 * s, y - 40 * s, 40 * s, 26 * s, c.rng, 0.02, 14), (255, 220, 120), (230, 170, 60))
    c.poly(P(x, y, s, [(44, -26), (56, -2), (26, -22)]), (255, 220, 120), (230, 170, 60))
    for dy in (-48, -40, -32):
        c.line([(x + 16 * s, y + dy * s), (x + 50 * s, y + dy * s)], (150, 100, 40), 1.4, alpha=0.6)


def goblet(c, x, y, s=1.0, wine=(150, 20, 50)):
    c.poly(P(x, y, s, [(-30, -100), (30, -100), (28, -76), (14, -56), (4, -48), (4, -16), (22, -10), (22, 0), (-22, 0), (-22, -10), (-4, -16), (-4, -48), (-14, -56), (-28, -76)]), (230, 240, 250), (170, 190, 210), alpha=0.9)
    c.poly(P(x, y, s, [(-28, -84), (28, -84), (26, -76), (14, -58), (-14, -58), (-26, -76)]), light(wine, 0.15), dark(wine, 0.4))
    c.ellipse(x - 12 * s, y - 90 * s, 3.4 * s, 8 * s, (255, 255, 255), alpha=0.5, blur=1)


def meat(c, x, y, s=1.0):
    c.line([(x + 40 * s, y - 16 * s), (x + 70 * s, y - 36 * s)], (240, 232, 212), 7 * s)
    c.ellipse(x + 76 * s, y - 42 * s, 7 * s, 7 * s, (240, 232, 212), (196, 184, 160))
    c.ellipse(x + 70 * s, y - 30 * s, 7 * s, 7 * s, (240, 232, 212), (196, 184, 160))
    c.poly(blob(x, y - 34 * s, 54 * s, 36 * s, c.rng, 0.06, 14), (210, 120, 60), (120, 54, 24))
    c.poly(blob(x - 8 * s, y - 44 * s, 36 * s, 20 * s, c.rng, 0.06, 12), (236, 160, 80), (190, 100, 40), alpha=0.8)
    c.ellipse(x - 18 * s, y - 50 * s, 10 * s, 5 * s, (255, 220, 160), alpha=0.6, blur=1.4)


def bomb(c, x, y, s=1.0):
    c.ellipse(x, y - 44 * s, 38 * s, 38 * s, (74, 78, 96), (14, 14, 22))
    c.ellipse(x - 12 * s, y - 58 * s, 10 * s, 7 * s, (170, 176, 200), alpha=0.6, blur=2)
    c.rect(x - 10 * s, y - 92 * s, x + 10 * s, y - 80 * s, (60, 62, 76), (30, 30, 40))
    c.curve([(x, y - 92 * s), (x + 10 * s, y - 112 * s), (x + 28 * s, y - 108 * s)], (210, 190, 140), 2.4 * s)
    c.glow(x + 30 * s, y - 108 * s, 22 * s, (255, 200, 80), alpha=0.8)
    c.poly(P(x, y, s, [(24, -108), (30, -124), (36, -108), (44, -112), (34, -102), (38, -94), (28, -102), (22, -96), (26, -104)]), (255, 240, 150), (255, 150, 40))


def burst(c, x, y, s=1.0):
    pts = []
    for i in range(24):
        a = math.radians(i * 15)
        r = 74 if i % 2 == 0 else 38
        pts.append((x + math.cos(a) * r * s, y - 60 * s + math.sin(a) * r * s))
    c.glow(x, y - 60 * s, 90 * s, (255, 150, 50), alpha=0.5)
    c.poly(pts, (255, 236, 130), (240, 100, 40))
    pts2 = []
    for i in range(24):
        a = math.radians(i * 15 + 7)
        r = 44 if i % 2 == 0 else 24
        pts2.append((x + math.cos(a) * r * s, y - 60 * s + math.sin(a) * r * s))
    c.poly(pts2, (255, 252, 220), (255, 200, 90))


def pistol(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-60, -62), (30, -62), (30, -42), (14, -42), (14, -28), (22, 0), (4, 0), (-6, -28), (-6, -42), (-60, -42)]), (86, 92, 108), (30, 32, 42))
    c.rect(x - 60 * s, y - 62 * s, x + 30 * s, y - 56 * s, (170, 176, 196), None, alpha=0.7)
    c.poly(P(x, y, s, [(2, -42), (14, -42), (14, -20), (8, 0), (-2, 0)]), WOOD[2], WOOD[0])
    c.poly(P(x, y, s, [(-60, -62), (-70, -60), (-70, -52), (-60, -50)]), (60, 64, 78))
    c.glow(x - 74 * s, y - 56 * s, 18 * s, (255, 220, 120), alpha=0.55)


def rocket(c, x, y, s=1.0):
    c.glow(x, y + 10 * s, 50 * s, (255, 160, 60), alpha=0.6)
    c.poly(P(x, y, s, [(-14, 6), (0, 54), (14, 6)]), (255, 230, 120), (240, 90, 30))
    c.poly(P(x, y, s, [(-18, -20), (-18, -110), (0, -150), (18, -110), (18, -20)]), (246, 248, 252), (176, 188, 210), vertical=False)
    c.poly(P(x, y, s, [(-18, -110), (0, -150), (18, -110)]), (210, 50, 50), (140, 26, 26))
    c.poly(P(x, y, s, [(-18, -40), (-44, 0), (-18, -8)]), (210, 50, 50), (140, 26, 26))
    c.poly(P(x, y, s, [(18, -40), (44, 0), (18, -8)]), (210, 50, 50), (140, 26, 26))
    c.ellipse(x, y - 84 * s, 10 * s, 10 * s, (90, 170, 230), (40, 90, 160))


def robot(c, x, y, s=1.0):
    col = (150, 164, 186)
    c.rect(x - 30 * s, y - 92 * s, x + 30 * s, y - 34 * s, light(col, 0.2), dark(col, 0.3), radius=6)
    c.rect(x - 22 * s, y - 34 * s, x - 8 * s, y, dark(col, 0.1), dark(col, 0.4))
    c.rect(x + 8 * s, y - 34 * s, x + 22 * s, y, dark(col, 0.1), dark(col, 0.4))
    c.rect(x - 46 * s, y - 88 * s, x - 32 * s, y - 50 * s, col, dark(col, 0.4), radius=4)
    c.rect(x + 32 * s, y - 88 * s, x + 46 * s, y - 50 * s, col, dark(col, 0.4), radius=4)
    c.rect(x - 22 * s, y - 128 * s, x + 22 * s, y - 94 * s, light(col, 0.25), dark(col, 0.3), radius=6)
    c.line([(x, y - 128 * s), (x, y - 146 * s)], col, 2.4 * s)
    c.glow(x, y - 148 * s, 12 * s, (255, 80, 80), alpha=0.8)
    for dx in (-9, 9):
        c.ellipse(x + dx * s, y - 112 * s, 5 * s, 5 * s, (110, 240, 255))
        c.glow(x + dx * s, y - 112 * s, 12 * s, (110, 240, 255), alpha=0.4)
    c.rect(x - 14 * s, y - 62 * s, x + 14 * s, y - 52 * s, (40, 50, 70), (20, 26, 40))
    for dx in (-8, 0, 8):
        c.ellipse(x + dx * s, y - 57 * s, 2.2 * s, 2.2 * s, (120, 240, 160))


def ufo(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-26, -40), (0, -50), (26, -40), (60, -4), (-60, -4)]), (190, 200, 216), (90, 100, 128), alpha=0.0)
    c.glow(x, y + 24 * s, 62 * s, (150, 255, 180), alpha=0.35)
    c.poly(P(x, y, s, [(-20, -4), (20, -4), (56, 120), (-56, 120)]), (170, 255, 200), (60, 160, 120), alpha=0.3)
    c.ellipse(x, y - 40 * s, 22 * s, 20 * s, (150, 240, 255), (60, 140, 200), alpha=0.85)
    c.ellipse(x, y - 20 * s, 62 * s, 16 * s, (200, 210, 226), (96, 106, 134))
    for dx in (-36, -12, 12, 36):
        c.ellipse(x + dx * s, y - 20 * s, 3.4 * s, 3.4 * s, (255, 240, 140))


def car(c, x, y, s=1.0, color=(200, 50, 50)):
    c.poly(P(x, y, s, [(-70, -10), (-70, -36), (-48, -42), (-30, -62), (24, -62), (46, -40), (72, -34), (72, -10)]), light(color, 0.2), dark(color, 0.35))
    c.poly(P(x, y, s, [(-26, -56), (-40, -40), (-4, -40), (-4, -56)]), (170, 210, 240), (90, 130, 170))
    c.poly(P(x, y, s, [(2, -56), (2, -40), (40, -40), (22, -56)]), (170, 210, 240), (90, 130, 170))
    for dx in (-44, 44):
        c.ellipse(x + dx * s, y - 8 * s, 15 * s, 15 * s, (30, 30, 36), (10, 10, 14))
        c.ellipse(x + dx * s, y - 8 * s, 7 * s, 7 * s, (170, 176, 190))
    c.ellipse(x + 70 * s, y - 28 * s, 4 * s, 4 * s, (255, 240, 170))
    c.glow(x + 78 * s, y - 28 * s, 24 * s, (255, 240, 170), alpha=0.4)


def compass(c, x, y, s=1.0):
    c.ellipse(x, y - 56 * s, 52 * s, 52 * s, GOLD[3], GOLD[0])
    c.ellipse(x, y - 56 * s, 44 * s, 44 * s, (244, 236, 214), (206, 190, 150))
    c.poly(P(x, y - 56 * s, s, [(0, -38), (7, 0), (0, 38), (-7, 0)]), (200, 50, 50), (110, 26, 26))
    c.poly(P(x, y - 56 * s, s, [(0, 38), (7, 0), (-7, 0)]), (60, 64, 80), (30, 32, 44))
    c.poly(P(x, y - 56 * s, s, [(-38, 0), (0, -7), (38, 0), (0, 7)]), (150, 130, 90), (110, 90, 60), alpha=0.8)
    c.ellipse(x, y - 56 * s, 4 * s, 4 * s, GOLD[2])
    c.ellipse(x, y - 112 * s, 6 * s, 6 * s, GOLD[2], GOLD[0])


def target(c, x, y, s=1.0):
    for i, col in enumerate(((240, 240, 236), (210, 50, 50), (240, 240, 236), (210, 50, 50), (240, 200, 60))):
        r = (56 - i * 11) * s
        c.ellipse(x, y - 60 * s, r, r, col, dark(col, 0.2))
    c.line([(x + 6 * s, y - 66 * s), (x + 64 * s, y - 112 * s)], WOOD[1], 3 * s)
    c.poly(P(x, y, s, [(62, -106), (74, -122), (66, -110), (80, -112), (68, -102)]), (240, 240, 236), (180, 180, 190))
    c.poly(P(x, y, s, [(6, -66), (0, -52), (14, -60)]), STEEL[2], STEEL[0])


def wand(c, x, y, s=1.0):
    c.glow(x + 40 * s, y - 112 * s, 46 * s, (255, 230, 150), alpha=0.6)
    c.line([(x - 40 * s, y - 10 * s), (x + 34 * s, y - 104 * s)], WOOD[0], 6 * s)
    c.line([(x - 40 * s, y - 10 * s), (x + 34 * s, y - 104 * s)], WOOD[2], 3.6 * s)
    pts = []
    for i in range(10):
        a = math.radians(i * 36 - 90)
        r = 18 if i % 2 == 0 else 7
        pts.append((x + 40 * s + math.cos(a) * r * s, y - 112 * s + math.sin(a) * r * s))
    c.poly(pts, (255, 250, 210), (255, 210, 90))
    for dx, dy in ((20, -130), (62, -96), (54, -130), (14, -92)):
        c.ellipse(x + dx * s, y + dy * s, 2.4 * s, 2.4 * s, (255, 250, 220))


def lamp_oil(c, x, y, s=1.0):
    c.glow(x, y - 70 * s, 56 * s, (255, 200, 100), alpha=0.5)
    c.poly(P(x, y, s, [(-18, 0), (-22, -20), (-26, -70), (26, -70), (22, -20), (18, 0)]), (220, 240, 255), (150, 190, 220), alpha=0.55)
    c.poly(P(x, y, s, [(-8, -24), (0, -60), (8, -24)]), (255, 230, 120), (240, 120, 30))
    c.rect(x - 24 * s, y - 80 * s, x + 24 * s, y - 70 * s, (60, 60, 70), (30, 30, 40))
    c.rect(x - 22 * s, y - 10 * s, x + 22 * s, y, (60, 60, 70), (30, 30, 40))
    c.curve([(x - 14 * s, y - 80 * s), (x, y - 112 * s), (x + 14 * s, y - 80 * s)], (60, 60, 70), 2.4 * s)


def magnifier(c, x, y, s=1.0):
    c.line([(x + 20 * s, y - 40 * s), (x + 62 * s, y)], WOOD[1], 9 * s)
    c.ellipse(x - 6 * s, y - 70 * s, 40 * s, 40 * s, GOLD[2], GOLD[0])
    c.ellipse(x - 6 * s, y - 70 * s, 34 * s, 34 * s, (200, 230, 250), (130, 170, 210), alpha=0.85)
    c.ellipse(x - 18 * s, y - 84 * s, 10 * s, 6 * s, (255, 255, 255), alpha=0.7, blur=1.4)


def helmet_guard(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-34, -10), (-38, -60), (-24, -92), (0, -100), (24, -92), (38, -60), (34, -10), (14, -10), (12, -50), (-12, -50), (-14, -10)]), STEEL[3], STEEL[0], vertical=False)
    c.poly(P(x, y, s, [(-4, -100), (4, -100), (4, -50), (-4, -50)]), STEEL[1], STEEL[0])
    c.poly(P(x, y, s, [(-4, -100), (0, -132), (4, -100)]), (210, 50, 50), (140, 26, 26))
    c.line([(x + 66 * s, y), (x + 66 * s, y - 150 * s)], WOOD[1], 3 * s)
    c.poly(P(x, y, s, [(66, -150), (72, -128), (66, -122), (60, -128)]), STEEL[3], STEEL[0])


def runner(c, x, y, s=1.0, color=(20, 18, 28)):
    c.ellipse(x + 6 * s, y - 118 * s, 10 * s, 10 * s, color)
    c.line([(x, y - 104 * s), (x - 6 * s, y - 56 * s)], color, 12 * s)
    c.line([(x - 6 * s, y - 56 * s), (x - 34 * s, y - 36 * s), (x - 52 * s, y - 4 * s)], color, 8 * s)
    c.line([(x - 6 * s, y - 56 * s), (x + 22 * s, y - 34 * s), (x + 16 * s, y)], color, 8 * s)
    c.line([(x, y - 100 * s), (x + 30 * s, y - 84 * s), (x + 46 * s, y - 98 * s)], color, 7 * s)
    c.line([(x, y - 100 * s), (x - 28 * s, y - 80 * s), (x - 40 * s, y - 94 * s)], color, 7 * s)
    for i in range(3):
        c.line([(x - 64 * s - i * 10 * s, y - 100 * s + i * 20 * s), (x - 90 * s - i * 10 * s, y - 100 * s + i * 20 * s)], (255, 255, 255), 1.4, alpha=0.4)


def hero(c, x, y, s=1.0, color=(14, 12, 20), cape=(160, 30, 40)):
    c.poly(P(x, y, s, [(-4, -86), (-40, -70), (-52, -30), (-30, -44), (-14, -10), (-10, -50)]), cape, dark(cape, 0.6))
    c.line([(x - 8 * s, y - 50 * s), (x - 12 * s, y)], color, 11 * s)
    c.line([(x + 8 * s, y - 50 * s), (x + 20 * s, y)], color, 11 * s)
    c.poly(P(x, y, s, [(-14, -50), (-12, -92), (12, -92), (14, -50)]), color, None)
    c.ellipse(x, y - 102 * s, 9 * s, 10 * s, color)
    c.line([(x + 10 * s, y - 88 * s), (x + 30 * s, y - 114 * s)], color, 8 * s)
    c.line([(x - 10 * s, y - 88 * s), (x - 26 * s, y - 64 * s)], color, 8 * s)
    sj.sword(c, x + 32 * s, y - 112 * s, s * 0.9, 18, STEEL)


def traveler(c, x, y, s=1.0):
    c.ellipse(x, y - 98 * s, 8 * s, 9 * s, (20, 18, 28))
    c.poly(P(x, y, s, [(-8, -92), (8, -92), (20, -30), (6, 0), (-2, -30), (-6, 0), (-18, -30)]), (24, 22, 34), None)
    c.poly(P(x, y, s, [(-8, -88), (-26, -60), (-22, -36), (-8, -60)]), (80, 56, 40), (50, 34, 24))
    c.line([(x + 18 * s, y - 80 * s), (x + 30 * s, y)], WOOD[1], 2.4 * s)
    c.line([(x + 8 * s, y - 80 * s), (x + 18 * s, y - 80 * s)], (24, 22, 34), 5 * s)


def fish(c, x, y, s=1.0, color=(240, 150, 60)):
    c.poly(P(x, y, s, [(-44, 0), (-20, -24), (16, -26), (36, -6), (56, -24), (56, 24), (36, 6), (16, 24), (-20, 22)]), light(color, 0.2), dark(color, 0.35))
    c.ellipse(x - 26 * s, y - 4 * s, 4 * s, 4 * s, (250, 250, 240))
    c.ellipse(x - 26 * s, y - 4 * s, 2 * s, 2 * s, (20, 20, 20))
    for i in range(4):
        c.curve([(x - 6 * s + i * 9 * s, y - 14 * s), (x - 2 * s + i * 9 * s, y), (x - 6 * s + i * 9 * s, y + 14 * s)], dark(color, 0.3), 1.4, alpha=0.6)


def rod(c, x, y, s=1.0):
    c.line([(x - 50 * s, y), (x + 40 * s, y - 120 * s)], WOOD[1], 3.6 * s)
    c.curve([(x + 40 * s, y - 120 * s), (x + 56 * s, y - 100 * s), (x + 62 * s, y - 50 * s)], (240, 240, 230), 0.9)
    c.ellipse(x + 62 * s, y - 46 * s, 3 * s, 3 * s, (220, 60, 60))


def potion_bubbling(c, x, y, s=1.0):
    sj.potion(c, x, y, s)


def swords_cross(c, x, y, s=1.0):
    sj.sword(c, x, y, s, 38)
    sj.sword(c, x, y, s, -38)
    c.glow(x, y - 56 * s, 40 * s, (255, 240, 200), alpha=0.35)


def amulet(c, x, y, s=1.0):
    c.curve([(x - 60 * s, y - 130 * s), (x - 34 * s, y - 80 * s), (x, y - 62 * s), (x + 34 * s, y - 80 * s), (x + 60 * s, y - 130 * s)], GOLD[2], 2.4 * s)
    c.glow(x, y - 40 * s, 50 * s, (140, 255, 210), alpha=0.5)
    c.poly(blob(x, y - 40 * s, 26 * s, 30 * s, c.rng, 0.0, 8), GOLD[3], GOLD[0])
    c.poly(P(x, y - 40 * s, s, [(0, -20), (14, 0), (0, 20), (-14, 0)]), (120, 255, 210), (30, 160, 130))
    c.ellipse(x, y - 40 * s, 3 * s, 3 * s, (255, 255, 255))


def map_(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-60, -10), (-60, -90), (-20, -100), (20, -88), (60, -98), (60, -8), (20, -18), (-20, -6)]), (240, 224, 180), (206, 180, 130))
    for xx in (-20, 20):
        c.line([(x + xx * s, y - 98 * s), (x + xx * s, y - 12 * s)], (160, 130, 90), 0.9, alpha=0.6)
    c.curve([(x - 44 * s, y - 30 * s), (x - 20 * s, y - 56 * s), (x + 6 * s, y - 40 * s), (x + 34 * s, y - 70 * s)], (190, 50, 40), 1.6 * s)
    for i in range(8):
        c.ellipse(x + (-44 + i * 10) * s, y + (-30 - (i % 3) * 8) * s, 0.9 * s, 0.9 * s, (190, 50, 40))
    xx, yy = x + 40 * s, y - 72 * s
    c.line([(xx - 7 * s, yy - 7 * s), (xx + 7 * s, yy + 7 * s)], (190, 50, 40), 3 * s)
    c.line([(xx - 7 * s, yy + 7 * s), (xx + 7 * s, yy - 7 * s)], (190, 50, 40), 3 * s)
    c.poly(P(x, y, s, [(-50, -80), (-40, -62), (-24, -76), (-34, -88)]), (110, 170, 110), None, alpha=0.7)


def padlock(c, x, y, s=1.0):
    c.curve([(x - 24 * s, y - 56 * s), (x - 24 * s, y - 100 * s), (x, y - 116 * s), (x + 24 * s, y - 100 * s), (x + 24 * s, y - 56 * s)], STEEL[2], 9 * s)
    c.rect(x - 38 * s, y - 62 * s, x + 38 * s, y, GOLD[2], GOLD[0], radius=6)
    c.ellipse(x, y - 38 * s, 7 * s, 7 * s, (30, 24, 20))
    c.poly(P(x, y, s, [(-3, -34), (3, -34), (5, -16), (-5, -16)]), (30, 24, 20))


def sparkle(c, x, y, s=1.0):
    for dx, dy, k in ((0, -60, 1.0), (-48, -96, 0.5), (50, -40, 0.6), (40, -110, 0.4), (-44, -26, 0.45)):
        r = 40 * k * s
        cx, cy = x + dx * s, y + dy * s
        c.glow(cx, cy, r * 1.5, (255, 240, 170), alpha=0.5)
        c.poly([(cx, cy - r), (cx + r * 0.2, cy - r * 0.2), (cx + r, cy), (cx + r * 0.2, cy + r * 0.2), (cx, cy + r), (cx - r * 0.2, cy + r * 0.2), (cx - r, cy), (cx - r * 0.2, cy - r * 0.2)], (255, 252, 230), (255, 214, 110))


def party(c, x, y, s=1.0):
    for dx, col in ((-30, (230, 60, 80)), (0, (80, 160, 240)), (30, (250, 210, 60))):
        c.line([(x + dx * 0.6 * s, y), (x + dx * s, y - 64 * s)], (230, 230, 230), 0.8, alpha=0.7)
        c.ellipse(x + dx * s, y - 90 * s, 20 * s, 26 * s, light(col, 0.25), dark(col, 0.3))
        c.ellipse(x + (dx - 6) * s, y - 98 * s, 5 * s, 8 * s, (255, 255, 255), alpha=0.5, blur=1.5)
    import random
    r = random.Random(c.seed)
    for _ in range(46):
        px, py = r.uniform(10, 246), r.uniform(10, 200)
        col = r.choice([(250, 90, 110), (90, 180, 250), (255, 220, 80), (120, 230, 150), (220, 130, 250)])
        c.poly([(px, py), (px + 5, py + 2), (px + 3, py + 7), (px - 2, py + 5)], col, None, alpha=0.9)


def tear_cloud(c, x, y, s=1.0):
    c.poly(blob(x, y - 100 * s, 54 * s, 24 * s, c.rng, 0.12, 16), (120, 130, 150), (70, 78, 98))
    for dx in (-30, -10, 10, 30):
        c.poly([(x + dx * s, y - 70 * s), (x + (dx - 4) * s, y - 52 * s), (x + (dx + 4) * s, y - 52 * s)], (150, 190, 255), None, alpha=0.9)
        c.ellipse(x + dx * s, y - 50 * s, 4.4 * s, 4.4 * s, (150, 190, 255))


def eyes_in_dark(c, x, y, s=1.0):
    for dx, dy, r in ((-60, -30, 1.0), (-14, -70, 1.4), (50, -44, 1.1), (80, -92, 0.8), (-80, -100, 0.8)):
        for sgn in (-1, 1):
            ex, ey = x + (dx + sgn * 9 * r) * s, y + dy * s
            c.glow(ex, ey, 14 * r * s, (255, 70, 50), alpha=0.5)
            c.poly([(ex - 6 * r * s, ey), (ex, ey - 3 * r * s), (ex + 6 * r * s, ey), (ex, ey + 3 * r * s)], (255, 240, 200), (255, 70, 50))


def anger_face(c, x, y, s=1.0):
    c.glow(x, y - 70 * s, 100 * s, (255, 60, 30), alpha=0.5)
    c.ellipse(x, y - 70 * s, 50 * s, 50 * s, (240, 80, 50), (160, 30, 20))
    c.poly(P(x, y - 70 * s, s, [(-34, -22), (-8, -6), (-8, 0), (-36, -12)]), (40, 10, 10))
    c.poly(P(x, y - 70 * s, s, [(34, -22), (8, -6), (8, 0), (36, -12)]), (40, 10, 10))
    c.ellipse(x - 18 * s, y - 70 * s, 6 * s, 6 * s, (255, 250, 220))
    c.ellipse(x + 18 * s, y - 70 * s, 6 * s, 6 * s, (255, 250, 220))
    c.ellipse(x - 18 * s, y - 70 * s, 2.6 * s, 2.6 * s, (30, 10, 10))
    c.ellipse(x + 18 * s, y - 70 * s, 2.6 * s, 2.6 * s, (30, 10, 10))
    c.curve([(x - 24 * s, y - 38 * s), (x, y - 50 * s), (x + 24 * s, y - 38 * s)], (40, 10, 10), 4 * s)
    for dx in (-30, 0, 30):
        sj.fire(c, x + dx * s, y - 112 * s, 0.35 * s)


def bell_alert(c, x, y, s=1.0):
    sj.bell(c, x, y, s)
    for sgn in (-1, 1):
        for r in (50, 66):
            c.curve([(x + sgn * r * s * math.cos(math.radians(a)), y - 46 * s + r * s * math.sin(math.radians(a))) for a in range(-40, 41, 20)], (255, 230, 120), 2.2 * s, alpha=0.8)


def throne(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-40, 0), (-40, -150), (-24, -170), (0, -150), (24, -170), (40, -150), (40, 0)]), RED[1], RED[0], vertical=False)
    c.poly(P(x, y, s, [(-32, -2), (-32, -140), (32, -140), (32, -2)]), RED[2], RED[1])
    c.rect(x - 50 * s, y - 62 * s, x + 50 * s, y - 40 * s, GOLD[2], GOLD[0], radius=3)
    c.rect(x - 56 * s, y - 80 * s, x - 42 * s, y, GOLD[2], GOLD[0])
    c.rect(x + 42 * s, y - 80 * s, x + 56 * s, y, GOLD[2], GOLD[0])
    c.poly(P(x, y, s, [(-26, -62), (26, -62), (22, -44), (-22, -44)]), RED[3], RED[1])
    for dx in (-40, 0, 40):
        c.ellipse(x + dx * s, y - 170 * s + (0 if dx == 0 else 14 * s), 5 * s, 5 * s, GOLD[3], GOLD[1])


def tombstone(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-30, 0), (-30, -74), (-20, -96), (0, -104), (20, -96), (30, -74), (30, 0)]), STONE[2], STONE[0], vertical=False)
    c.line([(x, y - 80 * s), (x, y - 40 * s)], STONE[0], 3.4 * s)
    c.line([(x - 12 * s, y - 66 * s), (x + 12 * s, y - 66 * s)], STONE[0], 3.4 * s)
    c.poly(P(x, y, s, [(-36, 0), (-34, -10), (34, -10), (36, 0)]), (50, 60, 44), (28, 36, 26))


def lighthouse(c, x, y, s=1.0):
    c.glow(x, y - 128 * s, 90 * s, (255, 240, 170), alpha=0.5)
    c.poly(P(x, y, s, [(-22, 0), (-14, -112), (14, -112), (22, 0)]), (244, 240, 236), (196, 90, 80), vertical=False)
    for k in (30, 66):
        c.poly(P(x, y, s, [(-20 + (k / 90) * 7, -k), (20 - (k / 90) * 7, -k), (19 - (k / 90) * 7, -k - 14), (-19 + (k / 90) * 7, -k - 14)]), (200, 60, 54), None, alpha=0.9)
    c.rect(x - 18 * s, y - 120 * s, x + 18 * s, y - 112 * s, (50, 50, 60), (30, 30, 40))
    c.rect(x - 12 * s, y - 138 * s, x + 12 * s, y - 120 * s, (255, 244, 190), (255, 210, 110))
    c.poly(P(x, y, s, [(-16, -138), (0, -156), (16, -138)]), (180, 50, 44), (120, 30, 30))


def windmill(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-26, 0), (-16, -90), (16, -90), (26, 0)]), (230, 206, 160), (160, 130, 90), vertical=False)
    c.poly(P(x, y, s, [(-22, -86), (0, -116), (22, -86)]), (150, 80, 50), (100, 50, 34))
    c.poly(P(x, y, s, [(-6, 0), (-6, -22), (0, -28), (6, -22), (6, 0)]), WOOD[1], WOOD[0])
    cx, cy = x, y - 96 * s
    for ang in (20, 110, 200, 290):
        a = math.radians(ang)
        ex, ey = cx + math.cos(a) * 70 * s, cy + math.sin(a) * 70 * s
        c.line([(cx, cy), (ex, ey)], WOOD[0], 3 * s)
        px, py = -math.sin(a), math.cos(a)
        c.poly([(cx + math.cos(a) * 14 * s, cy + math.sin(a) * 14 * s), (ex, ey), (ex + px * 20 * s, ey + py * 20 * s), (cx + math.cos(a) * 14 * s + px * 20 * s, cy + math.sin(a) * 14 * s + py * 20 * s)], (244, 236, 214), (200, 190, 160))
    c.ellipse(cx, cy, 5 * s, 5 * s, WOOD[0])


def market_stall(c, x, y, s=1.0):
    c.rect(x - 56 * s, y - 36 * s, x + 56 * s, y, WOOD[2], WOOD[0])
    c.line([(x - 52 * s, y - 36 * s), (x - 52 * s, y - 100 * s)], WOOD[0], 4 * s)
    c.line([(x + 52 * s, y - 36 * s), (x + 52 * s, y - 100 * s)], WOOD[0], 4 * s)
    for i in range(6):
        col = (214, 60, 56) if i % 2 == 0 else (250, 244, 230)
        c.poly(P(x, y, s, [(-60 + i * 20, -100), (-40 + i * 20, -100), (-44 + i * 20, -82), (-56 + i * 20, -82)]), col, dark(col, 0.2))
    for i, col in enumerate(((220, 60, 50), (250, 190, 50), (110, 190, 80), (220, 60, 50), (250, 190, 50))):
        c.ellipse(x + (-40 + i * 20) * s, y - 44 * s, 8 * s, 8 * s, light(col, 0.2), dark(col, 0.2))
