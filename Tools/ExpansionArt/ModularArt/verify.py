"""Independent, read-only SVG/PNG/JSON contract checks; no Unity or renderer."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
RUNTIME = PROJECT / "Assets/DeadCircuit/Resources/Art/Expansion"
NS = "{http://www.w3.org/2000/svg}"
KEYS = {"machine_31", "door_open", "machine_32", "machine_33",
        "machine_34", "machine_35", "floor_room", "roof_tile", "cluster_link"}
KEYS.update(f"wall_{i}" for i in range(16))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def palette_from_existing_source():
    # Parse the authoritative literal; do not import the authoring implementation.
    tree = ast.parse((ROOT.parent / "SvgBatch/pixel_svg.py").read_text(encoding="utf-8-sig"))
    assignment = next(n for n in tree.body if isinstance(n, ast.Assign)
                      and any(isinstance(t, ast.Name) and t.id == "PALETTE" for t in n.targets))
    return ast.literal_eval(assignment.value)


def verify(png_path=None):
    png_path = Path(png_path) if png_path else RUNTIME / "modules.png"
    layout_path = ROOT / "Sources/modules.layout.json"
    layout = json.loads(layout_path.read_text(encoding="utf-8"))
    catalog = json.loads((ROOT / "Sources/modules.catalog.json").read_text(encoding="utf-8"))
    if png_path.resolve() == (RUNTIME / "modules.png").resolve():
        assert layout == json.loads((RUNTIME / "modules.json").read_text(encoding="utf-8"))
    assert (layout["width"], layout["height"]) == (288, 144)
    sprites = layout["sprites"]
    assert len(sprites) == len(catalog) == 25
    assert len({e["name"] for e in sprites}) == 25
    assert {e["name"] for e in sprites} == KEYS
    assert [e["name"] for e in sprites] == [e["name"] for e in catalog]
    footprints = {e["name"]: e["footprint"] for e in catalog}
    for key, expected in (("machine_32", [2, 2]), ("machine_34", [1, 2]), ("machine_35", [3, 2])):
        assert footprints[key] == expected, key

    palette = palette_from_existing_source()
    source_path = ROOT / "Sources/modules.svg"
    source = ET.parse(source_path).getroot()
    assert source.tag == NS+"svg"
    assert source.attrib == {"width": "288", "height": "144",
                             "viewBox": "0 0 288 144", "shape-rendering": "crispEdges"}
    assert all(e.tag in {NS+t for t in ("svg", "style", "g", "title", "rect")}
               for e in source.iter()), "Forbidden SVG element (bitmap/text/filter/gradient/etc.)"
    styles = source.findall(NS+"style")
    assert len(styles) == 1 and not styles[0].attrib
    css = styles[0].text or ""
    rules = re.findall(r"\.c-([A-Za-z]+)\{fill:(#[0-9a-f]{6})\}", css)
    assert len(rules) == len(palette) and dict(rules) == palette
    assert ''.join(f'.c-{n}{{fill:{c}}}' for n, c in rules) == css
    colors = {"c-"+n: (*bytes.fromhex(c[1:]), 255) for n, c in palette.items()}
    groups = source.findall(NS+"g")
    assert len(groups) == 25 and len(list(source)) == 26
    assert [g.get("id") for g in groups] == [e["name"] for e in sprites]
    with Image.open(png_path) as png:
        assert png.mode == "RGBA" and png.size == (288, 144)
        actual = np.array(png)
    expected = np.zeros_like(actual)
    content_mask = np.zeros(actual.shape[:2], dtype=bool)
    frames, digests, records = {}, set(), []
    rect_count = 0
    for i, (entry, meta, group) in enumerate(zip(sprites, catalog, groups)):
        key, x, y = entry["name"], entry["x"], entry["y"]
        assert set(entry) == {"name", "x", "y", "w", "h"}
        assert all(type(entry[n]) is int for n in ("x", "y", "w", "h"))
        assert (entry["w"], entry["h"]) == (32, 32)
        assert (x, y) == ((i % 8)*36+2, (i//8)*36+2)
        assert group.attrib == {"id": key, "transform": f"translate({x} {y})"}
        titles = group.findall(NS+"title")
        assert len(titles) == 1 and titles[0].text == meta["label"] and not titles[0].attrib
        rects = group.findall(NS+"rect")
        assert len(list(group)) == len(rects)+1
        for rect in rects:
            assert set(rect.attrib) == {"class", "x", "y", "width", "height"}
            assert not list(rect) and not rect.text
            for field in ("x", "y", "width", "height"):
                assert re.fullmatch(r"0|[1-9][0-9]*", rect.get(field)), (key, field)
            rx, ry, rw, rh = (int(rect.get(f)) for f in ("x", "y", "width", "height"))
            assert 0 <= rx < rx+rw <= 32 and 0 <= ry < ry+rh <= 32
            assert rect.get("class") in colors
            expected[y+ry:y+ry+rh, x+rx:x+rx+rw] = colors[rect.get("class")]
            rect_count += 1
        frame = actual[y:y+32, x:x+32]
        assert np.any(frame[:, :, 3]), key+" empty"
        digest = hashlib.sha256(frame.tobytes()).hexdigest()
        assert digest not in digests, key+" duplicates another frame"
        digests.add(digest)
        frames[key] = frame
        content_mask[y:y+32, x:x+32] = True
        records.append({"key": key, "opaquePixels": int(np.count_nonzero(frame[:, :, 3])),
                        "sha256": digest})
    mismatches = int(np.count_nonzero(np.any(actual != expected, axis=2)))
    assert mismatches == 0, f"{mismatches} PNG pixels differ from parsed SVG"
    assert not actual[~content_mask, 3].any(), "gutter or unused slot spill"
    assert set(np.unique(actual[:, :, 3])).issubset({0, 255})
    rgba_values = set(map(tuple, actual[actual[:, :, 3] > 0]))
    assert rgba_values.issubset(set(colors.values()))

    # Independent geometry/topology test for every E/N/W/S connector.
    wall_coverage = []
    edge_pairs = edge_pixels = 0
    for mask in range(16):
        frame = frames[f"wall_{mask}"]
        alpha = frame[:, :, 3] > 0
        shape = np.zeros((32, 32), dtype=bool)
        shape[11:21, 11:21] = True
        if mask & 1:
            shape[11:21, 16:32] = True
        if mask & 2:
            shape[:16, 11:21] = True
        if mask & 4:
            shape[11:21, :16] = True
        if mask & 8:
            shape[16:32, 11:21] = True
        assert np.array_equal(alpha, shape), f"wall_{mask} connection geometry"
        assert np.count_nonzero(alpha) <= 540, f"wall_{mask} resembles full building"
        wall_coverage.append(int(np.count_nonzero(alpha)))
        for bit, edge in ((1, alpha[:, -1]), (2, alpha[0]), (4, alpha[:, 0]), (8, alpha[-1])):
            assert bool(edge.any()) == bool(mask & bit), (mask, bit)
        for other in range(16):
            if mask & 1 and other & 4:
                assert np.array_equal(frame[:, -1], frames[f"wall_{other}"][:, 0])
                edge_pairs += 1
                edge_pixels += 32
            if mask & 8 and other & 2:
                assert np.array_equal(frame[-1], frames[f"wall_{other}"][0])
                edge_pairs += 1
                edge_pixels += 32
    # Measure the longest fully transparent column run over ALL 32 rows.
    # A clear slice near the door alone would miss a leaf/lintel further along the path.
    clear_columns = np.all(frames["door_open"][:, :, 3] == 0, axis=0)
    runs, start = [], None
    for x in range(33):
        if x < 32 and clear_columns[x]:
            if start is None:
                start = x
        elif start is not None:
            runs.append((start, x-1))
            start = None
    assert runs, "open doorway has no continuous N/S passage"
    passage_left, passage_right = max(runs, key=lambda r: r[1]-r[0])
    passage_width = passage_right-passage_left+1
    assert passage_width >= 20 and (passage_left, passage_right) == (6, 25), "open doorway too narrow or off-center"
    player_diameter_px = round(0.56*32, 2)
    total_clearance_px = round(passage_width-player_diameter_px, 2)
    assert total_clearance_px >= 0, "player collision diameter exceeds visual doorway"
    assert frames["machine_31"][11:21, passage_left:passage_right+1, 3].all(), "closed door has a hole"
    # Shared jambs retain exactly the same RGBA pixels in both door states.
    for left, right in ((2, 6), (26, 30)):
        assert np.array_equal(frames["machine_31"][9:23, left:right],
                              frames["door_open"][9:23, left:right]), "door-state jamb mismatch"
    for key in ("machine_31", "door_open"):
        assert frames[key][10:22, 3:5, 3].all() and frames[key][10:22, 27:29, 3].all()
        for mask in range(16):
            if mask & 1:
                assert np.array_equal(frames[f"wall_{mask}"][:, -1], frames[key][:, 0])
            if mask & 4:
                assert np.array_equal(frames[key][:, -1], frames[f"wall_{mask}"][:, 0])
    assert np.count_nonzero(frames["floor_room"][:, :, 3]) == 1024
    assert np.count_nonzero(frames["roof_tile"][:, :, 3]) == 1024

    legacy_keys = []
    for stem in ("icons", "world"):
        legacy = json.loads((RUNTIME / f"{stem}.json").read_text(encoding="utf-8"))
        legacy_keys.extend(e["name"] for e in legacy["sprites"])
    assert len(legacy_keys) == len(set(legacy_keys)) == 241
    assert not (set(legacy_keys) & KEYS), "legacy atlas key collision"
    baseline_path = ROOT / "Reports/protected_baseline.json"
    baseline = json.loads(baseline_path.read_text(encoding="utf-8")) if baseline_path.exists() else {}
    for rel, digest in baseline.items():
        assert sha(PROJECT / rel) == digest, f"protected file changed: {rel}"
    return {"status": "pass", "sprites": 25, "uniqueKeys": 25, "uniqueFrames": len(digests),
            "size": [288, 144], "logicalPixels": 32, "pitch": 36, "gutter": 2,
            "sourceRectangles": rect_count, "integerGrid": True, "paletteColors": len(palette),
            "usedColors": len(rgba_values), "sourcePixelEquality": True,
            "sourcePixelMismatches": mismatches, "partialAlphaPixels": 0, "offPalettePixels": 0,
            "gutterOpaquePixels": 0, "unusedSlots": 7, "unusedSlotOpaquePixels": 0,
            "wallMasks": 16, "wallCoverageMinMax": [min(wall_coverage), max(wall_coverage)],
            "wallAdjacentEdgePairs": edge_pairs, "wallEdgePixelsCompared": edge_pixels,
            "wallSeamMismatches": 0, "doorOpenClearPassageWidth": passage_width,
            "doorOpenClearPassageX": [passage_left, passage_right],
            "doorPassageMeasuredAcrossRows": 32, "doorPlayerDiameterPx": player_diameter_px,
            "doorOpenTotalClearancePx": total_clearance_px,
            "doorOpenClearancePerSidePx": round(total_clearance_px/2, 2),
            "doorSharedJambPixelsIdentical": True,
            "doorJambsAndClosedBarrier": True, "doorWallJoinPairs": 32,
            "footprints": {k: footprints[k] for k in ("machine_32", "machine_34", "machine_35")},
            "legacySpriteCount": len(legacy_keys), "legacyKeyCollisions": 0,
            "protectedFilesChecked": len(baseline), "pngSHA256": sha(png_path),
            "svgSHA256": sha(source_path), "frames": records}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--png", type=Path, help="Check staged raster against source instead of published runtime PNG")
    args = parser.parse_args()
    print(json.dumps(verify(args.png), ensure_ascii=False))
