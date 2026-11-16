"""Cuts the Level 2 VFX sheets (Tools/VfxSheets/src/*.webp) into transparent per-frame PNGs.

  python -I Tools/VfxSheets/make_vfx.py

Output: Assets/Resources/Level02/Vfx/<name>_NN.png plus vfx.json (read by VfxSheets in FightFx.cs).

Two source styles:
  green  - effect painted over flat chroma green. Alpha is estimated from how far green sits above red/blue, the
           background is then un-mixed out of the colour so translucent glow and smoke carry no green fringe.
  black  - glow painted over black. Alpha = brightest channel, colour is un-premultiplied (additive-looking glow).

Every frame of a sheet is cropped to one common box so the animation stays registered:
  pivot c = box centred on the cell centre, pivot b = box ends at the lowest painted row (ground / base anchored).
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Level02", "Vfx"))

# name, mode, cols, rows, pivot, green prior c (how bright green is in the effect itself, relative to red/blue),
# crop: "common" keeps one box for the whole sheet, "each" crops every frame (separate props), scale: final cell width
SHEETS = [
    dict(name="sun",      mode="black", cols=1, rows=1, pivot="c", c=0.6, crop="common", cell=512),
    dict(name="mandala",  mode="black", cols=1, rows=1, pivot="c", c=0.6, crop="common", cell=512),
    dict(name="mandalaw", src="mandala", white=True, mode="black", cols=1, rows=1, pivot="c", c=0.6, crop="common", cell=512),   # same yantra in white so code can tint it any colour
    dict(name="rock",     mode="green", cols=4, rows=2, pivot="c", c=0.5, crop="each"),
    dict(name="crack",    mode="green", cols=4, rows=1, pivot="c", c=0.5, crop="common"),
    dict(name="dust",     mode="green", cols=4, rows=2, pivot="b", c=0.8, gcap=0.85, crop="common"),
    dict(name="vanish",   mode="green", cols=4, rows=2, pivot="c", c=0.3, crop="common"),
    dict(name="aura",     mode="green", cols=4, rows=2, pivot="c", c=0.7, gcap=0.9, crop="common"),
    dict(name="orb",      mode="green", cols=4, rows=2, pivot="c", c=0.3, crop="common"),
    dict(name="wind",     mode="green", cols=4, rows=2, pivot="c", c=0.9, crop="common"),
    dict(name="crackle",  mode="green", cols=4, rows=2, pivot="c", c=0.8, crop="common"),
    dict(name="bolt",     mode="green", cols=4, rows=2, pivot="b", c=0.8, crop="common"),
    dict(name="fireburst", mode="green", cols=4, rows=2, pivot="c", c=0.6, gcap=0.9, crop="common"),
    dict(name="fire",     mode="green", cols=4, rows=2, pivot="b", c=0.6, crop="common"),
    dict(name="slash",    mode="green", cols=4, rows=2, pivot="c", c=0.9, crop="common"),
    dict(name="impact",   mode="green", cols=4, rows=2, pivot="c", c=0.6, crop="common"),
    dict(name="spark",    mode="green", cols=4, rows=2, pivot="c", c=0.6, crop="common"),
]


def bg_colour(a):
    h, w = a.shape[:2]
    px = np.concatenate([a[:6, :6].reshape(-1, 3), a[-6:, -6:].reshape(-1, 3), a[:6, -6:].reshape(-1, 3), a[-6:, :6].reshape(-1, 3)])
    return np.median(px, axis=0)


def key_green(rgb, bg, c, gcap=None):
    p = rgb.astype(np.float32)
    r, g, b = p[..., 0], p[..., 1], p[..., 2]
    m = np.maximum(r, b)
    t = (g - c * m) / max(bg[1], 1.0)
    a_green = 1.0 - np.clip((t - 0.10) / 0.90, 0.0, 1.0)
    a_low = np.clip((m - max(bg[0], bg[2])) / (255.0 - max(bg[0], bg[2])), 0.0, 1.0)   # a pixel this bright in red/blue is at least this opaque
    a = np.maximum(a_green, a_low)
    a[a < 0.04] = 0.0
    a = np.clip((a - 0.04) / 0.96, 0.0, 1.0)
    safe = np.maximum(a, 1e-3)[..., None]
    fg = (p - (1.0 - a)[..., None] * bg[None, None, :]) / safe
    fg = np.clip(fg, 0, 255)
    if gcap:   # the effect itself is never greener than this: whatever green is left is background bleeding through the fade
        fg[..., 1] = np.minimum(fg[..., 1], gcap * np.maximum(fg[..., 0], fg[..., 2]))
    fg[a <= 0] = 0
    return fg, a


def key_black(rgb):
    p = rgb.astype(np.float32)
    mx = p.max(axis=2) / 255.0
    a = np.clip(mx * 1.1, 0.0, 1.0)
    a[mx < 0.03] = 0.0
    fg = p / np.maximum(mx, 1e-3)[..., None]
    fg = np.clip(fg, 0, 255)
    fg[a <= 0] = 0
    return fg, a


def to_rgba(fg, a):
    return np.dstack([fg, (a * 255.0)[..., None]]).astype(np.float32)


def resize_premult(rgba, w, h):
    """Resize without dark/bright halos: scale colour*alpha, then divide the alpha back out."""
    a = rgba[..., 3:4] / 255.0
    pm = np.dstack([rgba[..., :3] * a, rgba[..., 3:4]])
    chans = [np.asarray(Image.fromarray(pm[..., i].astype(np.float32), mode="F").resize((w, h), Image.LANCZOS)) for i in range(4)]
    out = np.dstack(chans)
    a2 = np.clip(out[..., 3:4] / 255.0, 0, 1)
    rgb = np.where(a2 > 1e-4, out[..., :3] / np.maximum(a2, 1e-4), 0)
    return np.dstack([np.clip(rgb, 0, 255), np.clip(out[..., 3:4], 0, 255)])


def bleed(rgba, iters=10):
    """Push colour outward into the transparent border (alpha stays 0) so bilinear filtering in Unity never blends glow with black."""
    rgb = rgba[..., :3].copy()
    filled = rgba[..., 3] > 8
    for _ in range(iters):
        if filled.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(filled.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                f2 = np.roll(filled, (dy, dx), (0, 1))
                acc += np.roll(rgb, (dy, dx), (0, 1)) * f2[..., None]
                cnt += f2
        newly = (~filled) & (cnt > 0)
        rgb[newly] = acc[newly] / cnt[newly][:, None]
        filled = filled | newly
    out = rgba.copy()
    out[..., :3] = rgb
    return out


def bbox(a, thr=10):
    ys, xs = np.where(a[..., 3] > thr)
    if len(xs) == 0:
        return None
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def crop_box(frame, x0, y0, x1, y1):
    """Crop that may reach outside the frame (padded with transparent pixels)."""
    h, w = frame.shape[:2]
    out = np.zeros((y1 - y0, x1 - x0, 4), np.float32)
    sx0, sy0, sx1, sy1 = max(x0, 0), max(y0, 0), min(x1, w), min(y1, h)
    if sx1 > sx0 and sy1 > sy0:
        out[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = frame[sy0:sy1, sx0:sx1]
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".png") or f == "vfx.json":
            os.remove(os.path.join(OUT, f))
    meta = []
    preview = []
    for s in SHEETS:
        im = Image.open(os.path.join(SRC, s.get("src", s["name"]) + ".webp")).convert("RGB")
        rgb = np.asarray(im)
        if s["mode"] == "green":
            bg = bg_colour(rgb.astype(np.float32))
            fg, a = key_green(rgb, bg, s["c"], s.get("gcap"))
        else:
            fg, a = key_black(rgb)
        if s.get("white"):
            fg = np.full_like(fg, 255.0)
        full = to_rgba(fg, a)
        H, W = full.shape[:2]
        cw, ch = W // s["cols"], H // s["rows"]
        frames = []
        for ry in range(s["rows"]):
            for cx in range(s["cols"]):
                frames.append(full[ry * ch:(ry + 1) * ch, cx * cw:(cx + 1) * cw])
        cell = cw
        if s.get("cell") and s["cell"] != cw:
            k = s["cell"] / cw
            frames = [resize_premult(f, int(round(f.shape[1] * k)), int(round(f.shape[0] * k))) for f in frames]
            cw, ch, cell = frames[0].shape[1], frames[0].shape[0], s["cell"]
        boxes = [bbox(f) for f in frames]
        valid = [b for b in boxes if b]
        pad = 3
        out_frames = []
        if s["crop"] == "each":
            for f, b in zip(frames, boxes):
                b = b or (0, 0, 2, 2)
                out_frames.append(crop_box(f, b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad))
        else:
            cx, cy = cw / 2.0, ch / 2.0
            hx = int(np.ceil(max(max(abs(b[0] - cx), abs(b[2] - cx)) for b in valid))) + pad
            if s["pivot"] == "c":
                hy = int(np.ceil(max(max(abs(b[1] - cy), abs(b[3] - cy)) for b in valid))) + pad
                x0, x1, y0, y1 = int(round(cx)) - hx, int(round(cx)) + hx, int(round(cy)) - hy, int(round(cy)) + hy
            else:
                y1 = max(b[3] for b in valid) + pad
                y0 = min(b[1] for b in valid) - pad
                x0, x1 = int(round(cx)) - hx, int(round(cx)) + hx
            out_frames = [crop_box(f, x0, y0, x1, y1) for f in frames]
        out_frames = [bleed(f) for f in out_frames]
        for i, f in enumerate(out_frames):
            Image.fromarray(np.clip(f, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "%s_%02d.png" % (s["name"], i)))
        meta.append(dict(name=s["name"], n=len(out_frames), cell=cell, pivot=s["pivot"]))
        preview.append((s["name"], out_frames))
        print("%-10s %2d frames  cell %4d  crop %dx%d" % (s["name"], len(out_frames), cell, out_frames[0].shape[1], out_frames[0].shape[0]))
    with open(os.path.join(OUT, "vfx.json"), "w") as fh:
        json.dump(dict(sheets=meta), fh, indent=1)

    if len(sys.argv) > 1:   # optional: python make_vfx.py preview.png  -> contact sheet on a mid-grey checker
        tile = 150
        rows = []
        for name, fr in preview:
            row = Image.new("RGB", (tile * 8, tile), (70, 70, 80))
            for i, f in enumerate(fr[:8]):
                t = Image.fromarray(np.clip(f, 0, 255).astype(np.uint8), "RGBA")
                t.thumbnail((tile - 4, tile - 4))
                row.paste(t, (i * tile + 2, 2), t)
            rows.append(row)
        sheet = Image.new("RGB", (tile * 8, tile * len(rows)))
        for i, r in enumerate(rows):
            sheet.paste(r, (0, i * tile))
        sheet.save(sys.argv[1])


if __name__ == "__main__":
    main()
