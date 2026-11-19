"""Render the stair SVGs, validate their pixels, and export the DemoArt atlas."""

from copy import deepcopy
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import xml.etree.ElementTree as ET

from PIL import Image, ImageColor
import resvg_py


ROOT = Path(__file__).resolve().parent
RUNTIME = ROOT.parents[2] / "Assets/DeadCircuit/Resources/Art/Expansion"
SOURCES = ROOT / "Sources"
REVIEW = ROOT / "Review"
PALETTE = ROOT.parent / "LandmarkArt/Sources/palette.json"
NS = "http://www.w3.org/2000/svg"
NAMES = ("floor_stairs_up", "floor_stairs_down")
SIZE, GUTTER = 32, 2
PITCH = SIZE + 2 * GUTTER

ET.register_namespace("", NS)


def tag(name):
    return "{" + NS + "}" + name


def canvas(width, height):
    return ET.Element(tag("svg"), {
        "width": str(width), "height": str(height), "viewBox": f"0 0 {width} {height}",
        "shape-rendering": "crispEdges",
    })


def place(target, source, x, y):
    group = ET.SubElement(target, tag("g"), {"transform": f"translate({x} {y})"})
    group.extend(deepcopy(list(source)))


def render(svg):
    return resvg_py.svg_to_bytes(svg_string=ET.tostring(svg, encoding="unicode"),
                                 skip_system_fonts=True, shape_rendering="crisp_edges")


def check(test, message):
    if not test:
        raise ValueError(message)


def decode(png):
    with Image.open(BytesIO(png)) as im:
        return im.convert("RGBA")


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")


def main():
    palette = json.loads(PALETTE.read_text(encoding="utf-8"))["colors"]
    allowed = {ImageColor.getrgb(color) for color in palette.values()}
    check(len(allowed) == 24, "Expected the shared landmark24 palette")
    sprites = [ET.parse(SOURCES / f"{name}.svg").getroot() for name in NAMES]
    width, height = len(sprites) * PITCH, PITCH
    atlas = canvas(width, height)
    layout = {"width": width, "height": height, "sprites": []}
    review = canvas(width, 2 * PITCH)
    for i, (name, sprite) in enumerate(zip(NAMES, sprites)):
        x, y = i * PITCH + GUTTER, GUTTER
        place(atlas, sprite, x, y)
        layout["sprites"].append(dict(name=name, x=x, y=y, w=SIZE, h=SIZE))
        for row, bg in enumerate(("dark", "cream")):
            ET.SubElement(review, tag("rect"), {
                "x": str(i * PITCH), "y": str(row * PITCH), "width": str(PITCH),
                "height": str(PITCH), "fill": palette[bg],
            })
            place(review, sprite, x, row * PITCH + y)

    png = render(atlas)
    pixels = decode(png)
    check(pixels.size == (width, height), "Atlas dimensions differ from its JSON")
    colors = pixels.getcolors(width * height)
    partial = sum(count for count, (_, _, _, a) in colors if 0 < a < 255)
    off_palette = sum(count for count, (r, g, b, a) in colors if a != 0 and (r, g, b) not in allowed)
    gutter = sum(pixels.getpixel((x, y))[3] != 0 for y in range(height) for x in range(width)
                 if not (GUTTER <= x % PITCH < GUTTER + SIZE and GUTTER <= y < GUTTER + SIZE))
    check(partial == off_palette == gutter == 0, "Partial alpha, palette drift or gutter spill")
    details = []
    for entry, source in zip(layout["sprites"], sprites):
        x, y = entry["x"], entry["y"]
        cell = pixels.crop((x, y, x + SIZE, y + SIZE))
        isolated = decode(render(source))
        bounds = isolated.getbbox()
        check(isolated.size == (SIZE, SIZE) and bounds is not None, "Empty or incorrectly sized stair")
        check(cell.tobytes() == isolated.tobytes(), f"Atlas crop differs from {entry['name']}.svg")
        details.append(dict(name=entry["name"], bounds=bounds,
                            opaquePixels=isolated.getchannel("A").histogram()[255]))

    preview = render(review)
    enlarged = decode(preview).resize((4 * width, 8 * PITCH), Image.Resampling.NEAREST)
    check(RUNTIME.is_dir(), "Missing runtime art directory")
    REVIEW.mkdir(exist_ok=True)
    (RUNTIME / "floors.png").write_bytes(png)
    write_json(RUNTIME / "floors.json", layout)
    (REVIEW / "floors_1x.png").write_bytes(preview)
    enlarged.save(REVIEW / "floors_4x.png", format="PNG", compress_level=9)
    report = dict(status="pass", art_source="code", renderer=f"resvg_py {resvg_py.__version__}",
                  atlasSize=pixels.size, atlasSha256=sha256(png).hexdigest(),
                  gutterPerCell=GUTTER, spillPixels=gutter, partialAlphaPixels=partial,
                  offPalettePixels=off_palette, sprites=details)
    write_json(REVIEW / "pixel_qc.json", report)
    print(json.dumps(report))


if __name__ == "__main__":
    main()
