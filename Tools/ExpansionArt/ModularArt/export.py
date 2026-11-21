"""Generate original SVG, lint/render via codeart2d, verify, then publish modules.

Only this subtree and runtime modules.png/modules.json are written. Existing
render runs are retained. Pillow is restricted to contact sheets and QA reads.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

from modular_svg import ROOT, PROJECT, RUNTIME, SOURCE, write_sources
from verify import verify

DEFAULT_RENDERER = Path("C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py")
PROTECTED = [
    "Assets/DeadCircuit/Resources/Art/Expansion/icons.png",
    "Assets/DeadCircuit/Resources/Art/Expansion/icons.json",
    "Assets/DeadCircuit/Resources/Art/Expansion/world.png",
    "Assets/DeadCircuit/Resources/Art/Expansion/world.json",
    "Tools/ExpansionArt/SvgBatch/pixel_svg.py",
    "Tools/ExpansionArt/SvgBatch/Sources/icons.svg",
    "Tools/ExpansionArt/SvgBatch/Sources/world.svg",
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+"\n", encoding="utf-8")


def run(argv, log):
    env = dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1")
    result = subprocess.run([str(a) for a in argv], cwd=PROJECT, env=env,
                            capture_output=True, text=True, encoding="utf-8",
                            errors="replace", timeout=120)
    record = {"argv": [str(a) for a in argv], "exitCode": result.returncode,
              "stdout": result.stdout, "stderr": result.stderr}
    write_json(log, record)
    if result.returncode:
        raise RuntimeError(f"{log}: exit {result.returncode}: {result.stdout[-1200:]} {result.stderr[-1200:]}")
    return record


def protected_baseline():
    path = ROOT / "Reports/protected_baseline.json"
    current = {rel: sha(PROJECT / rel) for rel in PROTECTED}
    if path.exists():
        original = json.loads(path.read_text(encoding="utf-8"))
        assert current == original, "Protected old art changed; do not silently reset baseline"
    else:
        write_json(path, current)


def review_sheets(png_path, layout):
    review = ROOT / "Review"
    review.mkdir(parents=True, exist_ok=True)
    with Image.open(png_path) as png:
        atlas = png.convert("RGBA")
    frames = {e["name"]: atlas.crop((e["x"], e["y"], e["x"]+32, e["y"]+32))
              for e in layout["sprites"]}
    # No drawing of runtime sprites: only compositing already-rendered frames,
    # checker backgrounds, human-readable review labels and footprint grids.
    font = ImageFont.load_default(size=11)
    names = [e["name"] for e in layout["sprites"]]
    rows = (len(names)+7)//8
    for scale in (1, 4):
        side = 32*scale
        cw, ch = max(100, side+16), side+42
        sheet = Image.new("RGB", (8*cw, rows*ch), "#192829")
        draw = ImageDraw.Draw(sheet)
        for i, key in enumerate(names):
            x, y = (i % 8)*cw, (i//8)*ch
            for yy in range(0, side, 4*scale):
                for xx in range(0, side, 4*scale):
                    c = "#344a43" if (xx//(4*scale)+yy//(4*scale)) % 2 else "#536a68"
                    draw.rectangle((x+xx, y+yy, x+xx+4*scale-1, y+yy+4*scale-1), fill=c)
            frame = frames[key].resize((side, side), Image.Resampling.NEAREST)
            sheet.paste(frame, (x, y), frame)
            draw.text((x+1, y+side+3), key, font=font, fill="#80d8bc")
            draw.text((x+1, y+side+18), f"32 px / {i:02d}", font=font, fill="#ddd5ac")
        sheet.save(review / f"modules_contact_{scale}x.png")
    # Atlas at exact 1x on light/dark grounds, retaining pitch and gutters.
    native = Image.new("RGB", (atlas.width, atlas.height*2+16), "#ddd5ac")
    native.paste(atlas, (0, 0), atlas)
    d = ImageDraw.Draw(native)
    d.rectangle((0, atlas.height+16, atlas.width-1, native.height-1), fill="#253b3b")
    native.paste(atlas, (0, atlas.height+16), atlas)
    native.save(review / "modules_native_light_dark.png")

    # Show exactly how normalized 32px content scales to occupied world cells.
    footprint_sheet = Image.new("RGB", (4*144, 136), "#253b3b")
    d = ImageDraw.Draw(footprint_sheet)
    for i, (key, fw, fh) in enumerate((("machine_32", 2, 2), ("machine_33", 1, 1),
                                      ("machine_34", 1, 2), ("machine_35", 3, 2))):
        x, y = i*144+8, 8
        for xx in range(fw):
            for yy in range(fh):
                footprint_sheet.paste(frames["floor_room"], (x+xx*32, y+yy*32))
        f = frames[key].resize((fw*32, fh*32), Image.Resampling.NEAREST)
        footprint_sheet.paste(f, (x, y), f)
        for xx in range(fw+1):
            d.line((x+xx*32, y, x+xx*32, y+fh*32), fill="#497b86")
        for yy in range(fh+1):
            d.line((x, y+yy*32, x+fw*32, y+yy*32), fill="#497b86")
        d.text((x, 84), key, font=font, fill="#80d8bc")
        d.text((x, 100), f"{fw} x {fh} cells", font=font, fill="#ddd5ac")
    footprint_sheet.save(review / "module_footprints_native.png")
    footprint_sheet.resize((footprint_sheet.width*4, footprint_sheet.height*4),
                           Image.Resampling.NEAREST).save(review / "module_footprints_4x.png")

    # A contact composition, NOT a building sprite: every floor and wall below
    # is a separately cropped 32px frame; both door states use the same room.
    examples = Image.new("RGB", (2*288+16, 280), "#253b3b")
    d = ImageDraw.Draw(examples)
    for n, door_key in enumerate(("machine_31", "door_open")):
        ox = n*304
        for yy in range(1, 7):
            for xx in range(1, 8):
                examples.paste(frames["floor_room"], (ox+xx*32, yy*32))
        examples.paste(frames["floor_room"], (ox+4*32, 7*32))
        for yy in range(1, 7):
            for xx in range(1, 8):
                if xx not in (1, 7) and yy not in (1, 6):
                    continue
                if (xx, yy) == (4, 6):
                    key = door_key
                else:
                    occupied = lambda a, b: 1 <= a <= 7 and 1 <= b <= 6 and (a in (1, 7) or b in (1, 6))
                    mask = sum(bit for bit, dx, dy in ((1, 1, 0), (2, 0, -1), (4, -1, 0), (8, 0, 1))
                               if occupied(xx+dx, yy+dy))
                    key = f"wall_{mask}"
                f = frames[key]
                examples.paste(f, (ox+xx*32, yy*32), f)
        for key, xx, yy, fw, fh in (("machine_32", 2, 2, 2, 2), ("machine_35", 4, 2, 3, 2)):
            f = frames[key].resize((fw*32, fh*32), Image.Resampling.NEAREST)
            examples.paste(f, (ox+xx*32, yy*32), f)
        d.text((ox+32, 8), "CLOSED" if n == 0 else "OPEN: clear N/S passage", font=font, fill="#80d8bc")
        d.text((ox+32, 264), "Per-tile floor + wall + door", font=font, fill="#ddd5ac")
    examples.save(review / "room_assembly_native.png")
    examples.resize((examples.width*3, examples.height*3), Image.Resampling.NEAREST).save(review / "room_assembly_3x.png")
    return [str(p.relative_to(ROOT)) for p in sorted(review.glob("*.png"))]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--renderer", type=Path, default=DEFAULT_RENDERER)
    parser.add_argument("--doctor", action="store_true", help="Run renderer conformance once for this environment")
    args = parser.parse_args()
    assert args.renderer.is_file(), args.renderer
    protected_baseline()
    authored = write_sources()
    # Repeat generation must preserve all source bytes.
    generated = sorted(SOURCE.glob("*"))
    before = {str(p): sha(p) for p in generated}
    write_sources()
    assert before == {str(p): sha(p) for p in generated}, "nondeterministic source generation"
    reports = ROOT / "Reports"
    run_index = 1
    while (reports / f"run_{run_index:03d}").exists():
        run_index += 1
    logdir = reports / f"run_{run_index:03d}"
    logdir.mkdir(parents=True)
    rendered = ROOT / "Renders" / f"run_{run_index:03d}"
    if args.doctor:
        run([sys.executable, "-B", args.renderer, "doctor", "--backend", "resvg_py",
             "--report", logdir / "renderer_doctor.json"], logdir / "doctor_command.json")
    run([sys.executable, "-B", args.renderer, "lint", "--svg", SOURCE / "modules.svg",
         "--profile", "pixel"], logdir / "lint_command.json")
    run([sys.executable, "-B", args.renderer, "render", "--svg", SOURCE / "modules.svg",
         "--output-dir", rendered, "--crisp", "--zoom", "4", "--strict-qc"],
        logdir / "render_command.json")
    png_path = rendered / "base/modules.png"
    staged = verify(png_path)
    write_json(logdir / "staged_verify.json", staged)
    metadata = json.loads((rendered / "codeart-meta.json").read_text(encoding="utf-8"))
    # Strict renderer gate plus independent SVG-to-raster verification precede publishing.
    assert metadata["qa"]["status"] == "pass", metadata["qa"]
    RUNTIME.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(png_path, RUNTIME / "modules.png")
    shutil.copyfile(SOURCE / "modules.layout.json", RUNTIME / "modules.json")
    checked = verify()
    checked["repeatSourceGenerationIdentical"] = True
    checked["renderMetadata"] = str((rendered / "codeart-meta.json").relative_to(ROOT))
    checked["art_source"] = "code"
    checked["disclosure"] = "code-drawn, no image model"
    checked["reviewSheets"] = review_sheets(png_path, json.loads((RUNTIME / "modules.json").read_text(encoding="utf-8")))
    write_json(logdir / "published_verify.json", checked)
    write_json(reports / "latest_verify.json", checked)
    write_json(ROOT / "codeart-meta.json", {"art_source": "code", "placeholder": False,
               "disclosure": "code-drawn, no image model", "source": "Sources/modules.svg",
               "sourceSHA256": checked["svgSHA256"], "rendererMetadata": checked["renderMetadata"],
               "verification": "Reports/latest_verify.json",
               "limits": "Static SVG/PNG/contact-sheet review only; no Unity, Play, game executable or build."})
    print(json.dumps({**authored, "status": checked["status"], "sha256": checked["pngSHA256"],
                      "sourcePixelMismatches": 0, "wallAdjacentEdgePairs": checked["wallAdjacentEdgePairs"],
                      "report": str((reports / "latest_verify.json").relative_to(PROJECT)),
                      "commands": str(logdir.relative_to(PROJECT)), "reviewSheets": len(checked["reviewSheets"])}))


if __name__ == "__main__":
    main()
