"""Extract synchronized Level03 terrain and road inputs from the composite map."""

from collections import deque
from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(
    r"C:\Users\Administrator\AppData\Local\Temp\codex-clipboard-9492cdfa-a915-461d-8215-4677d66dc7df.png"
)
REFERENCE_DIR = ROOT / "Assets" / "Level03" / "References"
DIAGNOSTIC_DIR = ROOT / "tmp" / "level03" / "diagnostics"

# Coordinates exclude the one-pixel black frame around each top map panel.
ROAD_CROP = (9, 47, 711, 612)
HEIGHT_CROP = (728, 47, 1435, 612)


def connected_components(mask: np.ndarray) -> list[np.ndarray]:
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    components: list[np.ndarray] = []
    for start_y, start_x in np.argwhere(mask):
        if visited[start_y, start_x]:
            continue
        queue = deque([(int(start_y), int(start_x))])
        visited[start_y, start_x] = True
        points: list[tuple[int, int]] = []
        while queue:
            y, x = queue.popleft()
            points.append((y, x))
            for next_y, next_x in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if (
                    0 <= next_y < height
                    and 0 <= next_x < width
                    and mask[next_y, next_x]
                    and not visited[next_y, next_x]
                ):
                    visited[next_y, next_x] = True
                    queue.append((next_y, next_x))
        component = np.zeros_like(mask, dtype=bool)
        rows, columns = zip(*points)
        component[np.asarray(rows), np.asarray(columns)] = True
        components.append(component)
    return components


def largest_component(mask: np.ndarray) -> np.ndarray:
    components = connected_components(mask)
    if not components:
        raise RuntimeError("No connected component found in reference mask")
    return max(components, key=np.count_nonzero)


def pil_mask(mask: np.ndarray) -> Image.Image:
    return Image.fromarray((mask.astype(np.uint8) * 255), mode="L")


def morph(mask: np.ndarray, close_size: int, expand_size: int = 0) -> np.ndarray:
    image = pil_mask(mask)
    if close_size > 1:
        image = image.filter(ImageFilter.MaxFilter(close_size))
        image = image.filter(ImageFilter.MinFilter(close_size))
    if expand_size > 1:
        image = image.filter(ImageFilter.MaxFilter(expand_size))
    return np.asarray(image) > 127


def bbox(mask: np.ndarray) -> tuple[int, int, int, int]:
    rows, columns = np.where(mask)
    return int(columns.min()), int(rows.min()), int(columns.max()) + 1, int(rows.max()) + 1


def extract_land(road_rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    red = road_rgb[:, :, 0].astype(np.int16)
    green = road_rgb[:, :, 1].astype(np.int16)
    blue = road_rgb[:, :, 2].astype(np.int16)

    # Ocean is strongly blue. Beige/gray pixels include land, mountain, and roads.
    ocean = (blue - red > 24) & (blue - green > 15) & (blue > 65)
    land_and_bridges = ~ocean
    land_and_bridges[:2, :] = False
    land_and_bridges[-2:, :] = False
    land_and_bridges[:, :2] = False
    land_and_bridges[:, -2:] = False

    # Bridge rails are light enough to join the four islands in pixel space. Keep
    # that joined component for the final coastline, then isolate the main island
    # by its stable map quadrant before finding its connected body.
    islands = largest_component(morph(land_and_bridges, 5, 3))
    rows, columns = np.indices(islands.shape)
    main_region = islands & (columns >= 195)
    main_region &= ~((columns > 545) & (rows < 105))
    main_island = largest_component(main_region)
    main_island = morph(main_island, 7, 3)
    return islands, main_island


def extract_roads(road_rgb: np.ndarray) -> np.ndarray:
    channels = road_rgb.astype(np.int16)
    brightness = channels.mean(axis=2)
    spread = channels.max(axis=2) - channels.min(axis=2)

    # Asphalt is dark and nearly neutral. The blue sea and brown mountain shadows
    # fail the channel-spread test, leaving one connected road network.
    candidates = (brightness < 145) & (spread < 26)
    candidates[:3, :] = False
    candidates[-3:, :] = False
    candidates[:, :3] = False
    candidates[:, -3:] = False
    candidates = morph(candidates, 3)

    components = connected_components(candidates)
    road_component = max(components, key=np.count_nonzero)
    if np.count_nonzero(road_component) < 5000:
        raise RuntimeError("Extracted road network is unexpectedly small")

    # Close tiny antialiasing gaps without materially changing the drawn route.
    return morph(road_component, 3, 3)


def align_height_to_main_island(
    height_rgb: np.ndarray,
    target_size: tuple[int, int],
    target_main_island: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    red = height_rgb[:, :, 0].astype(np.int16)
    green = height_rgb[:, :, 1].astype(np.int16)
    blue = height_rgb[:, :, 2].astype(np.int16)
    ocean = (blue - red > 22) & (blue - green > 13) & (blue > 65)
    source_land = morph(~ocean, 7, 3)

    # Remove frame and the lower-right elevation legend before component search.
    source_land[:3, :] = False
    source_land[-3:, :] = False
    source_land[:, :3] = False
    source_land[:, -3:] = False
    source_land[325:555, 605:707] = False
    source_rows, source_columns = np.indices(source_land.shape)
    source_main_region = source_land & (source_columns >= 195)
    source_main_region &= ~((source_columns > 545) & (source_rows < 105))
    source_main = largest_component(source_main_region)

    source_box = bbox(source_main)
    target_box = bbox(target_main_island)
    source_crop = Image.fromarray(height_rgb).crop(source_box)
    target_width = target_box[2] - target_box[0]
    target_height = target_box[3] - target_box[1]
    resized = source_crop.resize((target_width, target_height), Image.Resampling.BICUBIC)

    aligned = Image.new("RGB", target_size, (255, 255, 255))
    aligned.paste(resized, (target_box[0], target_box[1]))
    aligned_rgb = np.asarray(aligned)

    resized_source_mask = pil_mask(source_main[source_box[1] : source_box[3], source_box[0] : source_box[2]])
    resized_source_mask = resized_source_mask.resize(
        (target_width, target_height), Image.Resampling.NEAREST
    )
    aligned_mask_image = Image.new("L", target_size, 0)
    aligned_mask_image.paste(resized_source_mask, (target_box[0], target_box[1]))
    aligned_mask = np.asarray(aligned_mask_image) > 127
    return aligned_rgb, aligned_mask


def build_height_input(
    islands: np.ndarray,
    main_island: np.ndarray,
    roads: np.ndarray,
    aligned_height_rgb: np.ndarray,
    aligned_height_land: np.ndarray,
) -> np.ndarray:
    height_input = np.zeros(islands.shape, dtype=np.uint8)
    height_input[islands] = 48

    luminance = aligned_height_rgb.astype(np.float32).mean(axis=2)
    inverted = np.clip((244.0 - luminance) / 205.0, 0.0, 1.0)
    # Suppress low-contrast contour noise so the surrounding main-island plain is flat.
    mountain = np.clip((inverted - 0.13) / 0.72, 0.0, 1.0)
    mountain = Image.fromarray((mountain * 255).astype(np.uint8), mode="L")
    mountain = mountain.filter(ImageFilter.GaussianBlur(3.2))
    mountain_values = np.asarray(mountain).astype(np.float32) / 255.0

    aligned_channels = aligned_height_rgb.astype(np.int16)
    aligned_spread = aligned_channels.max(axis=2) - aligned_channels.min(axis=2)
    main_interior = np.asarray(
        pil_mask(main_island).filter(ImageFilter.MinFilter(31))
    ) > 127
    valid_mountain = main_interior & aligned_height_land & (aligned_spread < 34)
    height_input[valid_mountain] = np.maximum(
        height_input[valid_mountain],
        (48 + mountain_values[valid_mountain] * 207).astype(np.uint8),
    )
    height_input[roads] = 48
    return height_input


def save_diagnostic(
    road_panel: Image.Image,
    height_panel: Image.Image,
    islands: np.ndarray,
    roads: np.ndarray,
    height_input: np.ndarray,
) -> None:
    width, height = road_panel.size
    overlay = np.asarray(road_panel).copy()
    overlay[roads] = np.array([255, 50, 50], dtype=np.uint8)
    overlay[~islands & ~roads] = (overlay[~islands & ~roads] * 0.65).astype(np.uint8)

    sheet = Image.new("RGB", (width * 3, height * 2), "white")
    sheet.paste(road_panel, (0, 0))
    sheet.paste(height_panel.resize((width, height), Image.Resampling.LANCZOS), (width, 0))
    sheet.paste(Image.fromarray(overlay), (width * 2, 0))
    sheet.paste(pil_mask(islands).convert("RGB"), (0, height))
    sheet.paste(pil_mask(roads).convert("RGB"), (width, height))
    sheet.paste(Image.fromarray(height_input, mode="L").convert("RGB"), (width * 2, height))
    draw = ImageDraw.Draw(sheet)
    labels = (
        "ROAD CROP",
        "HEIGHT CROP",
        "ROAD EXTRACTION (RED)",
        "LAND MASK",
        "ROAD MASK",
        "UNITY HEIGHT INPUT",
    )
    for index, label in enumerate(labels):
        x = (index % 3) * width + 12
        y = (index // 3) * height + 10
        draw.rectangle((x - 5, y - 4, x + 210, y + 22), fill=(0, 0, 0))
        draw.text((x, y), label, fill=(255, 255, 255))
    sheet.save(DIAGNOSTIC_DIR / "Level03_ReferenceExtraction.png")


def main() -> None:
    REFERENCE_DIR.mkdir(parents=True, exist_ok=True)
    DIAGNOSTIC_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copy2(SOURCE, REFERENCE_DIR / "Level03_FinalComposite.png")

    composite = Image.open(SOURCE).convert("RGB")
    road_panel = composite.crop(ROAD_CROP)
    height_panel = composite.crop(HEIGHT_CROP)
    road_rgb = np.asarray(road_panel)
    height_rgb = np.asarray(height_panel)

    islands, main_island = extract_land(road_rgb)
    roads = extract_roads(road_rgb)
    aligned_height_rgb, aligned_height_land = align_height_to_main_island(
        height_rgb, road_panel.size, main_island
    )
    height_input = build_height_input(
        islands, main_island, roads, aligned_height_rgb, aligned_height_land
    )

    Image.fromarray(height_input, mode="L").save(
        REFERENCE_DIR / "Level03_HeightReference.png"
    )
    pil_mask(roads).save(REFERENCE_DIR / "Level03_RoadPlan_Active.png")
    road_panel.save(REFERENCE_DIR / "Level03_RoadPanel_Cropped.png")
    height_panel.save(REFERENCE_DIR / "Level03_HeightPanel_Cropped.png")
    save_diagnostic(road_panel, height_panel, islands, roads, height_input)

    print(f"road panel: {road_panel.size}")
    print(f"land pixels: {np.count_nonzero(islands)}")
    print(f"road pixels: {np.count_nonzero(roads)}")
    print(f"main island bbox: {bbox(main_island)}")


if __name__ == "__main__":
    main()
