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
COMPLETION_CATALOG = PROJECT / "Tools/ArtCompletion/completion.catalog.json"
NS = "{http://www.w3.org/2000/svg}"
COMPLETION_MODULES = (
    "module_controller", "module_casing", "module_input", "module_output",
    "module_maintenance", "module_exhaust", "module_energy_steam",
    "module_energy_electric", "module_process_furnace",
    "module_process_assembly", "module_process_ore",
)
AVATAR_ARMOR_19_PARTS = ("head", "torso", "arm", "leg", "shoe")
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


def catalog_labels(path):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    rows = data if isinstance(data, list) else data.get("sprites")
    assert isinstance(rows, list), f"{path} needs a sprite catalog list"
    result = {}
    for row in rows:
        name, label = row.get("name"), row.get("label")
        assert isinstance(name, str) and name and isinstance(label, str) and label, path
        assert name not in result, ("duplicate catalog label", path, name)
        result[name] = label
    return result


def runtime_coverage(source_labels):
    core = (PROJECT / "Assets/DeadCircuit/Scripts/Core/DemoState.cs").read_text(encoding="utf-8-sig")
    survival = (PROJECT / "Assets/DeadCircuit/Scripts/Core/SurvivalState.cs").read_text(encoding="utf-8-sig")
    art = (PROJECT / "Assets/DeadCircuit/Scripts/Presentation/DemoArt.cs").read_text(encoding="utf-8-sig")
    keys, atlas_stats, completion_keys = set(), [], set()
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
                if path.stem == "completion":
                    completion_keys.add(key)
                x, y, w, h = (entry[f] for f in ("x", "y", "w", "h"))
                assert 0 <= x < x + w <= png.width and 0 <= y < y + h <= png.height, key
        atlas_stats.append({"atlas": path.stem, "sprites": len(layout["sprites"])})
    assert not (RETIRED_WORLD | RETIRED_ICONS) & keys
    runtime_labels = dict(source_labels)
    if completion_keys:
        assert COMPLETION_CATALOG.exists(), COMPLETION_CATALOG
        supplemental = catalog_labels(COMPLETION_CATALOG)
        assert set(supplemental) == completion_keys, "completion catalog/runtime key drift"
        assert not set(supplemental) & set(source_labels), "completion catalog duplicates source labels"
        runtime_labels.update(supplemental)
    fallbacks = {}
    for match in re.finditer(r'((?:case\s+"[^"]+":\s*)+)fallback\s*=\s*"([^"]+)"', art):
        for key in re.findall(r'case\s+"([^"]+)"', match[1]):
            fallbacks[key] = match[2]
    aliases = dict(re.findall(r'if\s*\(key\s*==\s*"([^"]+)"\)\s*key\s*=\s*"([^"]+)"', art))

    def direct_contract(prefix, enum_name, text, expected_total, label_mode):
        entries = enum_names(text, enum_name)
        expected = [f"{prefix}_{i}" for i in range(len(entries))]
        missing = [key for key in expected if key not in keys]
        assert len(entries) == expected_total, (enum_name, len(entries), expected_total)
        assert not missing, (enum_name, "missing direct runtime key", missing)
        for index, name in enumerate(entries):
            key = expected[index]
            if key not in runtime_labels and enum_name == "BuildKind":
                # machine_31..35 come from modules.json, which has no world/icons/completion label.
                continue
            assert key in runtime_labels, (enum_name, "missing catalog label", key)
            label = runtime_labels[key]
            if label_mode == "suffix":
                assert label.split(" / ")[-1] == name, (enum_name, index, name, label)
            else:
                assert label.split()[0] == name, (enum_name, index, name, label)
        return {"total": len(entries), "directFrames": len(expected), "missing": missing,
                "labelMode": label_mode}

    contracts = {
        "FoodKind": direct_contract("food", "FoodKind", survival, 31, "prefix"),
        "GearKind": direct_contract("gear", "GearKind", core, 23, "prefix"),
        "PartKind": direct_contract("part", "PartKind", core, 7, "prefix"),
        "ItemKind": direct_contract("item", "ItemKind", core, 66, "prefix"),
        "NodeKind": direct_contract("node", "NodeKind", core, 18, "prefix"),
        "BuildKind": direct_contract("machine", "BuildKind", core, 67, "suffix"),
    }
    expected_completion = (
        {f"item_{i}" for i in range(25, 66)} |
        {f"gear_{i}" for i in range(19, 23)} |
        {f"machine_{i}" for i in range(36, 67)} |
        {f"node_{i}" for i in range(8, 18)} |
        set(COMPLETION_MODULES)
        | {f"avatar_armor_19_{part}_{frame}" for part in AVATAR_ARMOR_19_PARTS
           for frame in range(4)}
    )
    assert completion_keys == expected_completion, (
        "completion keys differ from the 117-frame contract",
        sorted(expected_completion - completion_keys),
        sorted(completion_keys - expected_completion),
    )
    module_missing = [key for key in COMPLETION_MODULES if key not in keys]
    assert not module_missing, ("module component art", module_missing)
    contracts["ModuleComponent"] = {
        "total": len(COMPLETION_MODULES), "directFrames": len(COMPLETION_MODULES),
        "missing": module_missing,
    }
    avatar_armor_19 = [
        f"avatar_armor_19_{part}_{frame}"
        for part in AVATAR_ARMOR_19_PARTS for frame in range(4)
    ]
    avatar_missing = [key for key in avatar_armor_19 if key not in keys]
    assert not avatar_missing, ("radiation suit wearable art", avatar_missing)
    contracts["AvatarArmor19"] = {
        "total": len(avatar_armor_19), "directFrames": len(avatar_armor_19),
        "missing": avatar_missing,
    }
    for prefix, enum in (("creature", "CreatureKind"), ("crop", "CropKind")):
        entries = enum_names(survival, enum)
        for i, name in enumerate(entries):
            for key in ([f"crop_{i}_0", f"crop_{i}_1"] if prefix == "crop" else [f"creature_{i}"]):
                assert key in keys, (enum, "missing direct runtime key", key)
                assert runtime_labels[key].split()[0] == name, (enum, i, name, runtime_labels[key])
        contracts[enum] = {"total": len(entries), "directFrames": len(entries) * (2 if prefix == "crop" else 1)}
    contracts["_atlasStats"] = atlas_stats
    contracts["_fallbackPolicy"] = {
        "mappingsRetained": len(fallbacks),
        "aliasesRetained": len(aliases),
        "fallbacksUsedAsDirect": [],
        "countedAsDirectCoverage": False,
    }
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
              "coverageNote": "Original world/icons remain 206 pixel-identical authored frames. "
                              "Runtime completion supplies direct art for ItemKind 66, GearKind 23, "
                              "BuildKind 67, NodeKind 18, and 11 named module components; retained "
                              "fallbacks are backup-only and are never counted as direct coverage. "
                              "The completion atlas also contains all 20 wearable frames for the "
                              "radiation suit."}
    if baseline_ref:
        result["baseline"] = compare_baseline(baseline_ref)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-ref", help="Optional read-only git ref for retained-frame/registration/meta comparison")
    args = parser.parse_args()
    print(json.dumps(verify(args.baseline_ref), ensure_ascii=False))
