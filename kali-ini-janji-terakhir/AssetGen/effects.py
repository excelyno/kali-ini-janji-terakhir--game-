"""Partikel kecil: debu langkah, cipratan air, serpihan kayu/batu, daun, kilau, hujan, salju, konfeti.

Partikel berwarna tetap disimpan sebagai sprite, jadi game tidak perlu FillEllipse/FillRectangle.
Pivot di tengah gambar.
"""
import math

from PIL import Image

from pixel import Output, canvas, ellipse, outline, rect


def _save_center(out: Output, name: str, img: Image.Image):
    out.save(name, img, (img.width // 2, img.height // 2))


def dust(i: int) -> Image.Image:
    r = 1 + i
    img = canvas(9, 7)
    alpha = max(70, 220 - i * 50)
    ellipse(img, (4 - r, 4 - r * 0.6, 4 + r, 4 + r * 0.6), (226, 210, 176, alpha))
    return img


def splash(i: int) -> Image.Image:
    img = canvas(13, 9)
    spread = 1 + i * 1.6
    for k, ang in enumerate((-150, -110, -70, -30)):
        x = 6 + math.cos(math.radians(ang)) * spread
        y = 6 + math.sin(math.radians(ang)) * spread * 0.8 + i * 0.7
        img.putpixel((round(x), round(min(8, y))), (150, 210, 255, 230))
    if i < 2:
        rect(img, (5, 6, 7, 7), (190, 230, 255, 200))
    return img


def ripple(i: int) -> Image.Image:
    img = canvas(17, 9)
    rx, ry = 3 + i * 1.6, (3 + i * 1.6) * 0.5
    for y in range(9):
        for x in range(17):
            d = math.hypot((x - 8) / rx, (y - 4) / ry)
            if abs(d - 1) < 0.22:
                img.putpixel((x, y), (220, 240, 255, max(50, 200 - i * 45)))
    return img


def sparkle(i: int) -> Image.Image:
    img = canvas(7, 7)
    length = [1, 2, 3, 1][i]
    img.putpixel((3, 3), (255, 255, 240, 255))
    for k in range(1, length + 1):
        for x, y in ((3 + k, 3), (3 - k, 3), (3, 3 + k), (3, 3 - k)):
            img.putpixel((x, y), (255, 244, 190, 210))
    return img


def chip(color) -> Image.Image:
    img = canvas(3, 3)
    rect(img, (0, 0, 2, 1), color)
    img.putpixel((1, 2), color)
    return img


def leaf(i: int) -> Image.Image:
    img = canvas(5, 4)
    c = (120, 178, 84, 255) if i == 0 else (96, 150, 70, 255)
    for x, y in ((1, 1), (2, 1), (3, 1), (2, 0), (2, 2), (1, 2)):
        img.putpixel((x, y), c)
    return img


def generate(out: Output):
    for i in range(4):
        _save_center(out, f"fx_dust_{i}", dust(i))
        _save_center(out, f"fx_splash_{i}", splash(i))
        _save_center(out, f"fx_ripple_{i}", ripple(i))
        _save_center(out, f"fx_sparkle_{i}", sparkle(i))
    for i in range(2):
        _save_center(out, f"fx_leaf_{i}", leaf(i))
    _save_center(out, "fx_chip_wood", chip((170, 116, 66, 255)))
    _save_center(out, "fx_chip_stone", chip((150, 150, 156, 255)))
    _save_center(out, "fx_clod", chip((120, 80, 48, 255)))

    rain = canvas(2, 7)
    for y in range(7):
        rain.putpixel((1 if y < 4 else 0, y), (180, 210, 255, 150 + y * 10))
    _save_center(out, "fx_rain", rain)
    snow = canvas(3, 3)
    for x, y in ((1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
        snow.putpixel((x, y), (255, 255, 255, 230))
    _save_center(out, "fx_snow", snow)
    for i, c in enumerate([(240, 90, 80), (250, 210, 70), (110, 200, 120), (110, 160, 240)]):
        img = canvas(3, 2)
        rect(img, (0, 0, 2, 1), (*c, 240))
        _save_center(out, f"fx_confetti_{i}", img)
