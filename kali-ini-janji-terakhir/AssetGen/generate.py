"""Generator sprite: membuat Assets/Sprites/*.png + sprites.json dari aset lama di Assets/FileAssets.

Jalankan dari folder proyek:
    python AssetGen/generate.py

Butuh: Python 3.10+, Pillow, numpy  (pip install pillow numpy)
Hasilnya sudah ikut di-commit, jadi game tidak butuh Python saat dijalankan.
Jalankan ulang hanya jika aset lama berubah atau generator diedit.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import characters  # noqa: E402
import crops_items  # noqa: E402
import effects  # noqa: E402
import objects  # noqa: E402
import terrain  # noqa: E402
import ui  # noqa: E402
from pixel import Output  # noqa: E402


def main():
    out = Output()
    for module in (characters, terrain, objects, crops_items, ui, effects):
        module.generate(out)
        print(f"  - {module.__name__}")
    out.finish()


if __name__ == "__main__":
    main()
