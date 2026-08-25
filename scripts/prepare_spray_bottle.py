"""Run inside Blender to create the Unity-ready sanitizer bottle FBX."""

import bpy
from pathlib import Path
import sys


def output_path() -> Path:
    separator = sys.argv.index("--")
    if len(sys.argv) <= separator + 1:
        raise RuntimeError("Expected output FBX path after --")
    return Path(sys.argv[separator + 1]).resolve()


def set_material(name: str, color: tuple[float, float, float, float]) -> None:
    material = bpy.data.materials.get(name)
    if material is None:
        return
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is not None:
        base_color = shader.inputs["Base Color"]
        for link in list(base_color.links):
            material.node_tree.links.remove(link)
        base_color.default_value = color
        shader.inputs["Roughness"].default_value = 0.48


def prepare() -> None:
    destination = output_path()
    destination.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.frame_set(1)

    mesh_objects = [item for item in scene.objects if item.type == "MESH"]
    for item in mesh_objects:
        item.hide_set(False)
        item.hide_render = False
        item.animation_data_clear()
        for modifier in list(item.modifiers):
            if modifier.type == "SUBSURF":
                modifier.levels = 1
                modifier.render_levels = 1
                bpy.context.view_layer.objects.active = item
                item.select_set(True)
                bpy.ops.object.modifier_apply(modifier=modifier.name)
                item.select_set(False)

    set_material("Material", (0.92, 0.96, 1.0, 1.0))
    set_material("Material.001", (0.03, 0.20, 0.67, 1.0))
    set_material("Material.002", (0.06, 0.42, 0.92, 1.0))

    bpy.ops.object.select_all(action="DESELECT")
    for item in mesh_objects:
        item.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(destination),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    triangles = sum(len(item.data.loop_triangles) for item in mesh_objects)
    print(f"BROCOLI_BOTTLE_EXPORT={destination}")
    print(f"BROCOLI_BOTTLE_TRIANGLES={triangles}")


prepare()
