from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tmp/imagegen/road_texture/Road_Gorilla_Source.png"
TARGET = ROOT / "Assets/Art/Textures/Road.png"
PREVIEW = ROOT / "tmp/imagegen/road_texture/Road_TilingPreview.png"
COMPARISON = ROOT / "tmp/imagegen/road_texture/Road_BeforeAfter.png"
SIZE = 2048
def make_tileable(image: Image.Image) -> Image.Image:
    source = image.convert("RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    pixels = np.asarray(source, dtype=np.uint8)
    region_size = SIZE // 2

    horizontal_diffs = np.abs(
        pixels[:, 1:].astype(np.int16) - pixels[:, :-1].astype(np.int16)
    ).mean(axis=(0, 2))
    vertical_diffs = np.abs(
        pixels[1:].astype(np.int16) - pixels[:-1].astype(np.int16)
    ).mean(axis=(1, 2))

    starts = np.arange(SIZE - region_size + 1)
    x_scores = horizontal_diffs[starts] + horizontal_diffs[starts + region_size - 2]
    y_scores = vertical_diffs[starts] + vertical_diffs[starts + region_size - 2]
    start_x = int(starts[np.argmin(x_scores)])
    start_y = int(starts[np.argmin(y_scores)])

    base = pixels[
        start_y : start_y + region_size,
        start_x : start_x + region_size,
    ]
    indices = np.concatenate((np.arange(region_size), np.arange(region_size - 1, -1, -1)))
    tile = base[indices][:, indices]
    return Image.fromarray(tile, mode="RGB")


def add_label(image: Image.Image, text: str) -> Image.Image:
    canvas = Image.new("RGB", (image.width, image.height + 42), (26, 26, 26))
    canvas.paste(image, (0, 42))
    ImageDraw.Draw(canvas).text((14, 13), text, fill=(240, 240, 240))
    return canvas


def main() -> None:
    with Image.open(SOURCE) as source_image:
        final = make_tileable(source_image)
    with Image.open(TARGET) as before_image:
        before = before_image.convert("RGB")
    final.save(TARGET, optimize=True)

    tile = final.resize((512, 512), Image.Resampling.LANCZOS)
    preview = Image.new("RGB", (1536, 1536))
    for y in range(3):
        for x in range(3):
            preview.paste(tile, (x * 512, y * 512))
    preview.save(PREVIEW, optimize=True)

    before_small = add_label(before.resize((768, 768), Image.Resampling.LANCZOS), "Before")
    after_small = add_label(final.resize((768, 768), Image.Resampling.LANCZOS), "After - Gorilla banana")
    comparison = Image.new("RGB", (1536, 810), (26, 26, 26))
    comparison.paste(before_small, (0, 0))
    comparison.paste(after_small, (768, 0))
    comparison.save(COMPARISON, optimize=True)

    pixels = np.asarray(final, dtype=np.float32)
    luma = pixels @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    horizontal_error = np.abs(pixels[:, 0] - pixels[:, -1]).mean()
    vertical_error = np.abs(pixels[0] - pixels[-1]).mean()
    print(
        {
            "size": final.size,
            "mode": final.mode,
            "mean_luma": round(float(luma.mean()), 2),
            "left_right_edge_mae": round(float(horizontal_error), 4),
            "top_bottom_edge_mae": round(float(vertical_error), 4),
        }
    )


if __name__ == "__main__":
    main()
