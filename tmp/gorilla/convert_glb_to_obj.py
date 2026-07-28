"""Convert Gorilla GLB outputs into Unity 2022's native OBJ import format."""

import argparse
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
DEPENDENCIES = ROOT / "tmp" / "gorilla_python_deps"
sys.path.insert(0, str(DEPENDENCIES))

import trimesh  # noqa: E402
from trimesh.exchange.obj import export_obj  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--name", required=True)
    args = parser.parse_args()

    scene = trimesh.load(args.input, force="scene")
    if not scene.geometry:
        raise RuntimeError(f"No geometry found in {args.input}")

    args.output_dir.mkdir(parents=True, exist_ok=True)
    obj_text, resources = export_obj(scene, include_texture=True, return_texture=True)
    obj_text = obj_text.replace("mtllib material.mtl", f"mtllib {args.name}.mtl")
    (args.output_dir / f"{args.name}.obj").write_text(obj_text, encoding="utf-8")
    for filename, content in resources.items():
        target_name = f"{args.name}.mtl" if filename.lower().endswith(".mtl") else filename
        if isinstance(content, str):
            (args.output_dir / target_name).write_text(content, encoding="utf-8")
        else:
            (args.output_dir / target_name).write_bytes(content)

    vertices = sum(len(mesh.vertices) for mesh in scene.geometry.values())
    faces = sum(len(mesh.faces) for mesh in scene.geometry.values())
    print(
        {
            "input": str(args.input),
            "output": str(args.output_dir / f"{args.name}.obj"),
            "geometries": len(scene.geometry),
            "vertices": vertices,
            "triangles": faces,
            "bounds": scene.bounds.tolist(),
        }
    )


if __name__ == "__main__":
    main()
