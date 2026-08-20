import argparse
import json

import bpy
import bmesh


def parse_args():
    argv = []
    if "--" in __import__("sys").argv:
        argv = __import__("sys").argv[__import__("sys").argv.index("--") + 1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    return parser.parse_args(argv)


def mesh_islands(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    remaining = set(bm.verts)
    islands = []
    while remaining:
        seed = remaining.pop()
        stack = [seed]
        vertices = [seed]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in remaining:
                    remaining.remove(other)
                    stack.append(other)
                    vertices.append(other)
        faces = {face for vertex in vertices for face in vertex.link_faces}
        coords = [vertex.co for vertex in vertices]
        islands.append({
            "vertices": len(vertices),
            "faces": len(faces),
            "bbox_min": [round(min(co[i] for co in coords), 6) for i in range(3)],
            "bbox_max": [round(max(co[i] for co in coords), 6) for i in range(3)],
        })
    bm.free()
    return sorted(islands, key=lambda value: value["faces"], reverse=True)


args = parse_args()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.source)

result = {"source": args.source, "objects": [], "images": [], "materials": []}
for obj in [value for value in bpy.context.scene.objects if value.type == "MESH"]:
    corners = [obj.matrix_world @ __import__("mathutils").Vector(corner) for corner in obj.bound_box]
    result["objects"].append({
        "name": obj.name,
        "mesh": obj.data.name,
        "vertices": len(obj.data.vertices),
        "faces": len(obj.data.polygons),
        "triangles": sum(max(1, len(poly.vertices) - 2) for poly in obj.data.polygons),
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "uv_layers": [layer.name for layer in obj.data.uv_layers],
        "bbox_min": [round(min(corner[i] for corner in corners), 6) for i in range(3)],
        "bbox_max": [round(max(corner[i] for corner in corners), 6) for i in range(3)],
        "islands": mesh_islands(obj.data)[:12],
    })

for image in bpy.data.images:
    result["images"].append({
        "name": image.name,
        "filepath": image.filepath,
        "size": list(image.size),
        "packed": image.packed_file is not None,
    })

for material in bpy.data.materials:
    nodes = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE":
                links = []
                for output in node.outputs:
                    for link in output.links:
                        links.append({
                            "output": output.name,
                            "to_node": link.to_node.name,
                            "to_socket": link.to_socket.name,
                        })
                nodes.append({
                    "name": node.name,
                    "image": node.image.name if node.image else None,
                    "links": links,
                })
    result["materials"].append({"name": material.name, "image_nodes": nodes})

print("INSPECTION_JSON=" + json.dumps(result, ensure_ascii=False))
