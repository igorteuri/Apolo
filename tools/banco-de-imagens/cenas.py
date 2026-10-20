"""As cenas do banco de imagens. Cada cena é uma função que desenha num Canvas; o decorador @cena registra o
identificador (nome do arquivo) e as palavras (português e inglês) que fazem o Apolo escolher essa imagem.
render(id) devolve a imagem 256x256."""
import math
import random

from base import Canvas, blob, dark, light, mix, rgb
import subjects as sj
import criaturas as cr
import objetos as ob

CENAS = {}
PALAVRAS = {}


def cena(ident, pt, en=''):
    def deco(fn):
        CENAS[ident] = fn
        PALAVRAS[ident] = (pt, en)
        return fn
    return deco


# ---------------------------------------------------------------- céus e chãos
def ceu_dia(c, horizon=0.64):
    c.sky(rgb('2f6fd0'), rgb('a9d8f5'), horizon)
    c.clouds(4, (255, 255, 255), alpha=0.7)


def ceu_entardecer(c, horizon=0.64):
    c.sky(rgb('4a3a7a'), rgb('f2a05a'), horizon)
    c.glow(190, 150, 70, (255, 190, 100), alpha=0.5)
    c.clouds(3, (255, 190, 150), alpha=0.5)


def ceu_noite(c, horizon=0.64):
    c.sky(rgb('0e1438'), rgb('34408a'), horizon)
    c.stars(70, 130)


def ceu_tempestade(c, horizon=0.64):
    c.sky(rgb('1e2330'), rgb('5a6478'), horizon)
    c.clouds(7, (90, 98, 116), alpha=0.8, scale=1.4)
    c.clouds(4, (50, 56, 70), alpha=0.7, ymax=70, scale=1.6)


def ceu_inferno(c, horizon=0.6):
    c.sky(rgb('1a0606'), rgb('c4401c'), horizon)
    c.glow(128, 150, 110, (255, 120, 40), alpha=0.5)
    c.clouds(5, (80, 20, 16), alpha=0.7, scale=1.3)


def ceu_deserto(c, horizon=0.6):
    c.sky(rgb('b43a22'), rgb('f5c07a'), horizon)
    c.clouds(3, (230, 120, 80), alpha=0.5)


def ceu_neve(c, horizon=0.62):
    c.sky(rgb('8aa6c8'), rgb('e6eef8'), horizon)
    c.clouds(4, (255, 255, 255), alpha=0.6)


def ceu_magico(c, horizon=0.62):
    c.sky(rgb('2a1f6a'), rgb('b06ad0'), horizon)
    c.stars(50, 120, (255, 230, 255))
    c.glow(128, 120, 90, (180, 130, 255), alpha=0.4)


def ceu_espaco(c, horizon=1.0):
    c.sky(rgb('06061a'), rgb('2a1858'), 1.0)
    c.stars(110, 230)
    c.glow(70, 80, 70, (120, 90, 230), alpha=0.35)


def ceu_interior(c, horizon=1.0):
    c.sky(rgb('1a1210'), rgb('4a3020'), 1.0)


def ceu_verde(c, horizon=0.64):
    c.sky(rgb('1d4a33'), rgb('9ac98a'), horizon)
    c.clouds(2, (230, 250, 230), alpha=0.45)


def chao_grama(c, y=165):
    c.ridge(y, 8, rgb('5aa044'), rgb('2f6b34'), seed=3)
    c.ridge(y + 22, 6, rgb('3f8a3e'), rgb('1f5a2c'), seed=4)


def chao_areia(c, y=165):
    c.ridge(y, 10, rgb('f0c27a'), rgb('c98a4a'), seed=5)
    c.ridge(y + 26, 7, rgb('d9a35c'), rgb('a86c34'), seed=6)


def chao_neve(c, y=165):
    c.ridge(y, 8, rgb('f4f8ff'), rgb('b8c8e0'), seed=7)
    c.ridge(y + 24, 6, rgb('dce6f5'), rgb('9fb2d0'), seed=8)


def chao_escuro(c, y=170):
    c.ridge(y, 7, rgb('3a3a48'), rgb('16161e'), seed=9)
    c.ridge(y + 26, 6, rgb('26262e'), rgb('0c0c12'), seed=10)


def chao_terra(c, y=170):
    c.ridge(y, 7, rgb('6a4a30'), rgb('2e1e12'), seed=9)
    c.ridge(y + 26, 6, rgb('4e361f'), rgb('20140c'), seed=10)


def montanhas_fundo(c, base=150, cor=(rgb('7a8cb4'), rgb('4a5a88'))):
    c.ridge(base - 40, 26, mix(cor[0], (255, 255, 255), 0.3), cor[1], roughness=0.9, seed=1)
    c.ridge(base - 18, 20, cor[0], dark(cor[1], 0.2), roughness=1.1, seed=2)


def mar(c, y=150):
    sj.waves(c, y, 262, rgb('3aa0c8'), rgb('0e3a6a'), 8, 3.2)


def figura(c, fn, ceu, chao, y=210, s=1.4, **kw):
    """Uma criatura ou objeto em primeiro plano, sobre um céu e um chão."""
    ceu(c)
    if chao:
        chao(c, y - 40)
    fn(c, 128, y, s, **kw)


def palco(c, cor_topo, cor_base, brilho=None):
    """Fundo liso com brilho, para objetos 'de museu'."""
    c.sky(cor_topo, cor_base, 1.0)
    if brilho:
        c.glow(128, 120, 100, brilho, alpha=0.4)
    c.ridge(205, 5, mix(cor_base, (0, 0, 0), 0.35), dark(cor_base, 0.6), roughness=0.4, seed=12)


# ================================================================ NATUREZA
@cena('floresta', 'floresta,bosque,pinheiro,mata,forest,woods,pine')
def _floresta(c):
    ceu_verde(c, 0.5)
    c.ridge(112, 14, rgb('3a7d52'), rgb('1d4a33'), seed=1)
    sj.forest_row(c, 150, 9, 0.9, sj.DEEP_GREENS, 1)
    c.mist(150, 40, (200, 240, 210), 0.35)
    c.ridge(190, 6, rgb('2a6244'), rgb('0f2a1c'), seed=2)
    sj.forest_row(c, 214, 6, 1.7, sj.DEEP_GREENS, 2)
    sj.forest_row(c, 236, 3, 2.2, sj.DEEP_GREENS, 3)


@cena('carvalho', 'arvore,carvalho,tree,oak')
def _carvalho(c):
    ceu_dia(c)
    montanhas_fundo(c, 150)
    chao_grama(c, 168)
    sj.oak(c, 128, 218, 1.35)
    sj.flowers(c, 20, 236, 232, 30, [(255, 230, 120), (255, 255, 255), (250, 150, 170)])


@cena('palmeira', 'palmeira,tropical,ilha,selva,island,palm,jungle')
def _palmeira(c):
    c.sky(rgb('2f9ad8'), rgb('f8e0b0'), 0.55)
    c.clouds(3, (255, 255, 255), alpha=0.7)
    mar(c, 136)
    c.ridge(190, 10, rgb('f6dca0'), rgb('d2a860'), seed=5)
    sj.palm(c, 80, 214, 1.5)
    sj.palm(c, 176, 206, 1.2)
    sj.sun(c, 200, 70, 16)


@cena('trevo', 'trevo,sorte,clover,luck')
def _trevo(c):
    c.sky(rgb('1d5a33'), rgb('7ac45a'), 1.0)
    c.glow(128, 120, 110, (220, 255, 190), alpha=0.5)
    for ang in (45, 135, 225, 315):
        a = math.radians(ang)
        cx, cy = 128 + math.cos(a) * 36, 130 + math.sin(a) * 36
        c.poly(blob(cx, cy, 36, 36, c.rng, 0.02, 12), (150, 220, 100), (30, 110, 50))
        c.line([(cx - math.cos(a) * 22, cy - math.sin(a) * 22), (cx + math.cos(a) * 18, cy + math.sin(a) * 18)], (40, 120, 60), 1.4, alpha=0.7)
    c.curve([(128, 130), (140, 180), (126, 226)], (30, 100, 44), 5)


@cena('erva', 'erva,ervas,planta,herb,plant')
def _erva(c):
    c.sky(rgb('3a7d52'), rgb('c8e8b0'), 0.6)
    chao_terra(c, 170)
    r = random.Random(4)
    for i in range(14):
        x = 20 + i * 16
        h = r.uniform(50, 100)
        c.curve([(x, 230), (x + r.uniform(-12, 12), 230 - h / 2), (x + r.uniform(-20, 20), 230 - h)], mix((60, 150, 70), (150, 210, 90), r.random()), 5)
        for k in range(3):
            yy = 230 - h * (0.4 + 0.2 * k)
            c.poly(blob(x + (-10 if k % 2 else 10), yy, 10, 5, r, 0.1, 8), (110, 200, 90), (40, 130, 60))
    for x in (60, 130, 200):
        c.ellipse(x, 140, 6, 6, (250, 230, 120))


@cena('cogumelo', 'cogumelo,fungo,mushroom')
def _cogumelo(c):
    c.sky(rgb('1b2a22'), rgb('3a5a40'), 1.0)
    c.glow(128, 130, 100, (170, 255, 200), alpha=0.3)
    chao_terra(c, 190)
    c.poly(blob(128, 170, 18, 38, c.rng, 0.03, 10), (250, 244, 226), (200, 190, 168))
    c.poly(blob(128, 112, 62, 44, c.rng, 0.03, 14) + [(190, 140), (66, 140)], (230, 70, 60), (140, 28, 28))
    for dx, dy, r in ((-30, 102, 8), (6, 88, 10), (34, 108, 7), (-6, 118, 6)):
        c.ellipse(128 + dx, dy, r, r * 0.8, (255, 250, 240))
    c.ellipse(60, 205, 12, 9, (230, 130, 70))
    c.ellipse(204, 210, 10, 8, (210, 200, 150))


@cena('flor', 'flor,flores,primavera,flower,spring')
def _flor(c):
    ceu_dia(c)
    c.ridge(150, 12, rgb('7cc06a'), rgb('4a8a48'), seed=2)
    chao_grama(c, 176)
    r = random.Random(9)
    for i in range(26):
        x, y = r.uniform(10, 246), r.uniform(190, 244)
        col = r.choice([(250, 110, 150), (255, 220, 90), (255, 255, 255), (170, 130, 250)])
        c.line([(x, y), (x, y + 12)], (40, 120, 50), 1.2)
        for k in range(5):
            a = math.radians(k * 72)
            c.ellipse(x + math.cos(a) * 4, y + math.sin(a) * 4, 3.2, 3.2, light(col, 0.2), col)
        c.ellipse(x, y, 2.4, 2.4, (255, 210, 70))


@cena('mar', 'mar,oceano,onda,ondas,maritimo,sea,ocean,wave')
def _mar(c):
    ceu_dia(c, 0.45)
    mar(c, 110)
    sj.waves(c, 150, 262, rgb('2a86b8'), rgb('0a2a5a'), 6, 5)
    c.poly([(0, 256), (0, 190), (40, 140), (90, 120), (140, 130), (176, 170), (150, 150), (120, 160), (200, 200), (256, 226), (256, 256)], rgb('4ab8d8'), rgb('0e3a6a'))
    c.poly([(40, 140), (90, 120), (140, 130), (176, 170), (150, 150), (110, 146), (70, 160)], (245, 252, 255), (170, 220, 240), alpha=0.9)


@cena('montanha', 'montanha,montanhas,pico,serra,mountain')
def _montanha(c):
    ceu_dia(c)
    sj.mountain(c, 70, 190, 150, 120, seed=1)
    sj.mountain(c, 180, 190, 170, 150, seed=2)
    sj.mountain(c, 128, 206, 210, 170, seed=3)
    chao_grama(c, 210)
    sj.forest_row(c, 236, 5, 1.0, sj.DEEP_GREENS, 4)


@cena('vulcao', 'vulcao,lava,volcano')
def _vulcao(c):
    ceu_inferno(c)
    c.glow(128, 90, 80, (255, 140, 40), alpha=0.6)
    c.poly([(30, 220), (100, 90), (128, 100), (156, 90), (226, 220)], rgb('4a2a26'), rgb('1a0c0c'))
    sj.fire(c, 128, 100, 1.1)
    c.poly([(116, 100), (104, 170), (120, 220), (140, 220), (146, 160), (140, 100)], (255, 200, 80), (230, 70, 20), alpha=0.9)
    c.rect(0, 218, 256, 256, rgb('2a0c0c'), rgb('100404'))
    c.rect(0, 226, 256, 240, (240, 90, 30), (190, 40, 20), alpha=0.8, blur=3)


@cena('deserto', 'deserto,dunas,areia,desert,sand')
def _deserto(c):
    ceu_deserto(c)
    sj.sun(c, 190, 96, 20, (255, 244, 200))
    c.ridge(150, 14, rgb('e8a860'), rgb('b8742f'), roughness=0.7, seed=21)
    c.ridge(186, 14, rgb('f0c27a'), rgb('c98a4a'), roughness=0.7, seed=22)
    c.ridge(222, 12, rgb('f6d490'), rgb('d9a35c'), roughness=0.7, seed=23)
    sj.palm(c, 60, 200, 0.7)


@cena('neve', 'neve,gelo,inverno,frio,snow,ice,winter,cold')
def _neve(c):
    ceu_neve(c)
    sj.mountain(c, 70, 170, 160, 100, seed=4)
    chao_neve(c, 172)
    sj.forest_row(c, 214, 5, 1.5, sj.DEEP_GREENS, 5, snow=True)
    sj.forest_row(c, 240, 3, 2.0, sj.DEEP_GREENS, 6, snow=True)
    r = random.Random(3)
    for _ in range(60):
        c.ellipse(r.uniform(0, 256), r.uniform(0, 256), 1.4, 1.4, (255, 255, 255), alpha=0.9)


@cena('fogueira', 'fogo,chama,incendio,fogueira,fire,flame,burn')
def _fogueira(c):
    ceu_noite(c, 0.7)
    c.ridge(160, 10, rgb('1e2a52'), rgb('0e1430'), seed=2)
    chao_terra(c, 190)
    sj.logs(c, 128, 214, 1.2)
    sj.fire(c, 128, 212, 1.4)
    r = random.Random(7)
    for _ in range(22):
        c.ellipse(r.uniform(90, 166), r.uniform(60, 190), 1.4, 1.4, (255, 200, 90), alpha=0.9)


@cena('agua', 'agua,gota,rio,lago,water,river,lake')
def _agua(c):
    ceu_dia(c, 0.45)
    montanhas_fundo(c, 130)
    c.rect(-4, 124, 260, 256, rgb('78c8d8'), rgb('2a7a9a'))
    for i in range(7):
        y = 140 + i * 15
        c.line([(x, y + math.sin(x / 12 + i) * 2) for x in range(0, 257, 8)], (220, 245, 250), 1.2, alpha=0.5)
    c.poly([(128, 110), (112, 146), (112, 160), (144, 160), (144, 146)], (150, 210, 250), (60, 130, 220))
    c.ellipse(128, 158, 18, 16, (150, 210, 250), (60, 130, 220))


@cena('raio', 'raio,relampago,tempestade,trovao,storm,lightning,thunder')
def _raio(c):
    ceu_tempestade(c, 0.7)
    mar(c, 170)
    sj.lightning(c, 128, 180, 1.3)
    c.ellipse(128, 180, 40, 6, (200, 215, 255), alpha=0.6, blur=3)


@cena('chuva', 'chuva,garoa,rain')
def _chuva(c):
    ceu_tempestade(c, 0.5)
    chao_escuro(c, 190)
    r = random.Random(5)
    for _ in range(90):
        x, y = r.uniform(0, 256), r.uniform(60, 250)
        c.line([(x, y), (x - 4, y + 14)], (180, 200, 240), 1.0, alpha=0.7)
    sj.pine(c, 70, 220, 1.4, sj.DEEP_GREENS)
    sj.pine(c, 190, 214, 1.2, sj.DEEP_GREENS)


@cena('lua', 'lua,noite,noturno,moon,night')
def _lua(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 128, 90, 44)
    c.ridge(170, 18, rgb('1e2a52'), rgb('0e1430'), seed=2)
    sj.forest_row(c, 212, 8, 1.2, [rgb('0e1c2a'), rgb('142638'), rgb('1a3044'), rgb('2a4a5a')], 3)


@cena('sol', 'sol,dia,ensolarado,sun,day')
def _sol(c):
    ceu_dia(c)
    sj.sun(c, 128, 90, 32)
    montanhas_fundo(c, 170)
    chao_grama(c, 190)


@cena('estrela', 'estrela,estrelas,star')
def _estrela(c):
    c.sky(rgb('0e1438'), rgb('34408a'), 1.0)
    c.stars(100, 240)
    pts = []
    for i in range(10):
        a = math.radians(i * 36 - 90)
        r = 62 if i % 2 == 0 else 26
        pts.append((128 + math.cos(a) * r, 120 + math.sin(a) * r))
    c.glow(128, 120, 100, (255, 240, 150), alpha=0.6)
    c.poly(pts, (255, 250, 200), (250, 190, 60))


@cena('heroi', 'heroi,epico,gloria,destino,hero,epic,glory,destiny')
def _heroi(c):
    ceu_entardecer(c)
    sj.sun(c, 128, 120, 38, (255, 230, 170))
    c.ridge(190, 14, rgb('3a2f66'), rgb('160f30'), roughness=1.0, seed=2)
    c.poly([(60, 256), (86, 200), (130, 182), (170, 200), (196, 256)], rgb('2a2238'), rgb('0e0a18'))
    ob.hero(c, 128, 196, 1.3)


@cena('galaxia', 'galaxia,espaco,cosmos,galaxy,space')
def _galaxia(c):
    ceu_espaco(c)
    for i in range(5):
        c.ellipse(128, 128, 110 - i * 18, 40 - i * 6, mix((120, 90, 230), (255, 200, 250), i / 4.0), alpha=0.25, blur=6)
    c.glow(128, 128, 36, (255, 240, 220), alpha=0.9)
    sj.moon(c, 206, 200, 14, (210, 190, 170))


@cena('vento', 'vento,wind')
def _vento(c):
    ceu_dia(c, 0.55)
    chao_grama(c, 170)
    for i, y in enumerate((70, 100, 130)):
        c.curve([(30, y), (90, y - 14), (150, y + 6), (200 - i * 10, y - 8), (230 - i * 10, y + 8)], (255, 255, 255), 4, alpha=0.8)
        c.curve([(180 - i * 10, y - 10), (212, y - 22), (230, y - 8)], (255, 255, 255), 3, alpha=0.8)
    r = random.Random(2)
    for i in range(24):
        x = 10 + i * 10.5
        c.curve([(x, 250), (x + 6, 226), (x + 22, 214)], mix((60, 150, 70), (150, 210, 90), r.random()), 3)


@cena('mundo', 'mundo,terra,world,earth')
def _mundo(c):
    ceu_espaco(c)
    c.glow(128, 128, 90, (120, 200, 255), alpha=0.5)
    c.ellipse(128, 128, 62, 62, rgb('4aa0e0'), rgb('0e3a8a'))
    for dx, dy, rx, ry in ((-20, -10, 24, 16), (24, 20, 20, 24), (-4, 36, 16, 10), (30, -30, 14, 10)):
        c.poly(blob(128 + dx, 128 + dy, rx, ry, c.rng, 0.2, 10), (110, 190, 90), (50, 120, 60))
    c.ellipse(108, 104, 14, 8, (255, 255, 255), alpha=0.35, blur=3)


# ================================================================ LUGARES
@cena('castelo', 'castelo,reino,fortaleza,castle,kingdom,fortress')
def _castelo(c):
    ceu_entardecer(c)
    montanhas_fundo(c, 150, (rgb('6a5a98'), rgb('3a2f66')))
    chao_grama(c, 176)
    sj.castle(c, 128, 200, 1.35)


@cena('casa', 'casa,lar,house,home')
def _casa(c):
    ceu_dia(c)
    chao_grama(c, 170)
    sj.house(c, 128, 214, 1.8, lit=False)
    sj.oak(c, 38, 214, 0.8)
    sj.flowers(c, 70, 190, 230, 14, [(250, 110, 150), (255, 220, 90)])


@cena('vila', 'vila,aldeia,village')
def _vila(c):
    ceu_entardecer(c)
    chao_grama(c, 166)
    sj.house(c, 60, 190, 0.8)
    sj.house(c, 200, 196, 0.9, wall=[rgb('6a6a7a'), rgb('9a9aa8'), rgb('c4c4d0')])
    sj.house(c, 128, 220, 1.2)
    sj.oak(c, 20, 236, 0.7)


@cena('igreja', 'igreja,templo,catedral,church,temple,cathedral')
def _igreja(c):
    ceu_entardecer(c)
    chao_grama(c, 176)
    sj.church(c, 128, 214, 1.4)


@cena('taverna', 'taverna,bar,cerveja,estalagem,pousada,tavern,inn,pub,beer')
def _taverna(c):
    ceu_noite(c, 0.7)
    c.ridge(160, 14, rgb('1e2a52'), rgb('0e1430'), seed=2)
    c.ridge(190, 8, rgb('2f4a2f'), rgb('122212'), seed=3)
    sj.tavern(c, 118, 214, 1.35)
    sj.moon(c, 206, 52, 18)


@cena('acampamento', 'acampamento,tenda,camp,tent,camping')
def _acampamento(c):
    ceu_noite(c, 0.7)
    c.ridge(160, 14, rgb('1e2a52'), rgb('0e1430'), seed=2)
    chao_terra(c, 188)
    sj.tent(c, 92, 214, 1.4)
    sj.logs(c, 184, 224, 0.8)
    sj.fire(c, 184, 222, 0.9)


@cena('caverna', 'caverna,buraco,cave,hole')
def _caverna(c):
    ceu_dia(c, 0.5)
    c.rect(0, 0, 256, 256, rgb('2a2a36'), rgb('0e0e14'), alpha=0.0)
    sj.cave(c, 128, 220, 1.5)
    chao_terra(c, 226)
    sj.rocks(c, 128, 246, 1.2)


@cena('cidade', 'cidade,urbano,city,urban')
def _cidade(c):
    c.sky(rgb('2a3a7a'), rgb('f0a870'), 0.8)
    r = random.Random(8)
    x = -10
    while x < 260:
        w, h = r.uniform(26, 44), r.uniform(60, 150)
        col = mix((70, 76, 104), (30, 32, 52), r.random())
        c.rect(x, 230 - h, x + w, 256, light(col, 0.15), col)
        for yy in range(int(230 - h + 8), 240, 14):
            for xx in range(int(x + 6), int(x + w - 6), 10):
                if r.random() < 0.5:
                    c.rect(xx, yy, xx + 4, yy + 7, (255, 220, 130))
        x += w + 2


@cena('fabrica', 'fabrica,industria,factory,industry')
def _fabrica(c):
    c.sky(rgb('3a3a46'), rgb('b08a6a'), 0.8)
    c.rect(30, 150, 226, 240, rgb('6a5a52'), rgb('3a302c'))
    for x in (60, 110, 160, 200):
        c.rect(x, 90 + (x % 3) * 14, x + 18, 150, rgb('8a6a58'), rgb('4a382e'))
        for k in range(3):
            c.ellipse(x + 9 + k * 6, 76 + (x % 3) * 14 - k * 22, 14 + k * 4, 9 + k * 2, (120, 120, 130), alpha=0.5, blur=4)
    for x in range(44, 220, 28):
        c.rect(x, 176, x + 14, 192, (255, 190, 90))
    chao_escuro(c, 232)


@cena('navio', 'navio,porto,ship,port,cruzeiro')
def _navio(c):
    ceu_entardecer(c, 0.55)
    mar(c, 140)
    sj.ship(c, 128, 190, 1.8)
    sj.waves(c, 196, 262, rgb('2a86b8'), rgb('0a2a5a'), 4, 4)


@cena('barco', 'barco,veleiro,boat,sail')
def _barco(c):
    ceu_dia(c, 0.55)
    mar(c, 140)
    sj.ship(c, 128, 196, 1.1, hull=[rgb('2a3a5a'), rgb('4a5a8a'), rgb('7a8ab8'), rgb('9aa8d0')])
    sj.waves(c, 200, 262, rgb('2a86b8'), rgb('0a2a5a'), 3, 4)


@cena('ancora', 'ancora,anchor')
def _ancora(c):
    c.sky(rgb('0a3a6a'), rgb('3aa0c8'), 1.0)
    for i in range(18):
        c.ellipse(random.Random(i).uniform(10, 246), random.Random(i + 40).uniform(20, 240), 4, 4, (220, 245, 255), alpha=0.35)
    ob.anchor(c, 128, 210, 1.5)


@cena('pirata', 'pirata,bandeira,pirate,flag')
def _pirata(c):
    ceu_tempestade(c, 0.7)
    mar(c, 190)
    ob.flag_pirate(c, 150, 210, 1.3)


@cena('estatua', 'estatua,ruina,ruinas,antigo,antiga,statue,ruins,ancient')
def _estatua(c):
    ceu_entardecer(c)
    chao_areia(c, 176)
    ob.column(c, 54, 214, 1.0)
    ob.column(c, 204, 220, 1.1, broken=True)
    ob.statue(c, 128, 220, 1.4)


@cena('vaso', 'vaso,antiguidade,grego,greco,roma,romano,egito,egipcio,greek,roman,egypt,vase')
def _vaso(c):
    palco(c, rgb('3a2a4a'), rgb('9a6a5a'), (255, 200, 150))
    ob.amphora(c, 128, 218, 1.7)


@cena('porta', 'porta,entrada,door,entrance')
def _porta(c):
    c.sky(rgb('1a1210'), rgb('4a3a30'), 1.0)
    c.rect(40, 0, 216, 256, STONE_BG[0], STONE_BG[1])
    sj.door(c, 128, 236, 1.7)


STONE_BG = (rgb('8d93a2'), rgb('4a4f5a'))


@cena('portal', 'portal,vortice,teletransporte,dimensao,ciclone,vortex,teleport,dimension')
def _portal(c):
    ceu_magico(c)
    chao_escuro(c, 184)
    sj.portal(c, 128, 222, 1.7)


# ================================================================ CRIATURAS
@cena('dragao', 'dragao,dragoes,dragon')
def _dragao(c):
    ceu_inferno(c)
    c.ridge(170, 24, rgb('3a1414'), rgb('160606'), roughness=1.2, seed=2)
    c.rect(0, 206, 256, 256, rgb('2a0c0c'), rgb('100404'))
    cr.dragon(c, 100, 222, 1.45, body=(70, 34, 40), wing=(150, 50, 40), fire_breath=True)


@cena('fantasma', 'fantasma,assombrado,espirito,assombracao,ghost,haunted,spirit')
def _fantasma(c):
    figura(c, cr.ghost, ceu_noite, chao_escuro, 212, 1.7)


@cena('caveira', 'caveira,morte,esqueleto,cripta,morto,skull,death,skeleton,crypt,dead')
def _caveira(c):
    ceu_interior(c)
    c.glow(128, 130, 110, (150, 70, 200), alpha=0.35)
    chao_escuro(c, 190)
    sj.skull(c, 128, 210, 1.8)


@cena('zumbi', 'zumbi,zumbis,zombie')
def _zumbi(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 200, 60, 20, (200, 230, 190))
    chao_escuro(c, 180)
    ob.tombstone(c, 50, 218, 0.8)
    cr.zombie(c, 140, 224, 1.5)


@cena('vampiro', 'vampiro,vampire')
def _vampiro(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 128, 92, 46, (250, 230, 210))
    chao_escuro(c, 190)
    cr.vampire(c, 128, 226, 1.4)


@cena('mago', 'mago,maga,bruxa,bruxo,feiticeiro,feiticeira,magia,magico,wizard,mage,witch,magic,sorcerer')
def _mago(c):
    figura(c, cr.wizard, ceu_magico, chao_escuro, 214, 1.35)


@cena('fada', 'fada,fae,fairy')
def _fada(c):
    ceu_verde(c, 0.6)
    c.glow(128, 130, 110, (255, 230, 255), alpha=0.35)
    sj.forest_row(c, 190, 6, 1.2, sj.DEEP_GREENS, 9)
    cr.fairy(c, 128, 190, 1.9)


@cena('elfo', 'elfo,elfos,elf,elves')
def _elfo(c):
    ceu_verde(c, 0.6)
    sj.forest_row(c, 190, 7, 1.4, sj.DEEP_GREENS, 10)
    chao_grama(c, 190)
    cr.elf(c, 128, 224, 1.5)


@cena('genio', 'genio,djinn,genie')
def _genio(c):
    figura(c, cr.genie, ceu_magico, chao_areia, 222, 1.3)


@cena('troll', 'troll,golem,gigante,giant')
def _troll(c):
    ceu_entardecer(c)
    sj.mountain(c, 70, 200, 170, 110, seed=7)
    chao_terra(c, 190)
    cr.giant(c, 140, 232, 1.2)


@cena('alien', 'alien,alienigena,invasor,invader')
def _alien(c):
    figura(c, cr.alien, ceu_espaco, None, 224, 1.7)
    c.ridge(210, 10, rgb('2a4a5a'), rgb('0a1a24'), seed=3)
    cr.alien(c, 128, 226, 1.7)


@cena('demonio', 'demonio,demonios,diabo,inferno,infernal,demon,devil,hell')
def _demonio(c):
    figura(c, cr.demon, ceu_inferno, chao_escuro, 220, 1.4)


@cena('anjo', 'anjo,celestial,divino,ceu,angel,divine,heaven')
def _anjo(c):
    c.sky(rgb('7ab0f0'), rgb('fff4d0'), 1.0)
    c.clouds(6, (255, 255, 255), ymin=140, ymax=250, alpha=0.8)
    cr.angel(c, 128, 200, 1.5)


@cena('ogro', 'ogro,oni,ogre')
def _ogro(c):
    ceu_verde(c)
    chao_terra(c, 190)
    cr.ogre(c, 128, 230, 1.2)


@cena('goblin', 'goblin,goblins,duende')
def _goblin(c):
    ceu_interior(c)
    c.glow(128, 150, 100, (255, 170, 70), alpha=0.3)
    chao_terra(c, 196)
    cr.goblin(c, 128, 226, 2.1)


@cena('lobo', 'lobo,matilha,lobisomem,wolf,werewolf,pack')
def _lobo(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 150, 80, 40)
    c.ridge(170, 16, rgb('1e2a52'), rgb('0e1430'), seed=2)
    chao_escuro(c, 196)
    cr.wolf(c, 120, 224, 1.5)


@cena('cobra', 'cobra,serpente,snake,serpent')
def _cobra(c):
    ceu_verde(c)
    chao_terra(c, 190)
    cr.snake(c, 128, 226, 1.5)


@cena('aranha', 'aranha,spider')
def _aranha(c):
    ceu_interior(c)
    c.glow(128, 140, 100, (170, 120, 255), alpha=0.25)
    cr.spider(c, 128, 230, 1.6)


@cena('morcego', 'morcego,bat')
def _morcego(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 128, 110, 52, (250, 236, 210))
    cr.bat(c, 128, 150, 1.6)


@cena('aguia', 'aguia,gaviao,eagle,hawk')
def _aguia(c):
    ceu_dia(c, 0.7)
    sj.mountain(c, 70, 220, 190, 140, seed=5)
    cr.eagle(c, 150, 170, 1.5)


@cena('coruja', 'coruja,owl')
def _coruja(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 190, 70, 26)
    cr.owl(c, 128, 230, 1.8)


@cena('cavalo', 'cavalo,cavalaria,horse,cavalry')
def _cavalo(c):
    ceu_entardecer(c)
    chao_grama(c, 176)
    cr.horse(c, 120, 226, 1.5)


@cena('kraken', 'polvo,kraken,tentaculo,octopus,tentacle')
def _kraken(c):
    ceu_tempestade(c, 0.55)
    mar(c, 150)
    cr.kraken(c, 128, 214, 1.1)
    sj.waves(c, 190, 262, rgb('2a86b8'), rgb('0a2a5a'), 3, 4)


@cena('tubarao', 'tubarao,shark')
def _tubarao(c):
    c.sky(rgb('3aa0c8'), rgb('0a2a5a'), 1.0)
    c.glow(128, 40, 90, (200, 240, 255), alpha=0.4)
    cr.shark(c, 128, 190, 1.6)


@cena('urso', 'urso,bear')
def _urso(c):
    ceu_verde(c, 0.6)
    sj.forest_row(c, 190, 6, 1.2, sj.DEEP_GREENS, 11)
    chao_grama(c, 190)
    cr.bear(c, 128, 228, 1.6)


@cena('rato', 'rato,ratos,esgoto,rat,sewer')
def _rato(c):
    ceu_interior(c)
    chao_escuro(c, 196)
    cr.rat(c, 128, 224, 2.0)


@cena('escorpiao', 'escorpiao,scorpion')
def _escorpiao(c):
    ceu_deserto(c)
    chao_areia(c, 176)
    cr.scorpion(c, 128, 226, 1.7)


@cena('javali', 'javali,porco,boar,pig')
def _javali(c):
    ceu_verde(c, 0.6)
    chao_terra(c, 190)
    cr.boar(c, 128, 226, 1.7)


@cena('cervo', 'cervo,veado,deer')
def _cervo(c):
    ceu_entardecer(c)
    sj.forest_row(c, 190, 6, 1.4, sj.DEEP_GREENS, 12)
    chao_grama(c, 190)
    cr.deer(c, 118, 226, 1.5)


@cena('unicornio', 'unicornio,unicorn')
def _unicornio(c):
    ceu_magico(c)
    chao_grama(c, 184)
    cr.unicorn(c, 118, 226, 1.4)


@cena('crocodilo', 'crocodilo,jacare,pantano,crocodile,alligator,swamp')
def _crocodilo(c):
    ceu_verde(c, 0.5)
    c.rect(0, 130, 256, 256, rgb('4a6a3a'), rgb('1a2a1a'))
    for x in (30, 90, 200):
        sj.palm(c, x, 160, 0.5)
    cr.crocodile(c, 128, 214, 1.5)


@cena('abelha', 'abelha,bee')
def _abelha(c):
    ceu_dia(c, 0.6)
    chao_grama(c, 190)
    cr.bee(c, 128, 200, 2.2)


@cena('pata', 'pata,rastro,cacada,caca,paw,track,hunt,hunting')
def _pata(c):
    ceu_verde(c, 0.4)
    chao_terra(c, 120)
    cr.paw(c, 128, 220, 1.6, (30, 24, 22))
    cr.paw(c, 60, 140, 0.8, (36, 28, 26))


@cena('osso', 'osso,ossos,bone,bones')
def _osso(c):
    ceu_deserto(c)
    chao_areia(c, 176)
    cr.bones(c, 128, 226, 1.8)


# ================================================================ ITENS
@cena('batalha', 'batalha,luta,combate,guerra,duelo,espada,battle,fight,combat,war,duel,sword')
def _batalha(c):
    ceu_inferno(c)
    chao_escuro(c, 190)
    ob.swords_cross(c, 128, 220, 1.4)


@cena('adaga', 'adaga,faca,punhal,dagger,knife')
def _adaga(c):
    palco(c, rgb('1a1a2a'), rgb('4a4a6a'), (200, 200, 255))
    sj.dagger(c, 128, 216, 2.2, -30)


@cena('arco', 'arco,flecha,arqueiro,bow,arrow,archer')
def _arco(c):
    ceu_verde(c)
    chao_grama(c, 190)
    ob.target(c, 190, 226, 0.8)
    sj.bow(c, 80, 226, 1.4)


@cena('escudo', 'escudo,defesa,shield,defense')
def _escudo(c):
    palco(c, rgb('1a2a4a'), rgb('4a6aa0'), (200, 220, 255))
    sj.shield(c, 128, 190, 2.1)


@cena('machado', 'machado,axe')
def _machado(c):
    palco(c, rgb('2a1a1a'), rgb('7a4a3a'), (255, 190, 150))
    sj.axe(c, 128, 224, 1.7, 14)


@cena('martelo', 'martelo,ferreiro,forja,hammer,blacksmith,forge')
def _martelo(c):
    ceu_interior(c)
    c.glow(128, 200, 100, (255, 140, 50), alpha=0.5)
    chao_escuro(c, 200)
    sj.anvil(c, 100, 214, 1.6)
    sj.hammer(c, 170, 226, 1.6, -25)


@cena('coroa', 'coroa,rei,rainha,realeza,crown,king,queen,royal')
def _coroa(c):
    palco(c, rgb('3a1a5a'), rgb('9a4a8a'), (255, 220, 150))
    sj.crown(c, 128, 196, 2.2)


@cena('gema', 'gema,joia,diamante,gem,jewel,diamond')
def _gema(c):
    c.sky(rgb('140a2e'), rgb('3a1f6a'), 1.0)
    c.stars(40, 220, (255, 220, 255))
    sj.gem(c, 128, 214, 2.1)


@cena('tesouro', 'tesouro,dinheiro,ouro,saque,treasure,gold,money,loot')
def _tesouro(c):
    ceu_interior(c)
    chao_terra(c, 190)
    sj.chest(c, 128, 224, 1.9)


@cena('chave', 'chave,key')
def _chave(c):
    palco(c, rgb('1a1a2a'), rgb('6a5a3a'), (255, 230, 150))
    sj.key(c, 128, 216, 1.7)


@cena('cadeado', 'trancado,cadeado,prisao,locked,lock,prison')
def _cadeado(c):
    palco(c, rgb('1a1a24'), rgb('4a4a5a'), (200, 200, 230))
    ob.padlock(c, 128, 214, 1.8)


@cena('pergaminho', 'pergaminho,carta,scroll,letter')
def _pergaminho(c):
    palco(c, rgb('3a2a1a'), rgb('9a7a4a'), (255, 220, 160))
    sj.scroll(c, 128, 216, 1.9)


@cena('mapa', 'mapa,viagem,explorar,map,travel,explore')
def _mapa(c):
    palco(c, rgb('3a2a1a'), rgb('8a6a3a'), (255, 220, 160))
    ob.map_(c, 128, 220, 1.9)


@cena('livro', 'livro,biblioteca,grimorio,historia,lore,book,library,grimoire,story')
def _livro(c):
    palco(c, rgb('1a1030'), rgb('5a3a8a'), (255, 220, 160))
    sj.book(c, 128, 210, 2.0)


@cena('cristal', 'cristal,profecia,oraculo,vidente,crystal,prophecy,oracle')
def _cristal(c):
    ceu_magico(c)
    chao_escuro(c, 190)
    sj.crystal_ball(c, 128, 218, 2.2)


@cena('amuleto', 'amuleto,talisma,amulet,talisman')
def _amuleto(c):
    palco(c, rgb('0a2a2a'), rgb('2a6a6a'), (140, 255, 210))
    ob.amulet(c, 128, 190, 2.0)


@cena('tocha', 'lanterna,tocha,luz,torch,lantern,light')
def _tocha(c):
    ceu_interior(c)
    c.rect(0, 150, 256, 256, STONE_BG[1], dark(STONE_BG[1], 0.6), alpha=0.8)
    sj.torch(c, 100, 230, 1.6)
    ob.lamp_oil(c, 180, 230, 1.1)


@cena('varinha', 'varinha,wand')
def _varinha(c):
    ceu_magico(c)
    ob.wand(c, 128, 220, 1.9)


@cena('alquimia', 'alquimia,pocao,veneno,laboratorio,alchemy,potion,poison,lab')
def _alquimia(c):
    palco(c, rgb('0a2a1a'), rgb('2a6a4a'), (140, 255, 170))
    sj.potion(c, 90, 216, 1.7)
    sj.potion(c, 170, 220, 1.2, (250, 120, 200))


@cena('bussola', 'bussola,navegacao,compass,navigation')
def _bussola(c):
    palco(c, rgb('1a2a3a'), rgb('5a7a8a'), (255, 240, 190))
    ob.compass(c, 128, 224, 1.8)


@cena('tempo', 'tempo,espera,ampulheta,time,wait,hourglass')
def _tempo(c):
    palco(c, rgb('2a1a1a'), rgb('8a5a3a'), (255, 220, 160))
    sj.hourglass(c, 128, 226, 1.7)


@cena('sino', 'sino,alarme,alerta,aviso,bell,alarm,alert')
def _sino(c):
    ceu_entardecer(c)
    ob.bell_alert(c, 128, 220, 1.8)


@cena('alvo', 'alvo,missao,objetivo,busca,target,mission,goal,quest')
def _alvo(c):
    ceu_dia(c)
    chao_grama(c, 190)
    ob.target(c, 128, 230, 1.7)


@cena('justica', 'justica,tribunal,julgamento,balanca,justice,court,trial')
def _justica(c):
    palco(c, rgb('1a1a3a'), rgb('5a5a9a'), (255, 240, 190))
    sj.scale_balance(c, 128, 224, 1.7)


@cena('cura', 'cura,curandeiro,medico,hospital,heal,healing,doctor')
def _cura(c):
    c.sky(rgb('1a5a4a'), rgb('7ae0b0'), 1.0)
    c.glow(128, 130, 110, (220, 255, 230), alpha=0.5)
    c.rect(100, 70, 156, 190, (250, 250, 250), (200, 235, 220), radius=6)
    c.rect(68, 102, 188, 158, (250, 250, 250), (200, 235, 220), radius=6)
    c.rect(112, 80, 144, 180, (80, 200, 130), (30, 140, 90), radius=4)
    c.rect(78, 112, 178, 148, (80, 200, 130), (30, 140, 90), radius=4)


@cena('dado', 'dado,dados,jogo,dice,game')
def _dado(c):
    palco(c, rgb('1a3a2a'), rgb('3a8a5a'), (200, 255, 210))
    sj.die(c, 128, 210, 2.1)


# ================================================================ MÚSICA E SOM
@cena('trombeta', 'trombeta,fanfarra,corneta,arauto,trumpet,fanfare,herald')
def _trombeta(c):
    ceu_entardecer(c)
    sj.sun(c, 128, 120, 30, (255, 230, 170))
    ob.trumpet(c, 120, 210, 1.7)


@cena('guitarra', 'guitarra,rock,metal,guitar')
def _guitarra(c):
    ceu_inferno(c)
    c.glow(128, 150, 100, (255, 100, 50), alpha=0.4)
    ob.guitar(c, 128, 222, 1.2)


@cena('violino', 'violino,celta,celtico,folk,violin,celtic')
def _violino(c):
    ceu_verde(c, 0.6)
    chao_grama(c, 190)
    ob.violin(c, 128, 226, 1.5)


@cena('tambor', 'tambor,tambores,ritmo,tribal,drum,drums,rhythm')
def _tambor(c):
    ceu_deserto(c)
    chao_areia(c, 180)
    ob.drum(c, 128, 224, 1.9)


@cena('piano', 'piano,teclado,keyboard')
def _piano(c):
    palco(c, rgb('1a1020'), rgb('5a3a6a'), (255, 220, 190))
    ob.piano(c, 128, 220, 1.5)


@cena('bardo', 'cantar,canto,voz,bardo,sing,voice,bard')
def _bardo(c):
    ceu_entardecer(c)
    chao_grama(c, 186)
    ob.guitar(c, 150, 226, 0.9, (150, 90, 40))
    ob.note(c, 76, 190, 1.4)


@cena('musica', 'musica,melodia,trilha,tema,music,melody,soundtrack,theme')
def _musica(c):
    c.sky(rgb('2a1a5a'), rgb('9a5aa0'), 1.0)
    c.stars(40, 200, (255, 230, 255))
    ob.note(c, 128, 220, 2.2)


@cena('som', 'som,volume,audio,ambiente,ambiencia,sound,ambience,ambient')
def _som(c):
    palco(c, rgb('0a1a3a'), rgb('2a5a9a'), (150, 220, 255))
    ob.speaker(c, 100, 226, 1.5)


@cena('drama', 'drama,teatro,mascara,intriga,disfarce,theater,mask,intrigue,disguise')
def _drama(c):
    palco(c, rgb('3a0a1a'), rgb('9a2a4a'), (255, 200, 160))
    ob.masks(c, 128, 224, 1.7)


@cena('cinema', 'cinema,cena,filme,abertura,intro,scene,movie,opening')
def _cinema(c):
    palco(c, rgb('1a1a2a'), rgb('4a4a6a'), (255, 240, 200))
    ob.clapper(c, 128, 220, 1.6)


# ================================================================ SENTIMENTOS E SITUAÇÕES
@cena('dormir', 'dormir,sono,descanso,calmo,calma,sleep,rest,calm')
def _dormir(c):
    ceu_noite(c, 0.8)
    sj.moon(c, 200, 60, 20, crescent=True)
    c.rect(0, 150, 256, 256, rgb('2a2a4a'), rgb('0e0e24'))
    ob.bed(c, 128, 226, 1.6)


@cena('festa', 'festa,celebracao,comemoracao,party,celebration')
def _festa(c):
    c.sky(rgb('3a1a6a'), rgb('d06aa0'), 1.0)
    ob.party(c, 128, 230, 1.5)


@cena('vitoria', 'vitoria,vencer,trofeu,campeao,victory,win,trophy,champion')
def _vitoria(c):
    ceu_entardecer(c)
    sj.sun(c, 128, 120, 40, (255, 240, 190))
    ob.trophy(c, 128, 226, 1.6)


@cena('triste', 'triste,tristeza,melancolia,luto,funeral,sad,sorrow,mourning')
def _triste(c):
    ceu_tempestade(c, 0.6)
    chao_escuro(c, 190)
    ob.tombstone(c, 128, 226, 1.5)
    ob.tear_cloud(c, 128, 150, 1.1)


@cena('medo', 'medo,horror,terror,susto,suspense,fear,scary')
def _medo(c):
    ceu_interior(c)
    sj.forest_row(c, 230, 8, 1.6, [rgb('0a0a12'), rgb('10101a'), rgb('16161e'), rgb('1e1e28')], 13)
    ob.eyes_in_dark(c, 128, 190, 1.4)


@cena('raiva', 'raiva,furia,ira,anger,angry,fury,rage')
def _raiva(c):
    ceu_inferno(c)
    ob.anger_face(c, 128, 226, 1.4)


@cena('amor', 'amor,romance,romantico,coracao,love,heart')
def _amor(c):
    c.sky(rgb('5a1a4a'), rgb('f090b0'), 1.0)
    c.stars(30, 200, (255, 230, 240))
    sj.heart(c, 128, 226, 2.0)


@cena('conversa', 'conversa,dialogo,fofoca,talk,conversation,dialog')
def _conversa(c):
    palco(c, rgb('1a2a4a'), rgb('4a7aaa'), (200, 230, 255))
    ob.speech(c, 128, 220, 1.7)


@cena('caminhada', 'caminhada,estrada,caminho,jornada,travessia,walk,road,path,journey')
def _caminhada(c):
    ceu_entardecer(c)
    montanhas_fundo(c, 150, (rgb('6a5a98'), rgb('3a2f66')))
    chao_grama(c, 160)
    c.poly([(110, 160), (146, 160), (230, 256), (30, 256)], rgb('c8a878'), rgb('7a5a38'))
    ob.traveler(c, 128, 224, 1.1)


@cena('correr', 'correr,fuga,fugir,corrida,run,escape,chase')
def _correr(c):
    ceu_tempestade(c, 0.6)
    chao_escuro(c, 190)
    ob.runner(c, 140, 228, 1.4)


@cena('investigacao', 'investigacao,detetive,misterio,crime,investigation,detective,mystery')
def _investigacao(c):
    palco(c, rgb('1a1a24'), rgb('4a4a5a'), (230, 220, 190))
    ob.magnifier(c, 118, 226, 1.9)


@cena('guarda', 'guarda,policia,guard,police')
def _guarda(c):
    ceu_entardecer(c)
    chao_grama(c, 184)
    ob.helmet_guard(c, 128, 226, 1.5)


@cena('pescar', 'pescar,pesca,fishing')
def _pescar(c):
    ceu_dia(c, 0.5)
    mar(c, 130)
    ob.rod(c, 70, 226, 1.4)
    ob.fish(c, 170, 200, 1.1)


@cena('comida', 'comida,banquete,carne,cozinha,refeicao,food,feast,meat,kitchen')
def _comida(c):
    ceu_interior(c)
    c.glow(128, 180, 100, (255, 170, 80), alpha=0.35)
    c.rect(0, 200, 256, 256, WOODS[0], WOODS[1])
    ob.meat(c, 120, 226, 1.7)


WOODS = (rgb('946036'), rgb('4a2f1d'))


@cena('vinho', 'vinho,bebida,wine,drink')
def _vinho(c):
    palco(c, rgb('2a0a1a'), rgb('7a2a4a'), (255, 190, 170))
    ob.goblet(c, 128, 220, 1.7)


@cena('brilho', 'brilho,encantamento,feitico,sparkle,enchant,spell,enchantment')
def _brilho(c):
    ceu_magico(c)
    ob.sparkle(c, 128, 220, 1.7)


@cena('bomba', 'bomba,explosao,explosivo,bomb,explosion')
def _bomba(c):
    ceu_inferno(c)
    ob.bomb(c, 128, 226, 1.7)


@cena('impacto', 'impacto,colisao,boom,crash')
def _impacto(c):
    c.sky(rgb('3a0a0a'), rgb('d05a2a'), 1.0)
    ob.burst(c, 128, 226, 1.7)


@cena('tiro', 'tiro,pistola,tiroteio,gun,pistol,shot')
def _tiro(c):
    palco(c, rgb('1a1a24'), rgb('5a4a3a'), (255, 220, 150))
    ob.pistol(c, 140, 222, 1.7)


# ================================================================ FICÇÃO E MODERNO
@cena('foguete', 'foguete,nave,ficcao,cyber,futuro,rocket,spaceship,scifi,future')
def _foguete(c):
    ceu_espaco(c)
    ob.rocket(c, 128, 200, 1.5)


@cena('robo', 'robo,maquina,android,robot,machine')
def _robo(c):
    ceu_espaco(c)
    c.ridge(210, 6, rgb('3a4a6a'), rgb('0e1830'), seed=3)
    ob.robot(c, 128, 224, 1.4)


@cena('ovni', 'ovni,disco voador,ufo')
def _ovni(c):
    ceu_noite(c, 0.8)
    c.ridge(200, 10, rgb('1e2a3a'), rgb('0a1018'), seed=3)
    ob.ufo(c, 128, 110, 1.6)


@cena('carro', 'carro,perseguicao,veiculo,car,vehicle')
def _carro(c):
    c.sky(rgb('2a3a7a'), rgb('f0a870'), 0.7)
    c.rect(0, 180, 256, 256, rgb('3a3a46'), rgb('16161e'))
    for x in range(0, 256, 60):
        c.rect(x, 226, x + 30, 232, (230, 230, 210))
    ob.car(c, 128, 220, 1.8)


# ================================================================ EXTRAS (ampliam o vocabulário)
@cena('ponte', 'ponte,bridge')
def _ponte(c):
    c.sky(rgb('5aa0d0'), rgb('d5ecf0'), 0.5)
    c.clouds(3, (255, 255, 255), alpha=0.6)
    c.ridge(110, 10, rgb('7cc06a'), rgb('4a8a48'), seed=2)
    c.rect(-4, 120, 260, 256, rgb('78c8d8'), rgb('3a8aa8'))
    sj.bridge(c, 128, 150, 1.0)
    c.ridge(196, 8, rgb('9ad05a'), rgb('4a8a3a'), seed=13)
    c.ridge(228, 8, rgb('7ab84a'), rgb('2f6a30'), seed=14)


@cena('piramide', 'piramide,pyramid,faraó,farao,pharaoh,tumba,tomb')
def _piramide(c):
    ceu_deserto(c)
    c.ridge(160, 8, rgb('c8743c'), rgb('8a4424'), roughness=0.8, seed=11)
    chao_areia(c, 178)
    sj.pyramid(c, 120, 200, 1.35)
    sj.pyramid(c, 200, 182, 0.55, body=[rgb('6a3a28'), rgb('a4603c'), rgb('c98458'), rgb('e8a874')])


@cena('cachoeira', 'cachoeira,queda,waterfall,falls')
def _cachoeira(c):
    ceu_verde(c, 0.5)
    c.poly([(0, 256), (0, 60), (96, 70), (96, 256)], rgb('6a6a7a'), rgb('2a2a38'))
    c.poly([(160, 256), (160, 70), (256, 60), (256, 256)], rgb('6a6a7a'), rgb('2a2a38'))
    c.rect(96, 64, 160, 220, (210, 240, 255), (120, 190, 230))
    for x in (106, 124, 142):
        c.line([(x, 70), (x + 2, 210)], (255, 255, 255), 2, alpha=0.7)
    c.ellipse(128, 228, 70, 20, (200, 235, 250), (80, 160, 210), alpha=0.9)
    c.ellipse(128, 218, 44, 14, (255, 255, 255), alpha=0.6, blur=4)


@cena('masmorra', 'masmorra,calabouco,dungeon,subterraneo,underground')
def _masmorra(c):
    ceu_interior(c)
    c.rect(0, 0, 256, 256, STONE_BG[1], dark(STONE_BG[1], 0.7), alpha=0.9)
    for y in range(20, 250, 34):
        for x in range(-20 if (y // 34) % 2 else 0, 256, 56):
            c.rect(x + 2, y + 2, x + 52, y + 30, light(STONE_BG[1], 0.1), STONE_BG[1], alpha=0.7, radius=3)
    c.poly([(60, 256), (60, 60), (80, 40), (176, 40), (196, 60), (196, 256)], (14, 12, 16), (30, 26, 32))
    for x in (84, 108, 132, 156):
        c.rect(x, 50, x + 6, 256, (110, 112, 126), (50, 52, 64))
    sj.torch(c, 30, 160, 0.8)
    sj.torch(c, 226, 160, 0.8)


@cena('cemiterio', 'cemiterio,tumulo,graveyard,cemetery,grave')
def _cemiterio(c):
    ceu_noite(c, 0.7)
    sj.moon(c, 190, 70, 26, (230, 236, 220))
    chao_escuro(c, 176)
    ob.tombstone(c, 60, 214, 0.9)
    ob.tombstone(c, 128, 226, 1.1)
    ob.tombstone(c, 200, 212, 0.8)
    c.mist(200, 24, (170, 190, 210), 0.35)


@cena('pantano', 'pantano,brejo,charco,swamp,marsh,bog')
def _pantano(c):
    ceu_verde(c, 0.5)
    c.rect(0, 130, 256, 256, rgb('4a6a3a'), rgb('1a2a1a'))
    c.mist(150, 40, (190, 230, 180), 0.4)
    for x in (40, 130, 210):
        sj.oak(c, x, 190 + (x % 3) * 12, 0.7, leaf=sj.DEEP_GREENS)
    c.ellipse(128, 236, 80, 12, (90, 140, 80), (40, 70, 40), alpha=0.8)


@cena('mercado', 'mercado,feira,comercio,loja,market,shop,trade')
def _mercado(c):
    ceu_dia(c, 0.6)
    chao_terra(c, 180)
    ob.market_stall(c, 128, 224, 1.7)


@cena('trono', 'trono,throne,corte,court')
def _trono(c):
    ceu_interior(c)
    c.rect(0, 0, 256, 256, rgb('3a1a2a'), rgb('120810'), alpha=0.8)
    c.glow(128, 120, 100, (255, 200, 120), alpha=0.35)
    ob.throne(c, 128, 226, 1.3)


@cena('farol', 'farol,lighthouse,beacon')
def _farol(c):
    ceu_noite(c, 0.7)
    mar(c, 180)
    c.poly([(70, 256), (86, 190), (170, 190), (190, 256)], rgb('3a3a4a'), rgb('0e0e18'))
    ob.lighthouse(c, 128, 196, 1.2)


@cena('moinho', 'moinho,fazenda,fazendeiro,windmill,farm,farmer')
def _moinho(c):
    ceu_dia(c)
    chao_grama(c, 170)
    ob.windmill(c, 128, 216, 1.4)


@cena('arena', 'arena,coliseu,torneio,gladiador,colosseum,tournament,gladiator')
def _arena(c):
    ceu_deserto(c)
    chao_areia(c, 190)
    for i in range(7):
        x = 20 + i * 36
        c.rect(x, 90, x + 28, 190, rgb('c8a070'), rgb('8a6a42'))
        c.poly([(x + 4, 190), (x + 4, 150), (x + 14, 138), (x + 24, 150), (x + 24, 190)], (40, 28, 22), (80, 56, 40))
    c.rect(10, 70, 246, 92, rgb('d8b080'), rgb('9a7a52'))
    sj.shield(c, 128, 244, 0.7)


@cena('porto', 'pier,cais,docas,dock,harbor')
def _cais(c):
    ceu_entardecer(c, 0.55)
    mar(c, 140)
    c.rect(0, 180, 256, 200, WOOD[2], WOOD[0] if hasattr(WOOD, '__len__') else None)
    for x in range(10, 256, 40):
        c.rect(x, 190, x + 10, 256, sj.WOOD[1], sj.WOOD[0])
    sj.ship(c, 190, 176, 0.9)


@cena('mago_torre', 'torre,torre do mago,tower,wizard tower')
def _torre(c):
    ceu_magico(c)
    chao_escuro(c, 190)
    sj.tower(c, 128, 222, 1.3)
    sj.moon(c, 200, 60, 16)


WOOD = sj.WOOD


def render(ident):
    c = Canvas(seed=sum(ord(ch) for ch in ident))
    CENAS[ident](c)
    return c.finish()
