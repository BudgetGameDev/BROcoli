"""Prepare UriZX's Sketchfab dungeon wall torch for BROcoli.

Run with Blender, for example:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python scripts/prepare_dungeon_wall_torch.py -- \
      --input /path/to/dungeon_wall_torch.glb \
      --output Temp/DungeonWallTorchPrepared

The script keeps the supplied normals and UVs, validates the mesh, gives it a
wall-facing origin, extracts its PBR textures, packs glTF metallic/roughness
into Unity's metallic/smoothness convention, and exports a Unity-ready FBX.
"""

from __future__ import annotations

import argparse
import json
import sys
from math import pi
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--max-triangles", type=int, default=5000)
    return parser.parse_args(argv)


def linked_image(node, input_name: str):
    socket = node.inputs.get(input_name)
    if socket is None or not socket.is_linked:
        return None
    source = socket.links[0].from_node
    if source.type == "TEX_IMAGE":
        return source.image
    if source.type in {"NORMAL_MAP", "SEPARATE_COLOR"}:
        for input_socket in source.inputs:
            if input_socket.is_linked:
                upstream = input_socket.links[0].from_node
                if upstream.type == "TEX_IMAGE":
                    return upstream.image
    return None


def save_png(image, path: Path, color_space: str) -> None:
    image.colorspace_settings.name = color_space
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()


def pack_metallic_smoothness(source, path: Path) -> None:
    width, height = source.size
    source_pixels = np.empty(width * height * 4, dtype=np.float32)
    source.pixels.foreach_get(source_pixels)
    source_pixels = source_pixels.reshape((-1, 4))

    # glTF: metallic=B, roughness=G. Unity URP Lit: metallic=R,
    # smoothness=A. The unused color channels remain black.
    packed_pixels = np.zeros_like(source_pixels)
    packed_pixels[:, 0] = source_pixels[:, 2]
    packed_pixels[:, 3] = 1.0 - source_pixels[:, 1]

    packed = bpy.data.images.new(
        "DungeonWallTorch_MetallicSmoothness",
        width=width,
        height=height,
        alpha=True,
    )
    packed.colorspace_settings.name = "Non-Color"
    packed.pixels.foreach_set(packed_pixels.ravel())
    packed.filepath_raw = str(path)
    packed.file_format = "PNG"
    packed.save()


def triangle_count(obj) -> int:
    return sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)


def main() -> None:
    args = parse_args()
    source = args.input.resolve()
    output = args.output.resolve()
    textures = output / "Textures"
    output.mkdir(parents=True, exist_ok=True)
    textures.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))

    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("The source GLB contains no mesh objects.")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objects[0]

    # Apply imported transforms before joining so the final FBX is one stable,
    # prefab-friendly mesh with identity transform.
    for obj in mesh_objects:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.objects.active = mesh_objects[0]
    if len(mesh_objects) > 1:
        bpy.ops.object.join()

    model = bpy.context.view_layer.objects.active
    model.name = "DungeonWallTorchModel"
    model.data.name = "DungeonWallTorchMesh"

    # Validate without replacing the supplied normals/tangents. The downloaded
    # mesh is already below the 5k-triangle budget, so no lossy decimation is
    # applied unless a future source revision exceeds that ceiling.
    model.data.validate(verbose=True, clean_customdata=False)
    before_triangles = triangle_count(model)
    if before_triangles > args.max_triangles:
        modifier = model.modifiers.new("Runtime triangle budget", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = args.max_triangles / before_triangles
        bpy.context.view_layer.objects.active = model
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    # The model protrudes toward Blender -Y. Put the origin on its positive-Y
    # mounting plane, horizontally centered, with its lowest point at Z=0.
    bpy.context.view_layer.update()
    bounds = [Vector(corner) for corner in model.bound_box]
    minimum = Vector(tuple(min(corner[axis] for corner in bounds) for axis in range(3)))
    maximum = Vector(tuple(max(corner[axis] for corner in bounds) for axis in range(3)))
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -maximum.y, -minimum.z))
    for vertex in model.data.vertices:
        vertex.co += offset
    model.data.update()

    # Blender's FBX exporter and Unity's left-handed importer otherwise mirror
    # this GLB vertically. Rotate the prepared mesh 180 degrees around its FBX
    # Z axis (rather than applying a reflection) so Unity gets +Y-up geometry
    # while the supplied normal orientation stays intact.
    model.data.transform(Matrix.Rotation(pi, 4, "Z"))
    model.data.update()

    material = next((slot.material for slot in model.material_slots if slot.material), None)
    if material is None or material.node_tree is None:
        raise RuntimeError("The source model has no node-based material.")
    principled = next(
        (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        raise RuntimeError("The source material has no Principled BSDF node.")

    base_color = linked_image(principled, "Base Color")
    emission = linked_image(principled, "Emission Color")
    normal = linked_image(principled, "Normal")
    metallic_roughness = linked_image(principled, "Metallic")
    if any(image is None for image in (base_color, emission, normal, metallic_roughness)):
        raise RuntimeError("Could not resolve all required PBR texture inputs.")

    save_png(base_color, textures / "DungeonWallTorch_BaseColor.png", "sRGB")
    save_png(emission, textures / "DungeonWallTorch_Emission.png", "sRGB")
    save_png(normal, textures / "DungeonWallTorch_Normal.png", "Non-Color")
    pack_metallic_smoothness(
        metallic_roughness,
        textures / "DungeonWallTorch_MetallicSmoothness.png",
    )

    bpy.ops.object.select_all(action="DESELECT")
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    fbx_path = output / "DungeonWallTorch.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
        use_tspace=True,
        path_mode="STRIP",
        embed_textures=False,
    )

    bpy.context.view_layer.update()
    final_bounds = [model.matrix_world @ Vector(corner) for corner in model.bound_box]
    final_minimum = Vector(tuple(min(corner[axis] for corner in final_bounds) for axis in range(3)))
    final_maximum = Vector(tuple(max(corner[axis] for corner in final_bounds) for axis in range(3)))
    report = {
        "source": str(source),
        "blenderVersion": bpy.app.version_string,
        "meshObjects": len(mesh_objects),
        "vertices": len(model.data.vertices),
        "trianglesBefore": before_triangles,
        "trianglesAfter": triangle_count(model),
        "boundsMin": [round(value, 6) for value in final_minimum],
        "boundsMax": [round(value, 6) for value in final_maximum],
        "fbx": str(fbx_path),
    }
    (output / "preparation-report.json").write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
    )
    print("DUNGEON_WALL_TORCH_PREPARED " + json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    main()
