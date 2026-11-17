"""Tile tanah (24x24 art = 48x48 layar) + autotile.

AUTOTILE: setiap jenis tanah punya 16 varian, satu untuk tiap kombinasi tetangga.
Nomor varian (mask) = jumlah bit tetangga yang SAMA jenisnya:
    1 = atas (N), 2 = kanan (E), 4 = bawah (S), 8 = kiri (W)
Sisi yang tetangganya berbeda diberi tepi (transparan + garis rim), sehingga
rumput di bawahnya terlihat dan petak-petak menyambung rapi seperti Stardew Valley.
Game cukup menghitung mask lalu menggambar "{jenis}_{mask}".
"""
import math

import numpy as np
from PIL import Image

from pixel import TILE_ART, Output, canvas, load_source, multiply, rng, to_art

N, E, S, W = 1, 2, 4, 8

GRASS = (118, 168, 78)
GRASS_DARK = (96, 146, 64)
GRASS_LIGHT = (140, 188, 92)


def grass_tile(seed: int) -> Image.Image:
    r = rng(seed)
    a = np.zeros((TILE_ART, TILE_ART, 4), np.uint8)
    a[:, :] = (*GRASS, 255)
    noise = r.random((TILE_ART, TILE_ART))
    a[noise < 0.10] = (*GRASS_DARK, 255)
    a[noise > 0.93] = (*GRASS_LIGHT, 255)
    for _ in range(4):                       # rumpun kecil berbentuk "v"
        x, y = int(r.integers(1, TILE_ART - 2)), int(r.integers(2, TILE_ART - 1))
        a[y, x] = a[y - 1, x - 1] = a[y - 1, x + 1] = (*GRASS_DARK, 255)
        a[y - 2, x + 1] = (*GRASS_LIGHT, 255)
    return Image.fromarray(a)


def texture_from(file_name: str, inset: float) -> Image.Image:
    """Ambil bagian tengah tekstur lama (tanpa bingkai) lalu jadikan 24x24 art."""
    src = load_source(file_name)
    w, h = src.size
    inner = src.crop((int(w * inset), int(h * inset), int(w * (1 - inset)), int(h * (1 - inset))))
    return to_art(inner, TILE_ART, TILE_ART)


def autotile(base: Image.Image, mask: int, cut: int, rim: list[tuple], jitter: int, seed: int) -> Image.Image:
    """Varian autotile: piksel dekat sisi 'terbuka' dibuang (cut) atau diberi warna rim."""
    r = rng(seed * 31 + mask)
    wobble = r.integers(0, jitter + 1, size=TILE_ART) if jitter else np.zeros(TILE_ART, int)
    a = np.array(base).copy()
    last = TILE_ART - 1
    radius = cut + len(rim) + 3              # pojok luar dibulatkan
    for y in range(TILE_ART):
        for x in range(TILE_ART):
            d = 99.0
            if not mask & N: d = min(d, y - wobble[x])
            if not mask & S: d = min(d, last - y - wobble[x])
            if not mask & W: d = min(d, x - wobble[y])
            if not mask & E: d = min(d, last - x - wobble[y])
            for open_a, open_b, cx, cy in ((N, W, radius, radius), (N, E, last - radius, radius),
                                          (S, W, radius, last - radius), (S, E, last - radius, last - radius)):
                if not mask & open_a and not mask & open_b:
                    inside_x = x < cx if cx == radius else x > cx
                    inside_y = y < cy if cy == radius else y > cy
                    if inside_x and inside_y:
                        d = min(d, radius - math.hypot(x - cx, y - cy))
            if d < cut:
                a[y, x] = 0
            elif d < cut + len(rim):
                a[y, x] = (*rim[int(d - cut)], 255)
    return Image.fromarray(a)


def water_tile(seed: int) -> Image.Image:
    r = rng(seed)
    a = np.zeros((TILE_ART, TILE_ART, 4), np.uint8)
    a[:, :] = (104, 178, 186, 255)
    a[r.random((TILE_ART, TILE_ART)) < 0.06] = (96, 166, 176, 255)
    for _ in range(3):                       # garis riak putih
        x, y = int(r.integers(2, TILE_ART - 6)), int(r.integers(3, TILE_ART - 3))
        a[y, x:x + 4] = (190, 232, 230, 255)
    return Image.fromarray(a)


def decor_flower(color) -> Image.Image:
    img = canvas(5, 6)
    stem = (70, 128, 52, 255)
    for (x, y) in ((2, 3), (2, 4), (2, 5), (1, 5)):
        img.putpixel((x, y), stem)
    for (x, y) in ((2, 0), (1, 1), (3, 1), (2, 2)):
        img.putpixel((x, y), color)
    img.putpixel((2, 1), (250, 230, 120, 255))
    return img


def decor_tuft(seed: int) -> Image.Image:
    img = canvas(7, 5)
    dark, light = (*GRASS_DARK, 255), (*GRASS_LIGHT, 255)
    blades = [(1, 2), (3, 0), (5, 1)] if seed == 0 else [(0, 1), (2, 0), (4, 1), (6, 2)]
    for x, top in blades:
        for y in range(top, 5):
            img.putpixel((x, y), light if y == top else dark)
    return img


def lily_pad(seed: int) -> Image.Image:
    img = canvas(8, 6)
    for y in range(6):
        for x in range(8):
            if ((x - 3.5) / 4) ** 2 + ((y - 2.5) / 3) ** 2 <= 1 and not (x in (3, 4) and y < 3):
                img.putpixel((x, y), (86, 150, 70, 255) if y < 4 else (66, 122, 58, 255))
    if seed == 1:
        img.putpixel((5, 1), (250, 200, 220, 255))
        img.putpixel((6, 1), (240, 160, 190, 255))
    return img


def generate(out: Output):
    for i in range(4):
        out.save(f"grass_{i}", grass_tile(100 + i))

    dirt = texture_from("Tanah(tanpaRumput).png", 0.06)
    path = multiply(dirt, (1.12, 1.1, 1.0))
    tilled = texture_from("Tanah(Dibajak).png", 0.07)
    tilled_wet = multiply(tilled, (0.62, 0.58, 0.66))
    terrains = {
        # nama: (tekstur, cut, warna rim dari luar ke dalam, jitter)
        "dirt": (dirt, 0, [(150, 108, 70)], 1),
        "path": (path, 1, [(168, 128, 86)], 1),
        "tilled": (tilled, 1, [(78, 48, 28), (110, 70, 40)], 0),
        "tilled_wet": (tilled_wet, 1, [(48, 30, 22), (70, 44, 30)], 0),
        "water": (water_tile(7), 0, [(70, 96, 60), (206, 190, 132), (186, 168, 112)], 0),
    }
    for name, (base, cut, rim, jitter) in terrains.items():
        for mask in range(16):
            out.save(f"{name}_{mask}", autotile(base, mask, cut, rim, jitter, seed=len(name)))

    colors = [(240, 240, 250, 255), (250, 210, 80, 255), (230, 110, 140, 255), (150, 160, 240, 255)]
    for i, c in enumerate(colors):
        out.save(f"decor_flower_{i}", decor_flower(c))
    for i in range(2):
        out.save(f"decor_tuft_{i}", decor_tuft(i))
        out.save(f"decor_lily_{i}", lily_pad(i))
