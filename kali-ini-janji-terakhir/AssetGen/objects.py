"""Benda di dunia: bangunan, pohon, batu, pagar, hewan, bayangan.

Semua sprite benda berjangkar di tengah-bawah (titik alas), sehingga game cukup
memanggil Draw(sprite, x, y) dengan (x, y) = posisi kaki benda.
"""
import numpy as np

from pixel import (TILE_ART, Output, canvas, ellipse, load_source, mirror, outline, rect,
                   shift_rows, to_art, with_alpha)


def _opaque(img):
    return np.array(img)[:, :, 3] > 0


# --------------------------------------------------------------------------- buildings
def house(out: Output):
    out.save_bottom_center("house", to_art(load_source("Rumah.png"), width=144))


def shop(out: Output):
    # Papan nama "TOKO BU LASTRI" tidak terbaca di resolusi rendah, jadi toko disimpan
    # langsung pada ukuran layar (skala 1) agar tulisannya tetap jelas.
    art = to_art(load_source("toko-bu-lastri.png"), width=192)
    out.save("shop", art, (art.width // 2, art.height), scale=1)


def shipping_bin(out: Output):
    """Peti jual kayu (bentuknya dibuat baru, warnanya dari peti-barang lama)."""
    wood, dark, light, metal = (150, 98, 58, 255), (104, 75, 57, 255), (200, 146, 92, 255), (120, 120, 130, 255)
    img = canvas(26, 22)
    rect(img, (1, 7, 24, 21), wood)                     # badan
    for y in (11, 15, 19):
        rect(img, (1, y, 24, y), dark)                 # sambungan papan
    rect(img, (0, 3, 25, 7), dark)                       # tutup
    rect(img, (1, 3, 24, 4), light)
    rect(img, (3, 7, 4, 21), metal)                      # pengikat besi
    rect(img, (21, 7, 22, 21), metal)
    rect(img, (11, 9, 14, 11), (230, 200, 90, 255))      # gembok
    out.save_bottom_center("shipping_bin", outline(img))


# --------------------------------------------------------------------------- nature
def trees(out: Output):
    tree = to_art(load_source("pohon.png"), width=75)
    out.save_bottom_center("tree", tree)

    # Tunggul: potong batang bagian bawah pohon, lalu beri permukaan potongan.
    a = _opaque(tree)
    h = tree.height
    cols = np.nonzero(a[h - 4:h].any(axis=0))[0]
    x0, x1 = int(cols.min()) - 1, int(cols.max()) + 2
    stump = canvas(x1 - x0 + 2, 12)
    stump.alpha_composite(tree.crop((x0, h - 9, x1, h)), (1, 3))
    ellipse(stump, (0, 0, stump.width - 1, 5), (196, 156, 104, 255))
    ellipse(stump, (3, 1, stump.width - 4, 4), (226, 190, 136, 255))
    out.save_bottom_center("stump", outline(stump))

    # Semak: tajuk pohon diperkecil menjadi gumpalan daun bulat.
    crown = tree.crop((0, 0, tree.width, int(tree.height * 0.72)))
    out.save_bottom_center("bush", to_art(crown, width=28))


def rocks(out: Output):
    src = load_source("batu.png")
    out.save_bottom_center("rock", to_art(src, width=20))
    out.save_bottom_center("rock_small", to_art(src, width=13))


def fences(out: Output):
    """Pagar autotile (16 varian) dari strip pagar.png lama: tiang + palang."""
    strip = to_art(load_source("pagar.png"), width=337)
    a = _opaque(strip)
    h = strip.height
    top_band = a[: max(2, h // 8)].any(axis=0)              # hanya tiang yang mencapai bagian atas
    xs = np.nonzero(top_band)[0]
    p0 = int(xs[0])
    p1 = p0
    while p1 + 1 < strip.width and top_band[p1 + 1]:
        p1 += 1
    post = strip.crop((p0, 0, p1 + 1, h))
    rail = strip.crop((p1 + 4, 0, p1 + 5, h))              # satu kolom palang di antara tiang
    rail_px = np.array(rail)
    rail_rows = [y for y in range(h) if rail_px[y, 0, 3] > 0]

    foot = 5                                                # tiang berdiri 5 art px di atas tepi bawah tile
    hgt = h + foot
    cx = TILE_ART // 2
    for mask in range(16):
        img = canvas(TILE_ART, hgt)
        if mask & 8:
            for x in range(0, cx):
                img.alpha_composite(rail, (x, 0))
        if mask & 2:
            for x in range(cx, TILE_ART):
                img.alpha_composite(rail, (x, 0))
        if mask & (1 | 4):                                  # palang tegak (pagar arah utara-selatan)
            y_top = 0 if mask & 1 else h - 6
            y_bot = hgt - 1 if mask & 4 else h - 1
            col = rail_px[rail_rows[0], 0] if rail_rows else (190, 150, 100, 255)
            rect(img, (cx - 1, y_top, cx, y_bot), tuple(int(c) for c in col))
            rect(img, (cx + 1, y_top, cx + 1, y_bot), (110, 76, 50, 255))
        img.alpha_composite(post, (cx - post.width // 2, 0))
        out.save(f"fence_{mask}", img, (TILE_ART // 2, hgt))


# --------------------------------------------------------------------------- animals
def chickens(out: Output):
    base = to_art(load_source("ayam.png"), width=16)
    img = canvas(base.width + 2, base.height + 2)
    img.alpha_composite(base, (1, 2))
    h = img.height
    feet = h - 3
    frames = {
        "idle": [img, shift_rows(img, 0, feet, 0, 1)],
        "walk": [shift_rows(img, 0, feet, 0, -1), img],
        "peck": [shift_rows(img, 0, int(h * 0.55), 1, 2)],
    }
    for action, imgs in frames.items():
        for i, f in enumerate(imgs):
            out.save_bottom_center(f"chicken_{action}_right_{i}", f, foot_margin=1)
            out.save_bottom_center(f"chicken_{action}_left_{i}", mirror(f), foot_margin=1)


def butterflies(out: Output):
    colors = {"white": (245, 245, 235, 255), "yellow": (250, 214, 80, 255), "pink": (240, 150, 190, 255)}
    body = (50, 36, 30, 255)
    for name, c in colors.items():
        for frame in range(2):
            img = canvas(7, 5)
            if frame == 0:
                for x, y in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (5, 0), (6, 0), (4, 1), (5, 1), (6, 1), (1, 3), (5, 3)):
                    img.putpixel((x, y), c)
            else:
                for x, y in ((2, 0), (2, 1), (4, 0), (4, 1), (2, 3), (4, 3)):
                    img.putpixel((x, y), c)
            for y in range(1, 5):
                img.putpixel((3, y), body)
            out.save_bottom_center(f"butterfly_{name}_{frame}", img)


def egg(out: Output):
    img = canvas(5, 6)
    ellipse(img, (0, 0, 4, 5), (248, 238, 214, 255))
    img.putpixel((1, 1), (255, 255, 250, 255))
    out.save_bottom_center("egg", outline(img))


def shadows(out: Output):
    """Bayangan elips semi-transparan: menggantikan FillEllipse di kode."""
    for name, (w, h) in {"s": (12, 4), "m": (18, 6), "l": (30, 8), "xl": (48, 12)}.items():
        img = canvas(w, h)
        ellipse(img, (0, 0, w - 1, h - 1), (0, 0, 0, 255))
        out.save(f"shadow_{name}", with_alpha(img, 0.28), (w // 2, h // 2))


def generate(out: Output):
    house(out)
    shop(out)
    shipping_bin(out)
    trees(out)
    rocks(out)
    fences(out)
    chickens(out)
    butterflies(out)
    egg(out)
    shadows(out)
