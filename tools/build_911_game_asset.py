import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--frame", required=True)
    parser.add_argument("--wheel-source", required=True)
    parser.add_argument("--output-fbx", required=True)
    parser.add_argument("--output-blend", required=True)
    parser.add_argument("--output-preview", required=True)
    parser.add_argument("--output-report", required=True)
    parser.add_argument("--albedo", required=True)
    parser.add_argument("--normal", required=True)
    return parser.parse_args(argv)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[i] for corner in corners) for i in range(3)))
    maximum = Vector(tuple(max(corner[i] for corner in corners) for i in range(3)))
    return minimum, maximum


def triangle_count(obj):
    return sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons)


def apply_decimate(obj, target_triangles):
    current = triangle_count(obj)
    if current <= target_triangles:
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new(name="GameBudget", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.001, min(1.0, target_triangles / current))
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def bake_world_transform(obj):
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)


def save_image(image_name, filepath, colorspace):
    image = bpy.data.images.get(image_name)
    if image is None:
        raise RuntimeError(f"Missing packed image: {image_name}")
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    image.colorspace_settings.name = colorspace
    image.filepath_raw = filepath
    image.file_format = "PNG"
    image.save()


def create_principled_material(name, base_color, metallic, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*base_color, 1.0)
    metallic_input = shader.inputs.get("Metallic IOR Level") or shader.inputs.get("Metallic")
    if metallic_input is not None:
        metallic_input.default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return material


def assign_wheel_materials(obj, tire_material, rim_material):
    obj.data.materials.clear()
    obj.data.materials.append(tire_material)
    obj.data.materials.append(rim_material)
    radii = [math.hypot(vertex.co.y, vertex.co.z) for vertex in obj.data.vertices]
    outer_radius = max(radii) if radii else 1.0
    rim_limit = outer_radius * 0.70
    for polygon in obj.data.polygons:
        radial = sum(math.hypot(obj.data.vertices[index].co.y, obj.data.vertices[index].co.z)
                     for index in polygon.vertices) / len(polygon.vertices)
        polygon.material_index = 1 if radial < rim_limit else 0
        polygon.use_smooth = True


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


args = parse_args()
for path in (args.output_fbx, args.output_blend, args.output_preview, args.output_report,
             args.albedo, args.normal):
    os.makedirs(os.path.dirname(path), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.frame)
frame_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(frame_objects) != 1:
    raise RuntimeError(f"Expected one frame mesh, found {len(frame_objects)}")
body = frame_objects[0]
body.name = "Body"
bake_world_transform(body)

save_image("Image_0", args.albedo, "sRGB")
save_image("Image_2", args.normal, "Non-Color")

new_min, new_max = world_bounds(body)
new_center = (new_min + new_max) * 0.5
new_size = new_max - new_min

before = set(bpy.context.scene.objects)
bpy.ops.import_scene.gltf(filepath=args.wheel_source)
imported = [obj for obj in bpy.context.scene.objects if obj not in before and obj.type == "MESH"]
by_name = {obj.name: obj for obj in imported}
high_body = by_name.get("part_0")
if high_body is None:
    raise RuntimeError("High-poly reference body part_0 was not found")
high_min, high_max = world_bounds(high_body)
high_center = (high_min + high_max) * 0.5
high_size = high_max - high_min
scale = sum(new_size[i] / high_size[i] for i in range(3)) / 3.0
translation = new_center - high_center * scale

wheel_mapping = {
    "part_1": "Wheel_FL_Spin",
    "part_3": "Wheel_FR_Spin",
    "part_4": "Wheel_RL_Spin",
    "part_5": "Wheel_RR_Spin",
}
wheels = {}
for source_name, target_name in wheel_mapping.items():
    obj = by_name.get(source_name)
    if obj is None:
        raise RuntimeError(f"Missing wheel object {source_name}")
    transformed = []
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        transformed.append(world * scale + translation)
    obj.matrix_world = Matrix.Identity(4)
    for vertex, coordinate in zip(obj.data.vertices, transformed):
        vertex.co = coordinate
    obj.name = target_name
    wheels[target_name] = obj

for obj in imported:
    if obj not in wheels.values():
        bpy.data.objects.remove(obj, do_unlink=True)

apply_decimate(body, 32000)
body.name = "Body"
for polygon in body.data.polygons:
    polygon.use_smooth = True

tire_material = create_principled_material("MAT_911_Tire", (0.012, 0.014, 0.016), 0.05, 0.56)
rim_material = create_principled_material("MAT_911_Rim", (0.24, 0.26, 0.29), 0.82, 0.22)

wheel_centers = {}
for name, wheel in wheels.items():
    apply_decimate(wheel, 1900)
    minimum, maximum = world_bounds(wheel)
    center = (minimum + maximum) * 0.5
    wheel_centers[name] = center.copy()
    for vertex in wheel.data.vertices:
        vertex.co -= center
    wheel.location = center
    assign_wheel_materials(wheel, tire_material, rim_material)

root = bpy.data.objects.new("Porsche911_GT3RS", None)
bpy.context.collection.objects.link(root)
body.parent = root

for side in ("FL", "FR"):
    spin = wheels[f"Wheel_{side}_Spin"]
    center = wheel_centers[spin.name]
    steer = bpy.data.objects.new(f"Wheel_{side}_Steer", None)
    bpy.context.collection.objects.link(steer)
    steer.empty_display_type = "PLAIN_AXES"
    steer.empty_display_size = 0.08
    steer.location = center
    steer.parent = root
    spin.parent = steer
    spin.location = Vector((0.0, 0.0, 0.0))

for side in ("RL", "RR"):
    wheels[f"Wheel_{side}_Spin"].parent = root

for obj in [body, *wheels.values()]:
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth_by_angle()
    obj.select_set(False)

bpy.ops.wm.save_as_mainfile(filepath=args.output_blend)

bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(
    filepath=args.output_fbx,
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
    embed_textures=False,
)

# Neutral studio preview.
floor_z = min((world_bounds(obj)[0].z for obj in wheels.values()), default=-0.32) - 0.008
bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, floor_z))
floor = bpy.context.object
floor.name = "PreviewFloor"
floor.data.materials.append(create_principled_material("MAT_PreviewFloor", (0.07, 0.075, 0.085), 0.0, 0.72))

world = bpy.context.scene.world or bpy.data.worlds.new("World")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.03, 0.04, 1.0)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.32

for name, location, energy, size in (
    ("Key", (2.4, -2.8, 2.6), 1050.0, 3.2),
    ("Fill", (-2.4, -0.8, 1.4), 620.0, 2.8),
    ("Rim", (0.5, 2.7, 2.1), 900.0, 2.2),
):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.collection.objects.link(light)
    light.location = location
    look_at(light, (0.0, 0.0, -0.02))

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (2.15, -3.25, 1.45)
camera_data.lens = 58
look_at(camera, (0.0, 0.0, -0.03))
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 760
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = args.output_preview
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

report = {
    "scale_from_high_reference": scale,
    "translation_from_high_reference": list(translation),
    "body_triangles": triangle_count(body),
    "wheel_triangles": {name: triangle_count(obj) for name, obj in wheels.items()},
    "total_triangles": triangle_count(body) + sum(triangle_count(obj) for obj in wheels.values()),
    "wheel_centers_blender": {name: list(center) for name, center in wheel_centers.items()},
    "hierarchy": {
        "front": ["Wheel_FL_Steer/Wheel_FL_Spin", "Wheel_FR_Steer/Wheel_FR_Spin"],
        "rear": ["Wheel_RL_Spin", "Wheel_RR_Spin"],
    },
}
with open(args.output_report, "w", encoding="utf-8") as handle:
    json.dump(report, handle, ensure_ascii=False, indent=2)
print("BUILD_REPORT=" + json.dumps(report, ensure_ascii=False))
