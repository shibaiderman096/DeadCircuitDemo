"""Rebuild the dedicated art that replaces borrowed industrial and gathering sprites.

Run python -B Tools/ArtCompletion/generate.py [--check] [--review PATH].
The default writes only the owned source SVG/catalog and runtime completion atlas.
--check is read-only and verifies those files against authoring code and resvg.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
import resvg_py

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
RUNTIME = PROJECT / 'Assets/DeadCircuit/Resources/Art/Expansion'
sys.path.insert(0, str(PROJECT / 'Tools/ExpansionArt/SvgBatch'))
from pixel_svg import RGB, PALETTE, build_svg
from items import all_items
from machines import all_machines
from nodes import all_nodes
from modules import all_modules
from radiation import all_radiation


def authored():
    return all_items() + all_machines() + all_nodes() + all_modules() + all_radiation()


def enum_values(source, name):
    body = re.search(r'public enum ' + name + r'\s*\{([^}]+)\}', source).group(1)
    result = {}
    current = 0
    for entry in body.split(','):
        parts = entry.strip().split('=')
        if len(parts) == 2:
            current = int(parts[1])
        result[current] = parts[0].strip()
        current += 1
    return result


def verify_coverage():
    state = (PROJECT / 'Assets/DeadCircuit/Scripts/Core/DemoState.cs').read_text(encoding='utf-8-sig')
    all_keys = {}
    frame_hashes = {}
    for path in sorted(RUNTIME.glob('*.json')):
        atlas = json.loads(path.read_text(encoding='utf-8-sig'))
        if 'sprites' not in atlas:
            continue
        with Image.open(path.with_suffix('.png')) as source:
            assert source.size == (atlas['width'], atlas['height']), path
            image = source.convert('RGBA')
            for cell in atlas['sprites']:
                key = cell['name']
                assert key not in all_keys, ('duplicate runtime key', key, path)
                x, y, w, h = (cell[k] for k in ('x', 'y', 'w', 'h'))
                assert 0 <= x < x + w <= image.width and 0 <= y < y + h <= image.height, key
                frame = image.crop((x, y, x + w, y + h))
                # Back-facing avatar face layers are intentionally transparent.
                intentional_empty = key in {f'avatar_face_{i}_1' for i in range(3)}
                assert frame.getbbox() is not None or intentional_empty, ('empty frame', key)
                all_keys[key] = path.stem
                frame_hashes[key] = hashlib.sha256(frame.tobytes()).hexdigest()
    contracts = {}
    for enum, prefix in (('ItemKind', 'item'), ('BuildKind', 'machine'), ('GearKind', 'gear'),
                         ('PartKind', 'part'), ('NodeKind', 'node')):
        entries = enum_values(state, enum)
        missing = [f'{prefix}_{i}' for i in entries if f'{prefix}_{i}' not in all_keys]
        assert not missing, (enum, 'missing direct art', missing)
        contracts[enum] = {'total': len(entries), 'direct': len(entries), 'missing': missing}
    # New completion art must not be a pixel-identical copy of any other runtime cell.
    for sprite in authored():
        assert [key for key, digest in frame_hashes.items()
                if digest == frame_hashes[sprite.name]] == [sprite.name], ('borrowed pixels', sprite.name)
    return contracts


def review(image, atlas, sprites, target):
    target = Path(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    cols, cw, ch = 10, 138, 172
    sheet = Image.new('RGB', (cols * cw, ((len(sprites) + cols - 1) // cols) * ch), '#192829')
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=10)
    for i, (entry, sprite) in enumerate(zip(atlas['sprites'], sprites)):
        x, y = i % cols * cw, i // cols * ch
        for yy in range(0, 128, 16):
            for xx in range(0, 128, 16):
                color = '#344a43' if (xx + yy) // 16 % 2 else '#253b3b'
                draw.rectangle((x + xx, y + yy, x + xx + 15, y + yy + 15), fill=color)
        frame = image.crop((entry['x'], entry['y'], entry['x'] + 32, entry['y'] + 32))
        sheet.paste(frame.resize((128, 128), Image.Resampling.NEAREST), (x, y), frame.resize((128, 128), Image.Resampling.NEAREST))
        draw.text((x, y + 130), sprite.name, fill='#80d8bc', font=font)
        parts = sprite.label.split(' / ')
        label = next((part for part in parts if part.isascii()), parts[0])
        draw.text((x, y + 142), label, fill='#ddd5ac', font=font)
        # Native size against a light background next to the large readable preview.
        draw.rectangle((x + 100, y + 133, x + 131, y + 164), fill='#ddd5ac')
        sheet.paste(frame, (x + 100, y + 133), frame)
    sheet.save(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--review', metavar='PNG', help='Optional contact sheet at 4x and native size')
    args = parser.parse_args()
    sprites = authored()
    assert len(sprites) == 117 and len({s.name for s in sprites}) == 117
    svg, atlas, catalog = build_svg('completion', sprites, cols=12)
    # Preserve each wearable's original skeleton anchor inside the padded 32px cell.
    for entry in atlas['sprites']:
        if entry['name'].startswith('avatar_armor_19_'):
            part = entry['name'].split('_')[3]
            entry.update(pixelsPerUnit=32, pivotX=.5,
                         pivotY=.3125 if part == 'head' else .5625 if part == 'shoe' else .6875)
    for entry, cell in zip(catalog, atlas['sprites']):
        if cell['name'].startswith('avatar_armor_19_'):
            entry['pivot'] = [cell['pivotX'], cell['pivotY']]
    raw = resvg_py.svg_to_bytes(svg_string=svg, skip_system_fonts=True)
    image = Image.open(io.BytesIO(raw)).convert('RGBA')
    actual = np.array(image)
    expected = np.zeros_like(actual)
    hashes = set()
    for entry, sprite in zip(atlas['sprites'], sprites):
        pixels = sprite.final()
        frame = np.zeros((32, 32, 4), dtype=np.uint8)
        visible = pixels >= 0
        frame[visible] = np.array(RGB, dtype=np.uint8)[pixels[visible]]
        assert visible.any(), sprite.name
        digest = hashlib.sha256(frame.tobytes()).hexdigest()
        assert digest not in hashes, ('duplicate completion pixels', sprite.name)
        hashes.add(digest)
        x, y = entry['x'], entry['y']
        expected[y:y + 32, x:x + 32] = frame
    assert np.array_equal(actual, expected), 'SVG output differs from authored palette/geometry'
    assert set(np.unique(actual[:, :, 3])) <= {0, 255}, 'partial alpha'
    meta = {
        'art_source': 'code', 'placeholder': False, 'disclosure': 'code-drawn, no image model',
        'canvas': [32, 32], 'camera': 'top-down with front faces', 'light': 'top left',
        'palette': PALETTE, 'pixelsPerUnit': 32, 'pivot': [.5, .5],
        'sprites': len(sprites), 'source_sha256': hashlib.sha256(svg.encode()).hexdigest(),
        'authors': ['items.py', 'machines.py', 'nodes.py', 'modules.py', 'radiation.py'],
    }
    files = {
        HERE / 'completion.svg': svg.encode(),
        HERE / 'completion.catalog.json': (json.dumps(catalog, ensure_ascii=False, indent=2) + '\n').encode(),
        HERE / 'codeart-meta.json': (json.dumps(meta, ensure_ascii=False, indent=2) + '\n').encode(),
        RUNTIME / 'completion.json': (json.dumps(atlas, ensure_ascii=False, indent=2) + '\n').encode(),
    }
    for path, data in files.items():
        if args.check:
            assert path.read_text(encoding='utf-8') == data.decode('utf-8'), ('out of sync', path)
        else:
            path.write_bytes(data)
    png_path = RUNTIME / 'completion.png'
    if args.check:
        with Image.open(png_path) as saved:
            assert np.array_equal(np.array(saved.convert('RGBA')), actual), 'runtime PNG out of sync'
    else:
        image.save(png_path, optimize=True)
    contracts = verify_coverage()
    if args.review:
        review(image, atlas, sprites, args.review)
    print(json.dumps({'status': 'pass', 'sprites': len(sprites), 'renderer': 'resvg_py',
                      'partialAlphaPixels': 0, 'duplicateFrames': 0, 'emptyFrames': 0,
                      'sourcePixelEquality': True, 'coverage': contracts}, ensure_ascii=False))


if __name__ == '__main__':
    main()
