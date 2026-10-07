"""Pixel-diff two --dump-sprites folders.

Usage:  python tools/diff_sprites.py <before_dir> <after_dir> [<diff_out_dir>]
Prints every state whose pixels differ (and how many), compares anchors.txt, and writes
side-by-side before | after | diff images (scaled 4x) for the differing ones.
"""
import sys
from pathlib import Path
from PIL import Image, ImageChops


def main(a: str, b: str, out: str = "") -> int:
    A, B = Path(a), Path(b)
    names = sorted({p.name for p in A.glob("*.png")} | {p.name for p in B.glob("*.png")})
    same, diff = 0, []
    for n in names:
        pa, pb = A / n, B / n
        if not pa.exists() or not pb.exists():
            diff.append((n, "missing in " + ("before" if not pa.exists() else "after")))
            continue
        ia, ib = Image.open(pa).convert("RGBA"), Image.open(pb).convert("RGBA")
        if ia.size != ib.size:
            diff.append((n, f"size {ia.size} -> {ib.size}"))
            continue
        d = ImageChops.difference(ia, ib)
        bbox = d.getbbox(alpha_only=False)      # (by default RGBA bboxes only look at alpha)
        if bbox is None:
            same += 1
            continue
        count = sum(1 for px in d.getdata() if px != (0, 0, 0, 0))
        diff.append((n, f"{count} px differ"))
        if out:
            Path(out).mkdir(parents=True, exist_ok=True)
            w, h = ia.size
            sheet = Image.new("RGBA", (w * 3 + 4, h), (40, 40, 48, 255))
            sheet.paste(ia, (0, 0), ia)
            sheet.paste(ib, (w + 2, 0), ib)
            mask = d.convert("L").point(lambda v: 255 if v else 0)
            sheet.paste(Image.new("RGBA", (w, h), (255, 0, 0, 255)), (2 * w + 4, 0), mask)
            sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(Path(out) / n)
    ta, tb = (A / "anchors.txt").read_text(), (B / "anchors.txt").read_text()
    print(f"{same} identical, {len(diff)} different (of {len(names)} images)")
    for n, why in diff:
        print(f"  DIFF {n}: {why}")
    print("anchors.txt identical" if ta == tb else "anchors.txt DIFFERS")
    if ta != tb:
        for la, lb in zip(ta.splitlines(), tb.splitlines()):
            if la != lb:
                print("  - " + la + "\n  + " + lb)
    return 0


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:]))
