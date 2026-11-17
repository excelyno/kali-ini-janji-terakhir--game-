"""Elemen UI: bingkai 9-slice, slot hotbar, bar, kursor tile, gelembung emosi, ikon kecil.

Bingkai 9-slice: pojok tidak ikut melar, sisi melar satu arah, tengah melar dua arah.
Ukuran pojok (dalam piksel layar) dicatat di sprites.json lewat pivot = (pojok, pojok).
"""
import numpy as np
from PIL import Image

from pixel import Output, canvas, ellipse, line, load_source, outline, rect, to_art

DARK = (60, 34, 22, 255)
WOOD = (190, 106, 48, 255)
WOOD_LIGHT = (232, 152, 72, 255)
WOOD_SHADE = (150, 78, 36, 255)
CREAM = (252, 226, 168, 255)
CREAM_SHADE = (232, 196, 136, 255)


def frame(interior, size=24, border=5) -> Image.Image:
    img = canvas(size, size)
    rect(img, (0, 1, size - 1, size - 2), DARK)
    rect(img, (1, 0, size - 2, size - 1), DARK)
    rect(img, (1, 1, size - 2, size - 2), WOOD)
    rect(img, (1, 1, size - 2, 1), WOOD_LIGHT)
    rect(img, (1, 1, 1, size - 2), WOOD_LIGHT)
    rect(img, (1, size - 2, size - 2, size - 2), WOOD_SHADE)
    rect(img, (size - 2, 1, size - 2, size - 2), WOOD_SHADE)
    rect(img, (border - 1, border - 1, size - border, size - border), DARK)
    rect(img, (border, border, size - border - 1, size - border - 1), interior)
    return img


def slot(selected: bool) -> Image.Image:
    img = canvas(26, 26)
    rect(img, (0, 0, 25, 25), (150, 92, 50, 255))
    rect(img, (1, 1, 24, 24), CREAM_SHADE)
    rect(img, (2, 2, 24, 24), CREAM)
    if selected:
        for i in range(2):
            rect(img, (i, i, 25 - i, 25 - i), (226, 64, 40, 255))
        rect(img, (2, 2, 23, 23), (255, 236, 180, 255))
    return img


def bar_back() -> Image.Image:
    img = canvas(10, 8)
    rect(img, (1, 0, 8, 7), DARK)
    rect(img, (0, 1, 9, 6), DARK)
    rect(img, (1, 1, 8, 6), (90, 60, 44, 255))
    return img


def bar_fill(color) -> Image.Image:
    img = canvas(1, 4)
    r, g, b = color
    for y, k in enumerate((1.25, 1.0, 1.0, 0.75)):
        img.putpixel((0, y), (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), 255))
    return img


def tile_cursor(color) -> Image.Image:
    img = canvas(24, 24)
    for (x0, y0, sx, sy) in ((0, 0, 1, 1), (23, 0, -1, 1), (0, 23, 1, -1), (23, 23, -1, -1)):
        for i in range(6):
            img.putpixel((x0 + sx * i, y0), color)
            img.putpixel((x0, y0 + sy * i), color)
            img.putpixel((x0 + sx * i, y0 + sy), color)
            img.putpixel((x0 + sx, y0 + sy * i), color)
    a = np.array(img)
    a[:, :, 3] = np.where(a[:, :, 3] > 0, 230, 0)
    return Image.fromarray(a)


def bubble(symbol: str) -> Image.Image:
    img = canvas(13, 15)
    rect(img, (1, 0, 11, 10), (255, 255, 255, 255))
    rect(img, (0, 1, 12, 9), (255, 255, 255, 255))
    for i, y in enumerate((11, 12, 13)):
        rect(img, (5, y, 7 - i, y), (255, 255, 255, 255))
    if symbol == "!":
        rect(img, (5, 2, 7, 6), (220, 60, 40, 255))
        rect(img, (5, 8, 7, 8), (220, 60, 40, 255))
    elif symbol == "egg":
        ellipse(img, (4, 2, 8, 8), (236, 214, 170, 255))
    elif symbol == "heart":
        for x, y in ((4, 3), (5, 3), (7, 3), (8, 3), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4), (9, 4),
                     (4, 5), (5, 5), (6, 5), (7, 5), (8, 5), (5, 6), (6, 6), (7, 6), (6, 7)):
            img.putpixel((x, y), (230, 70, 90, 255))
    elif symbol == "...":
        for x in (3, 6, 9):
            img.putpixel((x, 5), (90, 90, 90, 255))
    return outline(img)


def icon(kind: str) -> Image.Image:
    img = canvas(9, 9)
    if kind == "coin":
        ellipse(img, (0, 0, 8, 8), (244, 196, 48, 255))
        ellipse(img, (2, 2, 6, 6), (255, 228, 120, 255))
        line(img, [(4, 2), (4, 6)], (200, 140, 30, 255))
    elif kind == "energy":
        for x, y in ((5, 0), (4, 1), (5, 1), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (6, 3),
                     (4, 4), (5, 4), (3, 5), (4, 5), (3, 6), (2, 7)):
            img.putpixel((x, y), (250, 216, 60, 255))
    elif kind == "water":
        for y in range(9):
            half = [0, 0, 1, 1, 2, 3, 3, 2, 1][y]
            rect(img, (4 - half, y, 4 + half, y), (80, 160, 236, 255))
        img.putpixel((3, 5), (200, 236, 255, 255))
    elif kind == "debt":
        ellipse(img, (1, 2, 7, 8), (170, 130, 80, 255))
        rect(img, (3, 0, 5, 2), (140, 100, 60, 255))
        line(img, [(4, 4), (4, 7)], (250, 220, 100, 255))
    elif kind == "quest":
        for x, y in ((4, 0), (4, 1), (3, 2), (4, 2), (5, 2), (0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (7, 3), (8, 3),
                     (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (2, 5), (3, 5), (5, 5), (6, 5), (1, 6), (2, 6), (6, 6), (7, 6), (1, 7), (7, 7)):
            img.putpixel((x, y), (250, 210, 60, 255))
    return outline(img)


def weather_icon(kind: str) -> Image.Image:
    img = canvas(14, 12)
    sun, cloud, cloud_dark = (250, 200, 60, 255), (240, 244, 250, 255), (150, 160, 180, 255)
    if kind == "cerah":
        ellipse(img, (3, 2, 10, 9), sun)
        for x, y in ((6, 0), (7, 0), (0, 5), (13, 5), (1, 1), (12, 1), (1, 10), (12, 10), (6, 11), (7, 11)):
            img.putpixel((x, y), sun)
    else:
        ellipse(img, (1, 3, 8, 9), cloud if kind != "hujan" else cloud_dark)
        ellipse(img, (5, 1, 13, 9), cloud if kind != "hujan" else cloud_dark)
        if kind == "berawan":
            ellipse(img, (8, 0, 12, 4), sun)
            ellipse(img, (5, 1, 13, 9), cloud)
        if kind == "hujan":
            for x in (3, 7, 11):
                img.putpixel((x, 10), (90, 160, 240, 255))
                img.putpixel((x - 1, 11), (90, 160, 240, 255))
        if kind == "salju":
            for x in (3, 7, 11):
                img.putpixel((x, 11), (255, 255, 255, 255))
    return outline(img)


def season_icon(kind: str) -> Image.Image:
    img = canvas(9, 9)
    if kind == "semi":
        for x, y in ((4, 1), (3, 2), (5, 2), (4, 3)):
            img.putpixel((x, y), (244, 140, 180, 255))
        img.putpixel((4, 2), (250, 230, 120, 255))
        line(img, [(4, 4), (4, 8)], (80, 150, 60, 255))
        img.putpixel((5, 6), (80, 150, 60, 255))
    elif kind == "panas":
        ellipse(img, (1, 1, 7, 7), (250, 190, 50, 255))
    elif kind == "gugur":
        ellipse(img, (1, 1, 7, 6), (220, 110, 40, 255))
        line(img, [(1, 7), (6, 2)], (150, 70, 30, 255))
    else:
        for i in range(9):
            img.putpixel((4, i), (200, 230, 255, 255))
            img.putpixel((i, 4), (200, 230, 255, 255))
        for i in (2, 6):
            img.putpixel((i, i), (200, 230, 255, 255))
            img.putpixel((i, 8 - i), (200, 230, 255, 255))
    return outline(img)


def keycap() -> Image.Image:
    img = canvas(11, 11)
    rect(img, (0, 1, 10, 10), DARK)
    rect(img, (1, 0, 9, 10), DARK)
    rect(img, (1, 1, 9, 8), (250, 244, 226, 255))
    rect(img, (1, 9, 9, 9), (190, 176, 150, 255))
    return img


def arrows(out: Output):
    down = canvas(7, 5)
    for y in range(4):
        rect(down, (y, y, 6 - y, y), (232, 152, 72, 255))
    out.save("ui_arrow_down", outline(down))
    right = canvas(5, 7)
    for x in range(4):
        rect(right, (x, x, x, 6 - x), (226, 64, 40, 255))
    out.save("ui_arrow_right", outline(right))


def generate(out: Output):
    out.save("ui_frame", frame(CREAM), (6, 6))                      # pivot = ukuran pojok 9-slice
    out.save("ui_frame_dark", frame((70, 48, 36, 255)), (6, 6))
    out.save("ui_slot", slot(False))
    out.save("ui_slot_selected", slot(True))
    out.save("ui_bar_back", bar_back(), (3, 3))
    for name, color in {"green": (110, 200, 70), "blue": (80, 160, 236), "gold": (240, 184, 60), "red": (226, 74, 50)}.items():
        out.save(f"ui_fill_{name}", bar_fill(color))
    out.save("ui_cursor_ok", tile_cursor((255, 255, 255, 255)))
    out.save("ui_cursor_bad", tile_cursor((240, 80, 60, 255)))
    for name, symbol in {"exclaim": "!", "egg": "egg", "heart": "heart", "dots": "..."}.items():
        out.save_bottom_center(f"ui_bubble_{name}", bubble(symbol))
    for kind in ("coin", "energy", "water", "debt", "quest"):
        out.save(f"ui_icon_{kind}", icon(kind))
    for kind in ("cerah", "berawan", "hujan", "salju"):
        out.save(f"ui_weather_{kind}", weather_icon(kind))
    for kind in ("semi", "panas", "gugur", "dingin"):
        out.save(f"ui_season_{kind}", season_icon(kind))
    out.save("ui_key", keycap())
    arrows(out)

    hero = to_art(load_source("HeroSection(homeScreen).png"), height=560)
    out.save("hero", hero, (hero.width // 2, hero.height), scale=1)
