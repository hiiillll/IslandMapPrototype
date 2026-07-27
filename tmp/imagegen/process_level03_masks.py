from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Level03/MapMasks/Source/Level03_ZoneMap_Generated.jpg"
OUTPUT = ROOT / "Assets/Level03/MapMasks"
TARGET_SIZE = (3000, 2500)

PALETTE = np.array(
    [
        (0, 0, 0),
        (255, 255, 0),
        (139, 69, 19),
        (0, 255, 0),
        (255, 0, 0),
        (255, 255, 255),
        (0, 255, 255),
        (255, 0, 255),
    ],
    dtype=np.uint8,
)

OCEAN = 0
BEACH = 1
LOWLAND = 2
MOUNTAIN = 3
BUILT_UP = 4
ROAD = 5
BRIDGE = 6
EXTRACTION = 7


def quantize_to_palette(image: Image.Image) -> np.ndarray:
    pixels = np.asarray(image.convert("RGB"), dtype=np.int32)
    best_distance = np.full(pixels.shape[:2], np.iinfo(np.int32).max, dtype=np.int32)
    labels = np.zeros(pixels.shape[:2], dtype=np.uint8)
    for index, color in enumerate(PALETTE.astype(np.int32)):
        delta = pixels - color
        distance = np.sum(delta * delta, axis=2, dtype=np.int32)
        choose = distance < best_distance
        best_distance[choose] = distance[choose]
        labels[choose] = index
    return labels


def pad_to_ratio(labels: np.ndarray, ratio: float) -> np.ndarray:
    height, width = labels.shape
    current_ratio = width / height
    if abs(current_ratio - ratio) < 1e-6:
        return labels
    if current_ratio > ratio:
        target_height = int(np.ceil(width / ratio))
        top = (target_height - height) // 2
        canvas = np.zeros((target_height, width), dtype=np.uint8)
        canvas[top : top + height] = labels
        return canvas
    target_width = int(np.ceil(height * ratio))
    left = (target_width - width) // 2
    canvas = np.zeros((height, target_width), dtype=np.uint8)
    canvas[:, left : left + width] = labels
    return canvas


def replace_extraction_aircraft_with_platform(labels: np.ndarray) -> np.ndarray:
    height, width = labels.shape
    center_x = int(width * 0.775)
    center_y = int(height * 0.128)
    radius = int(width * 0.073)
    yy, xx = np.ogrid[:height, :width]
    disk = (xx - center_x) ** 2 + (yy - center_y) ** 2 <= radius * radius
    labels[disk] = EXTRACTION
    return disk


def resize_labels(labels: np.ndarray) -> np.ndarray:
    image = Image.fromarray(labels, mode="L")
    return np.asarray(image.resize(TARGET_SIZE, Image.Resampling.NEAREST), dtype=np.uint8)


def close_mask(mask: np.ndarray, size: int) -> np.ndarray:
    image = Image.fromarray((mask.astype(np.uint8) * 255), mode="L")
    image = image.filter(ImageFilter.MaxFilter(size)).filter(ImageFilter.MinFilter(size))
    return np.asarray(image, dtype=np.uint8) > 127


def fill_enclosed_holes(mask: np.ndarray) -> np.ndarray:
    flood_image = Image.fromarray((mask.astype(np.uint8) * 255), mode="L")
    draw = ImageDraw.Draw(flood_image)
    width, height = flood_image.size
    for seed in ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)):
        ImageDraw.floodfill(flood_image, seed, 128, thresh=0)
    flooded = np.asarray(flood_image, dtype=np.uint8)
    outside = flooded == 128
    return ~outside


def land_from_semantics(labels: np.ndarray) -> np.ndarray:
    seed = (labels == BEACH) | (labels == LOWLAND) | (labels == MOUNTAIN) | (labels == BUILT_UP)
    closed = close_mask(seed, 51)
    return fill_enclosed_holes(closed)


def zhang_suen(binary: np.ndarray) -> np.ndarray:
    image = binary.astype(np.uint8).copy()
    changed = True
    while changed:
        changed = False
        for phase in (0, 1):
            padded = np.pad(image, 1, mode="constant")
            p2 = padded[:-2, 1:-1]
            p3 = padded[:-2, 2:]
            p4 = padded[1:-1, 2:]
            p5 = padded[2:, 2:]
            p6 = padded[2:, 1:-1]
            p7 = padded[2:, :-2]
            p8 = padded[1:-1, :-2]
            p9 = padded[:-2, :-2]
            neighbors = (p2, p3, p4, p5, p6, p7, p8, p9)
            neighbor_count = sum(neighbors)
            transitions = sum(
                ((neighbors[index] == 0) & (neighbors[(index + 1) % 8] == 1)).astype(np.uint8)
                for index in range(8)
            )
            center = image == 1
            marker = center & (neighbor_count >= 2) & (neighbor_count <= 6) & (transitions == 1)
            if phase == 0:
                marker &= (p2 * p4 * p6 == 0) & (p4 * p6 * p8 == 0)
            else:
                marker &= (p2 * p4 * p8 == 0) & (p2 * p6 * p8 == 0)
            if np.any(marker):
                image[marker] = 0
                changed = True
    return image.astype(bool)


def create_centerline(road_mask: np.ndarray) -> np.ndarray:
    source = Image.fromarray((road_mask.astype(np.uint8) * 255), mode="L")
    small_size = (TARGET_SIZE[0] // 2, TARGET_SIZE[1] // 2)
    small = np.asarray(source.resize(small_size, Image.Resampling.NEAREST), dtype=np.uint8) > 127
    small_skeleton = zhang_suen(small)
    enlarged = Image.fromarray((small_skeleton.astype(np.uint8) * 255), mode="L").resize(
        TARGET_SIZE,
        Image.Resampling.NEAREST,
    )
    return zhang_suen(np.asarray(enlarged, dtype=np.uint8) > 127)


def create_heightmap(land_mask: np.ndarray, mountain_mask: np.ndarray) -> np.ndarray:
    land_image = Image.fromarray((land_mask.astype(np.uint8) * 255), mode="L")
    coast_falloff = np.asarray(land_image.filter(ImageFilter.GaussianBlur(55)), dtype=np.float32) / 255.0
    mountain_image = Image.fromarray((mountain_mask.astype(np.uint8) * 255), mode="L")
    mountain_near = np.asarray(mountain_image.filter(ImageFilter.GaussianBlur(70)), dtype=np.float32) / 255.0
    mountain_far = np.asarray(mountain_image.filter(ImageFilter.GaussianBlur(210)), dtype=np.float32) / 255.0
    mountain = mountain_near * 0.72 + mountain_far * 0.28
    maximum = float(mountain.max())
    if maximum > 0.0:
        mountain /= maximum
    height = 8.0 + 34.0 * coast_falloff + 213.0 * np.power(mountain, 0.82)
    height[~land_mask] = 0.0
    return np.clip(np.rint(height / 255.0 * 65535.0), 0, 65535).astype(np.uint16)


def save_binary(mask: np.ndarray, filename: str) -> None:
    Image.fromarray(mask.astype(np.uint8) * 255, mode="L").save(OUTPUT / filename)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source_image = Image.open(SOURCE)
    labels = quantize_to_palette(source_image)
    labels = pad_to_ratio(labels, TARGET_SIZE[0] / TARGET_SIZE[1])
    labels = resize_labels(labels).copy()
    platform_mask = replace_extraction_aircraft_with_platform(labels)

    raw_counts = {str(index): int(np.count_nonzero(labels == index)) for index in range(len(PALETTE))}
    land_mask = land_from_semantics(labels)
    land_mask[platform_mask] = False
    beach_mask = (labels == BEACH) & land_mask
    bridge_mask = close_mask(labels == BRIDGE, 11)
    extraction_mask = close_mask(labels == EXTRACTION, 11)
    bridge_or_extraction = bridge_mask | extraction_mask
    raw_road_mask = (labels == ROAD) | bridge_or_extraction
    road_mask = close_mask(raw_road_mask, 21)

    zone_labels = labels.copy()
    zone_labels[land_mask & (zone_labels == OCEAN)] = LOWLAND
    outside_nonroad = (~land_mask) & (~bridge_or_extraction) & (zone_labels != ROAD)
    zone_labels[outside_nonroad] = OCEAN
    zone_labels[road_mask] = ROAD
    zone_labels[bridge_mask] = BRIDGE
    zone_labels[extraction_mask] = EXTRACTION

    centerline = create_centerline(road_mask)
    heightmap = create_heightmap(land_mask, zone_labels == MOUNTAIN)

    save_binary(land_mask, "Level03_LandMask.png")
    Image.fromarray(heightmap, mode="I;16").save(OUTPUT / "Level03_Heightmap.png")
    save_binary(beach_mask, "Level03_BeachMask.png")
    save_binary(road_mask, "Level03_RoadMask.png")
    save_binary(centerline, "Level03_RoadCenterline.png")
    Image.fromarray(PALETTE[zone_labels], mode="RGB").save(OUTPUT / "Level03_ZoneMap.png")

    counts = {str(index): int(np.count_nonzero(zone_labels == index)) for index in range(len(PALETTE))}
    print(
        {
            "source_size": source_image.size,
            "output_size": TARGET_SIZE,
            "raw_zone_counts": raw_counts,
            "zone_counts": counts,
            "land_pixels": int(land_mask.sum()),
            "road_pixels": int(road_mask.sum()),
            "centerline_pixels": int(centerline.sum()),
        }
    )


if __name__ == "__main__":
    main()
