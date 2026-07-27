from pathlib import Path

import numpy as np
from PIL import Image

from process_level03_masks import SOURCE, pad_to_ratio, quantize_to_palette, resize_labels


ROOT = Path(__file__).resolve().parents[2]
zone = np.asarray(Image.open(ROOT / "Assets/Level03/MapMasks/Level03_ZoneMap.png").convert("RGB"))
width = zone.shape[1]


def components(mask):
    remaining = set(np.flatnonzero(mask).tolist())
    result = []
    while remaining:
        start = remaining.pop()
        stack = [start]
        area = 0
        min_x = width
        min_y = zone.shape[0]
        max_x = 0
        max_y = 0
        sum_x = 0
        sum_y = 0
        while stack:
            index = stack.pop()
            y, x = divmod(index, width)
            area += 1
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            sum_x += x
            sum_y += y
            for next_index in (index - width, index + width, index - 1, index + 1):
                if next_index in remaining:
                    next_y, next_x = divmod(next_index, width)
                    if abs(next_x - x) <= 1 and abs(next_y - y) <= 1:
                        remaining.remove(next_index)
                        stack.append(next_index)
        result.append((area, (min_x, min_y, max_x, max_y), (sum_x // area, sum_y // area)))
    return sorted(result, reverse=True)


for color_name, color in (("magenta", (255, 0, 255)), ("cyan", (0, 255, 255))):
    mask = np.all(zone == color, axis=2)
    print(color_name, components(mask)[:20])

raw_labels = quantize_to_palette(Image.open(SOURCE))
raw_labels = resize_labels(pad_to_ratio(raw_labels, 3000 / 2500))
print("raw magenta", components(raw_labels == 7)[:20])
