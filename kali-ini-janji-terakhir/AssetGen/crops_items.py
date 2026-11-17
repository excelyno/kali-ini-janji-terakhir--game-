"""Tanaman per tahap, ikon barang, ikon alat, dan frame ayunan alat.

Nama hasil:
  crop_{jenis}_{tahap}     tahap 0 = benih, 1 = tunas, 2 = remaja, 3 = siap panen
  item_{id}                ikon barang 16x16 art (wortel, kayu, telur, ...)
  seed_{jenis}             ikon kantong benih
  tool_{nama}              ikon alat untuk hotbar
  swing_{nama}_{arah}_{i}  alat yang sedang diayun; pivot = genggaman tangan
"""
import math

from PIL import Image

from pixel import Output, canvas, ellipse, line, load_source, mirror, outline, rect, to_art

CROPS = ["lobak", "wortel", "tomat", "jagung", "labu"]

LEAF = (86, 160, 64, 255)
LEAF_DARK = (58, 120, 50, 255)
LEAF_LIGHT = (140, 200, 90, 255)


# --------------------------------------------------------------------------- ripe plants
def _blades(img, cx, base, heights, color=LEAF, spread=2):
    for i, hgt in enumerate(heights):
        dx = (i - (len(heights) - 1) / 2) * spread
        line(img, [(cx, base), (cx + dx, base - hgt)], color)


def ripe_lobak():
    img = canvas(24, 32)
    _blades(img, 12, 24, [9, 12, 10], LEAF)
    _blades(img, 12, 24, [7, 9], LEAF_LIGHT, spread=4)
    ellipse(img, (7, 22, 16, 30), (238, 232, 220, 255))
    ellipse(img, (7, 22, 16, 26), (176, 78, 160, 255))
    img.putpixel((9, 24), (220, 140, 210, 255))
    return outline(img)


def ripe_wortel():
    img = canvas(24, 32)
    _blades(img, 12, 26, [11, 14, 12, 9], LEAF_LIGHT, spread=3)
    _blades(img, 12, 26, [8, 10], LEAF, spread=5)
    ellipse(img, (8, 25, 15, 30), (236, 124, 40, 255))
    img.putpixel((10, 26), (250, 170, 90, 255))
    return outline(img)


def ripe_tomat():
    img = canvas(24, 32)
    rect(img, (11, 6, 12, 31), (130, 92, 56, 255))         # ajir
    for (x, y) in ((6, 22), (14, 18), (5, 12), (14, 9), (9, 26)):
        ellipse(img, (x, y, x + 6, y + 4), LEAF)
        img.putpixel((x + 2, y + 1), LEAF_LIGHT)
    for (x, y) in ((6, 16), (14, 13), (8, 22), (15, 22), (10, 8)):
        ellipse(img, (x, y, x + 3, y + 3), (220, 52, 40, 255))
        img.putpixel((x + 1, y + 1), (255, 150, 130, 255))
    return outline(img)


def ripe_jagung():
    img = canvas(24, 32)
    rect(img, (11, 3, 12, 31), (112, 150, 62, 255))         # batang
    line(img, [(12, 26), (4, 18)], LEAF)
    line(img, [(12, 20), (20, 12)], LEAF)
    line(img, [(12, 14), (5, 8)], LEAF_LIGHT)
    line(img, [(12, 30), (19, 25)], LEAF_DARK)
    for (x, y) in ((13, 13), (6, 18)):
        ellipse(img, (x, y, x + 4, y + 8), (244, 204, 62, 255))
        line(img, [(x, y + 8), (x + 2, y + 3)], LEAF)
    line(img, [(12, 3), (10, 0)], (210, 186, 110, 255))     # bunga jantan
    line(img, [(12, 3), (14, 0)], (210, 186, 110, 255))
    return outline(img)


def ripe_labu():
    img = canvas(24, 32)
    ellipse(img, (1, 18, 9, 23), LEAF)
    ellipse(img, (15, 17, 23, 22), LEAF)
    ellipse(img, (3, 20, 20, 31), (232, 130, 40, 255))
    for x in (7, 11, 15):
        line(img, [(x, 21), (x, 30)], (196, 96, 30, 255))
    rect(img, (11, 17, 12, 20), (110, 84, 40, 255))
    img.putpixel((6, 23), (252, 180, 100, 255))
    return outline(img)


RIPE = {"lobak": ripe_lobak, "wortel": ripe_wortel, "tomat": ripe_tomat, "jagung": ripe_jagung, "labu": ripe_labu}


def seed_mound():
    img = canvas(24, 32)
    ellipse(img, (6, 27, 17, 31), (112, 74, 44, 255))
    for x, y in ((8, 28), (12, 28), (15, 29)):
        img.putpixel((x, y), (232, 212, 150, 255))
    return img


def crops(out: Output):
    sprout = to_art(load_source("harvest-tanaman(dummy).png"), height=8)
    young = to_art(load_source("BenihTumbuh(keciltransparant).png"), height=15)
    stage1 = canvas(24, 32)
    stage1.alpha_composite(sprout, ((24 - sprout.width) // 2, 31 - sprout.height))
    stage2 = canvas(24, 32)
    stage2.alpha_composite(young, ((24 - young.width) // 2, 31 - young.height))
    for kind in CROPS:
        stages = [seed_mound(), stage1, stage2, RIPE[kind]()]
        for i, img in enumerate(stages):
            out.save_bottom_center(f"crop_{kind}_{i}", img)
    return {k: RIPE[k]() for k in CROPS}


# --------------------------------------------------------------------------- item icons
def produce_icon(kind: str) -> Image.Image:
    img = canvas(16, 16)
    if kind == "lobak":
        _blades(img, 8, 7, [5, 6, 5], LEAF)
        ellipse(img, (3, 6, 12, 14), (238, 232, 220, 255))
        ellipse(img, (3, 6, 12, 10), (176, 78, 160, 255))
    elif kind == "wortel":
        for i in range(9):
            ellipse(img, (3 + i, 4 + i, 7 + i - i // 3, 8 + i - i // 3), (236, 124, 40, 255))
        _blades(img, 4, 5, [4, 5], LEAF_LIGHT)
    elif kind == "tomat":
        ellipse(img, (2, 4, 13, 14), (220, 52, 40, 255))
        ellipse(img, (4, 6, 6, 8), (255, 150, 130, 255))
        line(img, [(6, 4), (10, 4)], LEAF)
        img.putpixel((8, 3), LEAF_DARK)
    elif kind == "jagung":
        ellipse(img, (5, 1, 10, 14), (244, 204, 62, 255))
        line(img, [(4, 14), (6, 5)], LEAF)
        line(img, [(11, 14), (10, 6)], LEAF)
    elif kind == "labu":
        ellipse(img, (1, 4, 14, 14), (232, 130, 40, 255))
        for x in (5, 8, 11):
            line(img, [(x, 5), (x, 13)], (196, 96, 30, 255))
        rect(img, (7, 2, 8, 4), (110, 84, 40, 255))
    return outline(img)


def material_icons(out: Output):
    wood = canvas(16, 16)
    rect(wood, (1, 5, 13, 11), (150, 98, 58, 255))
    line(wood, [(1, 7), (13, 7)], (120, 76, 44, 255))
    ellipse(wood, (11, 5, 15, 11), (220, 180, 120, 255))
    ellipse(wood, (12, 7, 14, 9), (180, 130, 80, 255))
    out.save("item_wood", outline(wood))

    stone = canvas(16, 16)
    rock = to_art(load_source("batu.png"), width=14)
    stone.alpha_composite(rock, (1, 15 - rock.height))
    out.save("item_stone", stone)

    egg = canvas(16, 16)
    ellipse(egg, (4, 2, 11, 13), (248, 238, 214, 255))
    ellipse(egg, (5, 4, 6, 6), (255, 255, 250, 255))
    out.save("item_egg", outline(egg))


def seed_packet(icon: Image.Image) -> Image.Image:
    img = canvas(16, 16)
    rect(img, (2, 1, 13, 14), (232, 212, 162, 255))
    rect(img, (2, 1, 13, 3), (196, 168, 116, 255))
    small = icon.resize((10, 10), Image.Resampling.NEAREST)
    img.alpha_composite(small, (3, 4))
    return outline(img)


def items(out: Output):
    for kind in CROPS:
        icon = produce_icon(kind)
        out.save(f"item_{kind}", icon)
        out.save(f"seed_{kind}", seed_packet(icon))
    material_icons(out)


# --------------------------------------------------------------------------- tools
TOOL_SOURCES = {"hoe": "Cangkul.png", "can": "Shower.png", "axe": "Kapak.png", "pickaxe": "Beliung(Pickaxe).png"}

# Sudut ayunan (derajat, berlawanan jarum jam) relatif ke gambar ikon aslinya.
SWING_ANGLES = {
    "right": [50, 0, -75],      # diangkat ke atas, ke depan, menghantam tanah
    "down": [50, -45, -125],
    "up": [50, 20, 0],
}
POUR_ANGLES = [0, -25, -45]     # penyiram dimiringkan


def rotate_with_pivot(img: Image.Image, pivot, angle: float):
    """Putar gambar (expand) dan hitung posisi baru titik pivot."""
    rotated = img.rotate(angle, resample=Image.Resampling.NEAREST, expand=True)
    cx, cy = img.width / 2, img.height / 2
    dx, dy = pivot[0] - cx, pivot[1] - cy
    t = math.radians(angle)
    nx = dx * math.cos(t) + dy * math.sin(t)
    ny = -dx * math.sin(t) + dy * math.cos(t)
    return rotated, (round(rotated.width / 2 + nx), round(rotated.height / 2 + ny))


def tools(out: Output):
    for name, file in TOOL_SOURCES.items():
        icon = to_art(load_source(file), width=18)
        out.save(f"tool_{name}", icon)
        grip = (icon.width // 2, icon.height - 3) if name == "can" else (2, icon.height - 3)
        angles = {d: POUR_ANGLES for d in SWING_ANGLES} if name == "can" else SWING_ANGLES
        for direction, frames in angles.items():
            for i, angle in enumerate(frames):
                img, pivot = rotate_with_pivot(icon, grip, angle)
                out.save(f"swing_{name}_{direction}_{i}", img, pivot)
                if direction == "right":
                    out.save(f"swing_{name}_left_{i}", mirror(img), (img.width - 1 - pivot[0], pivot[1]))

    out.save("ui_hand", to_art(load_source("Hand.png"), width=14))
    out.save("ui_backpack", to_art(load_source("BackPack.png"), width=16))


def generate(out: Output):
    crops(out)
    items(out)
    tools(out)
