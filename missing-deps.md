# Missing dependencies (optional)

These tools unlock the **deferred parts of the autoplay / E2E harness**
(see `plans/2026-06-13-autoplay-e2e-harness.md`). None are required — frames,
telemetry, scenario assertions, and hot-reload lighting tuning all work without
them. They add **video/montage output** and **offline luminance analysis**.

Platform: macOS (this machine). Copy-paste when you're back at the computer.

## TL;DR

```bash
# 1. Homebrew packages (video + image montage)
brew install ffmpeg imagemagick

# 2. Python image lib for the luminance analyzer
python3 -m pip install --user Pillow
```

Then verify:

```bash
ffmpeg -version | head -1
magick -version | head -1
python3 -c "import PIL; print('Pillow', PIL.__version__)"
```

## What each one unlocks

| Dependency | Install | Used for |
|---|---|---|
| **ffmpeg** | `brew install ffmpeg` | Phase 3 video capture — encode a run's `frames/*.png` into an `.mp4` to watch a playthrough |
| **ImageMagick** | `brew install imagemagick` | Frame **contact sheets / montages** (one image overview of a run); provides `magick` / `montage` |
| **Pillow (PIL)** | `python3 -m pip install --user Pillow` | `scripts/analyze-frames.py` — prints mean luminance in top/mid/bottom screen bands (objective fog-of-war + overexposure checks). Runs automatically at the end of every `scripts/autoplay-*.sh` run once installed. |

## If Homebrew isn't installed

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

## If `pip install` is blocked (PEP 668 "externally-managed-environment")

```bash
# easiest: allow the user-site install
python3 -m pip install --user --break-system-packages Pillow
# or use a throwaway venv
python3 -m venv ~/.venvs/brocoli && ~/.venvs/brocoli/bin/pip install Pillow
```

## Quick use once installed

Replace `<run>` with an output dir (e.g. `AutoplayRuns/20260613-150000`, or whatever
`--out` you passed; default runs land in `AutoplayRuns/`, which is git-ignored).

```bash
# Luminance bands — automatic at the end of any autoplay run, or manually:
python3 scripts/analyze-frames.py <run>/frames

# MP4 from a run's frames:
ffmpeg -framerate 10 -pattern_type glob -i '<run>/frames/frame_*.png' \
  -c:v libx264 -pix_fmt yuv420p <run>/run.mp4

# Contact sheet (5 columns):
magick montage '<run>/frames/frame_*.png' -tile 5x -geometry 240x+2+2 <run>/contact.png
```

> Note: the mp4/montage commands above are manual one-liners. Wiring them into a
> single `scripts/autoplay.sh build→run→montage→summarize` pipeline is the last
> remaining Phase 3 item.

## Not needed: macOS Screen Recording permission

Earlier we hit `screencapture` being blocked (the responsible process is the
detached **zellij** daemon, not Alacritty/claude). **You do not need to fix this**
for the harness — it captures the game's own framebuffer in-engine via
`ScreenCapture.CaptureScreenshot`, which needs no OS permission. Only relevant if
you ever want OS-level screenshots from this shell: grant Screen Recording to
`~/.cargo/bin/zellij` and restart the zellij server.
