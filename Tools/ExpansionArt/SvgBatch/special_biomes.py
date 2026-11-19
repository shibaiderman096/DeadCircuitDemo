"""Authoring source for the four dedicated natural-biome art variants.

The runtime world atlas is kept stable. This small companion atlas contains
only the additional terrain, ecology, tree, and ground-detail sprites used by
the generator's biome ordinals 6..9. One logical SVG unit is one pixel.
"""
from pathlib import Path

import numpy as np

from pixel_svg import NAMES, Sprite, export_svg

ROOT = Path(__file__).resolve().parent

SPECIAL_BIOMES = (
    (6, "Swamp", "沼泽"),
    (7, "SnowMountain", "雪山"),
    (8, "SakuraGrove", "樱花林"),
    (9, "RiverValley", "河谷"),
)


def _periodic_grid():
    yy, xx = np.indices((32, 32))
    # Mirrored coordinates keep the outermost pixels identical on opposite
    # edges while retaining broad, readable clusters inside the tile.
    px = np.minimum(xx, 31 - xx)
    py = np.minimum(yy, 31 - yy)
    return xx, yy, px, py


def terrain(index, english, chinese):
    s = Sprite(f"terrain_{index}", f"{english} / {chinese} seamless ground", False)
    xx, yy, px, py = _periodic_grid()
    shade = np.zeros((32, 32), dtype=np.int16)
    if index == 6:  # peat, shallow pools, reeds
        shade.fill(NAMES.index("shadow"))
        shade[((px // 5 + py // 4) % 5) == 1] = NAMES.index("soil")
        shade[((px + 2 * py) % 17 < 4) & (py % 7 < 4)] = NAMES.index("olive")
        shade[((px - py) % 19 == 0) & (py % 6 < 3)] = NAMES.index("leaf")
        shade[(px % 13 == 4) & (py % 9 == 3)] = NAMES.index("blue")
    elif index == 7:  # packed snow and slate flecks
        shade.fill(NAMES.index("ice"))
        shade[((px // 7 + py // 6) % 4) == 1] = NAMES.index("white")
        shade[((px + py * 3) % 23 == 5) & (px % 8 < 5)] = NAMES.index("cream")
        rocks = ((px >= 7) & (px <= 11) & (py >= 8) & (py <= 10)) | ((px >= 19) & (px <= 23) & (py >= 21) & (py <= 23))
        shade[rocks] = NAMES.index("steel")
        shade[rocks & ((px + py) % 3 == 0)] = NAMES.index("dark")
    elif index == 8:  # spring grass and blossom petals
        shade.fill(NAMES.index("shadow"))
        shade[((px // 6 + py // 5) % 4) == 1] = NAMES.index("leaf")
        shade[((px + 2 * py) % 19 < 3) & (py % 8 < 4)] = NAMES.index("green")
        petals = ((px + py * 2) % 29 == 7) | ((px * 2 + py) % 31 == 12)
        shade[petals] = NAMES.index("rose")
        shade[petals & ((px + py) % 3 == 0)] = NAMES.index("cream")
    else:  # damp bank, gravel, and tiny wet flecks
        shade.fill(NAMES.index("soil"))
        shade[((px // 5 + py // 7) % 4) == 1] = NAMES.index("shadow")
        shade[((px + py) % 17 < 3) & (px % 9 < 6)] = NAMES.index("olive")
        gravel = ((px * 3 + py) % 23 == 6) | ((px + py * 3) % 27 == 9)
        shade[gravel] = NAMES.index("steel")
        shade[gravel & ((px + 2 * py) % 4 == 0)] = NAMES.index("blue")
    s.p = shade
    return s


def ecology(index, english, chinese):
    s = Sprite(f"biome_{index}", f"{english} / {chinese} ecology cluster")
    if index == 6:
        for x, y, h in ((6, 8, 18), (14, 4, 22), (23, 10, 16)):
            s.line(x, 28, x - 2, y, "leaf", 2).rect(x - 3, y, 5, 2, "green")
        s.oval(5, 22, 8, 5, "blue").oval(19, 24, 9, 4, "blue")
        s.rect(10, 27, 4, 2, "olive")
    elif index == 7:
        s.rect(14, 8, 4, 20, "brown")
        for x, y, w in ((4, 20, 24), (7, 15, 18), (10, 10, 13), (13, 5, 8)):
            s.rect(x, y, w, 4, "dark").rect(x + 2, y, w - 4, 2, "ice")
        s.rect(7, 27, 18, 2, "steel")
    elif index == 8:
        s.rect(15, 13, 3, 16, "brown").line(16, 19, 6, 10, "brown", 2).line(17, 17, 26, 8, "brown", 2)
        for x, y, w, h in ((3, 6, 11, 8), (18, 5, 11, 8), (8, 11, 16, 9)):
            s.oval(x, y, w, h, "rose").rect(x + 3, y + 2, max(2, w - 6), 2, "cream")
        s.rect(12, 27, 10, 2, "leaf")
    else:
        s.rect(15, 10, 3, 19, "brown")
        for x, y in ((5, 8), (10, 12), (21, 7), (25, 12), (8, 18), (20, 19)):
            s.line(16, 13, x, y + 8, "leaf", 2).rect(x, y, 5, 3, "green")
        s.rect(4, 27, 24, 2, "blue")
    return s


def tree(index, english, chinese):
    s = Sprite(f"biome_tree_{index}", f"{english} / {chinese} tree")
    if index == 6:  # cypress / mangrove silhouette
        s.rect(14, 19, 5, 11, "brown").line(16, 22, 8, 12, "brown", 2).line(17, 20, 25, 10, "brown", 2)
        for x, y, w, h in ((3, 8, 13, 10), (17, 6, 12, 11), (8, 15, 18, 9)):
            s.oval(x, y, w, h, "shadow").rect(x + 3, y + 2, max(2, w - 6), 3, "olive")
        s.line(8, 24, 6, 29, "leaf", 1).line(24, 23, 26, 29, "leaf", 1)
    elif index == 7:  # snow covered conifer
        s.rect(14, 20, 5, 10, "brown")
        for x, y, w in ((8, 19, 16), (10, 14, 12), (12, 9, 8), (14, 4, 4)):
            s.rect(x, y, w, 4, "dark").rect(x + 2, y, max(1, w - 4), 2, "ice")
        s.rect(5, 27, 23, 2, "steel")
    elif index == 8:  # cherry canopy
        s.rect(15, 20, 4, 10, "brown").line(17, 22, 8, 14, "brown", 2).line(17, 21, 25, 13, "brown", 2)
        for x, y, w, h in ((4, 8, 13, 10), (16, 6, 13, 11), (8, 13, 18, 10)):
            s.oval(x, y, w, h, "rose").rect(x + 3, y + 2, max(2, w - 6), 2, "cream")
        s.rect(10, 28, 15, 2, "leaf")
    else:  # willow
        s.rect(15, 12, 4, 18, "brown").line(17, 15, 7, 8, "brown", 2).line(18, 15, 27, 9, "brown", 2)
        for x, y, w, h in ((3, 6, 12, 10), (17, 5, 12, 11), (7, 13, 20, 11)):
            s.oval(x, y, w, h, "leaf").rect(x + 3, y + 3, max(2, w - 6), 2, "green")
        s.line(7, 19, 5, 28, "green", 1).line(25, 18, 27, 28, "green", 1)
    return s


def detail(index, english, chinese):
    s = Sprite(f"deco_biome_{index}", f"{english} / {chinese} ground detail")
    if index == 6:
        for x, y in ((7, 24), (12, 20), (18, 25), (24, 19)):
            s.line(x, 29, x - 2, y, "leaf", 2).rect(x - 3, y, 5, 2, "green")
        s.oval(14, 24, 6, 3, "blue")
    elif index == 7:
        s.rect(6, 24, 20, 5, "steel").rect(9, 21, 12, 4, "ice").rect(12, 19, 7, 3, "cream")
        s.rect(4, 27, 7, 2, "dark").rect(23, 26, 6, 3, "dark")
    elif index == 8:
        for x, y in ((7, 10), (14, 22), (22, 13), (25, 24), (10, 17)):
            s.rect(x, y, 3, 2, "rose").rect(x + 1, y - 1, 1, 4, "cream")
        s.rect(4, 27, 24, 2, "leaf")
    else:
        for x, y, w in ((5, 24, 5), (13, 21, 7), (23, 25, 5)):
            s.rect(x, y, w, 3, "steel").rect(x + 1, y, max(2, w - 2), 1, "blue")
        s.line(9, 28, 13, 22, "leaf", 1).line(22, 28, 25, 20, "green", 1)
    return s


def all_specials():
    sprites = []
    for index, english, chinese in SPECIAL_BIOMES:
        sprites.append(terrain(index, english, chinese))
    for index, english, chinese in SPECIAL_BIOMES:
        sprites.append(ecology(index, english, chinese))
    for index, english, chinese in SPECIAL_BIOMES:
        sprites.append(tree(index, english, chinese))
    for index, english, chinese in SPECIAL_BIOMES:
        sprites.append(detail(index, english, chinese))
    return sprites


if __name__ == "__main__":
    result = export_svg("biomes", all_specials())
    print(result)
