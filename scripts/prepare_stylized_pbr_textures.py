"""Turn a downloaded PBR texture set into the Unity maps this project uses.

Handles the JulioVII stylized packs and the ambientCG sets, which disagree on
almost every file name. The source packs ship separate metallic, roughness and
glossiness JPEGs. Unity's URP Lit shader wants one metallic/smoothness texture,
so this script combines them and writes the remaining maps under the
repository's naming convention:

    <Name>_BaseColor.jpg  <Name>_Normal.jpg  <Name>_Occlusion.jpg
    <Name>_Height.jpg     <Name>_MetallicGloss.png (RGB metallic, alpha smoothness)

Usage:
    python3 scripts/prepare_stylized_pbr_textures.py \
        --source /tmp/sty_src/Wood --name StylizedWoodPlanks \
        --output LocalPackages/.../Textures/StylizedPbr01/WoodPlanks --size 2048
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

# The packs are inconsistent about capitalisation and about which of the
# roughness/glossiness pair they ship, so each map is matched by keyword.
MAP_KEYWORDS = {
    "basecolor": ("basecolor", "_color"),
    "normal": ("normal",),
    "occlusion": ("ambientocclusion", "_ao"),
    "height": ("height", "displacement"),
    "metallic": ("metallic", "metalness"),
    "roughness": ("roughness",),
    "glossiness": ("glossiness",),
}


def find_map(source: Path, kind: str) -> Path | None:
    for candidate in sorted(source.glob("*.jpg")) + sorted(source.glob("*.png")):
        name = candidate.stem.lower()
        # OpenGL normals are the other handedness; Unity wants the DirectX map.
        if kind == "normal" and name.endswith(("normalogl", "normalgl")):
            continue
        if any(keyword in name for keyword in MAP_KEYWORDS[kind]):
            return candidate
    return None


def load(source: Path, kind: str, size: int) -> Image.Image | None:
    path = find_map(source, kind)
    if path is None:
        return None
    return Image.open(path).resize((size, size), Image.LANCZOS)


def write_jpeg(image: Image.Image, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(destination, quality=92, subsampling=0)
    print(f"wrote {destination}")


def metallic_gloss(source: Path, size: int, metallic_scale: float) -> Image.Image:
    metallic = load(source, "metallic", size)
    if metallic is None:
        # Sets that hold no metal at all ship no metalness map; a black one
        # says the same thing and keeps every material on the same shader path.
        metallic = Image.new("L", (size, size), 0)
    glossiness = load(source, "glossiness", size)
    if glossiness is None:
        roughness = load(source, "roughness", size)
        if roughness is None:
            raise SystemExit(f"{source} has neither glossiness nor roughness map")
        glossiness = Image.eval(roughness.convert("L"), lambda value: 255 - value)
    metallic = metallic.convert("L")
    if metallic_scale != 1.0:
        # A fully metallic prop turns black in a dungeon with no sky and no
        # reflection probes, so the source metal is dialled back to a half
        # metal that still catches torchlight.
        metallic = Image.eval(metallic, lambda value: int(value * metallic_scale))
    return Image.merge("RGBA", (metallic, metallic, metallic, glossiness.convert("L")))


def desaturate(image: Image.Image, amount: float) -> Image.Image:
    grey = image.convert("L").convert("RGB")
    return Image.blend(image.convert("RGB"), grey, amount)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--name", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--size", default=2048, type=int)
    parser.add_argument(
        "--metallic-scale",
        default=1.0,
        type=float,
        help="Scale the metallic map, for metals lit only by torches.",
    )
    parser.add_argument(
        "--brightness",
        default=1.0,
        type=float,
        help="Scale the base color, to lift a set that reads too dark.",
    )
    parser.add_argument(
        "--desaturate",
        default=0.0,
        type=float,
        help="Blend the base color towards greyscale, 0 keeps the source hue.",
    )
    parser.add_argument(
        "--mask-size",
        default=0,
        type=int,
        help="Size for the metallic/smoothness map; 0 matches --size. Masks "
        "carry no detail the eye reads directly, so a prop set can halve it.",
    )
    parser.add_argument(
        "--no-height",
        action="store_true",
        help="Skip the height map, for materials that do not use parallax.",
    )
    arguments = parser.parse_args()

    source: Path = arguments.source
    output: Path = arguments.output
    if not source.is_dir():
        raise SystemExit(f"{source} is not a directory")

    base = load(source, "basecolor", arguments.size)
    if base is None:
        raise SystemExit(f"{source} has no base color map")
    if arguments.desaturate > 0:
        base = desaturate(base, arguments.desaturate)
    if arguments.brightness != 1.0:
        base = Image.eval(
            base.convert("RGB"), lambda value: min(255, int(value * arguments.brightness))
        )
    write_jpeg(base, output / f"{arguments.name}_BaseColor.jpg")

    wanted = [("normal", "Normal"), ("occlusion", "Occlusion")]
    if not arguments.no_height:
        wanted.append(("height", "Height"))
    for kind, suffix in wanted:
        image = load(source, kind, arguments.size)
        if image is None:
            print(f"skipped {suffix}: {source} has no {kind} map")
            continue
        write_jpeg(image, output / f"{arguments.name}_{suffix}.jpg")

    mask_size = arguments.mask_size or arguments.size
    combined = metallic_gloss(source, mask_size, arguments.metallic_scale)
    destination = output / f"{arguments.name}_MetallicGloss.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    combined.save(destination)
    print(f"wrote {destination}")


if __name__ == "__main__":
    main()
