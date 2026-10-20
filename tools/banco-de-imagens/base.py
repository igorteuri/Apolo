"""Base de desenho do banco de imagens do Apolo (cenas ilustradas 256x256).

Tudo é desenhado em coordenadas "lógicas" de 0 a 256, com suavização (supersampling), degradês e uma textura leve
de pintura no final. Não usa nenhuma imagem externa: as cenas são geradas por código.
"""
import math
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W = 256          # tamanho final
S = 3            # supersampling


def rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def light(c, t):
    return mix(c, (255, 255, 255), t)


def dark(c, t):
    return mix(c, (0, 0, 0), t)


class Canvas:
    def __init__(self, seed=1):
        self.n = W * S
        self.img = Image.new('RGB', (self.n, self.n), (20, 20, 30))
        self.rng = random.Random(seed)
        self.nrng = np.random.default_rng(seed)
        self.seed = seed

    # ---------- preenchimento ----------
    def _fill(self, mask, c1, c2, bbox, vertical=True):
        x0, y0, x1, y1 = [int(v) for v in bbox]
        x0, y0 = max(0, x0), max(0, y0)
        x1, y1 = min(self.n, max(x1, x0 + 1)), min(self.n, max(y1, y0 + 1))
        w, h = x1 - x0, y1 - y0
        if w <= 0 or h <= 0:
            return
        if c2 is None or c1 == c2:
            layer = Image.new('RGB', (w, h), c1)
        else:
            g = Image.linear_gradient('L')
            if not vertical:
                g = g.rotate(90)
            g = g.resize((w, h))
            layer = Image.composite(Image.new('RGB', (w, h), c2), Image.new('RGB', (w, h), c1), g)
        self.img.paste(layer, (x0, y0), mask.crop((x0, y0, x1, y1)))

    def _bounds(self, pts, pad=0):
        xs = [p[0] * S for p in pts]
        ys = [p[1] * S for p in pts]
        return (min(xs) - pad, min(ys) - pad, max(xs) + pad, max(ys) + pad)

    def _mask(self):
        return Image.new('L', (self.n, self.n), 0)

    def poly(self, pts, c1, c2=None, alpha=1.0, blur=0.0, vertical=True):
        m = self._mask()
        ImageDraw.Draw(m).polygon([(x * S, y * S) for x, y in pts], fill=int(255 * alpha))
        pad = 0
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
            pad = int(blur * S * 3)
        self._fill(m, c1, c2, self._bounds(pts, pad), vertical)

    def ellipse(self, cx, cy, rx, ry, c1, c2=None, alpha=1.0, blur=0.0, vertical=True):
        m = self._mask()
        ImageDraw.Draw(m).ellipse([(cx - rx) * S, (cy - ry) * S, (cx + rx) * S, (cy + ry) * S], fill=int(255 * alpha))
        pad = 0
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
            pad = int(blur * S * 3)
        self._fill(m, c1, c2, ((cx - rx) * S - pad, (cy - ry) * S - pad, (cx + rx) * S + pad, (cy + ry) * S + pad), vertical)

    def rect(self, x0, y0, x1, y1, c1, c2=None, alpha=1.0, blur=0.0, vertical=True, radius=0):
        m = self._mask()
        d = ImageDraw.Draw(m)
        box = [x0 * S, y0 * S, x1 * S, y1 * S]
        if radius:
            d.rounded_rectangle(box, radius=radius * S, fill=int(255 * alpha))
        else:
            d.rectangle(box, fill=int(255 * alpha))
        pad = 0
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
            pad = int(blur * S * 3)
        self._fill(m, c1, c2, (x0 * S - pad, y0 * S - pad, x1 * S + pad, y1 * S + pad), vertical)

    def line(self, pts, color, width=2.0, alpha=1.0, blur=0.0):
        m = self._mask()
        d = ImageDraw.Draw(m)
        d.line([(x * S, y * S) for x, y in pts], fill=int(255 * alpha), width=max(1, int(width * S)), joint='curve')
        r = width * S / 2
        for x, y in (pts[0], pts[-1]):
            d.ellipse([x * S - r, y * S - r, x * S + r, y * S + r], fill=int(255 * alpha))
        pad = 0
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
            pad = int(blur * S * 3)
        b = self._bounds(pts, width * S + pad)
        self._fill(m, color, None, b)

    def curve(self, pts, color, width=2.0, alpha=1.0, steps=24):
        """Linha suave passando perto dos pontos (curva de Catmull-Rom)."""
        self.line(smooth(pts, steps), color, width, alpha)

    # ---------- cenário ----------
    def sky(self, top, bottom, horizon=0.62):
        h = int(self.n * horizon)
        self._fill(self._full_mask(0, h), top, bottom, (0, 0, self.n, h))
        self._fill(self._full_mask(h, self.n), bottom, bottom, (0, h, self.n, self.n))

    def _full_mask(self, y0, y1):
        m = self._mask()
        ImageDraw.Draw(m).rectangle([0, y0, self.n, y1], fill=255)
        return m

    def glow(self, cx, cy, r, color, alpha=0.8):
        for i in range(6):
            t = i / 5.0
            self.ellipse(cx, cy, r * (1.0 - 0.14 * i), r * (1.0 - 0.14 * i), color, alpha=alpha * (0.12 + 0.18 * t), blur=r * 0.12)

    def stars(self, n=60, ymax=140, color=(255, 255, 240)):
        for _ in range(n):
            x, y = self.rng.uniform(4, 252), self.rng.uniform(4, ymax)
            r = self.rng.choice([0.5, 0.7, 0.9, 1.2])
            self.ellipse(x, y, r, r, color, alpha=self.rng.uniform(0.5, 1.0))

    def clouds(self, n, color, ymin=20, ymax=110, alpha=0.55, scale=1.0):
        for _ in range(n):
            cx, cy = self.rng.uniform(0, 256), self.rng.uniform(ymin, ymax)
            for _k in range(5):
                self.ellipse(cx + self.rng.uniform(-30, 30) * scale, cy + self.rng.uniform(-6, 6),
                             self.rng.uniform(18, 34) * scale, self.rng.uniform(7, 13) * scale, color, alpha=alpha * 0.6, blur=4)

    def ridge(self, base, amp, c1, c2, roughness=0.5, seed=0, detail=7, y_bottom=256):
        """Silhueta de morros/colinas que acaba na parte de baixo da imagem."""
        r = random.Random(self.seed * 100 + seed)
        pts = []
        phases = [(r.uniform(0, 6.28), r.uniform(0.6, 1.4)) for _ in range(detail)]
        for i in range(0, 257, 4):
            x = i / 256.0
            y = 0
            for k, (ph, f) in enumerate(phases):
                y += math.sin(x * (k + 1) * 2.2 * f * roughness * 3 + ph) / (k + 1) ** 1.1
            pts.append((i, base - amp * y / 1.8))
        pts += [(256, y_bottom), (0, y_bottom)]
        self.poly(pts, c1, c2)
        return pts

    def mist(self, y, h, color, alpha=0.35):
        self.rect(-10, y - h / 2, 266, y + h / 2, color, alpha=alpha, blur=h * 0.4)

    # ---------- acabamento ----------
    def finish(self, grain=1.0, vignette=0.22):
        im = self.img.resize((W, W), Image.LANCZOS)
        a = np.asarray(im).astype(np.float32)
        rng = np.random.default_rng(self.seed + 7)
        for scale, amp in ((6, 0.045), (24, 0.04), (96, 0.03)):
            n = rng.standard_normal((W // scale + 3, W // scale + 3)).astype(np.float32)
            n = (n - n.min()) / max(1e-6, (n.max() - n.min()))
            nimg = Image.fromarray((n * 255).astype(np.uint8)).resize((W, W), Image.BICUBIC)
            nn = (np.asarray(nimg).astype(np.float32) / 255.0 - 0.5) * 2.0
            a *= (1.0 + grain * amp * nn[..., None])
        yy, xx = np.mgrid[0:W, 0:W]
        r = np.sqrt((xx - W / 2.0) ** 2 + (yy - W / 2.0) ** 2) / (W * 0.72)
        a *= (1.0 - vignette * np.clip(r, 0, 1) ** 2.2)[..., None]
        out = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        out = out.filter(ImageFilter.MedianFilter(3)).filter(ImageFilter.SMOOTH)
        return out


def smooth(pts, steps=24):
    """Catmull-Rom: devolve uma lista de pontos mais densa e suave."""
    if len(pts) < 3:
        return list(pts)
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for s in range(steps):
            t = s / float(steps)
            t2, t3 = t * t, t * t * t
            out.append((0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
                        0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)))
    out.append(pts[-1])
    return out


def blob(cx, cy, rx, ry, rng, wobble=0.22, n=18):
    """Contorno irregular (folhagem, nuvem, rocha)."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        k = 1.0 + rng.uniform(-wobble, wobble)
        pts.append((cx + math.cos(a) * rx * k, cy + math.sin(a) * ry * k))
    return smooth(pts + [pts[0], pts[1]], 6)
