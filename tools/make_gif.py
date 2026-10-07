"""Build docs/clawd.gif from frames rendered by `Clawd.exe --render-frames <dir>`.

Usage:  python tools/make_gif.py <frames_dir> <out.gif>
The frames are a little fake desktop (opaque), rendered at 30 fps. One shared palette is picked
from a sample of frames and applied without dithering, so the pixel art stays crisp.
"""
import sys
from pathlib import Path
from PIL import Image

FRAME_MS = 33


def main(frames_dir: str, out_path: str) -> None:
    files = sorted(Path(frames_dir).glob("f*.png"))
    if not files:
        sys.exit(f"no frames in {frames_dir}")
    frames = [Image.open(f).convert("RGB") for f in files]

    # palette from a strip of sampled frames
    sample = frames[:: max(1, len(frames) // 24)]
    w, h = sample[0].size
    strip = Image.new("RGB", (w, h * len(sample)))
    for i, im in enumerate(sample):
        strip.paste(im, (0, i * h))
    pal = strip.quantize(colors=255, method=Image.Quantize.MEDIANCUT)

    out = [im.quantize(palette=pal, dither=Image.Dither.NONE) for im in frames]
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    out[0].save(out_path, save_all=True, append_images=out[1:], duration=FRAME_MS, loop=0, optimize=True, disposal=1)
    print(f"{len(out)} frames -> {out_path} ({Path(out_path).stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
