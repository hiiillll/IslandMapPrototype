import argparse
import os
import sys

import bpy
from mathutils import Vector


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True)
parser.add_argument("--output-dir", required=True)
args = parser.parse_args(argv)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.wm.open_mainfile(filepath=args.source)
os.makedirs(args.output_dir, exist_ok=True)

for obj in bpy.context.scene.objects:
    obj.hide_render = obj.type == "MESH" and not obj.name.startswith("Wheel_")

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False

world = scene.world or bpy.data.worlds.new("DebugWorld")
scene.world = world
world.color = (0.03, 0.03, 0.03)

camera_data = bpy.data.cameras.new("WheelDebugCamera")
camera = bpy.data.objects.new("WheelDebugCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera_data.lens = 75
scene.camera = camera

light_data = bpy.data.lights.new("WheelDebugLight", type="AREA")
light_data.energy = 900
light_data.shape = "DISK"
light_data.size = 1.0
light = bpy.data.objects.new("WheelDebugLight", light_data)
bpy.context.collection.objects.link(light)

for name in ("Wheel_FL_Spin", "Wheel_RL_Spin"):
    wheel = bpy.data.objects[name]
    center = wheel.matrix_world.translation
    for suffix, offset in (
        ("side", Vector((-0.72, 0.0, 0.02))),
        ("three_quarter", Vector((-0.62, -0.34, 0.24))),
    ):
        camera.location = center + offset
        look_at(camera, center)
        light.location = center + Vector((-0.50, -0.25, 0.45))
        look_at(light, center)
        scene.render.filepath = os.path.join(args.output_dir, f"{name}_{suffix}.png")
        bpy.ops.render.render(write_still=True)
