import argparse
import json
import sys

import bpy
from mathutils import Vector


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True)
args = parser.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=args.source)

objects = []
for obj in bpy.context.scene.objects:
    entry = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "local_location": list(obj.location),
        "world_location": list(obj.matrix_world.translation),
        "local_rotation": list(obj.rotation_euler),
    }
    if obj.type == "MESH":
        world_corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        entry.update({
            "triangles": sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons),
            "uv_layers": [layer.name for layer in obj.data.uv_layers],
            "material_slots": len(obj.material_slots),
            "world_bounds": {
                "min": [min(corner[i] for corner in world_corners) for i in range(3)],
                "max": [max(corner[i] for corner in world_corners) for i in range(3)],
            },
        })
    objects.append(entry)

print("FBX_INSPECTION=" + json.dumps(objects, ensure_ascii=False))
