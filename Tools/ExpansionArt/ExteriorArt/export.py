"""Export the canonical exterior SVG to its isolated runtime atlas.

Run from any directory with:
    python -B Tools/generate_art.py --exteriors-only

The SVG in Sources/ is authoritative and is never rewritten by this exporter.
Only exteriors.png/json and their Unity metas are published.
"""

from copy import deepcopy
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import re
import uuid
import xml.etree.ElementTree as ET

from PIL import Image, ImageColor
import resvg_py

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
RUNTIME = PROJECT / 'Assets/DeadCircuit/Resources/Art/Expansion'
SOURCE = ROOT / 'Sources/exteriors.svg'
PALETTE = json.loads((ROOT.parent / 'ModularArt/Sources/palette.json').read_text(encoding='utf-8'))['colors']
NS = 'http://www.w3.org/2000/svg'
SIZE, GUTTER, PITCH, COLS = 32, 2, 36, 4
NAMES = (
    'exterior_roof_tile', 'exterior_roof_metal', 'exterior_roof_flat', 'exterior_roof_glass',
    'exterior_facade_brick', 'exterior_facade_concrete', 'exterior_facade_industrial',
    'exterior_cornice', 'exterior_roof_vent', 'exterior_roof_hatch',
)
ET.register_namespace('', NS)


def check(condition, message):
    if not condition:
        raise ValueError(message)


def tag(local):
    return '{' + NS + '}' + local


def canvas(width, height, source_root=None):
    root = ET.Element(tag('svg'), {
        'width': str(width), 'height': str(height), 'viewBox': f'0 0 {width} {height}',
        'shape-rendering': 'crispEdges',
    })
    if source_root is not None:
        style = source_root.find(tag('style'))
        check(style is not None, 'Exterior SVG is missing its palette style')
        root.append(deepcopy(style))
    return root


def render(svg):
    return resvg_py.svg_to_bytes(svg_string=svg.decode('utf-8'),
                                 skip_system_fonts=True, shape_rendering='crisp_edges')


def decode(png):
    with Image.open(BytesIO(png)) as image:
        return image.convert('RGBA')


def read_layout(root):
    """Read runtime rectangles from the authored SVG's named, grid-aligned groups."""
    check(root.tag == tag('svg') and root.get('shape-rendering') == 'crispEdges',
          'Expected crisp-edge exterior SVG root')
    width, height = int(root.get('width', '0')), int(root.get('height', '0'))
    check((width, height) == (COLS * PITCH, 3 * PITCH), 'Unexpected exterior SVG dimensions')
    check(root.get('viewBox') == f'0 0 {width} {height}', 'Exterior SVG viewBox mismatch')
    groups = [child for child in root if child.tag == tag('g')]
    check([group.get('id') for group in groups] == list(NAMES),
          'SVG groups must match the ten runtime keys in manifest order')
    entries = []
    for i, group in enumerate(groups):
        match = re.fullmatch(r'translate\((\d+) (\d+)\)', group.get('transform', ''))
        check(match is not None, f'{NAMES[i]}: expected integer grid translation')
        x, y = map(int, match.groups())
        expected = ((i % COLS) * PITCH + GUTTER, (i // COLS) * PITCH + GUTTER)
        check((x, y) == expected, f'{NAMES[i]}: unexpected atlas position {(x, y)}')
        entries.append(dict(name=NAMES[i], x=x, y=y, w=SIZE, h=8 if NAMES[i] == 'exterior_cornice' else SIZE))
    return dict(width=width, height=height, sprites=entries), groups


def verify(root, png, layout, groups):
    """Check SVG-to-atlas identity, alpha, palette, gutters, and repeat edges."""
    atlas = decode(png)
    check(atlas.size == (layout['width'], layout['height']), 'PNG/manifest size mismatch')
    check([entry['name'] for entry in layout['sprites']] == list(NAMES), 'Expected exactly ten named frames')
    allowed = {ImageColor.getrgb(value) for value in PALETTE.values()}
    colors = atlas.getcolors(atlas.width * atlas.height)
    partial = sum(count for count, (_, _, _, a) in colors if 0 < a < 255)
    off_palette = sum(count for count, (r, g, b, a) in colors if a and (r, g, b) not in allowed)
    check(partial == off_palette == 0, 'Partial alpha or off-palette pixels')
    mask = Image.new('L', atlas.size, 0)
    details = []
    for entry, group in zip(layout['sprites'], groups):
        name, x, y, w, h = (entry[key] for key in ('name', 'x', 'y', 'w', 'h'))
        check(w == 32 and h == (8 if name == 'exterior_cornice' else 32), 'Unexpected frame size')
        check(mask.crop((x, y, x + w, y + h)).getbbox() is None, 'Overlapping frames')
        mask.paste(255, (x, y, x + w, y + h))
        frame = atlas.crop((x, y, x + w, y + h))
        isolated = canvas(w, h, root)
        isolated_group = deepcopy(group)
        isolated_group.set('transform', 'translate(0 0)')
        isolated.append(isolated_group)
        isolated_svg = ET.tostring(isolated, encoding='utf-8', xml_declaration=True)
        check(frame.tobytes() == decode(render(isolated_svg)).tobytes(),
              f'{name}: atlas crop differs from the SVG source')
        alpha = frame.getchannel('A')
        opaque = alpha.histogram()[255]
        if name not in NAMES[-2:]:
            check(opaque == w * h, f'{name}: must be fully opaque')
            check(frame.crop((0, 0, 1, h)).tobytes() == frame.crop((w - 1, 0, w, h)).tobytes(),
                  f'{name}: horizontal tile seam mismatch')
            if name != 'exterior_cornice':
                check(frame.crop((0, 0, w, 1)).tobytes() == frame.crop((0, h - 1, w, h)).tobytes(),
                      f'{name}: vertical tile seam mismatch')
        else:
            check(0 < opaque < w * h, f'{name}: prop must have a transparent background')
            left, top, right, bottom = frame.getbbox()
            check(min(left, top) >= 2 and right <= w - 2 and bottom <= h - 2,
                  f'{name}: unsafe transparent-prop margin')
        details.append(dict(name=name, size=[w, h], opaquePixels=opaque, bounds=frame.getbbox()))
    spill = sum(a != 0 and m == 0 for a, m in zip(atlas.getchannel('A').tobytes(), mask.tobytes()))
    check(spill == 0, 'Pixels outside manifest frames')
    return dict(status='pass', atlasSize=list(atlas.size), frames=len(details),
                partialAlphaPixels=partial, offPalettePixels=off_palette, gutterSpillPixels=spill, sprites=details)


def meta_bytes():
    """Use Expansion's importer template, deterministic unique GUIDs, and requested settings."""
    files = {}
    for suffix in ('png', 'json'):
        text = (RUNTIME / f'modules.{suffix}.meta').read_text(encoding='utf-8')
        guid = uuid.uuid5(uuid.NAMESPACE_URL, f'deadcircuit:art/expansion/exteriors.{suffix}').hex
        text, count = re.subn(r'^guid: [0-9a-f]{32}$', 'guid: ' + guid, text, flags=re.MULTILINE)
        check(count == 1, f'Expected one template GUID in modules.{suffix}.meta')
        if suffix == 'png':
            readable = text.count('  isReadable: 0')
            text = text.replace('  isReadable: 0', '  isReadable: 1')
            text, sprite_modes = re.subn(r'^  spriteMode: \d+$', '  spriteMode: 1', text, flags=re.MULTILINE)
            text, ppu = re.subn(r'^  spritePixelsToUnits: \d+$',
                                '  spritePixelsToUnits: 32', text, flags=re.MULTILINE)
            compression = text.count('    textureCompression: 1')
            text = text.replace('    textureCompression: 1', '    textureCompression: 0')
            check(readable == sprite_modes == ppu == 1 and compression >= 3,
                  'Failed to set texture importer values')
            check('    filterMode: 0\n' in text and '    enableMipMap: 0\n' in text,
                  'Texture importer must use point filtering without mipmaps')
            compressions = [line.rsplit(': ', 1)[1] for line in text.splitlines()
                            if line.startswith('    textureCompression:')]
            check(len(compressions) >= 4 and set(compressions) == {'0'},
                  'Every platform must disable texture compression')
            text = '\n'.join(line.rstrip() for line in text.splitlines()) + '\n'
        for existing in PROJECT.joinpath('Assets').rglob('*.meta'):
            if existing == RUNTIME / f'exteriors.{suffix}.meta':
                continue
            check(f'guid: {guid}' not in existing.read_text(encoding='utf-8'), f'Duplicate GUID: {guid}')
        files[RUNTIME / f'exteriors.{suffix}.meta'] = text.encode('utf-8')
    return files


def main():
    source = SOURCE.read_bytes()
    root = ET.fromstring(source)
    layout, groups = read_layout(root)
    png = render(source)
    report = verify(root, png, layout, groups)
    files = {RUNTIME / 'exteriors.png': png,
             RUNTIME / 'exteriors.json': (json.dumps(layout, indent=2) + '\n').encode('utf-8')}
    files.update(meta_bytes())
    for path, data in files.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.suffix == '.png':
            path.write_bytes(data)
        else:
            path.write_text(data.decode('utf-8'), encoding='utf-8')
    report.update(renderer='resvg_py ' + resvg_py.__version__,
                  pngSha256=sha256(png).hexdigest(), sourceSha256=sha256(source).hexdigest(),
                  pixelsPerUnit=32, pivot=[0.5, 0.5])
    print(json.dumps(report, separators=(',', ':')))


if __name__ == '__main__':
    main()
