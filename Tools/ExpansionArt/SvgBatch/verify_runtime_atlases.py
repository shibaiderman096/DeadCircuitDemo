"""Read-only checks for current authored SVG/PNG/JSON and honest runtime coverage."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

from pixel_svg import build_svg
from svg_icons import all_icons
from svg_world import all_world
from svg_machines_survivor import all_extended

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
RUNTIME = PROJECT / "Assets/DeadCircuit/Resources/Art/Expansion"
NS = "{http://www.w3.org/2000/svg}"
RETIRED_WORLD = ({f"site_{i}" for i in range(12)} |
                 {f"deco_{i}" for i in (0, 1, 2, 3, 4, 5, 7)} |
                 {"fx_" + name for name in ("lightning", "ember", "fire_large",
                  "frost_cloud", "ice_shard", "hit_flesh", "hit_metal", "hit_wood")})
RETIRED_ICONS = {"ui_panel", "ui_button", "ui_button_down", "ui_slot",
                 "ui_slot_selected", "ui_windowframe", "ui_tooltip", "ui_progress"}
RETIRED = {"world": RETIRED_WORLD, "icons": RETIRED_ICONS}
EXPECTED_COUNTS = {"world": 124, "icons": 82}


def enum_names(text, name):
    body = re.search(r"enum\s+" + name + r"\s*\{([^}]+)\}", text).group(1)
    result = []
    for field in body.split(","):
        if not field.strip():
            continue
        pieces = field.strip().split("=")
        if len(pieces) == 2:
            assert int(pieces[1]) == len(result), (name, field, "noncontiguous ordinal")
        result.append(pieces[0].strip())
    return result


def authored_batches():
    return {"icons": all_icons(), "world": all_world() + all_extended()}


def registration(entry):
    # DemoArt.Load: absent/nonpositive PPU means the original 32 PPU, centred pivot.
    if entry.get("pixelsPerUnit", 0) <= 0:
        return (32, .5, .5)
    return (entry["pixelsPerUnit"], entry.get("pivotX", 0), entry.get("pivotY", 0))


def runtime_coverage(labels):
    core = (PROJECT / "Assets/DeadCircuit/Scripts/Core/DemoState.cs").read_text(encoding="utf-8-sig")
    survival = (PROJECT / "Assets/DeadCircuit/Scripts/Core/SurvivalState.cs").read_text(encoding="utf-8-sig")
    art = (PROJECT / "Assets/DeadCircuit/Scripts/Presentation/DemoArt.cs").read_text(encoding="utf-8-sig")
    keys = set()
    for path in sorted(RUNTIME.glob("*.json")):
        layout = json.loads(path.read_text(encoding="utf-8-sig"))
        if "sprites" not in layout:
            continue
        with Image.open(path.with_suffix(".png")) as png:
            assert png.size == (layout["width"], layout["height"]), str(path)
            for entry in layout["sprites"]:
                key = entry["name"]
                assert key not in keys, "duplicate runtime atlas key: " + key
                keys.add(key)
                x, y, w, h = (entry[f] for f in ("x", "y", "w", "h"))
                assert 0 <= x < x + w <= png.width and 0 <= y < y + h <= png.height, key
    assert not (RETIRED_WORLD | RETIRED_ICONS) & keys
    fallbacks = {}
    for match in re.finditer(r'((?:case\s+"[^"]+":\s*)+)fallback\s*=\s*"([^"]+)"', art):
        for key in re.findall(r'case\s+"([^"]+)"', match[1]):
            fallbacks[key] = match[2]
    aliases = dict(re.findall(r'if\s*\(key\s*==\s*"([^"]+)"\)\s*key\s*=\s*"([^"]+)"', art))

    def resolve(key):
        key = aliases.get(key, key)
        if key in keys:
            return key
        target = fallbacks.get(key)
        return target if target in keys else None

    contracts = {}
    for prefix, enum, text, direct_count in (
            ("food", "FoodKind", survival, 31), ("gear", "GearKind", core, 19),
            ("part", "PartKind", core, 7), ("item", "ItemKind", core, 25),
            ("node", "NodeKind", core, 8)):
        entries = enum_names(text, enum)
        direct, borrowed, unresolved = [], [], []
        for i, name in enumerate(entries):
            key = f"{prefix}_{i}"
            if key in labels:
                assert labels[key].split()[0] == name, (enum, i, name)
                direct.append(key)
            elif resolve(key):
                borrowed.append({"key": key, "enum": name, "uses": resolve(key)})
            else:
                unresolved.append(key)
        assert len(direct) == direct_count, (enum, direct)
        if prefix == "node":
            assert unresolved == ["node_15", "node_16", "node_17"]
            view = (PROJECT / "Assets/DeadCircuit/Scripts/Presentation/DemoWorldView.cs").read_text(encoding="utf-8-sig")
            assert "DrawGroundPickup(v, state.kind, state.id)" in view
            pickup_rule = re.search(r"bool IsGroundPickup\(NodeKind kind\)\s*=>\s*([^;]+)", core).group(1)
            assert re.findall(r"NodeKind\.(\w+)", pickup_rule) == entries[15:]
            assert all(resolve(key) for key in ("item_63", "item_64", "item_65"))
            procedural = unresolved
            unresolved = []
        else:
            procedural = []
        assert not unresolved, (enum, "unresolved icon", unresolved)
        contracts[enum] = {"total": len(entries), "directFrames": len(direct),
                           "fallbackOrAlias": borrowed, "groundPickupPresentation": procedural,
                           "unresolved": unresolved}
    assert len(contracts["ItemKind"]["fallbackOrAlias"]) == 41
    assert aliases.get("gear_19") == "gear_17"
    assert resolve("item_63") == "deco_6"
    assert [row["key"] for row in contracts["NodeKind"]["fallbackOrAlias"]] == [f"node_{i}" for i in range(8, 15)]
    for i, name in enumerate(enum_names(core, "BuildKind")[:31]):
        assert labels[f"machine_{i}"].split(" / ")[-1] == name
    buildings = enum_names(core, "BuildKind")
    direct = [f"machine_{i}" for i in range(len(buildings)) if f"machine_{i}" in keys]
    borrowed = [{"key": f"machine_{i}", "uses": resolve(f"machine_{i}")}
                for i in range(len(buildings)) if f"machine_{i}" not in keys and resolve(f"machine_{i}")]
    assert direct == [f"machine_{i}" for i in range(36)]
    assert [row["key"] for row in borrowed] == [f"machine_{i}" for i in range(36, 61)]
    missing = [f"machine_{i}" for i in range(len(buildings)) if resolve(f"machine_{i}") is None]
    assert missing == [f"machine_{i}" for i in range(61, 66)]
    modular = (PROJECT / "Assets/DeadCircuit/Scripts/Core/ModularMachineCatalog.cs").read_text(encoding="utf-8-sig")
    assert all("BuildKind." + name in modular for name in buildings[63:66])
    contracts["BuildKind"] = {"total": len(buildings), "directFrames": len(direct),
                             "fallback": borrowed, "knownMissingStandalone": missing[:2],
                             "compositeMachines": missing[2:]}
    for prefix, enum in (("creature", "CreatureKind"), ("crop", "CropKind")):
        entries = enum_names(survival, enum)
        for i, name in enumerate(entries):
            for key in ([f"crop_{i}_0", f"crop_{i}_1"] if prefix == "crop" else [f"creature_{i}"]):
                assert labels[key].split()[0] == name
        contracts[enum] = {"total": len(entries), "directFrames": len(entries) * (2 if prefix == "crop" else 1)}
    return contracts


def compare_baseline(ref):
    def original(path):
        relative = path.relative_to(PROJECT).as_posix()
        return subprocess.check_output(["git", "show", f"{ref}:{relative}"], cwd=PROJECT, timeout=30)

    results = {}
    for stem, removed in RETIRED.items():
        current = json.loads((RUNTIME / (stem + ".json")).read_text(encoding="utf-8"))
        previous = json.loads(original(RUNTIME / (stem + ".json")))
        old_entries = {e["name"]: e for e in previous["sprites"]}
        new_entries = {e["name"]: e for e in current["sprites"]}
        assert set(old_entries) - set(new_entries) == removed, stem + " unexpected removals"
        assert not set(new_entries) - set(old_entries), stem + " unexpected additions"
        old = Image.open(io.BytesIO(original(RUNTIME / (stem + ".png")))).convert("RGBA")
        new = Image.open(RUNTIME / (stem + ".png")).convert("RGBA")
        old_catalog = {e["name"]: e for e in json.loads(original(ROOT / "Sources" / (stem + ".catalog.json")))}
        catalog = json.loads((ROOT / "Sources" / (stem + ".catalog.json")).read_text(encoding="utf-8"))
        for key, entry in new_entries.items():
            before = old_entries[key]
            assert (entry["w"], entry["h"], registration(entry)) == (before["w"], before["h"], registration(before)), key
            def crop(image, e):
                return image.crop((e["x"], e["y"], e["x"] + e["w"], e["y"] + e["h"])).tobytes()
            assert crop(old, before) == crop(new, entry), key + " changed pixels"
        assert all(row == old_catalog[row["name"]] for row in catalog), stem + " catalog registration changed"
        for suffix in (".png.meta", ".json.meta"):
            path = RUNTIME / (stem + suffix)
            assert path.read_bytes().replace(b"\r\n", b"\n") == original(path).replace(b"\r\n", b"\n"), str(path) + " changed beyond checkout line endings"
        results[stem] = {"retainedPixelIdentical": len(new_entries), "removed": len(removed),
                         "registrationPreserved": True, "metaUnchangedIgnoringCRLF": True}
    return {"ref": ref, "atlases": results}


def verify(baseline_ref=None):
    names, labels, hashes, atlas_results = set(), {}, set(), []
    for stem, sprites in authored_batches().items():
        source_text = (ROOT / "Sources" / (stem + ".svg")).read_text(encoding="utf-8")
        source = ET.fromstring(source_text)
        layout = json.loads((RUNTIME / (stem + ".json")).read_text(encoding="utf-8"))
        authored_source, authored_layout, authored_catalog = build_svg(stem, sprites)
        assert source_text == authored_source, stem + " SVG differs from authoring code"
        assert layout == authored_layout == json.loads((ROOT / "Sources" / (stem + ".layout.json")).read_text(encoding="utf-8"))
        catalog = json.loads((ROOT / "Sources" / (stem + ".catalog.json")).read_text(encoding="utf-8"))
        assert catalog == authored_catalog
        assert len(layout["sprites"]) == EXPECTED_COUNTS[stem]
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
        assert np.array_equal(actual, expected), stem + " PNG pixels differ from SVG rectangles/gutters"
        assert set(np.unique(actual[:, :, 3])) <= {0, 255}
        atlas_results.append({"atlas": stem, "sprites": len(layout["sprites"]),
                              "size": [layout["width"], layout["height"]],
                              "sha256": hashlib.sha256(png_path.read_bytes()).hexdigest()})
    assert len(names) == 206 and not names & (RETIRED_WORLD | RETIRED_ICONS)
    result = {"status": "pass", "sprites": len(names), "uniqueFrames": len(hashes),
              "atlases": atlas_results, "runtimeCoverage": runtime_coverage(labels),
              "authoringEquality": True, "sourcePixelEquality": True, "transparentGutters": True,
              "coverageNote": "Integrity passes with reported fallbacks and known missing art; this does not claim every enum has a dedicated image."}
    if baseline_ref:
        result["baseline"] = compare_baseline(baseline_ref)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-ref", help="Optional read-only git ref for retained-frame/registration/meta comparison")
    args = parser.parse_args()
    print(json.dumps(verify(args.baseline_ref), ensure_ascii=False))
