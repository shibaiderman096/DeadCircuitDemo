"""Original 32px SVG artwork. No raster API is used to author the art.

All coordinates below are logical integer pixels. The sole runtime rasterizer
is codeart2d/svg_render.py, invoked by export.py.
"""
from dataclasses import dataclass, field
from html import escape
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
SOURCE = ROOT / "Sources"
RUNTIME = PROJECT / "Assets/DeadCircuit/Resources/Art/Expansion"
CONTENT, PITCH, GUTTER, COLS = 32, 36, 2, 8

# Read the existing palette without importing code that could emit old atlases.
_spec = importlib.util.spec_from_file_location("existing_pixel_svg", ROOT.parent / "SvgBatch/pixel_svg.py")
_palette_module = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_palette_module)
PALETTE = dict(_palette_module.PALETTE)


@dataclass
class Sprite:
    name: str
    label: str
    footprint: tuple = (1, 1)
    role: str = "module"
    rects: list = field(default_factory=list)

    def rect(self, x, y, w, h, color):
        assert all(type(v) is int for v in (x, y, w, h)), (self.name, x, y, w, h)
        assert 0 <= x < x+w <= CONTENT and 0 <= y < y+h <= CONTENT
        assert color in PALETTE, color
        self.rects.append((x, y, w, h, color))
        return self

    def dot(self, x, y, color):
        return self.rect(x, y, 1, 1, color)

    def row(self, y, runs):
        for x, w, color in runs:
            self.rect(x, y, w, 1, color)
        return self


def frame(s, x, y, w, h, color="steel"):
    s.rect(x, y, w, h, "ink").rect(x+1, y+1, w-2, h-2, color)
    s.rect(x+1, y+1, w-2, 1, "light")
    return s


def base(s, y=27):
    s.rect(2, y, 28, 4, "ink").rect(3, y, 26, 1, "light")
    s.rect(3, y+1, 26, 2, "dark")
    for x in (5, 25):
        s.dot(x, y+1, "steel")


def pipe(s, x, y, w, h):
    s.rect(x, y, w, h, "ink")
    if w >= h:
        s.rect(x, y+1, w, h-2, "steel").rect(x, y+1, w, 1, "light")
    else:
        s.rect(x+1, y, w-2, h, "steel").rect(x+1, y, 1, h, "light")


def door(opened):
    s = Sprite("door_open" if opened else "machine_31",
               "Open doorway / side-hinged leaf" if opened else "Closed doorway / Door", role="door")
    # The opening points north/south. No top lintel or bottom sill occludes it.
    if opened:
        # Side-swung leaf stays west of x=6; the nearer jamb occludes its base.
        # Draw it first so the shared jamb pixels are identical in both states.
        s.rect(2, 1, 4, 20, "ink").rect(3, 2, 2, 18, "brown")
        s.rect(3, 2, 1, 18, "tan").rect(3, 3, 2, 2, "steel")
        s.rect(3, 15, 2, 2, "steel").dot(4, 7, "gold")
    for x in (2, 26):
        s.rect(x, 9, 4, 14, "ink").rect(x+1, 10, 2, 12, "steel")
        s.rect(x+1, 10, 2, 2, "light").rect(x+1, 20, 2, 2, "dark")
        s.dot(x+1, 13, "cream").dot(x+1, 18, "light")
    # Small connector caps share wall horizontal edge cross sections.
    for x in (0, 30):
        s.rect(x, 11, 2, 10, "ink").rect(x, 12, 2, 2, "light")
        s.rect(x, 14, 2, 5, "steel").rect(x, 19, 2, 1, "dark")
    if not opened:
        # Fill the complete x=6..25 gap when closed (20 logical pixels).
        s.rect(6, 11, 20, 10, "ink").rect(7, 12, 18, 8, "brown")
        s.rect(7, 12, 18, 1, "sand").rect(7, 18, 18, 2, "soil")
        for x in (11, 16, 21):
            s.rect(x, 13, 1, 5, "soil")
        s.rect(7, 14, 18, 1, "steel").rect(7, 17, 18, 1, "steel")
        s.dot(8, 14, "light").dot(23, 14, "light")
        s.rect(20, 15, 3, 2, "ink").dot(21, 15, "gold")
    return s


def chamber():
    s = Sprite("machine_32", "Refractory combustion chamber / CombustionChamber", (2, 2))
    base(s)
    # Separate refractory firebox: no attached boiler drum or chimney.
    s.rect(6, 7, 21, 21, "ink").rect(7, 8, 19, 18, "rust")
    s.rect(8, 5, 17, 3, "ink").rect(9, 6, 15, 2, "light")
    s.rect(5, 8, 23, 3, "ink").rect(6, 8, 21, 2, "steel")
    s.rect(7, 11, 19, 1, "tan").rect(7, 24, 19, 2, "soil")
    for x in (7, 24):
        s.rect(x, 11, 2, 13, "brown").rect(x, 11, 1, 12, "tan")
    s.rect(10, 12, 13, 12, "ink").rect(11, 13, 11, 9, "soil")
    # Stepped fuel/flame silhouettes behind an iron grate.
    s.rect(11, 19, 11, 3, "rust").rect(12, 17, 3, 5, "amber")
    s.rect(16, 14, 3, 8, "amber").rect(19, 18, 3, 4, "amber")
    s.rect(16, 18, 3, 4, "gold").rect(17, 20, 2, 2, "cream")
    for x in (12, 16, 20):
        s.rect(x, 19, 1, 4, "steel")
    s.rect(10, 23, 13, 2, "steel").rect(11, 23, 11, 1, "light")
    # Coal hatch on west; heat coupling flange to east.
    frame(s, 1, 16, 6, 8, "soil")
    s.rect(2, 18, 4, 3, "brown").dot(3, 17, "tan")
    pipe(s, 26, 13, 5, 6)
    s.rect(27, 12, 2, 8, "light").dot(27, 14, "dark").dot(27, 17, "dark")
    return s


def chimney():
    s = Sprite("machine_33", "Independent flue stack / Chimney")
    base(s)
    s.rect(9, 6, 14, 22, "ink").rect(10, 7, 12, 20, "rust")
    s.rect(10, 7, 2, 20, "tan").rect(20, 7, 2, 20, "soil")
    for y in (10, 15, 20, 25):
        s.rect(12, y, 8, 1, "brown")
        s.rect(15 if y % 2 else 17, y-3, 1, 3, "brown")
    # Open dark flue, metal weather cap and two retaining bands.
    s.rect(7, 2, 18, 6, "ink").rect(8, 3, 16, 4, "steel")
    s.rect(10, 3, 12, 2, "ink").rect(9, 6, 14, 1, "light")
    for y in (12, 22):
        s.rect(8, y, 16, 3, "ink").rect(9, y, 14, 1, "light")
        s.rect(9, y+1, 14, 1, "steel").dot(11, y+1, "cream").dot(21, y+1, "dark")
    return s


def pump():
    s = Sprite("machine_34", "Feedwater pump / FeedwaterPump", (1, 2))
    base(s)
    # Intake strainer, vertical pump casing and a distinct cool-water outlet.
    s.rect(4, 20, 8, 8, "ink").rect(5, 21, 6, 6, "blue")
    for y in (22, 25):
        s.rect(5, y, 6, 1, "ice")
    s.rect(7, 21, 1, 6, "steel").rect(9, 21, 1, 6, "steel")
    pipe(s, 7, 13, 5, 8)
    pipe(s, 10, 13, 8, 5)
    frame(s, 13, 9, 11, 16, "steel")
    s.rect(15, 10, 2, 12, "light").rect(21, 11, 2, 12, "dark")
    s.rect(12, 8, 13, 3, "ink").rect(13, 8, 11, 1, "light")
    s.rect(16, 3, 5, 5, "ink").rect(17, 4, 3, 4, "amber")
    s.rect(11, 2, 15, 3, "ink").rect(12, 2, 13, 1, "rust")
    s.rect(18, 0, 2, 2, "ink")
    pipe(s, 23, 15, 8, 5)
    s.rect(27, 14, 2, 7, "light").rect(29, 16, 2, 3, "cyan")
    s.rect(16, 13, 4, 5, "ink").rect(17, 14, 2, 3, "teal")
    return s


def flywheel(s):
    # Source ring is 12x18 so the 3x2 footprint makes it circular (36x36).
    outer = [4, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 4]
    inner = [None, None, None, 5, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 5, None, None, None]
    for yy, left in enumerate(outer):
        right = 12-left
        for xx in range(left, right):
            # Ring, plus four spokes; unpainted quadrants stay transparent.
            hole = inner[yy]
            in_hole = hole is not None and hole <= xx < 12-hole
            if not in_hole or xx in (5, 6) or yy in (8, 9):
                edge = xx == left or xx == right-1 or yy in (0, 17)
                color = "ink" if edge else "tan" if xx < 6 else "brown"
                s.dot(19+xx, 6+yy, color)
    s.rect(23, 13, 4, 4, "ink").rect(24, 14, 2, 2, "light")


def engine():
    s = Sprite("machine_35", "Horizontal steam engine and flywheel / SteamEngine", (3, 2))
    base(s)
    # The cylinder and crosshead are separate from the flywheel.
    s.rect(3, 11, 9, 12, "ink").rect(4, 12, 7, 10, "steel")
    s.rect(4, 12, 7, 2, "light").rect(4, 20, 7, 2, "dark")
    for x in (3, 10):
        s.rect(x, 10, 2, 14, "ink").rect(x, 11, 1, 12, "light")
    pipe(s, 5, 5, 4, 6)
    pipe(s, 1, 4, 9, 4)
    s.rect(2, 3, 2, 6, "light")
    s.rect(11, 15, 10, 3, "ink").rect(11, 16, 10, 1, "cream")
    s.rect(14, 12, 3, 12, "ink").rect(15, 13, 1, 10, "steel")
    s.rect(12, 23, 8, 3, "ink").rect(13, 23, 6, 1, "light")
    flywheel(s)
    s.rect(17, 18, 8, 2, "rust").rect(18, 18, 6, 1, "amber")
    s.dot(23, 18, "cream")
    s.rect(5, 24, 5, 3, "dark").rect(23, 24, 5, 3, "dark")
    return s


def wall_has(mask, x, y):
    # Infinite connector continuations prevent dark end caps at tile seams.
    return ((11 <= x < 21 and 11 <= y < 21)
            or (bool(mask & 1) and x >= 16 and 11 <= y < 21)
            or (bool(mask & 2) and y < 16 and 11 <= x < 21)
            or (bool(mask & 4) and x < 16 and 11 <= y < 21)
            or (bool(mask & 8) and y >= 16 and 11 <= x < 21))


def wall(mask):
    s = Sprite(f"wall_{mask}", f"Brick wall / E1 N2 W4 S8 mask {mask}", role="wall")
    # This is a narrow wall network, never a filled house silhouette.
    # Integer unit rects coalesce into SVG row runs, not a raster bitmap.
    for y in range(32):
        row = []
        for x in range(32):
            if not wall_has(mask, x, y):
                row.append(None)
                continue
            present = lambda dx, dy: wall_has(mask, x+dx, y+dy)
            if not present(0, -1) or not present(-1, 0) or not present(0, 1) or not present(1, 0):
                c = "ink"
            elif not present(0, -2) or not present(0, -3) or not present(-2, 0):
                c = "light"
            elif not present(0, 2) or not present(2, 0):
                c = "dark"
            elif y % 6 == 5 or (x + (y//6 % 2)*4) % 8 == 4:
                c = "shadow"
            else:
                c = "steel"
            # Standardized join cross-section; exact matching for every mask pair.
            if (x < 2 and mask & 4) or (x >= 30 and mask & 1):
                c = {11: "ink", 12: "light", 13: "light", 19: "dark", 20: "ink"}.get(y, "steel")
            if (y < 2 and mask & 2) or (y >= 30 and mask & 8):
                c = {11: "ink", 12: "light", 13: "light", 19: "dark", 20: "ink"}.get(x, "steel")
            row.append(c)
        x = 0
        while x < 32:
            c, end = row[x], x+1
            while end < 32 and row[end] == c:
                end += 1
            if c is not None:
                s.rect(x, y, end-x, 1, c)
            x = end
    return s


def floor_room():
    s = Sprite("floor_room", "Interior concrete floor / one tile", role="floor")
    s.rect(0, 0, 32, 32, "shadow")
    # Repeat-safe slab seams. Deliberately restrained behind machinery.
    s.rect(0, 0, 32, 1, "dark").rect(0, 0, 1, 32, "dark")
    s.rect(1, 1, 30, 1, "steel").rect(1, 2, 1, 29, "steel")
    s.rect(5, 24, 6, 1, "dark").rect(22, 8, 4, 1, "steel")
    s.rect(23, 9, 2, 1, "steel").dot(7, 7, "steel").dot(27, 26, "dark")
    return s


def all_sprites():
    return [door(False), door(True), chamber(), chimney(), pump(), engine(),
            *(wall(i) for i in range(16)), floor_room()]


def write_sources():
    sprites = all_sprites()
    width, height = COLS*PITCH, ((len(sprites)+COLS-1)//COLS)*PITCH
    SOURCE.mkdir(parents=True, exist_ok=True)
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" shape-rendering="crispEdges">',
             '<style>'+''.join(f'.c-{n}{{fill:{v}}}' for n, v in PALETTE.items())+'</style>']
    layout = {"width": width, "height": height, "sprites": []}
    catalog = []
    for i, s in enumerate(sprites):
        x, y = (i % COLS)*PITCH+GUTTER, (i//COLS)*PITCH+GUTTER
        parts.append(f'<g id="{s.name}" transform="translate({x} {y})"><title>{escape(s.label)}</title>')
        for rx, ry, rw, rh, c in s.rects:
            parts.append(f'<rect class="c-{c}" x="{rx}" y="{ry}" width="{rw}" height="{rh}"/>')
        parts.append('</g>')
        layout["sprites"].append({"name": s.name, "x": x, "y": y, "w": CONTENT, "h": CONTENT})
        catalog.append({"name": s.name, "label": s.label, "footprint": list(s.footprint),
                        "role": s.role, "pivot": [0.5, 0.5], "pixelsPerUnit": 32})
    parts.append('</svg>')
    (SOURCE / "modules.svg").write_text('\n'.join(parts)+'\n', encoding="utf-8")
    for name, data in (("modules.layout.json", layout), ("modules.catalog.json", catalog),
                       ("palette.json", {"colors": PALETTE})):
        (SOURCE / name).write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding="utf-8")
    return {"sprites": len(sprites), "size": [width, height], "art_source": "code",
            "disclosure": "code-drawn, no image model"}


if __name__ == "__main__":
    print(json.dumps(write_sources()))
