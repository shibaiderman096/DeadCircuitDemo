"""Export the editable modular avatar SVG as a deterministic Unity atlas.

art_source=existing; SVG-authored/code-rendered; no image model.
Public entry point: generate(review_dir=None). JSON rectangles are top-left;
Unity pivots are normalized bottom-left. Runtime files are byte-compared before
writing, and existing .meta files are never overwritten.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.metadata
import io
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Tools/avatar-art'
OUT = ROOT / 'Assets/DeadCircuit/Resources/Art/Expansion'
SVG = '{http://www.w3.org/2000/svg}'
FIELDS = ('name', 'x', 'y', 'w', 'h', 'pixelsPerUnit', 'pivotX', 'pivotY')
PARTS = {'head': (16, 16, .5, .125), 'face': (16, 16, .5, .125),
         'hair': (16, 16, .5, .125), 'torso': (16, 16, .5, .875),
         'arm': (8, 16, .5, .875), 'sleeve': (8, 16, .5, .875),
         'leg': (8, 16, .5, .875), 'shoe': (8, 8, .5, .75)}
GEAR = {10: ('head',), 11: ('torso', 'arm'), 12: ('leg',), 13: ('shoe',),
        14: ('head',), 15: ('torso',), 16: ('head',), 17: ('torso',), 18: ('shoe',)}
EMPTY = {f'avatar_face_{s}_1' for s in range(3)}
EXPECTED = (tuple(f'avatar_head_{f}' for f in range(4)) +
            tuple(f'avatar_{part}_{s}_{f}' for part, count in (('face', 3), ('hair', 5), ('torso', 3))
                  for s in range(count) for f in range(4)) +
            tuple(f'avatar_arm_{f}' for f in range(4)) +
            tuple(f'avatar_sleeve_{s}_{f}' for s in range(3) for f in range(4)) +
            tuple(f'avatar_{part}_{f}' for part in ('leg', 'shoe') for f in range(4)) +
            tuple(f'avatar_armor_{g}_{part}_{f}' for g, parts in GEAR.items() for part in parts for f in range(4)))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def write_changed(path, data):
    if path.exists() and path.read_bytes() == data:
        return False
    path.write_bytes(data)
    return True


def initialize_meta(path, data):
    try:
        with path.open('xb') as stream:
            stream.write(data)
    except FileExistsError:
        pass


def json_bytes(data):
    return (json.dumps(data, indent=2) + '\n').encode('utf-8')


def png_bytes(image):
    stream = io.BytesIO()
    image.save(stream, format='PNG', optimize=False, compress_level=9)
    return stream.getvalue()


def read_sources():
    catalog = json.loads((SOURCE / 'avatar-parts.catalog.json').read_text(encoding='utf-8'))
    source = (SOURCE / 'avatar-parts.svg').read_text(encoding='utf-8')
    root = ET.fromstring(source)
    require((catalog['width'], catalog['height']) == (290, 128), 'Unexpected atlas dimensions')
    require(catalog['art_source'] == 'existing' and catalog['route'] == 'SVG-authored/code-rendered'
            and catalog['imageModelUsed'] is False, 'Existing-art provenance must remain explicit')
    require(root.tag == SVG + 'svg' and root.attrib == dict(width='290', height='128',
            viewBox='0 0 290 128', **{'shape-rendering': 'crispEdges'}), 'SVG native integer grid mismatch')
    require(all(e.tag in (SVG + 'title', SVG + 'desc', SVG + 'style', SVG + 'g') for e in root),
            'No embedded resources, gradients, filters, text or raster art')
    reference_colors = set()
    for ref in catalog['references']:
        data = (ROOT / ref['path']).read_bytes()
        require(sha256(data) == ref['sha256'], f'Existing reference drift: {ref["path"]}')
        reference_colors.update(re.findall(r'#[0-9a-f]{6}', data.decode('utf-8')))
    palette = catalog['palette']
    require(0 < len(palette) <= 24, 'Keep the shared industrial palette compact')
    require(set(palette.values()) - set(catalog['tintRamp']) <= reference_colors,
            'Only the neutral tint ramp may extend existing reference colors')
    require(all(re.fullmatch(r'#[0-9a-f]{6}', color) for color in palette.values()), 'Invalid palette hex')
    for color in catalog['tintRamp']:
        require(len(set(bytes.fromhex(color[1:]))) == 1, 'Tint ramp must be neutral white/gray')
    styles = root.findall(SVG + 'style')
    require(len(styles) == 1, 'One flat-fill palette stylesheet is required')
    pattern = r'[.]c-([a-z]+)[{]fill:(#[0-9a-f]{6})[}]'
    css = styles[0].text or ''
    require(dict(re.findall(pattern, css)) == palette and not re.sub(pattern, '', css).strip(),
            'Only explicit opaque palette fills are allowed')
    sprites, groups = catalog['sprites'], root.findall(SVG + 'g')
    names = [s['name'] for s in sprites]
    require(tuple(names) == EXPECTED and len(set(names)) == 112, 'Key/order/uniqueness contract mismatch')
    require([g.get('id') for g in groups] == names, 'SVG/catalog group order mismatch')
    rects, minimum_gap = [], 1000
    for i, (s, group) in enumerate(zip(sprites, groups)):
        x, y, w, h = (s[k] for k in ('x', 'y', 'w', 'h'))
        require(all(type(s[k]) is int for k in ('x', 'y', 'w', 'h', 'pixelsPerUnit')), 'Non-integer cell/PPU')
        require((w, h, s['pivotX'], s['pivotY']) == PARTS[s['part']] and s['pixelsPerUnit'] == 32,
                f'{s["name"]}: size/pivot/PPU mismatch')
        require((x, y) == (2 + i % 16 * 18, 2 + i // 16 * 18), 'Packed grid drift')
        require(x + w <= 290 and y + h <= 128 and min(x, y) >= 2, 'Atlas boundary escape')
        require(group.attrib == {'id': s['name'], 'transform': f'translate({x} {y})'}, 'Non-integer group transform')
        require(bool(s.get('allowEmpty')) == (s['name'] in EMPTY), 'Empty layers must be explicitly declared')
        colors = set()
        for e in group:
            if e.tag == SVG + 'title':
                require(not e.attrib and not list(e), 'Malformed title')
                continue
            require(e.tag == SVG + 'rect' and set(e.attrib) == {'class', 'x', 'y', 'width', 'height'},
                    'Artwork must use flat native-grid rectangles only')
            c = e.get('class', '').removeprefix('c-')
            require(c in palette and e.get('class') == 'c-' + c, 'Undefined color class')
            values = [e.get(k, '') for k in ('x', 'y', 'width', 'height')]
            require(all(re.fullmatch('[0-9]+', v) for v in values), 'Fractional SVG geometry')
            rx, ry, rw, rh = map(int, values)
            require(min(rx, ry, rw, rh) >= 1 and rx + rw <= w - 1 and ry + rh <= h - 1,
                    f'{s["name"]}: geometry violates 1px internal padding')
            colors.add(palette[c])
        if s['tint'] != 'none':
            require(colors <= set(catalog['tintRamp']) | {palette['ink']}, 'Colored pixel in tintable part')
        elif 'gear' in s:
            require(colors <= reference_colors, 'Armor must preserve original equipment colors')
        for ox, oy, ow, oh in rects:
            dx, dy = max(x - ox - ow, ox - x - w), max(y - oy - oh, oy - y - h)
            gap = max(dx, dy)
            require(gap >= 2, 'Sprite rectangles require >=2px gutter')
            minimum_gap = min(minimum_gap, gap)
        rects.append((x, y, w, h))
    for path in (ROOT / 'Assets/DeadCircuit/Resources/Art').rglob('*.json'):
        if path == OUT / 'avatar-parts.json':
            continue
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        other_names = {s.get('name') for s in data.get('sprites', [])} if isinstance(data, dict) else set()
        require(not (set(names) & other_names), f'Global sprite key collision: {path}')
    return catalog, source, minimum_gap


def world_bbox(s, bbox, anchor):
    px, py = s['w'] * s['pivotX'], s['h'] * s['pivotY']
    return [anchor[0] + bbox[0] - px, anchor[1] + s['h'] - bbox[3] - py,
            anchor[0] + bbox[2] - px, anchor[1] + s['h'] - bbox[1] - py]


def validate_pixels(atlas, catalog, minimum_gap):
    require(atlas.mode == 'RGBA' and atlas.size == (290, 128), 'PNG RGBA/dimension mismatch')
    require(sum(atlas.getchannel('A').histogram()[1:255]) == 0, 'Nonbinary alpha')
    all_colors = atlas.getcolors(atlas.width * atlas.height)
    allowed = {tuple(bytes.fromhex(c[1:])) for c in catalog['palette'].values()}
    require(all(c[:3] in allowed if c[3] else c[:3] == (0, 0, 0) for _, c in all_colors),
            'Off-palette pixels or nonzero RGB under transparent pixels')
    occupied = Image.new('L', atlas.size)
    frames, checks, by_name = {}, [], {s['name']: s for s in catalog['sprites']}
    exact = {'head': (2, 2, 14, 14), 'torso': (3, 2, 13, 14),
             'arm': (2, 2, 6, 14), 'leg': (2, 2, 7, 14), 'shoe': (1, 2, 7, 6)}
    for s in catalog['sprites']:
        name, x, y, w, h = (s[k] for k in ('name', 'x', 'y', 'w', 'h'))
        frame = atlas.crop((x, y, x + w, y + h))
        bbox = frame.getchannel('A').getbbox()
        require((bbox is None) == (name in EMPTY), f'{name}: unexpected empty/nonempty layer')
        if bbox:
            padding = min(bbox[0], bbox[1], w - bbox[2], h - bbox[3])
            require(padding >= 1, f'{name}: internal padding failure')
        else:
            padding = None
        if name.startswith(('avatar_head_', 'avatar_torso_', 'avatar_arm_', 'avatar_leg_', 'avatar_shoe_')):
            require(bbox == exact[s['part']], f'{name}: body footprint mismatch: {bbox}')
        if name.startswith('avatar_sleeve_') or name.startswith('avatar_armor_11_arm_'):
            require(frame.getchannel('A').crop((0, 11, 8, 16)).getbbox() is None, 'Sleeve covers the 3px hand')
            require(bbox == (2, 2, 6, 7 if s.get('style') == 0 else 11), 'Sleeve length mismatch')
        if name.startswith('avatar_face_'):
            require(frame.getchannel('A').histogram()[255] <= 24, 'Face overlay obscures too much skin')
        occupied.paste(255, (x, y, x + w, y + h))
        frames[name] = frame
        checks.append(dict(name=name, bbox=list(bbox) if bbox else None, padding=padding,
                           opaquePixels=frame.getchannel('A').histogram()[255],
                           sha256=sha256(frame.tobytes())))
    require(ImageChops.subtract(atlas.getchannel('A'), occupied).getbbox() is None, 'Gutter/exterior pixel spill')
    # Verify the specified pivots actually assemble without gaps at the neck/hip/feet.
    anchors = catalog['contract']['anchorsPx']
    anchor_checks = []
    for f in range(4):
        for part, anchor_key, bottom, top in (('head', 'neck', 24, 36), ('torso', 'torso', 12, 24),
                                             ('leg', 'legLeft', 0, 12), ('arm', 'armLeft', 11, 23),
                                             ('shoe', 'shoeLeft', 0, 4)):
            name = f'avatar_torso_0_{f}' if part == 'torso' else f'avatar_{part}_{f}'
            bounds = world_bbox(by_name[name], frames[name].getbbox(), anchors[anchor_key])
            require((bounds[1], bounds[3]) == (bottom, top), f'{name}: assembly anchor mismatch')
            anchor_checks.append(dict(name=name, worldBBox=bounds))
        for gear in (10, 14, 16):
            alpha = frames[f'avatar_armor_{gear}_head_{f}'].getchannel('A')
            eye_pixels = [(11, 7), (11, 8)] if f == 0 else [(4, 7), (4, 8)] if f == 2 else [(5, 7), (10, 7), (5, 8), (10, 8)] if f == 3 else []
            require(all(alpha.getpixel(p) == 0 for p in eye_pixels), 'Headgear obscures eyes')
    require(anchors['shoeLeft'][1] == anchors['legLeft'][1] - 8 and
            anchors['shoeRight'][1] == anchors['legRight'][1] - 8, 'Shoe attachment is not -8px')
    return frames, dict(spriteCount=112, declaredEmptySprites=sorted(EMPTY),
        nonemptySprites=109, partialAlphaPixels=0, offPalettePixels=0,
        transparentRgbLeaks=0, gutterSpillPixels=0, minimumGutter=minimum_gap,
        minimumInternalPadding=min(c['padding'] for c in checks if c['padding'] is not None),
        usedColorCount=len({c[:3] for _, c in all_colors if c[3]}),
        anchors=anchor_checks, frames=checks)


def texture_meta():
    guid = hashlib.md5(b'DeadCircuit/Art/Expansion/avatar-parts.png').hexdigest()
    return f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    alphaTestReferenceValue: 0.5
  isReadable: 0
  streamingMipmaps: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 100
  spriteMode: 0
  spritePixelsToUnits: 32
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  userData: art_source=existing; SVG-authored/code-rendered; modular avatar parts
  assetBundleName:
  assetBundleVariant:
'''.encode('utf-8')


LOOKS = [
    ('01 cropped / natural / tee', 0, 0, 0, '#e1b48c', '#554234', '#7c9f8d', '#57717a', '#a48358', ()),
    ('02 side-part / resolute', 1, 1, 1, '#bc825b', '#302c29', '#c89a58', '#526b66', '#7b674f', ()),
    ('03 spiked / smile', 2, 2, 2, '#e9c6a6', '#bc7046', '#a3584d', '#56667f', '#69766e', ()),
    ('04 neck-length / natural', 3, 0, 0, '#8c6047', '#392e2e', '#68a5a2', '#8c8064', '#536a68', ()),
    ('05 ponytail / smile', 4, 2, 1, '#c18c69', '#e1c58a', '#748251', '#4c6774', '#a88056', ()),
    ('06 bald / resolute / hood', 5, 1, 2, '#8e6247', '#493e34', '#aea77c', '#485a5b', '#87816b', ()),
    ('07 work: 10/11/12/13', 4, 2, 0, '#dfb796', '#42372f', '#668b85', '#6c7673', '#8e785b', (10, 11, 12, 13)),
    ('08 steel: 14/15/12/13', 1, 1, 1, '#bb8962', '#6a4c37', '#b8986c', '#587d7c', '#8a6b4b', (14, 15, 12, 13)),
    ('09 tactical: 16/17/12/18', 3, 0, 2, '#e1b895', '#46382b', '#778459', '#536a68', '#536a68', (16, 17, 12, 18)),
]


def tint(frame, color):
    rgb = ImageChops.multiply(frame.convert('RGB'), Image.new('RGB', frame.size, color))
    rgb.putalpha(frame.getchannel('A'))
    return rgb


def compose(frames, by_name, f, look, exploded=False, sample_idle=False):
    _, hair, face, top, skin, hair_color, top_color, pants, shoes, gear = look
    canvas = Image.new('RGBA', (80 if exploded else 40, 86 if exploded else 42))
    origin = (40, 64) if exploded else (20, 39)
    headgear = next((g for g in gear if g in (10, 14, 16)), None)
    head_anchor, torso_anchor = ((0, 42), (0, 24)) if exploded else ((0, 24), (0, 24))
    leg_anchors = ((-7, 8), (5, 8)) if exploded else ((-3, 12), (2, 12))
    arm_anchors = ((-20, 24), (20, 24)) if exploded else ((-7, 23), (7, 23))
    shoe_anchors = ((-10, -10), (8, -10)) if exploded else ((-3, 4), (2, 4))
    if sample_idle:
        side = f in (0, 2)
        arm_anchors = ((-1, 23), (1, 23)) if side else ((-7, 23), (7, 23))
        leg_anchors = ((-1, 12), (1, 12)) if side else ((-3, 12), (3, 12))
        shoe_anchors = tuple((x, y - 8) for x, y in leg_anchors)
    queue = []

    def put(name, anchor, color=None, order=0):
        s = by_name[name]
        frame = frames[name]
        if color:
            frame = tint(frame, color)
        x = origin[0] + anchor[0] - int(s['w'] * s['pivotX'])
        y = origin[1] - anchor[1] - int(s['h'] * (1 - s['pivotY']))
        queue.append((order if sample_idle else 0, frame, x, y))

    for anchor in leg_anchors:
        put(f'avatar_leg_{f}', anchor, pants, -3)
        if 12 in gear:
            put(f'avatar_armor_12_leg_{f}', anchor, order=-2)
    for anchor in shoe_anchors:
        put(f'avatar_shoe_{f}', anchor, shoes, -1)
        for g in (13, 18):
            if g in gear:
                put(f'avatar_armor_{g}_shoe_{f}', anchor)
    for limb, anchor in enumerate(arm_anchors):
        order = 4 if (limb == 1) == (f != 2) else -6
        put(f'avatar_arm_{f}', anchor, skin, order)
        put(f'avatar_sleeve_{top}_{f}', anchor, top_color, order + 1)
        if 11 in gear:
            put(f'avatar_armor_11_arm_{f}', anchor, order=order + 2)
    put(f'avatar_torso_{top}_{f}', torso_anchor, top_color, 1)
    for g in (11, 15, 17):
        if g in gear:
            put(f'avatar_armor_{g}_torso_{f}', torso_anchor, order=2)
    put(f'avatar_head_{f}', head_anchor, skin, 7)
    put(f'avatar_face_{face}_{f}', head_anchor, order=8)
    if hair != 5 and headgear is None:
        put(f'avatar_hair_{hair}_{f}', head_anchor, hair_color, 9)
    if headgear is not None:
        put(f'avatar_armor_{headgear}_head_{f}', head_anchor, order=10)
    for _, frame, x, y in sorted(queue, key=lambda entry: entry[0]):
        canvas.alpha_composite(frame, (x, y))
    return canvas


def review_images(directory, atlas, catalog, frames, report):
    directory.mkdir(parents=True, exist_ok=True)
    by_name = {s['name']: s for s in catalog['sprites']}
    font = ImageFont.load_default(size=11)

    def save(name, im):
        write_changed(directory / name, png_bytes(im))

    save('atlas-native.png', atlas)
    save('atlas-4x.png', atlas.resize((1160, 512), Image.Resampling.NEAREST))
    for bg_name, bg, fg in (('dark', '#111d20', '#eee9d3'), ('light', '#ddd5ac', '#192829')):
        for scale in (1, 4):
            composite = Image.new('RGBA', (atlas.width * scale, atlas.height * scale), bg)
            composite.alpha_composite(atlas.resize(composite.size, Image.Resampling.NEAREST))
            save(f'atlas-{scale}x-{bg_name}.png', composite)
            cell_w, cell_h = 176, 54 if scale == 1 else 108
            contact = Image.new('RGBA', (cell_w * 8, 30 + cell_h * 14), bg)
            draw = ImageDraw.Draw(contact)
            draw.text((8, 8), f'Independent parts | {scale}x nearest | 112 keys | empty face back = intentional', fill=fg, font=font)
            for i, s in enumerate(catalog['sprites']):
                x, y = i % 8 * cell_w, 30 + i // 8 * cell_h
                draw.text((x + 4, y + 3), s['name'], fill=fg, font=font)
                frame = frames[s['name']]
                frame = frame.resize((frame.width * scale, frame.height * scale), Image.Resampling.NEAREST)
                contact.alpha_composite(frame, (x + 6, y + 22))
                if s.get('allowEmpty'):
                    draw.text((x + 24, y + 25), 'EMPTY: BACK FACE', fill=fg, font=font)
            save(f'parts-{scale}x-{bg_name}.png', contact)
            col_w = 80 if scale == 1 else 176
            row_h = 64 if scale == 1 else 190
            gallery = Image.new('RGBA', (170 + col_w * 4, 34 + row_h * len(LOOKS)), bg)
            draw = ImageDraw.Draw(gallery)
            for f, label in enumerate(('f0 RIGHT', 'f1 BACK', 'f2 LEFT', 'f3 FRONT')):
                draw.text((170 + f * col_w + 8, 10), label, fill=fg, font=font)
            for row, look in enumerate(LOOKS):
                y = 34 + row * row_h
                draw.text((8, y + 8), look[0], fill=fg, font=font)
                draw.text((8, y + 25), 'skin / hair / top / pants / shoes', fill=fg, font=ImageFont.load_default(size=9))
                for f in range(4):
                    character = compose(frames, by_name, f, look)
                    if scale != 1:
                        character = character.resize((40 * scale, 42 * scale), Image.Resampling.NEAREST)
                    gallery.alpha_composite(character, (170 + f * col_w + 4, y + 2))
            save(f'assembled-{scale}x-{bg_name}.png', gallery)
    # Expose every seam and mark actual shared anchors at exactly 4x.
    diagram = Image.new('RGBA', (1040, 440), '#111d20')
    draw = ImageDraw.Draw(diagram)
    fg, accent = '#eee9d3', '#80d8bc'
    draw.text((16, 12), 'MODULAR BLOCK AVATAR | actual anchors / exploded parts | 4x nearest', fill=fg, font=font)
    assembled = compose(frames, by_name, 3, LOOKS[2]).resize((160, 168), Image.Resampling.NEAREST)
    diagram.alpha_composite(assembled, (80, 76))
    draw.text((80, 52), 'assembled: 36px body', fill=fg, font=font)
    ox, oy = 160, 232
    for label, ax, ay in (('neck (0,24)', 0, 24), ('hip (0,12)', 0, 12),
                          ('shoulder (-7,23)', -7, 23), ('shoulder (+7,23)', 7, 23),
                          ('shoe = leg - 8px', 2, 4)):
        x, y = ox + ax * 4, oy - ay * 4
        draw.line((x - 3, y, x + 3, y), fill=accent)
        draw.line((x, y - 3, x, y + 3), fill=accent)
        lx = 12 if ax < 0 else 242
        ly = y - 15 if ay == 24 else y + 3
        draw.line((x, y, lx + (95 if ax < 0 else -4), ly + 5), fill=accent)
        draw.text((lx, ly), label, fill=accent, font=font)
    exploded = compose(frames, by_name, 3, LOOKS[2], True).resize((320, 344), Image.Resampling.NEAREST)
    diagram.alpha_composite(exploded, (520, 56))
    for text, x, y in (('head + face + hair', 632, 64), ('independent arm + sleeve', 442, 175),
                       ('torso; hood down', 846, 215), ('two reused legs', 846, 294),
                       ('shoe layers', 846, 354)):
        draw.text((x, y), text, fill=fg, font=font)
    draw.text((20, 414), 'Pivots: head (8,2) | torso (8,14) | limbs (4,14) | shoes (4,6) | 32 PPU', fill=fg, font=font)
    save('rig-exploded-4x.png', diagram)
    write_changed(directory / 'validation.json', json_bytes(report))


def generate(review_dir=None):
    import resvg_py
    catalog, source, minimum_gap = read_sources()
    with Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_string=source))) as rendered:
        atlas = rendered.convert('RGBA')
    frames, checks = validate_pixels(atlas, catalog, minimum_gap)
    data = dict(width=atlas.width, height=atlas.height,
                sprites=[{k: s[k] for k in FIELDS} for s in catalog['sprites']])
    outputs = {'avatar-parts.png': png_bytes(atlas), 'avatar-parts.json': json_bytes(data)}
    OUT.mkdir(parents=True, exist_ok=True)
    changed = [name for name, value in outputs.items() if write_changed(OUT / name, value)]
    initialize_meta(OUT / 'avatar-parts.png.meta', texture_meta())
    guid = hashlib.md5(b'DeadCircuit/Art/Expansion/avatar-parts.json').hexdigest()
    initialize_meta(OUT / 'avatar-parts.json.meta', f'''fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
'''.encode('utf-8'))
    with Image.open(OUT / 'avatar-parts.png') as published:
        validate_pixels(published, catalog, minimum_gap)
    require(json.loads((OUT / 'avatar-parts.json').read_text(encoding='utf-8')) == data, 'JSON readback failed')
    report = dict(art_source='existing', route='SVG-authored/code-rendered', imageModelUsed=False,
                  renderer=f'resvg-py {importlib.metadata.version("resvg-py")}',
                  atlasSize=list(atlas.size), sourceSha256=sha256(source.encode('utf-8')),
                  files={name: sha256(value) for name, value in outputs.items()}, **checks)
    if review_dir is not None:
        review_images(Path(review_dir), atlas, catalog, frames, report)
    print(json.dumps({**{k: v for k, v in report.items() if k not in ('frames', 'anchors')}, 'changed': changed}))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review-dir', type=Path)
    args = parser.parse_args()
    generate(args.review_dir)
