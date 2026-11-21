# Modular steam equipment and enterable-room tiles

Original SVG/code-drawn pixel art; no image model, imported bitmap, gradient, filter, SVG text or external resource. All source geometry is integer rectangles on a 32x32 logical canvas, using the existing `SvgBatch/pixel_svg.py` palette unchanged.

## Files and loader contract

- `Sources/modules.svg`: editable, grouped SVG atlas; each group ID is the runtime sprite key.
- `Sources/modules.layout.json`: runtime-format rectangle list (`width`, `height`, `sprites`, and `name/x/y/w/h` entries). Coordinates use a top-left origin, exactly as the existing `DemoArt` loader expects.
- `Sources/modules.catalog.json`: labels, roles, centered pivots, 32 pixels per unit and unrotated world footprints. This stays outside Resources and does not enter the runtime loader.
- `Sources/palette.json`: a copy of the authoritative existing palette for review.
- `Assets/DeadCircuit/Resources/Art/Expansion/modules.png` and `modules.json`: the only published runtime files. `DemoArt` already discovers matching JSON/PNG names in this directory. No Presentation changes are included.
- `modular_svg.py`: deterministic SVG authoring; does not use Pillow or write raster art.
- `export.py`: codeart2d lint/render, independent pre-publication checks, publication, contact composition and reports. Pillow only reads QA pixels or composes review sheets.
- `verify.py`: independent, read-only SVG/PNG/JSON checker. Parses source geometry and the palette literal independently of the authoring module; never invokes Unity or a rasterizer.
- `Renders/run_NNN/`: retained SVG renderer output and its native metadata.
- `Reports/run_NNN/`: exact subprocess argument arrays, exit codes, stdout/stderr, staged and published verification.
- `Review/`: native and enlarged contacts, light/dark native atlas, actual-footprint sheet, and a room assembled from individual tiles in both door states. These compositions are review images, not runtime building sprites.

## Contents

23 unique keys in an 8-column, 288x108 atlas. Every content rectangle is 32x32, placed at `column*36+2, row*36+2`. Each cell has a transparent 2px gutter on every side. One unused cell remains fully transparent. The unused roof and coupling overlays have been retired from the generator, SVG, catalog, runtime atlas and current review sheets.

| Key | Visual / footprint |
| --- | --- |
| `machine_31` | Closed wooden/metal door between stone jambs; horizontal barrier |
| `door_open` | Same jambs; leaf swung behind the west jamb; continuous 20px transparent north/south passage (`x=6..25`) |
| `machine_32` | Refractory combustion chamber, coal hatch, grate and heat coupling; 2x2 |
| `machine_33` | Separate brick flue stack, open cap and metal bands; 1x1 |
| `machine_34` | Intake strainer, feedwater casing, valve and cool-water outlet; 1x2 |
| `machine_35` | Steam cylinder, crosshead, crank and open flywheel; 3x2 |
| `wall_0` .. `wall_15` | Thin connected stone/metal wall segments; bit 0 east, bit 1 north, bit 2 west, bit 3 south |
| `floor_room` | One repeatable interior concrete floor tile |

The largest wall mask covers 540/1024 pixels (52.73%); even the four-way junction leaves corner areas transparent. Straight walls occupy only a narrow band, separate from floor and roof layers. Both door states match the wall east/west edge profile. The open door does not paint a lintel or sill across its passage. Rotate the sprite at integration time if an east/west passage is needed.

The door jambs occupy `x=2..5` and `x=26..29`, with identical RGBA pixels in both states over `y=9..22`. The side-swung leaf is behind the west jamb and never enters `x=6..25`. The closed panel fills this complete 20px gap over `y=11..20`. The independent checker measures the longest run of columns transparent through all 32 rows instead of reporting a hard-coded width. Against the supplied 0.56-cell collision diameter (17.92px at 32px/cell), the centered opening leaves 2.08px total visual clearance, or 1.04px per side. This is static pixel clearance, not a runtime collision test.

The historical door-width revision changed only `machine_31` and `door_open` in the then-25-frame atlas. `Reports/door_width_revision.json` records that comparison against `Reports/door_width_revision_baseline.json`. Previous render runs and reports retain their original layouts as history. Retiring the two unused overlays preserves every pixel, label, footprint, pivot and PPU of the remaining 23 frames; their slot coordinates also stay unchanged while the fourth atlas row is removed.

Machine source frames follow the existing normalized 32x32 convention. The view scales that frame to its world footprint. The engine's source flywheel is correspondingly narrow (12x18), becoming circular when its source frame is scaled to 3x2 cells. `Review/module_footprints_native.png` shows these exact nearest-neighbor footprint scales; grid lines are QA overlays only.

## Reproduce using the requested PowerShell 7

Run this complete block from any working directory:

```powershell
& 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1118-904-0_26.1118.904.0_x64__3k8sg7r9htsxt/dependencies/native/powershell/pwsh.exe' -NoProfile -Command {
    Set-Location -LiteralPath 'D:/unity/DeadCircuitDemo'
    python -B 'Tools/ExpansionArt/ModularArt/export.py' --doctor --renderer 'C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py'
    if ($LASTEXITCODE -ne 0) { throw 'SVG export failed' }
    python -B 'Tools/ExpansionArt/ModularArt/verify.py'
    if ($LASTEXITCODE -ne 0) { throw 'Independent verification failed' }
}
```

`--doctor` is a once-per-environment optional conformance check. Ordinary reruns can omit it. Every export chooses a new `run_NNN` render/log directory and retains prior runs. It regenerates only this subtree's source/review/report files and the two runtime modules files. Existing atlases and all code remain untouched. Python needs the already-used NumPy/Pillow packages and one supported SVG backend; this export used `resvg_py` 0.5.0.

The renderer calls performed by the exporter are also directly reproducible. `manual_001` below must be a new directory (choose another name if it already exists):

```powershell
python -B 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/modular_svg.py'
python -B 'C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py' lint --svg 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/Sources/modules.svg' --profile pixel
python -B 'C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py' render --svg 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/Sources/modules.svg' --output-dir 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/Renders/manual_001' --crisp --zoom 4 --strict-qc
python -B 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/verify.py' --png 'D:/unity/DeadCircuitDemo/Tools/ExpansionArt/ModularArt/Renders/manual_001/base/modules.png'
```

Use `export.py` to publish, since it runs validation before copying the rendered PNG and matching JSON.

## Verification and limits

`verify.py` prints current counts, per-frame hashes/opaque areas and atlas/source hashes. `Reports/latest_verify.json` records the current 23-frame check. Root `codeart-meta.json` records the current source hash and maintenance render check; its historical renderer pointer and prior run folders retain the previous full export. The read-only checker validates:

- Exactly the 23 current keys; unique frame pixels and no collisions with the current icons/world keys, whose count may change independently.
- Integer canvas, viewBox, translations, rectangle geometry and source palette; a restrictive SVG element/attribute allowlist rejects bitmap, text, external resources, gradients, opacity and filters.
- PNG equality with an independently reconstructed image from parsed SVG rectangles, including zero opaque gutter/unused-cell pixels and binary alpha.
- All 16 exact E/N/W/S occupancy masks and 128 possible east/west or north/south matching wall-edge pairs (4096 RGBA pixels), plus 32 wall/door joins.
- Measured open-door continuous passage of at least 20px across all 32 rows (`x=6..25`), visual clearance against the supplied player diameter, identical jamb pixels in both states and a closed barrier filling the complete gap.
- Chamber/pump/engine footprint metadata.
- Centered pivots and 32 pixels per unit for every retained frame.
- Seven pre-existing atlas/source files retain their baseline SHA-256 hashes.
- Repeating SVG generation preserves source bytes.

The SVG renderer's own strict QC separately checks portable pixel lint, alpha and palette. No warnings or failures occurred in the accepted render. Static contacts were inspected at native and enlarged sizes. This proves source/raster contracts and visual tile composition only: Unity import, game rendering, movement, collision, rotation, door interaction and roof visibility remain integration checks for the main agent. No Unity launch, Play, executable, game build, Git staging or Git commit is performed.
