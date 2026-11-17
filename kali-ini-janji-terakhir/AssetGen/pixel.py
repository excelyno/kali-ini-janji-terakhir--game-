"""Alat bantu pixel-art yang dipakai semua generator.

Konvensi skala:
  * "art pixel" = 1 piksel seni. Semua aset dibuat dalam art pixel.
  * Saat disimpan, setiap art pixel diperbesar SCALE kali (nearest neighbour),
    sehingga game tinggal menggambar PNG apa adanya tanpa skala ulang.
  * 1 tile = TILE_ART art pixel = TILE_ART * SCALE piksel layar (24 * 2 = 48).
"""
from __future__ import annotations

import json
import os
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

SCALE = 2
TILE_ART = 24
TILE = TILE_ART * SCALE

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
SOURCE_DIR = os.path.join(PROJECT, "Assets", "FileAssets")
OUTPUT_DIR = os.path.join(PROJECT, "Assets", "Sprites")

OUTLINE = (52, 34, 24, 255)


# --------------------------------------------------------------------------- output
class Output:
    """Menyimpan PNG dan mencatat pivot (titik jangkar) tiap sprite ke sprites.json."""

    def __init__(self, folder: str = OUTPUT_DIR):
        self.folder = folder
        self.pivots: dict[str, list[int]] = {}
        os.makedirs(folder, exist_ok=True)
        for name in os.listdir(folder):  # folder ini 100% hasil generator: aman dibersihkan
            if name.endswith(".png") or name == "sprites.json":
                os.remove(os.path.join(folder, name))

    def save(self, name: str, art: Image.Image, pivot: tuple[int, int] | None = None, scale: int = SCALE):
        """Simpan gambar art-pixel. pivot dalam art pixel (None = pojok kiri atas)."""
        big = art.resize((art.width * scale, art.height * scale), Image.Resampling.NEAREST)
        big.save(os.path.join(self.folder, name + ".png"))
        if pivot is not None:
            self.pivots[name] = [pivot[0] * scale, pivot[1] * scale]

    def save_bottom_center(self, name: str, art: Image.Image, foot_margin: int = 0):
        """Pivot di tengah bawah: titik kaki/alas benda di dunia."""
        self.save(name, art, (art.width // 2, art.height - foot_margin))

    def finish(self):
        with open(os.path.join(self.folder, "sprites.json"), "w", encoding="utf-8") as f:
            json.dump({"scale": SCALE, "tile": TILE, "pivots": self.pivots}, f, indent=1, sort_keys=True)
        print(f"{len(os.listdir(self.folder)) - 1} sprite ditulis ke {self.folder}")


# --------------------------------------------------------------------------- source images
def load_source(file_name: str) -> Image.Image:
    """Muat aset lama, buang latar putih (flood-fill dari tepi), lalu potong ke isinya."""
    img = Image.open(os.path.join(SOURCE_DIR, file_name)).convert("RGBA")
    a = np.array(img)
    h, w = a.shape[:2]
    white = (a[:, :, 3] > 200) & (a[:, :, :3] > 235).all(axis=2)
    if white[0, 0] or white[0, -1] or white[-1, 0]:
        seen = np.zeros_like(white)
        queue = deque([(0, x) for x in range(w)] + [(h - 1, x) for x in range(w)]
                      + [(y, 0) for y in range(h)] + [(y, w - 1) for y in range(h)])
        while queue:
            y, x = queue.popleft()
            if seen[y, x] or not white[y, x]:
                continue
            seen[y, x] = True
            if y > 0: queue.append((y - 1, x))
            if y < h - 1: queue.append((y + 1, x))
            if x > 0: queue.append((y, x - 1))
            if x < w - 1: queue.append((y, x + 1))
        a[seen, 3] = 0
    return crop_to_content(Image.fromarray(a))


def crop_to_content(img: Image.Image, alpha_min: int = 40) -> Image.Image:
    a = np.array(img)
    ys, xs = np.nonzero(a[:, :, 3] > alpha_min)
    if len(xs) == 0:
        return img
    return img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def to_art(img: Image.Image, width: int | None = None, height: int | None = None,
           factor: float | None = None) -> Image.Image:
    """Turunkan gambar resolusi tinggi ke ukuran art pixel (nearest = tetap tajam)."""
    if factor is not None:
        width, height = max(1, round(img.width * factor)), max(1, round(img.height * factor))
    elif width and not height:
        height = max(1, round(img.height * width / img.width))
    elif height and not width:
        width = max(1, round(img.width * height / img.height))
    small = img.resize((width, height), Image.Resampling.NEAREST)
    return hard_alpha(small)


def hard_alpha(img: Image.Image, threshold: int = 110) -> Image.Image:
    """Pixel art tidak punya tepi setengah transparan."""
    a = np.array(img.convert("RGBA"))
    a[:, :, 3] = np.where(a[:, :, 3] > threshold, 255, 0)
    return Image.fromarray(a)


# --------------------------------------------------------------------------- canvas helpers
def canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def paste_bottom_center(base: Image.Image, img: Image.Image, dx: int = 0, dy: int = 0) -> Image.Image:
    out = base.copy()
    x = (base.width - img.width) // 2 + dx
    y = base.height - img.height + dy
    out.alpha_composite(img, (x, y))
    return out


def mirror(img: Image.Image) -> Image.Image:
    return img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)


def shift(img: Image.Image, dx: int, dy: int) -> Image.Image:
    out = canvas(img.width, img.height)
    out.paste(img, (dx, dy), img)
    return out


def shift_rows(img: Image.Image, top: int, bottom: int, dx: int, dy: int) -> Image.Image:
    """Geser hanya baris top..bottom-1 (misal badan saja, kaki tetap)."""
    a = np.array(img)
    part = np.zeros_like(a)
    part[top:bottom] = a[top:bottom]
    rest = a.copy()
    rest[top:bottom] = 0
    moved = np.roll(np.roll(part, dy, axis=0), dx, axis=1)
    out = Image.fromarray(rest)
    out.alpha_composite(Image.fromarray(moved))
    return out


def outline(img: Image.Image, color=OUTLINE) -> Image.Image:
    """Tambah garis tepi 1 art pixel di sekeliling bentuk (gaya pixel art)."""
    a = np.array(img)
    solid = a[:, :, 3] > 0
    grown = solid.copy()
    grown[1:, :] |= solid[:-1, :]
    grown[:-1, :] |= solid[1:, :]
    grown[:, 1:] |= solid[:, :-1]
    grown[:, :-1] |= solid[:, 1:]
    edge = grown & ~solid
    a[edge] = color
    return Image.fromarray(a)


def pad(img: Image.Image, n: int = 1) -> Image.Image:
    out = canvas(img.width + 2 * n, img.height + 2 * n)
    out.alpha_composite(img, (n, n))
    return out


def rgb_to_hsv(a: np.ndarray):
    rgb = a[:, :, :3].astype(float) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    d = mx - mn
    h = np.zeros_like(mx)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    m = d > 1e-6
    rm = m & (mx == r)
    gm = m & (mx == g) & ~rm
    bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h = h * 60
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    c = v * s
    hp = (h % 360) / 60
    x = c * (1 - np.abs(hp % 2 - 1))
    z = np.zeros_like(h)
    conds = [(hp < 1), (hp < 2), (hp < 3), (hp < 4), (hp < 5), (hp >= 5)]
    rgbs = [(c, x, z), (x, c, z), (z, c, x), (z, x, c), (x, z, c), (c, z, x)]
    r = np.select(conds, [t[0] for t in rgbs])
    g = np.select(conds, [t[1] for t in rgbs])
    b = np.select(conds, [t[2] for t in rgbs])
    m = v - c
    return np.stack([(r + m), (g + m), (b + m)], axis=2) * 255


def recolor(img: Image.Image, rules) -> Image.Image:
    """rules: daftar (mask_fn(h,s,v) -> bool[], fn(h,s,v) -> (h,s,v)). Hanya piksel yang cocok diubah."""
    a = np.array(img).copy()
    h, s, v = rgb_to_hsv(a)
    opaque = a[:, :, 3] > 0
    for mask_fn, change in rules:
        m = mask_fn(h, s, v) & opaque
        nh, ns, nv = change(h.copy(), s.copy(), v.copy())
        rgb = hsv_to_rgb(nh, np.clip(ns, 0, 1), np.clip(nv, 0, 1))
        a[m, :3] = np.clip(rgb[m], 0, 255).astype(np.uint8)
    return Image.fromarray(a)


def multiply(img: Image.Image, factor: tuple[float, float, float]) -> Image.Image:
    a = np.array(img).astype(float)
    for i in range(3):
        a[:, :, i] *= factor[i]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def with_alpha(img: Image.Image, alpha: float) -> Image.Image:
    a = np.array(img).copy()
    a[:, :, 3] = (a[:, :, 3].astype(float) * alpha).astype(np.uint8)
    return Image.fromarray(a)


def draw_pixels(img: Image.Image, rows: list[str], palette: dict[str, tuple], x0: int = 0, y0: int = 0):
    """Gambar dari peta karakter: setiap huruf = satu art pixel ('.' = kosong)."""
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in palette:
                img.putpixel((x0 + x, y0 + y), palette[ch])


def ellipse(img: Image.Image, box, fill):
    ImageDraw.Draw(img).ellipse(box, fill=fill)


def rect(img: Image.Image, box, fill):
    ImageDraw.Draw(img).rectangle(box, fill=fill)


def line(img: Image.Image, pts, fill, width: int = 1):
    ImageDraw.Draw(img).line(pts, fill=fill, width=width)


def rng(seed: int) -> np.random.Generator:
    return np.random.default_rng(seed)
