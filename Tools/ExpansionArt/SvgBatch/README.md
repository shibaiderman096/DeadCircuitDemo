# DeadCircuit expansion SVG batch — runtime handoff

Original **code-drawn, no image model** pixel art. No service request, external
asset download, authentication/model/key change, package installation, GUI,
Unity/game launch, build, Git operation, loader edit or original-art edit was
performed for this SVG batch. The existing character identity was referenced
from this project's own Tools/protagonist.svg and redrawn at 32 logical pixels.

## Runtime files (four new files only)

| Resources path | PNG size | Named frames |
|---|---:|---:|
| Art/Expansion/icons.png + icons.json | 576 × 216 | 90 |
| Art/Expansion/world.png + world.json | 576 × 360 | 151 |

The PNG and JSON basenames match exactly. JSON is precisely
{width,height,sprites:[{name,x,y,w,h}]}. Coordinates are **PNG top-left**.
Every content rectangle is 32 × 32, starts at (column*36+2,row*36+2) and
has two truly transparent pixels on every outer side. Empty trailing cells
are transparent and are **not** named assets. PPU 32 and centered pivot
(0.5,0.5) are the main loader's convention; no extra fields were inserted.
Use point sampling; UI shells are textures, not automatic 9-slice definitions.

## Coverage and fixed indices

* icons: food_0..30 (31), gear_0..18 (19), part_0..6 (7), item_0..24 (25).
  These follow FoodKind/GearKind/PartKind/ItemKind source enum order.
* UI extras: ui_panel, ui_button, ui_button_down, ui_slot, ui_slot_selected,
  ui_windowframe, ui_tooltip, ui_progress (8).
* world: crop_0_0..crop_7_1 (16; second index 0=seedling, 1=mature),
  creature_0..11 (CreatureKind), enemy_0..11 (EnemyCatalog variant order),
  site_0..11 (WorldGenerationCatalog.Sites entrances/front facades),
  biome_0..5 (ecology clusters), terrain_0..5 (opaque ground), deco_0..7 (8).
* machine_0..30 follows **DemoCatalog.BuildNames exactly**, independently
  checked against both its Chinese names and BuildKind enum. Waterwheel,
  shaft, crusher, boiler, steam systems, transformer coils, power poles,
  robotic assemblers, reactor dome and cooling tower use distinct structures.
* node_0..7: Tree, Ore, Scrap, Crate, Wall, CopperOre, TinOre, UraniumOre.
* survivor_0_0..survivor_3_3: 4 directions × 4 unique gait poses,
  0=east, 1=north, 2=west, 3=south. Rust cap/coat, steel armour,
  teal visor/chest core and radio backpack preserve the original project's
  identity. All 16 poses are empty-handed: held/slung guns are removed so
  the main renderer can overlay equipped weapons/tools. Originals are
  untouched. Each direction has four
  distinct boot masks; body bob is one pixel, with fixed frame center.
* 24 FX: **fx_rain, fx_snow, fx_dust, fx_fog, fx_muzzle, fx_spark,
  fx_flame, fx_ice, fx_soil, fx_poison, fx_wave**; plus fx_lightning,
  fx_ember, fx_fire_large, fx_frost_cloud, fx_ice_shard, fx_hit_flesh,
  fx_hit_metal, fx_hit_wood, fx_muzzle_shotgun, fx_muzzle_rifle,
  fx_muzzle_sniper, fx_explosion, fx_smoke.

Biome/terrain order: Meadow, DeciduousForest, ConiferousForest, Desert,
Wetland, Tundra. Ground is the only full-opaque/full-content exception.
Each ground repeats seamlessly **with itself**; no cross-biome transitions,
water/road/floor overlays or 47-tile autotile set are claimed.

Sources/*.catalog.json provides the complete English/Chinese label mapping.
Sources/*.layout.json mirrors the exact runtime rectangles; SVG groups
are named with runtime keys. All artwork uses integer rectangle runs and
literal palette CSS classes with crispEdges. No SVG text, embedded bitmap,
gradient, filter, partial opacity or external reference is used.

## Main visual review correction, round 1

The initial art-only enemy/site ordering was incorrect and is superseded.
Authoritative Core contracts are now read directly and asserted in export:

| ID | enemy_* / EnemyCatalog name | site_* / SiteKind and Sites name |
|---:|---|---|
| 0 | Walker / 普通丧尸 | Residence / 住宅 |
| 1 | Sprinter / 快跑丧尸 | Shop / 商店 |
| 2 | Brute / 暴徒丧尸 | Warehouse / 仓库 |
| 3 | Crawler / 爬行丧尸 | Workshop / 车间 |
| 4 | Spitter / 喷吐丧尸 | Clinic / 诊所 |
| 5 | Armored / 装甲丧尸 | Farmhouse / 农舍 |
| 6 | Burning / 燃烧丧尸 | GasStation / 加油站 |
| 7 | Screamer / 尖啸丧尸 | PoliceStation / 警局 |
| 8 | Hazmat / 防化感染者 | School / 学校 |
| 9 | Frost / 冻伤丧尸 | Apartment / 公寓 |
| 10 | Bomber / 自爆丧尸 | Greenhouse / 温室 |
| 11 | NightElite / 夜行精英 | Substation / 变电站 |

Screamer has an open jaw/exposed throat and cry marks; Bomber is a biped
infected carrying a strapped explosive bundle, not a drone. NightElite is
a hooded humanoid with teeth/claws and broken antler-like trophies.
All 12 sites are building facades/entrances with category-specific signage,
including storefront awning, loading shutter, medical cross, barn doors,
fuel canopy, police shield, school book, apartment balconies, greenhouse
glazing/plants and a fenced substation gate. Devices remain machine_*.

Only enemy_3..11 (9), site_0..11 (12), survivor_0_0..3_3 (16) change:
37 frames total. The other 114 world frames and all 90 icons are exactly
pixel-identical to the original final-render baseline (204 preserved).
Every runtime key/rectangle and both JSON files remain unchanged. The
runtime replacement is limited to world.png. Sources/world.svg and its
catalog, review contacts and current QA/handoff reports are regenerated.
Reports/Renders/final/world and spill_world_final are retained historical
evidence, not the active corrected world revision.

## Reproduction

Use the supplied full Python path and -B; all subprocesses time out at 120s.
export_and_qc.py sets child TEMP/TMP to this batch's Temp folder, disables
bytecode and captures command evidence under Reports. No logs go elsewhere.

For the current source, a full fresh export uses
`export_and_qc.py --render-id current --publish`. A read-only check of the
published SVG/PNG/JSON files uses `verify_runtime_atlases.py`; it starts no
renderer, Unity Editor or game and checks source rectangle pixel equality,
transparent gutters, unique frames and the actual Core enum ordinals.
The following command reproduces the retained historical correction pass:

~~~powershell
& 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1118-904-0_26.1118.904.0_x64__3k8sg7r9htsxt/dependencies/python/python.exe' -B Tools/ExpansionArt/SvgBatch/revise_visual_contracts.py --review-only --publish
~~~

The command verifies the existing visualreview1 world render and publishes
idempotently, without rasterizer/service retries. Remove --review-only only
for the first render into a new revision directory. The guarded script accepts
only the known original world PNG or this corrected PNG, checks all 37 changed
frames against the retained baseline and refuses unrelated external changes.
export_and_qc.py remains the general full-batch authoring exporter, with both
Core contract checks; its old final render is no longer source-current for world.

Actual route: authoring grids → editable SVG → codeart2d svg_render.py
--backend chrome --crisp --zoom 4 --strict-qc → runtime PNG. Headless
Chrome 157.0.8103.79 passed the existing doctor; resvg_py and resvg_js_cli were
unavailable, so no install or alternate forbidden rasterizer was attempted.
Pillow is used for review sheets only, never as the SVG rasterization route.

## Checks and evidence

* Reports/atlas_qa.json: 241 named, nonempty, unique-pixel frames, exact schema,
  dimensions, RGBA/binary alpha, literal palette, rectangles, bounds and gutters.
  Re-read rendered PNG pixels exactly match the integer SVG authoring grid.
* Reports/icons_lint.json and world_lint.json: pixel SVG lint PASS.
* Reports/Renders/final/icons/codeart-meta.json and
  Reports/Renders/visualreview1/world/codeart-meta.json: actual renderer/version, source
  and output hashes, native/4× dimensions and strict palette/alpha QA PASS.
* Reports/spill_icons_final/sheet-qc.json and spill_world_visualreview1/sheet-qc.json:
  cross_cell_components, cross_cell_visible, sheet_edge, empty_cells,
  boundary_band, safe_frame and faint_specks all PASS, no WARN/FAIL.
  Parameters: 36px cells, band 2, safe margin 2/36, min-area 1 because sparks,
  snow and small details intentionally use single-pixel components; no pixel
  was removed to obtain a pass. First 90/151 cells are expected, respectively.
* Six terrain edge pairs are pixel-equal. Each 3×3 assembled patch (9,216
  pixels) matches a separate global-coordinate evaluation of the periodic
  source material; 55,296 pixels compared in total, zero differences.
* Each survivor direction has four distinct complete frames and four distinct
  boot alpha masks. Old gun-barrel regions are transparent in all 16 poses;
  native/enlarged visual review separately checks empty hands. Motion feel
  and in-game timing remain untested.
* Reports/visual_revision1_qa.json and handoff_check.json: both actual Core
  source hashes, all 24 enemy/site index/name-prefix checks, 241 unique names
  and pixel frames across the two atlases, Python syntax, 37 changed frames,
  zero changed pixels outside those rectangles, and unchanged JSON/icons hashes.
* Only expected identical silhouette pairs remain: UI slot/selected slot and
  panel/windowframe, whose shared outlines are intentional UI states/shells.
  They have different pixels. All item/gear/world silhouette masks differ
  (opaque terrain masks excluded by definition).

Review/ contains native light/dark views and labeled 4× contact sheets for all
frames, plus machines_nodes, living_world, environment_fx, survivor gait and
terrain repetition sheets. The main agent must review visual suitability.
Self-review viewed both native and enlarged images, including the corrected
enemy/site contacts and all 16 unarmed gait poses. Main aesthetic approval of
this revision remains pending. Earlier authoring corrected ore/pickaxe
silhouettes and replaced an overly lattice-like tundra motif before publishing.
Early authoring assertions caught one mushroom clipping the frame and a helper
return omission; both were corrected before successful rendering/publication.

## Limits

This is flat, stylized code-drawn pixel art. Creatures/enemies/machines and FX
are single static poses; only the survivor has gait frames. No attack/death
animations, temporal FX sequence, full building rotation variants, terrain
transitions or in-game aesthetic/performance validation are supplied. Main
owns loading, scene/HUD hookup, import configuration, compile and Git review.
No audio was newly synthesized or altered by the SVG batch.
