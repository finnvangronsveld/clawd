"""Labelled contact sheet of a --dump-sprites folder.

Usage:  python tools/contact_sheet.py <dump_dir> <out.png> [scale] [name,name,...]
Crops each 1-px-per-cell pose to the area around the pet, scales it up crisply and labels it.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def main(src: str, out: str, scale: str = "4", names: str = "") -> None:
    s = int(scale)
    files = sorted(Path(src).glob("*.png"))
    files = [f for f in files if not f.name.startswith("render_")]
    if names:
        want = names.split(",")
        files = [Path(src) / (n + ".png") for n in want if (Path(src) / (n + ".png")).exists()]
    crop = (9, 0, 51, 34)                                 # around the 22x16 box + props
    cw, ch = (crop[2] - crop[0]) * s, (crop[3] - crop[1]) * s
    cols = 8
    rows = (len(files) + cols - 1) // cols
    pad, label = 6, 16
    sheet = Image.new("RGB", (cols * (cw + pad) + pad, rows * (ch + label + pad) + pad), (34, 36, 44))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("segoeui.ttf", 12)
    except OSError:
        font = ImageFont.load_default()
    for i, f in enumerate(files):
        im = Image.open(f).convert("RGBA").crop(crop).resize((cw, ch), Image.NEAREST)
        x = pad + (i % cols) * (cw + pad)
        y = pad + (i // cols) * (ch + label + pad)
        d.rectangle([x, y, x + cw - 1, y + ch - 1], fill=(52, 55, 66))
        sheet.paste(im, (x, y), im)
        d.text((x + 2, y + ch + 1), f.stem, fill=(200, 200, 210), font=font)
    sheet.save(out)
    print(f"{len(files)} poses -> {out}")


if __name__ == "__main__":
    main(*sys.argv[1:])
