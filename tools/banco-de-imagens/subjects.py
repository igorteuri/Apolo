"""Elementos (árvores, castelos, objetos, criaturas...) usados nas cenas do banco de imagens.

Cada função desenha no Canvas c. (x, y) é o ponto do chão no centro do elemento e s é a escala (1.0 = tamanho padrão).
Os degradês vão do lado iluminado (cima/direita) para o lado de sombra.
"""
import math

from base import blob, dark, light, mix, rgb, smooth

# paletas
GREENS = [rgb('2f6b3a'), rgb('3f8a45'), rgb('64a84a'), rgb('9bd05a')]
DEEP_GREENS = [rgb('1d4a33'), rgb('2a6244'), rgb('3a7d52'), rgb('5da069')]
STONE = [rgb('4a4f5a'), rgb('6a7080'), rgb('8d93a2'), rgb('b6bccb')]
WOOD = [rgb('4a2f1d'), rgb('6e4528'), rgb('946036'), rgb('c08a52')]
GOLD = [rgb('8a5a12'), rgb('c18a1c'), rgb('f0bf3a'), rgb('ffe58a')]
STEEL = [rgb('5a6270'), rgb('8892a4'), rgb('c4ccda'), rgb('f2f6fc')]
RED = [rgb('5a1414'), rgb('8f1f1f'), rgb('c93a2e'), rgb('f2704f')]


def P(x, y, s, pts):
    return [(x + px * s, y + py * s) for px, py in pts]


# ------------------------------------------------------------------ natureza
def oak(c, x, y, s=1.0, leaf=GREENS, trunk=WOOD):
    rng = c.rng
    c.poly(P(x, y, s, [(-7, 0), (-5, -40), (-9, -62), (-3, -58), (0, -48), (6, -62), (9, -58), (5, -40), (8, 0)]), trunk[1], trunk[0], vertical=False)
    c.poly(P(x, y, s, [(2, 0), (4, -40), (8, 0)]), trunk[0], None, alpha=0.5)
    spots = [(-30, -70, 30, 22), (28, -72, 30, 22), (0, -92, 36, 26), (-18, -52, 28, 20), (24, -50, 26, 18), (0, -68, 34, 24), (-40, -56, 18, 14)]
    for i, (dx, dy, rx, ry) in enumerate(spots):
        pts = blob(x + dx * s, y + dy * s, rx * s, ry * s, rng)
        base = leaf[min(i % 3, 2)]
        c.poly(pts, mix(base, leaf[0], 0.35), base)
    for i, (dx, dy, rx, ry) in enumerate(spots[2:6]):
        c.poly(blob(x + (dx + 6) * s, y + (dy - 8) * s, rx * 0.6 * s, ry * 0.5 * s, rng), leaf[2], leaf[3], alpha=0.85)


def pine(c, x, y, s=1.0, leaf=DEEP_GREENS, snow=False):
    c.rect(x - 3 * s, y - 14 * s, x + 3 * s, y, WOOD[0], WOOD[1], vertical=False)
    h = 0
    for i, (w, ht) in enumerate([(26, 34), (21, 32), (16, 30), (11, 28)]):
        top = y - 14 * s - (i * 24 + ht) * s
        base_y = y - 12 * s - i * 24 * s
        c.poly(P(0, 0, 1, [(x, top), (x + w * s, base_y), (x - w * s, base_y)]), leaf[2 - min(i, 2)], leaf[0], vertical=True)
        c.poly(P(0, 0, 1, [(x, top), (x + w * s, base_y), (x + w * s * 0.3, base_y)]), leaf[3], None, alpha=0.35)
        if snow:
            c.poly(P(0, 0, 1, [(x, top), (x + w * s * 0.55, top + (base_y - top) * 0.5), (x - w * s * 0.55, top + (base_y - top) * 0.5)]), (240, 245, 255), None, alpha=0.9)


def forest_row(c, y, n, s0, leaf, seed=0, x0=-10, x1=266, snow=False):
    import random
    r = random.Random(c.seed * 31 + seed)
    xs = sorted(r.uniform(x0, x1) for _ in range(n))
    for x in xs:
        pine(c, x, y + r.uniform(-4, 4), s0 * r.uniform(0.75, 1.25), leaf, snow=snow)


def palm(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-4, 0), (0, -60), (-14, -96), (-8, -98), (6, -62), (4, 0)]), WOOD[2], WOOD[0], vertical=False)
    for ang in (-160, -125, -90, -55, -20, 15):
        a = math.radians(ang)
        tipx, tipy = x - 10 * s + math.cos(a) * 46 * s, y - 98 * s + math.sin(a) * 26 * s + 14 * s
        c.poly([(x - 10 * s, y - 98 * s), ((x - 10 * s + tipx) / 2, (y - 98 * s + tipy) / 2 - 12 * s), (tipx, tipy), ((x - 10 * s + tipx) / 2, (y - 98 * s + tipy) / 2 - 2 * s)], GREENS[3], GREENS[0])


def mountain(c, x, y, w, h, body=STONE, snow=True, seed=0):
    import random
    r = random.Random(c.seed * 17 + seed)
    pts = [(x - w / 2, y), (x - w * 0.18, y - h * 0.62), (x - w * 0.06, y - h * 0.55), (x, y - h), (x + w * 0.1, y - h * 0.58), (x + w * 0.26, y - h * 0.72), (x + w / 2, y)]
    c.poly(pts, body[2], body[0])
    c.poly([(x, y - h), (x + w * 0.1, y - h * 0.58), (x + w * 0.26, y - h * 0.72), (x + w / 2, y), (x + w * 0.05, y)], body[0], None, alpha=0.45)
    if snow:
        c.poly([(x, y - h), (x - w * 0.09, y - h * 0.72), (x - w * 0.03, y - h * 0.78), (x + w * 0.02, y - h * 0.7), (x + w * 0.06, y - h * 0.76), (x + w * 0.1, y - h * 0.58)], (244, 248, 255), (200, 215, 235))


def rocks(c, x, y, s=1.0, body=STONE, n=3):
    for i in range(n):
        dx = (i - (n - 1) / 2.0) * 30 * s
        pts = blob(x + dx, y - 10 * s, 20 * s * (1 - 0.15 * i), 14 * s, c.rng, 0.18, 10)
        c.poly(pts, body[2], body[0])


def flowers(c, x0, x1, y, n, colors, seed=0):
    import random
    r = random.Random(c.seed * 5 + seed)
    for _ in range(n):
        x, yy = r.uniform(x0, x1), y + r.uniform(-4, 14)
        col = r.choice(colors)
        c.line([(x, yy), (x, yy + 8)], GREENS[0], 1.0)
        c.ellipse(x, yy, 2.4, 2.4, col)


def waves(c, y0, y1, c_top, c_bottom, n=6, amp=3.0):
    c.rect(-4, y0, 260, y1, c_top, c_bottom)
    import random
    r = random.Random(c.seed * 3)
    for i in range(n):
        yy = y0 + (y1 - y0) * (i + 1) / (n + 1)
        pts = [(x, yy + math.sin(x / 14.0 + i * 1.7) * amp) for x in range(-4, 262, 6)]
        c.line(pts, light(c_top, 0.35), 1.2, alpha=0.55)


def fire(c, x, y, s=1.0):
    c.glow(x, y - 20 * s, 44 * s, (255, 150, 40), alpha=0.5)
    c.poly(P(x, y, s, [(-18, 0), (-20, -16), (-8, -34), (-12, -52), (0, -70), (4, -46), (14, -32), (20, -14), (16, 0)]), (255, 210, 70), (230, 70, 20))
    c.poly(P(x, y, s, [(-9, 0), (-10, -12), (-2, -26), (0, -40), (6, -22), (10, -10), (8, 0)]), (255, 245, 170), (255, 170, 40))


def logs(c, x, y, s=1.0):
    for ang, dx in ((14, -12), (-14, 12)):
        a = math.radians(ang)
        c.poly(P(x + dx * s, y, s, [(-26 * math.cos(a), -26 * math.sin(a) - 4), (26 * math.cos(a), 26 * math.sin(a) - 4), (26 * math.cos(a), 26 * math.sin(a) + 5), (-26 * math.cos(a), -26 * math.sin(a) + 5)]), WOOD[2], WOOD[0])


def moon(c, x, y, r, color=(245, 240, 215), crescent=False):
    c.glow(x, y, r * 2.4, (190, 200, 255), alpha=0.5)
    c.ellipse(x, y, r, r, light(color, 0.2), dark(color, 0.12))
    if crescent:
        c.ellipse(x + r * 0.45, y - r * 0.15, r * 0.92, r * 0.92, (22, 28, 60))
    else:
        for dx, dy, rr in ((-0.3, -0.2, 0.2), (0.3, 0.25, 0.28), (0.1, -0.45, 0.14)):
            c.ellipse(x + dx * r, y + dy * r, rr * r, rr * r, dark(color, 0.18), alpha=0.5)


def sun(c, x, y, r, color=(255, 236, 160)):
    c.glow(x, y, r * 3.2, (255, 220, 140), alpha=0.7)
    c.ellipse(x, y, r, r, (255, 250, 220), color)


def lightning(c, x, y, s=1.0):
    pts = P(x, y, s, [(0, -120), (-16, -64), (-2, -64), (-18, 0), (16, -52), (2, -52), (14, -120)])
    c.glow(x, y - 60 * s, 50 * s, (200, 210, 255), alpha=0.5)
    c.poly(pts, (255, 255, 230), (200, 215, 255))


# ------------------------------------------------------------------ construções
def castle(c, x, y, s=1.0, body=STONE, roof=RED, lit=True):
    # muralha
    c.poly(P(x, y, s, [(-62, 0), (-62, -46), (62, -46), (62, 0)]), body[2], body[0])
    for i in range(-6, 7):
        c.rect(x + (i * 9 - 3) * s, y - 54 * s, x + (i * 9 + 3) * s, y - 46 * s, body[2], body[1])
    # torres
    for dx, h, w in ((-54, 92, 15), (54, 92, 15), (0, 112, 20), (-26, 70, 12), (26, 70, 12)):
        c.poly(P(x + dx * s, y, s, [(-w, 0), (-w, -h), (w, -h), (w, 0)]), body[3], body[0], vertical=False)
        c.poly(P(x + dx * s, y, s, [(-w - 4, -h), (0, -h - w * 2.1), (w + 4, -h)]), roof[2], roof[0])
        c.poly(P(x + dx * s, y, s, [(0, -h - w * 2.1), (w + 4, -h), (w * 0.2, -h)]), roof[0], None, alpha=0.4)
        c.rect(x + dx * s - 2.5 * s, y - (h - 24) * s, x + dx * s + 2.5 * s, y - (h - 36) * s, (30, 24, 20) if not lit else (255, 214, 110), radius=2)
    # portão
    c.poly(P(x, y, s, [(-11, 0), (-11, -24), (-6, -33), (6, -33), (11, -24), (11, 0)]), (28, 22, 20) if not lit else (60, 36, 20), (14, 10, 10))
    # bandeira
    c.line([(x, y - 112 * s - 42 * s), (x, y - 112 * s - 62 * s)], (60, 50, 50), 1.4)
    c.poly(P(x, y, s, [(0, -174), (16, -168), (0, -162)]), (230, 60, 50), None)


def house(c, x, y, s=1.0, wall=None, roof=None, lit=True, chimney=True):
    wall = wall or [rgb('7a5a3c'), rgb('a98259'), rgb('d2ae7c')]
    roof = roof or RED
    c.rect(x - 34 * s, y - 40 * s, x + 34 * s, y, wall[2], wall[0])
    c.poly(P(x, y, s, [(-42, -38), (0, -76), (42, -38)]), roof[2], roof[0])
    c.poly(P(x, y, s, [(0, -76), (42, -38), (10, -38)]), roof[0], None, alpha=0.4)
    if chimney:
        c.rect(x + 16 * s, y - 76 * s, x + 26 * s, y - 52 * s, STONE[1], STONE[0])
    c.rect(x - 7 * s, y - 24 * s, x + 7 * s, y, WOOD[1], WOOD[0], radius=3)
    c.rect(x - 28 * s, y - 30 * s, x - 14 * s, y - 16 * s, (255, 206, 100) if lit else (60, 70, 90), (230, 140, 50) if lit else (40, 50, 70))
    c.rect(x + 14 * s, y - 30 * s, x + 28 * s, y - 16 * s, (255, 206, 100) if lit else (60, 70, 90), (230, 140, 50) if lit else (40, 50, 70))
    if lit:
        c.glow(x - 21 * s, y - 23 * s, 20 * s, (255, 190, 90), alpha=0.35)
        c.glow(x + 21 * s, y - 23 * s, 20 * s, (255, 190, 90), alpha=0.35)


def tavern(c, x, y, s=1.0):
    house(c, x, y, s * 1.2, wall=[rgb('5a3b27'), rgb('8a5e3c'), rgb('b8895a')], roof=[rgb('3a2a22'), rgb('5a3a2a'), rgb('7a4e38')])
    # placa
    c.line([(x + 40 * s, y - 50 * s), (x + 62 * s, y - 50 * s)], (40, 30, 24), 1.6)
    c.rect(x + 50 * s, y - 50 * s, x + 66 * s, y - 36 * s, (200, 150, 60), (150, 100, 40), radius=2)
    c.ellipse(x + 58 * s, y - 43 * s, 3.4 * s, 4 * s, (250, 240, 200))
    # caneca
    c.rect(x - 70 * s, y - 14 * s, x - 56 * s, y, WOOD[2], WOOD[0], radius=3)


def tent(c, x, y, s=1.0, color=None):
    color = color or [rgb('8f3a2a'), rgb('c4573a'), rgb('e57b52')]
    c.poly(P(x, y, s, [(-46, 0), (0, -66), (46, 0)]), color[1], color[0])
    c.poly(P(x, y, s, [(0, -66), (46, 0), (8, 0)]), color[0], None, alpha=0.45)
    c.poly(P(x, y, s, [(-10, 0), (0, -30), (10, 0)]), (40, 24, 20), (20, 12, 10))
    c.line([(x, y - 66 * s), (x, y - 78 * s)], WOOD[0], 1.6)


def cave(c, x, y, s=1.0, body=STONE):
    c.poly(blob(x, y - 36 * s, 74 * s, 56 * s, c.rng, 0.12, 16) + [(x + 74 * s, y + 10), (x - 74 * s, y + 10)], body[2], body[0])
    c.poly(P(x, y, s, [(-30, 0), (-34, -30), (-20, -56), (0, -62), (22, -54), (34, -28), (30, 0)]), (10, 8, 14), (30, 22, 30))
    c.glow(x, y - 20 * s, 30 * s, (255, 160, 60), alpha=0.15)


def pyramid(c, x, y, s=1.0, body=None):
    body = body or [rgb('7a3a22'), rgb('b4623a'), rgb('d98a58'), rgb('f1b078')]
    c.poly(P(x, y, s, [(-90, 0), (0, -112), (30, 0)]), body[2], body[1])
    c.poly(P(x, y, s, [(0, -112), (90, 0), (30, 0)]), body[1], body[0])
    for i in range(1, 6):
        yy = -i * 18
        c.line([(x + (-90 + i * 14.4) * s, y + yy * s), (x + (30 + i * 12) * s, y + yy * s)], body[0], 0.8, alpha=0.5)


def ship(c, x, y, s=1.0, hull=None, sail=(240, 236, 222)):
    hull = hull or WOOD
    c.poly(P(x, y, s, [(-56, -14), (56, -14), (40, 6), (-40, 6)]), hull[2], hull[0])
    c.line([(x, y - 14 * s), (x, y - 100 * s)], hull[0], 2.4)
    c.poly(P(x, y, s, [(2, -96), (44, -58), (2, -26)]), sail, mix(sail, (150, 140, 120), 0.55))
    c.poly(P(x, y, s, [(-2, -90), (-36, -56), (-2, -30)]), mix(sail, (200, 190, 170), 0.2), mix(sail, (150, 140, 120), 0.4))
    c.poly(P(x, y, s, [(0, -100), (16, -94), (0, -88)]), (200, 50, 40))


def bridge(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-130, -26), (130, -26), (130, -8), (-130, -8)]), STONE[3], STONE[1])
    for i in range(-6, 7):
        c.rect(x + (i * 20 - 8) * s, y - 30 * s, x + (i * 20 + 8) * s, y - 26 * s, STONE[3], STONE[2], radius=1)
    c.poly(P(x, y, s, [(-130, -8), (130, -8), (130, 36), (-130, 36)]), STONE[1], STONE[0])
    for ax in (-60, 0, 60):
        c.poly(P(x + ax * s, y, s, [(-22, 36), (-22, 14), (-12, 2), (12, 2), (22, 14), (22, 36)]), (24, 40, 52), (40, 70, 86))


def door(c, x, y, s=1.0, frame=STONE):
    c.poly(P(x, y, s, [(-34, 0), (-34, -86), (-20, -112), (20, -112), (34, -86), (34, 0)]), frame[2], frame[0])
    c.poly(P(x, y, s, [(-24, 0), (-24, -84), (-14, -102), (14, -102), (24, -84), (24, 0)]), WOOD[2], WOOD[0], vertical=False)
    c.line([(x, y), (x, y - 102 * s)], WOOD[0], 1.2)
    c.ellipse(x + 12 * s, y - 52 * s, 2.6 * s, 2.6 * s, GOLD[2])


def portal(c, x, y, s=1.0, c1=(120, 80, 255), c2=(60, 220, 255)):
    c.glow(x, y - 60 * s, 90 * s, c1, alpha=0.55)
    for i in range(6):
        t = i / 5.0
        c.ellipse(x, y - 60 * s, (48 - 6 * i) * s, (68 - 8 * i) * s, mix(c1, c2, t), mix(c2, (255, 255, 255), t * 0.6), alpha=0.9)
    c.ellipse(x, y - 60 * s, 14 * s, 22 * s, (255, 255, 255), alpha=0.85, blur=3)


def tower(c, x, y, s=1.0, body=STONE, roof=None):
    roof = roof or [rgb('2a2340'), rgb('46386e'), rgb('6a56a0')]
    c.poly(P(x, y, s, [(-20, 0), (-16, -120), (16, -120), (20, 0)]), body[3], body[0], vertical=False)
    c.poly(P(x, y, s, [(-26, -118), (0, -176), (26, -118)]), roof[2], roof[0])
    c.rect(x - 3 * s, y - 100 * s, x + 3 * s, y - 84 * s, (255, 214, 110), radius=2)
    c.rect(x - 3 * s, y - 62 * s, x + 3 * s, y - 46 * s, (255, 214, 110), radius=2)
    c.glow(x, y - 92 * s, 20 * s, (255, 190, 90), alpha=0.3)


def church(c, x, y, s=1.0):
    c.rect(x - 30 * s, y - 56 * s, x + 30 * s, y, STONE[3], STONE[1])
    c.poly(P(x, y, s, [(-34, -54), (0, -86), (34, -54)]), STONE[1], STONE[0])
    c.rect(x - 9 * s, y - 112 * s, x + 9 * s, y - 56 * s, STONE[3], STONE[1], vertical=False)
    c.poly(P(x, y, s, [(-11, -110), (0, -140), (11, -110)]), STONE[1], STONE[0])
    c.line([(x, y - 140 * s), (x, y - 160 * s)], GOLD[2], 1.6)
    c.line([(x - 5 * s, y - 152 * s), (x + 5 * s, y - 152 * s)], GOLD[2], 1.6)
    c.poly(P(x, y, s, [(-7, 0), (-7, -20), (0, -28), (7, -20), (7, 0)]), WOOD[1], WOOD[0])
    c.poly(P(x, y, s, [(-5, -86), (-5, -98), (0, -104), (5, -98), (5, -86)]), (255, 214, 110), (240, 150, 70))


# ------------------------------------------------------------------ objetos
def sword(c, x, y, s=1.0, angle=0.0, blade=STEEL):
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)

    def T(px, py):
        return (x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s)

    c.poly([T(-6, -74), T(0, -112), T(6, -74), T(6, 0), T(-6, 0)], blade[3], blade[1], vertical=False)
    c.poly([T(0, -112), T(6, -74), T(6, 0), T(0, 0)], blade[1], None, alpha=0.4)
    c.poly([T(-20, 0), T(20, 0), T(20, 7), T(-20, 7)], GOLD[2], GOLD[0])
    c.poly([T(-4, 7), T(4, 7), T(4, 30), T(-4, 30)], WOOD[2], WOOD[0], vertical=False)
    c.ellipse(*T(0, 34), 6 * s, 6 * s, GOLD[2], GOLD[0])


def shield(c, x, y, s=1.0, face=None):
    face = face or [rgb('7a1c1c'), rgb('b02a2a'), rgb('dc4a3a')]
    outline = [(-32, -52), (32, -52), (34, -12), (0, 26), (-34, -12)]
    c.poly(P(x, y, s, [(px * 1.1, py * 1.1) for px, py in outline]), GOLD[1], GOLD[0])
    c.poly(P(x, y, s, outline), face[2], face[0])
    c.poly(P(x, y, s, [(0, -52), (32, -52), (34, -12), (0, 26)]), face[0], None, alpha=0.35)
    c.poly(P(x, y, s, [(-5, -44), (5, -44), (5, 12), (0, 20), (-5, 12)]), GOLD[3], GOLD[1])
    c.poly(P(x, y, s, [(-22, -30), (22, -30), (22, -22), (-22, -22)]), GOLD[3], GOLD[1])


def skull(c, x, y, s=1.0, bone=(236, 228, 208)):
    c.poly(blob(x, y - 44 * s, 38 * s, 34 * s, c.rng, 0.05, 14), light(bone, 0.2), dark(bone, 0.25))
    c.poly(P(x, y, s, [(-20, -22), (20, -22), (16, 0), (-16, 0)]), bone, dark(bone, 0.3))
    c.ellipse(x - 14 * s, y - 44 * s, 10 * s, 12 * s, (24, 20, 24))
    c.ellipse(x + 14 * s, y - 44 * s, 10 * s, 12 * s, (24, 20, 24))
    c.poly(P(x, y, s, [(0, -34), (-5, -24), (5, -24)]), (30, 26, 30))
    for i in range(-2, 3):
        c.line([(x + i * 6 * s, y - 14 * s), (x + i * 6 * s, y - 2 * s)], dark(bone, 0.5), 1.0, alpha=0.8)


def crown(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-44, 0), (-48, -50), (-24, -26), (0, -64), (24, -26), (48, -50), (44, 0)]), GOLD[3], GOLD[0])
    c.rect(x - 44 * s, y - 8 * s, x + 44 * s, y, GOLD[1], GOLD[0])
    for dx, col in ((-24, (220, 50, 60)), (0, (60, 120, 230)), (24, (60, 200, 110))):
        c.ellipse(x + dx * s, y - 4 * s, 5 * s, 5 * s, light(col, 0.4), col)
    for dx, dy in ((-48, -52), (0, -66), (48, -52)):
        c.ellipse(x + dx * s, y + dy * s, 4.5 * s, 4.5 * s, (255, 250, 220), GOLD[2])


def gem(c, x, y, s=1.0, color=(70, 200, 230)):
    top = light(color, 0.55)
    c.glow(x, y - 30 * s, 54 * s, color, alpha=0.45)
    c.poly(P(x, y, s, [(-40, -40), (-20, -66), (20, -66), (40, -40), (0, 0)]), light(color, 0.25), dark(color, 0.3))
    c.poly(P(x, y, s, [(-20, -66), (0, -40), (20, -66)]), top, color)
    c.poly(P(x, y, s, [(-40, -40), (0, -40), (0, 0)]), color, dark(color, 0.35), alpha=0.8)
    c.poly(P(x, y, s, [(40, -40), (0, -40), (0, 0)]), dark(color, 0.1), dark(color, 0.5), alpha=0.8)


def chest(c, x, y, s=1.0, open_=True):
    c.rect(x - 42 * s, y - 36 * s, x + 42 * s, y, WOOD[2], WOOD[0], radius=3)
    for dx in (-30, 30):
        c.rect(x + (dx - 3) * s, y - 36 * s, x + (dx + 3) * s, y, GOLD[1], GOLD[0])
    if open_:
        c.poly(P(x, y, s, [(-42, -36), (-36, -76), (36, -76), (42, -36)]), WOOD[1], WOOD[0])
        c.poly(P(x, y, s, [(-30, -40), (-26, -70), (26, -70), (30, -40)]), (40, 24, 16), None, alpha=0.6)
        c.glow(x, y - 44 * s, 52 * s, (255, 220, 110), alpha=0.6)
        for i in range(9):
            dx = (i - 4) * 8 * s
            c.ellipse(x + dx, y - 38 * s - (4 - abs(i - 4)) * 2.6 * s, 6 * s, 3.8 * s, GOLD[3], GOLD[1])
        c.ellipse(x - 14 * s, y - 52 * s, 4 * s, 4 * s, (255, 250, 200))
        c.ellipse(x + 12 * s, y - 56 * s, 3.4 * s, 3.4 * s, (255, 250, 200))
    else:
        c.poly(blob(x, y - 38 * s, 42 * s, 22 * s, c.rng, 0.02, 12) + [(x + 42 * s, y - 36 * s), (x - 42 * s, y - 36 * s)], WOOD[2], WOOD[1])
    c.rect(x - 6 * s, y - 28 * s, x + 6 * s, y - 14 * s, GOLD[3], GOLD[1], radius=2)


def key(c, x, y, s=1.0):
    c.ellipse(x, y - 60 * s, 22 * s, 22 * s, GOLD[3], GOLD[1])
    c.ellipse(x, y - 60 * s, 10 * s, 10 * s, (30, 26, 34))
    c.rect(x - 5 * s, y - 40 * s, x + 5 * s, y + 20 * s, GOLD[2], GOLD[0], vertical=False)
    for dy in (6, 16):
        c.rect(x + 5 * s, y + (dy - 5) * s, x + 20 * s, y + (dy + 3) * s, GOLD[2], GOLD[0])


def scroll(c, x, y, s=1.0):
    c.rect(x - 38 * s, y - 70 * s, x + 38 * s, y, (240, 224, 186), (214, 190, 140))
    for dy in (14, 26, 38, 50):
        c.line([(x - 28 * s, y - dy * s), (x + 28 * s, y - dy * s)], (120, 92, 60), 1.2, alpha=0.7)
    for dy in (-70, 0):
        c.rect(x - 44 * s, y + (dy - 6) * s, x + 44 * s, y + (dy + 6) * s, WOOD[2], WOOD[0], radius=5)


def book(c, x, y, s=1.0, cover=None):
    cover = cover or [rgb('3a1f5a'), rgb('5e3a8c'), rgb('8a62c0')]
    c.poly(P(x, y, s, [(-46, -14), (46, -14), (46, 8), (-46, 8)]), (244, 236, 214), (200, 188, 160))
    c.poly(P(x, y, s, [(-46, -62), (0, -52), (0, -14), (-46, -22)]), cover[2], cover[0])
    c.poly(P(x, y, s, [(46, -62), (0, -52), (0, -14), (46, -22)]), cover[1], cover[0])
    c.poly(P(x, y, s, [(-40, -54), (-2, -46), (-2, -18), (-40, -26)]), (250, 244, 226), (222, 210, 180))
    c.poly(P(x, y, s, [(40, -54), (2, -46), (2, -18), (40, -26)]), (244, 236, 214), (214, 200, 168))
    c.glow(x, y - 70 * s, 30 * s, (255, 220, 140), alpha=0.35)


def crystal_ball(c, x, y, s=1.0, color=(150, 110, 255)):
    c.poly(P(x, y, s, [(-26, 0), (-18, -16), (18, -16), (26, 0)]), GOLD[1], GOLD[0])
    c.glow(x, y - 48 * s, 54 * s, color, alpha=0.5)
    c.ellipse(x, y - 48 * s, 34 * s, 34 * s, light(color, 0.45), dark(color, 0.35))
    c.ellipse(x - 10 * s, y - 60 * s, 9 * s, 6 * s, (255, 255, 255), alpha=0.7, blur=1)
    c.line([(x - 12 * s, y - 40 * s), (x + 4 * s, y - 52 * s), (x + 14 * s, y - 36 * s)], (255, 255, 255), 1.2, alpha=0.5)


def potion(c, x, y, s=1.0, color=(120, 240, 150)):
    c.glow(x, y - 30 * s, 40 * s, color, alpha=0.4)
    c.ellipse(x, y - 26 * s, 28 * s, 26 * s, light(color, 0.2), dark(color, 0.3))
    c.rect(x - 8 * s, y - 62 * s, x + 8 * s, y - 40 * s, (230, 240, 240), (170, 190, 195))
    c.rect(x - 10 * s, y - 70 * s, x + 10 * s, y - 62 * s, WOOD[2], WOOD[0], radius=2)
    for dx, dy, r in ((-8, -30, 3), (6, -22, 2.4), (2, -38, 2)):
        c.ellipse(x + dx * s, y + dy * s, r * s, r * s, (255, 255, 255), alpha=0.6)


def hourglass(c, x, y, s=1.0):
    c.rect(x - 30 * s, y - 100 * s, x + 30 * s, y - 92 * s, WOOD[2], WOOD[0], radius=2)
    c.rect(x - 30 * s, y - 8 * s, x + 30 * s, y, WOOD[2], WOOD[0], radius=2)
    c.poly(P(x, y, s, [(-24, -92), (24, -92), (4, -50), (24, -8), (-24, -8), (-4, -50)]), (200, 230, 240), (140, 180, 200), alpha=0.45)
    c.poly(P(x, y, s, [(-18, -86), (18, -86), (2, -56), (-2, -56)]), (244, 210, 120), (210, 160, 70))
    c.poly(P(x, y, s, [(-20, -10), (20, -10), (8, -26), (0, -34), (-8, -26)]), (244, 210, 120), (210, 160, 70))
    c.line([(x, y - 56 * s), (x, y - 34 * s)], (244, 210, 120), 1.2)


def bell(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-34, 0), (-30, -20), (-22, -34), (-18, -62), (-8, -76), (8, -76), (18, -62), (22, -34), (30, -20), (34, 0)]), GOLD[3], GOLD[0], vertical=False)
    c.ellipse(x, y + 2 * s, 8 * s, 6 * s, GOLD[1], GOLD[0])
    c.rect(x - 4 * s, y - 86 * s, x + 4 * s, y - 74 * s, WOOD[1], WOOD[0])


def torch(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-5, 0), (-3, -70), (3, -70), (5, 0)]), WOOD[2], WOOD[0], vertical=False)
    fire(c, x, y - 66 * s, s * 0.7)


def scale_balance(c, x, y, s=1.0):
    c.line([(x, y), (x, y - 100 * s)], GOLD[1], 3)
    c.line([(x - 60 * s, y - 92 * s), (x + 60 * s, y - 92 * s)], GOLD[2], 2.4)
    for dx in (-60, 60):
        c.line([(x + dx * s, y - 92 * s), (x + (dx - 22) * s, y - 46 * s)], GOLD[0], 1.0)
        c.line([(x + dx * s, y - 92 * s), (x + (dx + 22) * s, y - 46 * s)], GOLD[0], 1.0)
        c.poly(P(x + dx * s, y, s, [(-28, -46), (28, -46), (18, -38), (-18, -38)]), GOLD[3], GOLD[1])
    c.poly(P(x, y, s, [(-22, 0), (22, 0), (10, -10), (-10, -10)]), GOLD[1], GOLD[0])


def die(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-40, -40), (0, -62), (40, -40), (0, -18)]), (252, 250, 240), (226, 222, 206))
    c.poly(P(x, y, s, [(-40, -40), (0, -18), (0, 26), (-40, 4)]), (236, 230, 214), (190, 184, 168))
    c.poly(P(x, y, s, [(40, -40), (0, -18), (0, 26), (40, 4)]), (214, 208, 190), (150, 142, 128))
    for dx, dy in ((0, -40), (-14, -46), (14, -34)):
        c.ellipse(x + dx * s, y + dy * s, 3.4 * s, 2.2 * s, (40, 30, 34))
    for dx, dy in ((-24, -6), (-14, 12), (-28, 14)):
        c.ellipse(x + dx * s, y + dy * s, 3 * s, 3.6 * s, (200, 60, 50))
    for dx, dy in ((14, -2), (26, 4), (20, 14)):
        c.ellipse(x + dx * s, y + dy * s, 3 * s, 3.6 * s, (60, 50, 50))


def anvil(c, x, y, s=1.0):
    c.poly(P(x, y, s, [(-36, -46), (46, -46), (30, -36), (14, -34), (14, -16), (24, 0), (-24, 0), (-14, -16), (-14, -34), (-30, -36)]), STEEL[2], STEEL[0])
    c.poly(P(x, y, s, [(-36, -46), (-62, -40), (-36, -34)]), STEEL[1], STEEL[0])
    c.poly(P(x, y, s, [(-36, -46), (46, -46), (46, -42), (-36, -42)]), STEEL[3], None, alpha=0.6)


def hammer(c, x, y, s=1.0, angle=-25):
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)

    def T(px, py):
        return (x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s)

    c.poly([T(-4, -10), T(4, -10), T(4, 70), T(-4, 70)], WOOD[2], WOOD[0], vertical=False)
    c.poly([T(-28, -42), T(28, -42), T(28, -8), T(-28, -8)], STEEL[2], STEEL[0])


def axe(c, x, y, s=1.0, angle=20):
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)

    def T(px, py):
        return (x + (px * ca - py * sa) * s, y + (px * sa + py * ca) * s)

    c.poly([T(-3, -90), T(3, -90), T(3, 20), T(-3, 20)], WOOD[2], WOOD[0], vertical=False)
    c.poly([T(3, -92), T(34, -80), T(38, -52), T(34, -34), T(3, -44)], STEEL[3], STEEL[0])


def bow(c, x, y, s=1.0):
    pts = [(x + math.cos(math.radians(a)) * 40 * s * 0.55, y - 54 * s + math.sin(math.radians(a)) * 54 * s) for a in range(-80, 81, 10)]
    c.line(pts, WOOD[2], 3.4)
    c.line([pts[0], pts[-1]], (230, 224, 200), 0.9)
    c.line([(x - 30 * s, y - 54 * s), (x + 60 * s, y - 54 * s)], WOOD[1], 1.4)
    c.poly(P(x, y, s, [(60, -54), (48, -59), (48, -49)]), STEEL[2], STEEL[0])


def dagger(c, x, y, s=1.0, angle=-35):
    sword(c, x, y, s * 0.7, angle)


def heart(c, x, y, s=1.0, color=(230, 50, 80)):
    c.glow(x, y - 36 * s, 52 * s, color, alpha=0.4)
    pts = []
    for i in range(0, 361, 8):
        t = math.radians(i)
        px = 16 * math.sin(t) ** 3
        py = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((x + px * 2.6 * s, y - 40 * s + py * 2.6 * s))
    c.poly(pts, light(color, 0.35), dark(color, 0.35))
    c.ellipse(x - 16 * s, y - 56 * s, 7 * s, 4 * s, (255, 255, 255), alpha=0.55, blur=1.2)
