"""Karakter: pemain + 3 warga (hasil recolor) dengan animasi 4 arah.

Sumber (aset lama):
  Karakter(MenghadapDepan)            -> idle bawah
  Karakter(jalanKebawah), (...2)      -> langkah bawah
  Karakter(MenghadapBelakang(keatas)) -> idle atas (langkah atas dibuat dengan mengangkat kaki)
  Karakter(JalanKanan), (LariKanan)   -> langkah kanan (kiri = cermin)

Nama hasil: {siapa}_{aksi}_{arah}_{frame}
  aksi: idle (2 frame napas), walk (4 frame), use (2 frame: ancang-ancang, hentak)
  arah: down, up, left, right
"""
from pixel import (Output, canvas, load_source, mirror, paste_bottom_center, recolor,
                   shift_rows, to_art)

import numpy as np
from PIL import Image

CANVAS_W, CANVAS_H = 32, 46       # art pixel; kaki di baris paling bawah
FRONT_HEIGHT_ART = 41             # tinggi pose depan dalam art pixel (resolusi asli gambar)


def _load_poses():
    files = {
        "front": "Karakter(MenghadapDepan).png",
        "down1": "Karakter(jalanKebawah).png",
        "down2": "Karakter(JalanKebawah2).png",
        "back": "Karakter(MenghadapBelakang(keatas)).png",
        "side": "Karakter(JalanKanan).png",
        "run": "Karakter(LariKanan).png",
    }
    raw = {k: load_source(v) for k, v in files.items()}
    factor = FRONT_HEIGHT_ART / raw["front"].height     # satu faktor untuk semua pose = ukuran konsisten
    base = canvas(CANVAS_W, CANVAS_H)
    return {k: paste_bottom_center(base, to_art(img, factor=factor)) for k, img in raw.items()}


def _body_rows(img):
    """(baris teratas, baris awal kaki, baris terbawah) dari bentuk karakter."""
    ys = np.nonzero(np.array(img)[:, :, 3].any(axis=1))[0]
    top, bottom = int(ys.min()), int(ys.max()) + 1
    legs = bottom - 7
    return top, legs, bottom


def bob(img, dy):
    """Badan (di atas kaki) naik/turun dy piksel; kaki tetap menapak."""
    top, legs, _ = _body_rows(img)
    moved = np.array(shift_rows(img, top - 1, legs, 0, dy))
    if dy < 0:  # isi celah antara badan dan kaki dengan baris aslinya
        original = np.array(img)
        gap = moved[legs - 1, :, 3] == 0
        moved[legs - 1][gap] = original[legs - 1][gap]
    return Image.fromarray(moved)


def lift_leg(img, left: bool):
    """Angkat satu kaki 1 piksel: dipakai untuk membuat langkah dari pose diam (hadap atas)."""
    _, legs, bottom = _body_rows(img)
    a = np.array(img)
    cols = np.nonzero(a[legs:bottom, :, 3].any(axis=0))[0]
    mid = (int(cols.min()) + int(cols.max()) + 1) // 2
    out = a.copy()
    xs = slice(0, mid) if left else slice(mid, a.shape[1])
    region = a[legs:bottom, xs].copy()
    out[legs:bottom, xs] = 0
    out[legs - 1:bottom - 1, xs] = np.where(region[:, :, 3:4] > 0, region, out[legs - 1:bottom - 1, xs])
    return Image.fromarray(out)


def build_frames(p):
    """Semua frame satu karakter dari pose dasarnya."""
    right_idle = p["side"]
    frames = {
        ("idle", "down"): [p["front"], bob(p["front"], 1)],
        ("idle", "up"): [p["back"], bob(p["back"], 1)],
        ("idle", "right"): [right_idle, bob(right_idle, 1)],
        ("walk", "down"): [p["down1"], bob(p["front"], -1), p["down2"], bob(p["front"], -1)],
        ("walk", "up"): [lift_leg(p["back"], True), bob(p["back"], -1), lift_leg(p["back"], False), bob(p["back"], -1)],
        ("walk", "right"): [p["side"], bob(p["side"], -1), p["run"], bob(p["run"], -1)],
        ("use", "down"): [bob(p["front"], -1), bob(p["down1"], 1)],
        ("use", "up"): [bob(p["back"], -1), bob(p["back"], 1)],
        ("use", "right"): [bob(p["side"], -1), bob(p["run"], 1)],
    }
    for action in ("idle", "walk", "use"):
        frames[(action, "left")] = [mirror(f) for f in frames[(action, "right")]]
    return frames


# Warna rambut pirang dan baju hijau pemain diganti untuk tiap warga.
HEAD_ROWS = 27   # rambut hanya dicari di area kepala & kuncir, agar tas & sepatu tidak ikut


def _hair(h, s, v):
    rows = np.indices(h.shape)[0]
    head = (rows < 20) | ((rows < HEAD_ROWS) & (v > 0.6))
    skin = (h < 32) & (s < 0.5) & (v > 0.72)
    return head & (h >= 20) & (h < 62) & (s > 0.4) & (v > 0.3) & ~skin


def _dress(h, s, v): return (h > 70) & (h < 175) & (s > 0.18)


NPC_LOOKS = {
    # Kakek Wira: rambut putih, baju cokelat tani.
    "wira": [(_hair, lambda h, s, v: (h, s * 0.08, v * 0.98)),
             (_dress, lambda h, s, v: (h * 0 + 30, s * 0.55, v * 0.85))],
    # Bu Lastri: rambut cokelat gelap, baju merah bata.
    "lastri": [(_hair, lambda h, s, v: (h * 0 + 18, s * 0.75, v * 0.5)),
               (_dress, lambda h, s, v: (h * 0 + 355, s * 1.1, v * 0.95))],
    # Pak Rentenir: rambut hitam, baju ungu tua.
    "rentenir": [(_hair, lambda h, s, v: (h * 0 + 230, s * 0.25, v * 0.28)),
                 (_dress, lambda h, s, v: (h * 0 + 275, s * 0.9, v * 0.7))],
}


def generate(out: Output):
    frames = build_frames(_load_poses())
    looks = {"player": None, **NPC_LOOKS}
    for who, rules in looks.items():
        for (action, direction), images in frames.items():
            for i, img in enumerate(images):
                art = img if rules is None else recolor(img, rules)
                out.save_bottom_center(f"{who}_{action}_{direction}_{i}", art, foot_margin=1)
    return frames
