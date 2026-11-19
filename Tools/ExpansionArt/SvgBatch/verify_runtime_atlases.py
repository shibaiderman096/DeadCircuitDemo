"""Read-only contract checks on published SVG/PNG/JSON; never starts a renderer or Unity."""
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


def enum_names(text, name):
    body = re.search(r"enum\s+" + name + r"\s*\{([^}]+)\}", text).group(1)
    result = []
    for field in body.split(","):
        pieces = field.strip().split("=")
        if len(pieces) == 2:
            assert int(pieces[1]) == len(result), (name, field, "noncontiguous ordinal")
        result.append(pieces[0].strip())
    return result


def verify():
    names, labels, hashes, atlas_results = set(), {}, set(), []
    for stem in ("icons", "world"):
        source = ET.parse(ROOT / "Sources" / (stem + ".svg")).getroot()
        layout = json.loads((RUNTIME / (stem + ".json")).read_text(encoding="utf-8"))
        assert layout == json.loads((ROOT / "Sources" / (stem + ".layout.json")).read_text(encoding="utf-8"))
        assert source.get("shape-rendering") == "crispEdges"
        assert not source.findall(".//" + NS + "image"), "raster embedded in SVG"
        palette = {key: tuple(bytes.fromhex(value)) + (255,) for key, value in
                   re.findall(r"\.([\w-]+)\{fill:#([0-9a-fA-F]{6})\}", source.find(NS + "style").text)}
        png_path = RUNTIME / (stem + ".png")
        with Image.open(png_path) as png:
            assert png.mode == "RGBA" and png.size == (layout["width"], layout["height"])
            actual = np.array(png)
        assert source.get("width") == str(layout["width"]) and source.get("height") == str(layout["height"])
        expected = np.zeros_like(actual)
        groups = {group.get("id"): group for group in source.findall(NS + "g")}
        catalog = json.loads((ROOT / "Sources" / (stem + ".catalog.json")).read_text(encoding="utf-8"))
        assert len(catalog) == len(groups) == len(layout["sprites"])
        for row in catalog:
            assert row["name"] not in labels
            labels[row["name"]] = row["label"]
        for index, entry in enumerate(layout["sprites"]):
            key, x, y = entry["name"], entry["x"], entry["y"]
            assert key not in names and entry["w"] == entry["h"] == 32
            assert (x, y) == ((index % 16) * 36 + 2, (index // 16) * 36 + 2)
            names.add(key)
            group = groups[key]
            assert group.get("transform") == f"translate({x} {y})"
            assert group.find(NS + "title").text == labels[key]
            for rect in group.findall(NS + "rect"):
                rx, ry, rw, rh = (int(rect.get(k)) for k in ("x", "y", "width", "height"))
                assert 0 <= rx < rx + rw <= 32 and 0 <= ry < ry + rh <= 32
                expected[y+ry:y+ry+rh, x+rx:x+rx+rw] = palette[rect.get("class")]
            frame = actual[y:y+32, x:x+32]
            assert frame[:, :, 3].any(), key + " is empty"
            digest = hashlib.sha256(frame.tobytes()).hexdigest()
            assert digest not in hashes, key + " duplicates another frame"
            hashes.add(digest)
        # Direct source-rectangle comparison also catches any nontransparent gutter pixels.
        assert np.array_equal(actual, expected), stem + " PNG pixels differ from SVG rectangles"
        atlas_results.append({"atlas": stem, "sprites": len(layout["sprites"]),
                              "size": [layout["width"], layout["height"]],
                              "sha256": hashlib.sha256(png_path.read_bytes()).hexdigest()})
    core = (PROJECT / "Assets/DeadCircuit/Scripts/Core/DemoState.cs").read_text(encoding="utf-8-sig")
    survival = (PROJECT / "Assets/DeadCircuit/Scripts/Core/SurvivalState.cs").read_text(encoding="utf-8-sig")
    contracts = {}
    for prefix, enum, text in (("food", "FoodKind", survival), ("gear", "GearKind", core),
                               ("part", "PartKind", core), ("item", "ItemKind", core),
                               ("node", "NodeKind", core), ("creature", "CreatureKind", survival)):
        entries = enum_names(text, enum)
        for i, name in enumerate(entries):
            assert labels[f"{prefix}_{i}"].split()[0] == name, (enum, i, name)
        contracts[enum] = len(entries)
    for i, name in enumerate(enum_names(core, "BuildKind")):
        assert labels[f"machine_{i}"].split(" / ")[-1] == name
    for i, name in enumerate(enum_names(survival, "CropKind")):
        for stage in range(2):
            assert labels[f"crop_{i}_{stage}"].split()[0] == name
    assert len(names) == 241
    return {"status": "pass", "sprites": len(names), "uniqueFrames": len(hashes),
            "atlases": atlas_results, "enumContracts": contracts,
            "sourcePixelEquality": True, "transparentGutters": True}


if __name__ == "__main__":
    print(json.dumps(verify(), ensure_ascii=False))
