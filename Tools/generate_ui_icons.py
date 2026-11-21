"""Generate Dead Circuit's compact semantic UI icon atlas.

The atlas is deliberately drawn on a 32px logical grid so the runtime can use
Point filtering without introducing soft edges.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


SIZE = 32
COLS = 6
ROWS = 6
OUTLINE = (8, 18, 14, 255)
INK = (241, 235, 220, 255)
CYAN = (130, 219, 215, 255)
AMBER = (242, 187, 98, 255)
MUTED = (171, 188, 177, 255)
RED = (244, 128, 113, 255)


def r(draw: ImageDraw.ImageDraw, box, fill):
    draw.rectangle(box, fill=fill)


def p(draw: ImageDraw.ImageDraw, points, fill):
    draw.polygon(points, fill=fill)


def h(draw, x1, x2, y, fill, width=2):
    r(draw, (x1, y, x2, y + width - 1), fill)


def v(draw, x, y1, y2, fill, width=2):
    r(draw, (x, y1, x + width - 1, y2), fill)


def outline_box(draw, x1, y1, x2, y2, fill, inner=None):
    r(draw, (x1, y1, x2, y2), OUTLINE)
    r(draw, (x1 + 2, y1 + 2, x2 - 2, y2 - 2), fill if inner is None else inner)


def arrow(draw, left=True, color=CYAN):
    if left:
        p(draw, [(5, 16), (12, 8), (15, 8), (15, 12), (25, 12), (25, 20), (15, 20), (15, 24), (12, 24)], OUTLINE)
        p(draw, [(7, 16), (13, 10), (13, 14), (23, 14), (23, 18), (13, 18), (13, 22)], color)
    else:
        p(draw, [(27, 16), (20, 8), (17, 8), (17, 12), (7, 12), (7, 20), (17, 20), (17, 24), (20, 24)], OUTLINE)
        p(draw, [(25, 16), (19, 10), (19, 14), (9, 14), (9, 18), (19, 18), (19, 22)], color)


def plus(draw, color=AMBER):
    r(draw, (14, 5, 18, 27), OUTLINE)
    r(draw, (5, 14, 27, 18), OUTLINE)
    r(draw, (15, 7, 17, 25), color)
    r(draw, (7, 15, 25, 17), color)


def cross(draw, color=RED):
    for i in range(6, 26):
        if 7 <= i <= 24:
            r(draw, (i, i, i + 2, i + 2), OUTLINE)
            r(draw, (30 - i, i, 32 - i, i + 2), OUTLINE)
    for i in range(8, 24):
        r(draw, (i, i + 1, i + 1, i + 2), color)
        r(draw, (29 - i, i + 1, 30 - i, i + 2), color)


def gear(draw, color=AMBER):
    for box in ((12, 4, 19, 7), (12, 25, 19, 28), (4, 12, 7, 19), (25, 12, 28, 19),
                (6, 6, 10, 10), (22, 6, 26, 10), (6, 22, 10, 26), (22, 22, 26, 26)):
        r(draw, box, OUTLINE)
    p(draw, [(10, 9), (22, 9), (25, 12), (25, 20), (22, 23), (10, 23), (7, 20), (7, 12)], OUTLINE)
    p(draw, [(11, 11), (21, 11), (23, 13), (23, 19), (21, 21), (11, 21), (9, 19), (9, 13)], color)
    r(draw, (14, 14, 18, 18), OUTLINE)


def draw_icon(name: str) -> Image.Image:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    if name == "ui_new_world":
        outline_box(draw, 5, 5, 24, 26, MUTED)
        r(draw, (8, 8, 21, 10), CYAN)
        plus(draw, AMBER)
    elif name == "ui_load_world":
        p(draw, [(4, 10), (12, 10), (15, 7), (27, 7), (29, 11), (27, 25), (4, 25)], OUTLINE)
        p(draw, [(7, 12), (13, 12), (16, 10), (25, 10), (26, 12), (24, 22), (7, 22)], AMBER)
        h(draw, 9, 22, 16, CYAN, 2)
    elif name == "ui_network":
        h(draw, 10, 22, 14, CYAN, 2); v(draw, 15, 10, 22, CYAN, 2)
        for x, y in ((5, 5), (19, 5), (11, 21)):
            outline_box(draw, x, y, x + 7, y + 7, AMBER)
    elif name == "ui_settings":
        gear(draw, CYAN)
    elif name == "ui_quit":
        outline_box(draw, 5, 4, 19, 27, MUTED)
        r(draw, (8, 7, 16, 24), (36, 58, 47, 255))
        arrow(draw, False, AMBER)
    elif name == "ui_back":
        arrow(draw, True, CYAN)
    elif name == "ui_random":
        outline_box(draw, 5, 5, 27, 27, AMBER)
        for x, y in ((10, 10), (20, 20), (20, 10), (10, 20), (15, 15)):
            r(draw, (x, y, x + 2, y + 2), OUTLINE)
    elif name == "ui_refresh":
        p(draw, [(17, 4), (26, 7), (23, 11), (20, 9), (13, 8), (9, 12), (7, 18), (4, 16), (6, 9), (12, 5)], OUTLINE)
        p(draw, [(16, 6), (23, 8), (21, 10), (19, 8), (14, 9), (10, 13), (9, 17), (6, 16), (8, 11), (13, 7)], CYAN)
        p(draw, [(15, 28), (6, 25), (9, 21), (12, 23), (19, 24), (23, 20), (25, 14), (28, 16), (26, 23), (20, 27)], OUTLINE)
        p(draw, [(16, 26), (9, 24), (11, 22), (13, 24), (18, 22), (22, 19), (23, 15), (26, 16), (24, 22), (19, 25)], AMBER)
    elif name == "ui_join":
        outline_box(draw, 4, 11, 14, 21, CYAN)
        r(draw, (14, 14, 20, 18), OUTLINE)
        r(draw, (18, 13, 27, 19), AMBER)
        v(draw, 21, 8, 14, AMBER, 2); v(draw, 21, 19, 24, AMBER, 2)
    elif name == "ui_cancel" or name == "ui_close":
        outline_box(draw, 4, 4, 27, 27, (36, 58, 47, 255))
        cross(draw, RED if name == "ui_cancel" else CYAN)
    elif name == "ui_create":
        outline_box(draw, 5, 16, 23, 27, AMBER)
        r(draw, (8, 19, 20, 22), CYAN)
        p(draw, [(8, 13), (12, 4), (16, 6), (12, 15)], OUTLINE)
        p(draw, [(10, 12), (13, 7), (14, 8), (11, 14)], MUTED)
        h(draw, 14, 27, 6, CYAN, 3)
    elif name == "ui_technology":
        outline_box(draw, 5, 5, 26, 26, (36, 58, 47, 255))
        h(draw, 9, 22, 10, CYAN, 2); h(draw, 9, 22, 20, AMBER, 2)
        v(draw, 12, 10, 20, CYAN, 2); v(draw, 19, 10, 20, AMBER, 2)
        for x, y, c in ((7, 8, AMBER), (22, 8, CYAN), (7, 18, CYAN), (22, 18, AMBER)):
            r(draw, (x, y, x + 3, y + 3), c)
    elif name == "ui_inventory":
        outline_box(draw, 4, 10, 28, 25, AMBER)
        r(draw, (8, 6, 24, 12), OUTLINE)
        r(draw, (10, 8, 22, 12), CYAN)
        h(draw, 7, 25, 16, CYAN, 2); v(draw, 15, 16, 22, OUTLINE, 2)
    elif name == "ui_growth":
        outline_box(draw, 5, 17, 27, 26, (36, 58, 47, 255))
        p(draw, [(16, 21), (11, 15), (12, 7), (17, 4), (22, 7), (21, 15)], OUTLINE)
        p(draw, [(16, 19), (13, 14), (14, 9), (17, 7), (20, 9), (19, 14)], CYAN)
        plus(draw, AMBER)
    elif name == "ui_inspect":
        r(draw, (5, 5, 20, 20), OUTLINE); r(draw, (8, 8, 17, 17), CYAN)
        p(draw, [(18, 18), (27, 27), (24, 30), (15, 21)], OUTLINE)
        p(draw, [(19, 20), (25, 26), (24, 27), (18, 21)], AMBER)
    elif name == "ui_help":
        outline_box(draw, 5, 5, 26, 26, CYAN)
        h(draw, 12, 19, 10, OUTLINE, 3); v(draw, 18, 10, 15, OUTLINE, 3)
        h(draw, 13, 18, 15, OUTLINE, 3); v(draw, 11, 15, 18, OUTLINE, 3)
        r(draw, (14, 22, 17, 25), OUTLINE)
    elif name == "ui_session":
        outline_box(draw, 5, 5, 26, 26, (36, 58, 47, 255))
        r(draw, (10, 9, 14, 22), AMBER); r(draw, (18, 9, 22, 22), CYAN)
    elif name == "ui_survival":
        p(draw, [(16, 3), (25, 15), (23, 23), (16, 28), (9, 23), (7, 15)], OUTLINE)
        p(draw, [(16, 6), (22, 15), (20, 21), (16, 25), (12, 21), (10, 15)], CYAN)
        r(draw, (14, 14, 18, 21), AMBER)
    elif name == "ui_save":
        outline_box(draw, 5, 4, 27, 27, CYAN)
        r(draw, (9, 7, 22, 13), AMBER); r(draw, (11, 8, 20, 11), OUTLINE)
        r(draw, (10, 18, 22, 24), OUTLINE); r(draw, (12, 19, 20, 22), MUTED)
    elif name == "ui_research":
        p(draw, [(12, 5), (20, 5), (20, 12), (24, 22), (21, 27), (11, 27), (8, 22), (12, 12)], OUTLINE)
        p(draw, [(14, 7), (18, 7), (18, 13), (21, 22), (19, 24), (13, 24), (11, 22), (14, 13)], CYAN)
        r(draw, (9, 15, 14, 18), AMBER)
    elif name == "ui_craft":
        gear(draw, AMBER)
        r(draw, (5, 22, 12, 25), CYAN); h(draw, 3, 14, 25, CYAN, 2)
    elif name == "ui_repair":
        gear(draw, CYAN)
        p(draw, [(5, 5), (10, 6), (13, 10), (10, 13), (6, 10)], OUTLINE)
        p(draw, [(7, 7), (9, 8), (11, 10), (9, 11), (7, 9)], AMBER)
    elif name == "ui_dismantle":
        r(draw, (5, 20, 26, 25), OUTLINE); r(draw, (8, 21, 23, 23), AMBER)
        p(draw, [(8, 17), (12, 5), (16, 6), (12, 19)], OUTLINE)
        p(draw, [(10, 17), (13, 8), (14, 8), (11, 18)], CYAN)
        r(draw, (18, 8, 24, 11), OUTLINE); h(draw, 20, 28, 9, AMBER, 3)
    elif name == "ui_eat":
        p(draw, [(8, 11), (12, 6), (20, 7), (24, 13), (22, 23), (16, 27), (10, 23), (6, 15)], OUTLINE)
        p(draw, [(10, 12), (13, 8), (19, 9), (22, 14), (20, 21), (16, 24), (11, 21), (8, 15)], RED)
        h(draw, 17, 24, 5, MUTED, 2); r(draw, (23, 3, 26, 7), CYAN)
    elif name == "ui_water":
        p(draw, [(16, 4), (25, 16), (23, 23), (16, 28), (9, 23), (7, 16)], OUTLINE)
        p(draw, [(16, 7), (22, 16), (20, 21), (16, 25), (12, 21), (10, 16)], CYAN)
    elif name == "ui_seed":
        p(draw, [(16, 24), (13, 17), (14, 10), (20, 7), (24, 8), (23, 14), (19, 19)], OUTLINE)
        p(draw, [(17, 21), (16, 17), (17, 12), (20, 10), (21, 10), (20, 14), (18, 18)], CYAN)
        p(draw, [(15, 25), (11, 18), (6, 17), (8, 22), (13, 25)], AMBER)
        v(draw, 15, 18, 28, MUTED, 2)
    elif name == "ui_harvest":
        v(draw, 15, 8, 27, MUTED, 2)
        for y, side in ((8, -1), (12, 1), (16, -1), (20, 1)):
            if side < 0: p(draw, [(14, y + 2), (7, y), (9, y + 5), (14, y + 6)], AMBER)
            else: p(draw, [(16, y + 2), (23, y), (21, y + 5), (16, y + 6)], AMBER)
        h(draw, 9, 22, 27, CYAN, 2)
    elif name == "ui_prev":
        arrow(draw, True, AMBER)
    elif name == "ui_next":
        arrow(draw, False, AMBER)
    elif name == "ui_equip":
        p(draw, [(8, 7), (13, 5), (17, 11), (24, 18), (22, 23), (16, 18), (10, 13)], OUTLINE)
        p(draw, [(10, 8), (13, 7), (15, 11), (22, 19), (21, 21), (17, 16), (12, 12)], CYAN)
        h(draw, 5, 11, 24, AMBER, 3)
    elif name == "ui_salvage":
        p(draw, [(16, 4), (25, 9), (25, 14), (21, 12), (21, 9), (16, 7), (11, 9), (11, 12), (7, 14), (7, 9)], OUTLINE)
        p(draw, [(16, 7), (22, 10), (22, 12), (20, 11), (20, 10), (16, 8), (12, 10), (12, 11), (10, 12), (10, 10)], CYAN)
        p(draw, [(7, 18), (12, 18), (10, 22), (16, 25), (22, 22), (20, 18), (25, 18), (24, 25), (17, 29), (9, 25)], AMBER)
    elif name == "ui_reload":
        outline_box(draw, 7, 5, 17, 27, MUTED)
        r(draw, (10, 8, 14, 12), AMBER); r(draw, (10, 15, 14, 19), CYAN); r(draw, (10, 22, 14, 24), AMBER)
        arrow(draw, False, CYAN)
    elif name == "ui_attach":
        outline_box(draw, 5, 10, 17, 22, CYAN)
        outline_box(draw, 15, 10, 27, 22, AMBER)
        r(draw, (14, 14, 18, 18), OUTLINE)
    elif name == "ui_detach":
        outline_box(draw, 4, 10, 15, 22, CYAN)
        outline_box(draw, 17, 10, 28, 22, AMBER)
        r(draw, (14, 13, 18, 19), (0, 0, 0, 0))
        v(draw, 15, 9, 23, OUTLINE, 2)
    elif name == "ui_armor":
        p(draw, [(16, 4), (26, 8), (24, 22), (16, 28), (8, 22), (6, 8)], OUTLINE)
        p(draw, [(16, 7), (23, 10), (21, 20), (16, 24), (11, 20), (9, 10)], CYAN)
        h(draw, 12, 20, 14, AMBER, 2); v(draw, 15, 10, 21, AMBER, 2)
    else:
        raise ValueError(name)

    return image


NAMES = [
    "ui_new_world", "ui_load_world", "ui_network", "ui_settings", "ui_quit", "ui_back",
    "ui_random", "ui_refresh", "ui_join", "ui_cancel", "ui_create", "ui_technology",
    "ui_inventory", "ui_growth", "ui_inspect", "ui_help", "ui_session", "ui_survival",
    "ui_save", "ui_close", "ui_research", "ui_craft", "ui_repair", "ui_dismantle",
    "ui_eat", "ui_water", "ui_seed", "ui_harvest", "ui_prev", "ui_next",
    "ui_equip", "ui_salvage", "ui_reload", "ui_attach", "ui_detach", "ui_armor",
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, default=Path("Assets/DeadCircuit/Resources/Art/Expansion"))
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    atlas = Image.new("RGBA", (COLS * SIZE, ROWS * SIZE), (0, 0, 0, 0))
    sprites = []
    for index, name in enumerate(NAMES):
        x = (index % COLS) * SIZE
        y = (index // COLS) * SIZE
        atlas.alpha_composite(draw_icon(name), (x, y))
        sprites.append({"name": name, "x": x, "y": y, "w": SIZE, "h": SIZE})

    png_path = args.output_dir / "ui_icons.png"
    json_path = args.output_dir / "ui_icons.json"
    atlas.save(png_path)
    json_path.write_text(json.dumps({"width": atlas.width, "height": atlas.height, "sprites": sprites}, indent=2) + "\n", encoding="utf-8")

    if args.preview:
        preview = Image.new("RGBA", (COLS * 48, ROWS * 48), (16, 31, 26, 255))
        for index, name in enumerate(NAMES):
            icon = draw_icon(name).resize((48, 48), Image.Resampling.NEAREST)
            preview.alpha_composite(icon, ((index % COLS) * 48, (index // COLS) * 48))
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        preview.save(args.preview)

    print(json.dumps({"png": str(png_path), "json": str(json_path), "icons": len(NAMES)}))


if __name__ == "__main__":
    main()
