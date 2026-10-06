"""Build docs/clawd.gif from frames rendered by `clawd.ps1 -RenderFrames <dir>`.

Usage:  python tools/make_gif.py <frames_dir> <out.gif>
Magenta (255,0,255) is the transparency key, exactly like on the desktop.
"""
import sys
from pathlib import Path
from PIL import Image

KEY = (255, 0, 255)
FRAME_MS = 60  # two 30 ms ticks per frame


def main(frames_dir: str, out_path: str) -> None:
    files = sorted(Path(frames_dir).glob("f*.png"))
    if not files:
        sys.exit(f"no frames in {frames_dir}")
    frames = [Image.open(f).convert("RGB") for f in files]

    # one shared palette: the art only uses a few dozen flat colors
    colors = {KEY: 0}
    for im in frames:
        for _, c in im.getcolors(maxcolors=1 << 16) or []:
            if c not in colors:
                colors[c] = len(colors)
    if len(colors) > 256:
        sys.exit(f"too many colors for a gif: {len(colors)}")
    palette = [0] * 768
    for c, i in colors.items():
        palette[i * 3:i * 3 + 3] = c

    out = []
    for im in frames:
        p = Image.new("P", im.size)
        p.putpalette(palette)
        p.putdata([colors[px] for px in im.getdata()])
        out.append(p)

    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    out[0].save(out_path, save_all=True, append_images=out[1:], duration=FRAME_MS,
                loop=0, transparency=0, disposal=2, optimize=False)
    print(f"{len(out)} frames, {len(colors)} colors -> {out_path} ({Path(out_path).stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
