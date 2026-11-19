"""Independent offline verification; never writes or repairs runtime artwork."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
SOURCE = ROOT / 'Sources'
RUNTIME = PROJECT / 'Assets/DeadCircuit/Resources/Art/Expansion'
BUILDINGS = ('Residence Shop Warehouse Workshop Clinic Farmhouse GasStation PoliceStation '
             'School Apartment Greenhouse Substation Restaurant Pharmacy HardwareStore '
             'Supermarket Library FireStation Hospital ResearchLab PowerPlant WaterTreatment '
             'RadioStation Bunker LumberMill MineOffice TrainDepot Barn HuntingCabin '
             'RangerStation Motel RecyclingYard').split()
CHARS = 'bsthnmpkglruvodfwea'
EXPECTED_KEYS = ([f'landmark_sign_{i}' for i in range(32)]
                 + [f'landmark_floor_{i}' for i in range(6)]
                 + [f'landmark_prop_{c}' for c in CHARS])


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def protected_check():
    baseline = json.loads((ROOT / 'Reports/protected_baseline.json').read_text(encoding='utf-8-sig'))
    changed = [p for p, h in baseline.items() if not (PROJECT/p).is_file() or sha(PROJECT/p) != h]
    assert not changed, 'Protected existing art changed: ' + ', '.join(changed)
    return len(baseline)


def load_png(path):
    with Image.open(path) as im:
        assert im.mode == 'RGBA', (str(path), im.mode)
        return np.array(im)


def rgba(h):
    return tuple(int(h[i:i+2], 16) for i in (1, 3, 5)) + (255,)


def svg_truth(path, palette):
    """An independent integer rectangle oracle, solely for in-memory QA.

    No oracle pixels are saved as images. The production rasterizer is resvg.
    """
    raw = Path(path).read_text(encoding='utf-8')
    assert '<!DOCTYPE' not in raw and '<!ENTITY' not in raw
    root = ET.fromstring(raw)
    assert root.tag == '{http://www.w3.org/2000/svg}svg'
    w, h = int(root.attrib['width']), int(root.attrib['height'])
    assert root.attrib['viewBox'] == f'0 0 {w} {h}'
    assert root.attrib['shape-rendering'] == 'crispEdges'
    truth = np.zeros((h, w, 4), dtype=np.uint8)
    styles = ''.join(e.text or '' for e in root if e.tag.endswith('}style'))
    classes = dict(re.findall(r'\.c-([A-Za-z0-9_-]+)\{fill:(#[0-9a-fA-F]{6})\}', styles))
    assert classes == palette, (path, 'palette drift')
    count, ids = 0, set()
    for node in root.iter():
        tag = node.tag.split('}')[-1]
        assert tag in {'svg', 'style', 'g', 'rect'}, (path, tag)
        assert not any(k in node.attrib for k in ('transform', 'opacity', 'fill-opacity',
                                                  'stroke', 'style', 'href', 'filter', 'mask',
                                                  'clip-path', 'rx', 'ry')), (path, node.attrib)
        if 'id' in node.attrib:
            assert node.attrib['id'] not in ids, (path, 'duplicate id')
            ids.add(node.attrib['id'])
        if tag != 'rect':
            continue
        assert set(node.attrib) == {'x', 'y', 'width', 'height', 'class'}, (path, node.attrib)
        vals = [node.attrib[a] for a in ('x', 'y', 'width', 'height')]
        assert all(re.fullmatch(r'[0-9]+', v) for v in vals), (path, vals)
        x, y, rw, rh = map(int, vals)
        assert 0 <= x < x+rw <= w and 0 <= y < y+rh <= h, (path, node.attrib)
        color = node.attrib['class']
        assert color.startswith('c-') and color[2:] in palette
        truth[y:y+rh, x:x+rw] = rgba(palette[color[2:]])
        count += 1
    assert count > 0, path
    return truth, count


def verify(png=None, published=True):
    protected_count = protected_check()
    png = Path(png or RUNTIME / 'landmarks.png')
    layout_path = RUNTIME / 'landmarks.json' if published else SOURCE / 'landmarks.layout.json'
    layout = json.loads(layout_path.read_text(encoding='utf-8'))
    assert set(layout) == {'width', 'height', 'sprites'}
    assert (layout['width'], layout['height']) == (288, 288)
    entries = layout['sprites']
    assert [e['name'] for e in entries] == EXPECTED_KEYS, 'Missing, duplicate or reordered sprite keys'
    assert len(set(EXPECTED_KEYS)) == len(EXPECTED_KEYS) == 57
    manifest = json.loads((SOURCE / 'manifest.json').read_text(encoding='utf-8'))
    assert manifest['buildingOrder'] == BUILDINGS and manifest['propCharacterOrder'] == CHARS
    assert manifest['categories'] == dict(sign=32, floor=6, prop=19)
    assert [s['name'] for s in manifest['sprites']] == EXPECTED_KEYS
    assert [s['label'] for s in manifest['sprites'][:32]] == BUILDINGS
    palette = json.loads((SOURCE / 'palette.json').read_text(encoding='utf-8'))['colors']
    original_palette = json.loads((ROOT.parent / 'ModularArt/Sources/palette.json').read_text(encoding='utf-8'))['colors']
    assert palette == original_palette and len(palette) == 24
    atlas = load_png(png)
    assert atlas.shape == (288, 288, 4)
    allowed = {rgba(c) for c in palette.values()}
    partial = int(np.count_nonzero((atlas[:,:,3] != 0) & (atlas[:,:,3] != 255)))
    off = sum(int(n) for c, n in Counter(map(tuple, atlas.reshape(-1, 4))).items() if c[3] and c not in allowed)
    assert partial == off == 0, (partial, off)
    truth, rect_count = svg_truth(SOURCE / 'landmarks.svg', palette)
    mismatches = int(np.count_nonzero(np.any(truth != atlas, axis=2)))
    assert mismatches == 0, ('SVG versus PNG', mismatches)
    content_mask = np.zeros((288, 288), dtype=bool)
    frames, hashes, stats = {}, set(), []
    for i, e in enumerate(entries):
        assert set(e) == {'name', 'x', 'y', 'w', 'h'}
        assert e == dict(name=EXPECTED_KEYS[i], x=(i % 8)*36+2, y=(i//8)*36+2, w=32, h=32)
        x, y = e['x'], e['y']
        content_mask[y:y+32, x:x+32] = True
        pixels = atlas[y:y+32, x:x+32]
        source, count = svg_truth(SOURCE / (e['name']+'.svg'), palette)
        assert source.shape == (32, 32, 4) and np.array_equal(pixels, source), e['name']
        occupied = int(np.count_nonzero(pixels[:,:,3]))
        assert occupied > 0, e['name']
        if not e['name'].startswith('landmark_floor_'):
            assert occupied < 1024, (e['name'], 'opaque backdrop')
        digest = hashlib.sha256(pixels.tobytes()).hexdigest()
        assert digest not in hashes, ('duplicate tile', e['name'])
        hashes.add(digest)
        frames[e['name']] = pixels
        stats.append(dict(name=e['name'], opaquePixels=occupied, rectangles=count, pixelSHA256=digest))
    spill = int(np.count_nonzero(atlas[:,:,3][~content_mask]))
    assert spill == 0, ('gutter or unused-cell spill', spill)
    # Four transparent pixels separate adjacent 32px content boxes; every 36px
    # slot reserves two transparent pixels on each side, including atlas edges.
    gutter_pixels = int(np.count_nonzero(~content_mask))
    edge_checks = []
    for i in range(6):
        f = frames[f'landmark_floor_{i}']
        assert np.all(f[:,:,3] == 255), ('floor coverage', i)
        horizontal = int(np.count_nonzero(np.any(f[:,0] != f[:,-1], axis=1)))
        vertical = int(np.count_nonzero(np.any(f[0,:] != f[-1,:], axis=1)))
        assert horizontal == vertical == 0, ('floor seam', i, horizontal, vertical)
        edge_checks.append(dict(name=f'landmark_floor_{i}', leftRightMismatches=horizontal,
                                topBottomMismatches=vertical))
    review_truths = {}
    for stem in ('landmarks_review', 'floor_tiling_review'):
        t, _ = svg_truth(SOURCE / (stem+'.svg'), palette)
        review_truths[stem] = t
    # Prove every floor repeat and adjacency was placed without clipping or a seam.
    tiled = review_truths['floor_tiling_review']
    tiling_placements = 0
    for i in range(6):
        expected = np.tile(frames[f'landmark_floor_{i}'], (3, 3, 1))
        x, y = (i % 3)*104, (i//3)*104
        assert np.array_equal(tiled[y:y+96, x:x+96], expected), ('3x3 floor repeat', i)
        tiling_placements += 9
    meta_check = None
    if published:
        assert json.loads((SOURCE / 'landmarks.layout.json').read_text(encoding='utf-8')) == layout
        metas = [RUNTIME / 'landmarks.png.meta', RUNTIME / 'landmarks.json.meta']
        guids = [re.search(r'^guid: ([0-9a-f]{32})$', p.read_text(encoding='utf-8'), re.M).group(1) for p in metas]
        assert len(set(guids)) == 2
        old_guids = {re.search(r'^guid: ([0-9a-f]{32})$', (PROJECT/p).read_text(encoding='utf-8'), re.M).group(1)
                     for p in json.loads((ROOT / 'Reports/protected_baseline.json').read_text(encoding='utf-8-sig')) if p.endswith('.meta')}
        assert not set(guids) & old_guids, 'New GUID collides with existing asset'
        texture = metas[0].read_text(encoding='utf-8')
        for flag in ('enableMipMap: 0', 'filterMode: 0', 'nPOTScale: 0', 'alphaIsTransparency: 1', 'textureCompression: 0'):
            assert flag in texture, flag
        for stem, truth in review_truths.items():
            p = ROOT / 'Review' / (stem+'_1x.png')
            native, enlarged = load_png(p), load_png(ROOT / 'Review' / (stem+'_4x.png'))
            assert np.array_equal(native, truth), ('review SVG mismatch', stem)
            assert np.array_equal(enlarged, np.repeat(np.repeat(native, 4, axis=0), 4, axis=1)), ('review scale', stem)
        meta_check = dict(guids=guids, nearestFilter=True, mipmaps=False, compression=False)
    protected_check()
    return dict(status='pass', art_source='code', disclosure='code-drawn, no image model',
                spriteCount=57, categories=dict(sign=32, floor=6, prop=19), dimensions=[288,288],
                svgCount=60, rectCount=rect_count, paletteColors=24, partialAlphaPixels=partial,
                offPalettePixels=off, sourcePixelMismatches=mismatches, gutterSpillPixels=spill,
                transparentGutterAndEmptyPixels=gutter_pixels, uniqueSprites=len(hashes),
                floorSeamChecks=edge_checks, floorTilingPlacements=tiling_placements,
                protectedFiles=protected_count, protectedHashChanges=0, metadata=meta_check,
                pngSHA256=sha(png), svgSHA256=sha(SOURCE/'landmarks.svg'),
                layoutSHA256=sha(layout_path), spriteStats=stats, warnings=[], failures=[],
                limits='Offline SVG/raster/metadata verification only; no Unity import, Play, build or in-game validation.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--png', type=Path)
    p.add_argument('--staged', action='store_true')
    p.add_argument('--report', type=Path)
    args = p.parse_args()
    result = verify(args.png, not args.staged)
    if args.report:
        target = args.report.resolve()
        assert target.is_relative_to(ROOT), 'Verification reports must stay in LandmarkArt'
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in {'spriteStats', 'floorSeamChecks'}}, ensure_ascii=False))
