#!/usr/bin/env python3
"""Offline luminance analysis of autoplay frames.

Reports average luminance (0=black .. 1=white) in top / middle / bottom screen
bands for a sample of frames. Useful for objectively checking the fog-of-war
gradient (top band should be darkest) and player/ground overexposure (a band near
1.0 is blown out). Gracefully no-ops if Pillow is not installed.

Usage: python3 analyze-frames.py <frames_dir>
"""

import glob
import os
import sys

try:
    from PIL import Image
except Exception:
    print(
        "[analyze] Pillow not installed; skipping luminance analysis "
        "(pip install Pillow to enable)."
    )
    sys.exit(0)

frames_dir = sys.argv[1] if len(sys.argv) > 1 else "."
files = sorted(glob.glob(os.path.join(frames_dir, "*.png")))
if not files:
    print("[analyze] no frames found in", frames_dir)
    sys.exit(0)

n = len(files)
sample = sorted({round(i * (n - 1) / 7.0) for i in range(8)}) if n > 1 else [0]


def band_luminance(img, y0, y1):
    w, h = img.size
    crop = img.crop((0, int(h * y0), w, int(h * y1))).convert("L")
    data = crop.getdata()
    return (sum(data) / len(data)) / 255.0


print("[analyze] mean luminance by screen band (0=black .. 1=white):")
print("  {:<18}{:>8}{:>8}{:>8}".format("frame", "top", "mid", "bottom"))
for i in sample:
    img = Image.open(files[i])
    top = band_luminance(img, 0.0, 0.33)
    mid = band_luminance(img, 0.33, 0.66)
    bot = band_luminance(img, 0.66, 1.0)
    print(f"  {os.path.basename(files[i]):<18}{top:>8.3f}{mid:>8.3f}{bot:>8.3f}")
