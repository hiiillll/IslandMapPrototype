import argparse
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
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
    parser.add_argument("--body-albedo", required=True)
    parser.add_argument("--body-normal", required=True)
    parser.add_argument("--wheel-albedo", required=True)
    parser.add_argument("--wheel-normal", required=True)
    return parser.parse_args(argv)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(corner[i] for corner in corners) for i in range(3)))
    maximum = Vector(tuple(max(corner[i] for corner in corners) for i in range(3)))
    return minimum, maximum


def triangle_count(obj):
    return sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons)


def bake_world_transform(obj):
    world_matrix = obj.matrix_world.copy()
    obj.parent = None
    obj.data.transform(world_matrix)
    obj.matrix_world = Matrix.Identity(4)


def find_material_images(obj):
    albedo = None
    normal = None
    for material in obj.data.materials:
        if material is None or not material.node_tree:
            continue
        for node in material.node_tree.nodes:
            if node.type != "TEX_IMAGE" or node.image is None:
                continue
            for output in node.outputs:
                for link in output.links:
                    if link.to_socket.name == "Base Color":
                        albedo = node.image
                    if link.to_node.type == "NORMAL_MAP":
                        normal = node.image
    if albedo is None or normal is None:
        raise RuntimeError(f"Could not locate albedo/normal images for {obj.name}")
    return albedo, normal


def save_image(image, filepath, colorspace):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    image.colorspace_settings.name = colorspace
    image.filepath_raw = filepath
    image.file_format = "PNG"
    image.save()


def weld_and_separate_loose(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()

    before = set(bpy.context.scene.objects)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    return [candidate for candidate in bpy.context.scene.objects
            if candidate.type == "MESH" and (candidate == obj or candidate not in before)]


def classify_wheel_parts(parts):
    wheels = {}
    for obj in parts:
        minimum, maximum = world_bounds(obj)
        center = (minimum + maximum) * 0.5
        size = maximum - minimum
        is_wheel = abs(center.x) > 0.25 and minimum.z < -0.20 and size.y > 0.24 and size.z > 0.24
        if not is_wheel:
            continue
        if center.y < 0.0:
            key = "FL" if center.x < 0.0 else "FR"
        else:
            key = "RL" if center.x < 0.0 else "RR"
        wheels[key] = obj
    return wheels


def mirror_centered_wheel(source):
    mirrored = source.copy()
    mirrored.data = source.data.copy()
    bpy.context.collection.objects.link(mirrored)
    bm = bmesh.new()
    bm.from_mesh(mirrored.data)
    for vertex in bm.verts:
        vertex.co.x = -vertex.co.x
    bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(mirrored.data)
    bm.free()
    mirrored.data.update()
    mirrored.location = Vector((-source.location.x, source.location.y, source.location.z))
    return mirrored


def center_wheel_object(obj):
    bake_world_transform(obj)
    coordinates = [vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(coordinate[i] for coordinate in coordinates) for i in range(3)))
    maximum = Vector(tuple(max(coordinate[i] for coordinate in coordinates) for i in range(3)))
    center = (minimum + maximum) * 0.5
    for vertex in obj.data.vertices:
        vertex.co -= center
    obj.location = center
    return center.copy()


def replace_source_tire_with_clean_core(obj):
    """Discard Meshy's fused tire/arch shell while preserving the rim and brakes."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    minimum_y = min(vertex.co.y for vertex in bm.verts)
    maximum_y = max(vertex.co.y for vertex in bm.verts)
    tire_radius = (maximum_y - minimum_y) * 0.5
    core_radius = tire_radius * 0.76
    selected = []
    for face in bm.faces:
        center = face.calc_center_median()
        radius = math.hypot(center.y, center.z)
        if radius > core_radius:
            selected.append(face)

    removed_triangles = sum(max(1, len(face.verts) - 2) for face in selected)
    if selected:
        bmesh.ops.delete(bm, geom=list(selected), context="FACES")
        loose_vertices = [vertex for vertex in bm.verts if not vertex.link_faces]
        if loose_vertices:
            bmesh.ops.delete(bm, geom=loose_vertices, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return removed_triangles, tire_radius


def align_wheel_axle_to_x(obj):
    """Straighten Meshy's slightly tilted wheel using the central rim geometry."""
    points = np.array([tuple(vertex.co) for vertex in obj.data.vertices], dtype=float)
    radial = np.sqrt(points[:, 1] ** 2 + points[:, 2] ** 2)
    tire_radius = max(np.ptp(points[:, 1]), np.ptp(points[:, 2])) * 0.5
    core = points[radial < tire_radius * 0.66]
    core -= np.mean(core, axis=0)
    values, vectors = np.linalg.eigh(np.cov(core, rowvar=False))
    axle = Vector(vectors[:, int(np.argmin(values))].tolist())
    if axle.x < 0.0:
        axle.negate()
    angle = math.degrees(axle.angle(Vector((1.0, 0.0, 0.0))))
    correction = axle.rotation_difference(Vector((1.0, 0.0, 0.0)))
    for vertex in obj.data.vertices:
        vertex.co = correction @ vertex.co
    obj.data.update()
    return angle


def create_clean_tire(name, outer_radius, half_width, material):
    inner_radius = outer_radius * 0.64
    major_radius = (outer_radius + inner_radius) * 0.5
    minor_radius = (outer_radius - inner_radius) * 0.5
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=64,
        minor_segments=16,
        location=(0.0, 0.0, 0.0),
        rotation=(0.0, math.radians(90.0), 0.0),
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    tire = bpy.context.object
    tire.name = name
    tire.scale.z = half_width / max(minor_radius, 1e-6)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    tire.data.materials.append(material)
    for polygon in tire.data.polygons:
        polygon.use_smooth = True
    return tire


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


args = parse_args()
for path in (args.output_fbx, args.output_blend, args.output_preview, args.output_report,
             args.body_albedo, args.body_normal, args.wheel_albedo, args.wheel_normal):
    os.makedirs(os.path.dirname(path), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.frame)
frame_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(frame_meshes) != 1:
    raise RuntimeError(f"Expected one frame mesh, found {len(frame_meshes)}")
body = frame_meshes[0]
body.name = "Body"
bake_world_transform(body)
body_albedo, body_normal = find_material_images(body)
save_image(body_albedo, args.body_albedo, "sRGB")
save_image(body_normal, args.body_normal, "Non-Color")

before_import = set(bpy.context.scene.objects)
bpy.ops.import_scene.gltf(filepath=args.wheel_source)
wheel_source_objects = [obj for obj in bpy.context.scene.objects
                        if obj not in before_import and obj.type == "MESH"]
if len(wheel_source_objects) != 1:
    raise RuntimeError(f"Expected one textured wheel-source mesh, found {len(wheel_source_objects)}")
wheel_source = wheel_source_objects[0]
bake_world_transform(wheel_source)
wheel_albedo, wheel_normal = find_material_images(wheel_source)
save_image(wheel_albedo, args.wheel_albedo, "sRGB")
save_image(wheel_normal, args.wheel_normal, "Non-Color")

loose_parts = weld_and_separate_loose(wheel_source)
wheels = classify_wheel_parts(loose_parts)
if not all(name in wheels for name in ("FL", "RL")):
    raise RuntimeError(f"Could not identify the left front/rear source wheels: {sorted(wheels)}")
source_front_left = wheels["FL"]
source_rear_left = wheels["RL"]
bake_world_transform(source_front_left)
bake_world_transform(source_rear_left)
source_triangles_removed = {}
axle_corrections_degrees = {}
tire_specs = {}
for side, source in (("FL", source_front_left), ("RL", source_rear_left)):
    source_center = center_wheel_object(source)
    minimum_x = min(vertex.co.x for vertex in source.data.vertices)
    maximum_x = max(vertex.co.x for vertex in source.data.vertices)
    source_triangles_removed[side], tire_radius = replace_source_tire_with_clean_core(source)
    tire_specs[side] = {
        "outer_radius": tire_radius,
        "half_width": (maximum_x - minimum_x) * 0.5,
    }
    axle_corrections_degrees[side] = align_wheel_axle_to_x(source)
    source.location = source_center
wheels = {
    "FL": source_front_left,
    "FR": mirror_centered_wheel(source_front_left),
    "RL": source_rear_left,
    "RR": mirror_centered_wheel(source_rear_left),
}

for obj in loose_parts:
    if obj not in wheels.values():
        bpy.data.objects.remove(obj, do_unlink=True)

for polygon in body.data.polygons:
    polygon.use_smooth = True

rubber_material = bpy.data.materials.new("MAT_Tire_Rubber")
rubber_material.use_nodes = True
rubber_bsdf = next(
    (node for node in rubber_material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
    None,
)
if rubber_bsdf is not None:
    rubber_bsdf.inputs["Base Color"].default_value = (0.008, 0.009, 0.011, 1.0)
    rubber_bsdf.inputs["Roughness"].default_value = 0.62
    rubber_bsdf.inputs["Metallic"].default_value = 0.0

named_wheels = {}
wheel_centers = {}
for side in ("FL", "FR", "RL", "RR"):
    wheel = wheels[side]
    wheel.name = f"Wheel_{side}_Spin"
    for polygon in wheel.data.polygons:
        polygon.use_smooth = True
    wheel_centers[side] = wheel.location.copy()
    named_wheels[side] = wheel

root = bpy.data.objects.new("Porsche911_GT3RS", None)
bpy.context.collection.objects.link(root)
body.parent = root

for side in ("FL", "FR"):
    steer = bpy.data.objects.new(f"Wheel_{side}_Steer", None)
    bpy.context.collection.objects.link(steer)
    steer.empty_display_type = "PLAIN_AXES"
    steer.empty_display_size = 0.08
    steer.location = wheel_centers[side]
    steer.parent = root
    named_wheels[side].parent = steer
    named_wheels[side].location = Vector((0.0, 0.0, 0.0))

for side in ("RL", "RR"):
    named_wheels[side].parent = root

wheel_tires = {}
for side in ("FL", "FR", "RL", "RR"):
    source_side = "FL" if side in ("FL", "FR") else "RL"
    spec = tire_specs[source_side]
    tire = create_clean_tire(
        f"Wheel_{side}_Tire",
        spec["outer_radius"],
        spec["half_width"],
        rubber_material,
    )
    tire.parent = named_wheels[side]
    tire.location = Vector((0.0, 0.0, 0.0))
    wheel_tires[side] = tire

for obj in [body, *named_wheels.values(), *wheel_tires.values()]:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth_by_angle()

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

floor_z = min(world_bounds(obj)[0].z for obj in wheel_tires.values()) - 0.008
bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, floor_z))
floor = bpy.context.object
floor_material = bpy.data.materials.new("MAT_PreviewFloor")
floor_material.diffuse_color = (0.075, 0.08, 0.09, 1.0)
floor.data.materials.append(floor_material)

world = bpy.context.scene.world or bpy.data.worlds.new("World")
bpy.context.scene.world = world
world.use_nodes = True
background = next((node for node in world.node_tree.nodes if node.type == "BACKGROUND"), None)
if background is None:
    background = world.node_tree.nodes.new("ShaderNodeBackground")
    world_output = next((node for node in world.node_tree.nodes if node.type == "OUTPUT_WORLD"), None)
    if world_output is None:
        world_output = world.node_tree.nodes.new("ShaderNodeOutputWorld")
    world.node_tree.links.new(background.outputs["Background"], world_output.inputs["Surface"])
background.inputs["Color"].default_value = (0.025, 0.03, 0.04, 1.0)
background.inputs["Strength"].default_value = 0.32

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
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

report = {
    "body_triangles": triangle_count(body),
    "wheel_triangles": {
        side: triangle_count(named_wheels[side]) + triangle_count(wheel_tires[side])
        for side in named_wheels
    },
    "wheel_core_triangles": {side: triangle_count(obj) for side, obj in named_wheels.items()},
    "new_tire_triangles": {side: triangle_count(obj) for side, obj in wheel_tires.items()},
    "total_triangles": (
        triangle_count(body)
        + sum(triangle_count(obj) for obj in named_wheels.values())
        + sum(triangle_count(obj) for obj in wheel_tires.values())
    ),
    "wheel_centers_blender": {side: list(center) for side, center in wheel_centers.items()},
    "right_side_source": "front_and_rear_mirrored_from_their_left_side_wheels",
    "source_tire_and_artifact_triangles_removed": {
        "FL": source_triangles_removed["FL"],
        "FR": source_triangles_removed["FL"],
        "RL": source_triangles_removed["RL"],
        "RR": source_triangles_removed["RL"],
    },
    "axle_corrections_degrees": {
        "FL": axle_corrections_degrees["FL"],
        "FR": axle_corrections_degrees["FL"],
        "RL": axle_corrections_degrees["RL"],
        "RR": axle_corrections_degrees["RL"],
    },
    "hierarchy": {
        "front": ["Wheel_FL_Steer/Wheel_FL_Spin", "Wheel_FR_Steer/Wheel_FR_Spin"],
        "rear": ["Wheel_RL_Spin", "Wheel_RR_Spin"],
    },
}
with open(args.output_report, "w", encoding="utf-8") as handle:
    json.dump(report, handle, ensure_ascii=False, indent=2)
print("BUILD_REPORT=" + json.dumps(report, ensure_ascii=False))
