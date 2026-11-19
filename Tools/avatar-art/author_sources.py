"""Editable integer-rectangle SVG builder; adapted existing art, no image model.
Run only when changing artwork; the exporter reads the resulting editable SVG.
All artwork coordinates are SVG top-left pixels, with integer flat fills.
"""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[1]
PALETTE = {
    'ink': '#192829', 'dark': '#253b3b', 'shadow': '#344a43',
    'steel': '#536a68', 'light': '#8ba39a', 'cream': '#ddd5ac',
    'soil': '#503e32', 'brown': '#76533b', 'tan': '#aa8054',
    'sand': '#c4ab75', 'gold': '#edc566', 'blue': '#497b86',
    'leaf': '#527445', 'olive': '#81824a', 'cyan': '#4c9e9b', 'teal': '#80d8bc',
    'white': '#ffffff', 'pale': '#dedede', 'gray': '#aeaeae', 'shade': '#777777',
}
FACES = ('natural', 'resolute', 'smile')
HAIR = ('cropped', 'side-part', 'spiked', 'neck-length', 'ponytail')
TOPS = ('short-sleeve', 'jacket', 'hoodie-down')
GEAR = {10: ('head',), 11: ('torso', 'arm'), 12: ('leg',), 13: ('shoe',),
        14: ('head',), 15: ('torso',), 16: ('head',), 17: ('torso',), 18: ('shoe',)}
PARTS = {'head': (16, 16, .5, .125), 'face': (16, 16, .5, .125),
         'hair': (16, 16, .5, .125), 'torso': (16, 16, .5, .875),
         'arm': (8, 16, .5, .875), 'sleeve': (8, 16, .5, .875),
         'leg': (8, 16, .5, .875), 'shoe': (8, 8, .5, .75)}


def rect(art, color, x, y, w, h):
    art.append((color, x, y, w, h))


def mirror(art, span):
    return [(c, span - x - w, y, w, h) for c, x, y, w, h in art]


def head(f):
    a = []
    for c, x, y, w, h in (('ink', 2, 2, 12, 12), ('pale', 3, 3, 10, 10),
                          ('white', 3, 3, 8, 2), ('gray', 11, 4, 2, 8),
                          ('gray', 3, 12, 10, 1)):
        rect(a, c, x, y, w, h)
    if f in (0, 2):
        rect(a, 'gray', 3, 5, 4, 7)
        rect(a, 'pale', 7, 5, 5, 7)
        rect(a, 'white', 8, 5, 3, 1)
        rect(a, 'shade', 5, 8, 1, 2)
        rect(a, 'pale', 4, 8, 1, 2)
    elif f == 1:
        rect(a, 'gray', 4, 10, 8, 2)
        rect(a, 'pale', 6, 11, 4, 2)
    return mirror(a, 16) if f == 2 else a


def face(s, f):
    a = []
    if f == 1:
        return a
    if f in (0, 2):
        rect(a, 'ink', 11, 7 if s != 1 else 8, 1, 2 if s == 0 else 1)
        rect(a, 'ink', 10, 6, 2 if s != 1 else 3, 1)
        rect(a, 'shade', 11, 11, 1 if s == 2 else 2, 1)
        if s == 1:
            rect(a, 'shade', 9, 12, 3, 1)
            rect(a, 'ink', 12, 10, 1, 1)
        if s == 2:
            rect(a, 'shade', 12, 10, 1, 1)
    else:
        for x in (5, 10):
            rect(a, 'ink', x, 7 if s != 1 else 8, 1, 2 if s == 0 else 1)
        for x in (4, 9 if s == 1 else 10):
            rect(a, 'ink', x, 6, 3 if s == 1 else 2, 1)
        rect(a, 'shade', 7, 11, 2, 1)
        if s == 1:
            rect(a, 'shade', 6, 12, 4, 1)
            rect(a, 'shade', 8, 9, 1, 1)
        if s == 2:
            for x, y in ((6, 10), (9, 10)):
                rect(a, 'shade', x, y, 1, 1)
    return mirror(a, 16) if f == 2 else a


def hair(s, f):
    a = []
    left, width = (1, 14) if s == 3 else (2, 12)
    rect(a, 'ink', left, 1 if s != 2 else 3, width, 4 if s != 2 else 2)
    rect(a, 'pale', left + 1, 2 if s != 2 else 3, width - 2, 2)
    rect(a, 'white', left + 1, 2 if s != 2 else 3, 5, 1)
    if s == 2:
        for x, h in ((3, 2), (7, 3), (11, 2)):
            rect(a, 'ink', x, 1, 2, h + 2)
            rect(a, 'white', x, 2, 1, h)
        rect(a, 'gray', 3, 4, 10, 1)
    if f == 1:
        depth = 13 if s == 3 else 9 if s == 4 else 8 if s == 1 else 7
        rect(a, 'ink', left, 4, width, depth - 3)
        rect(a, 'pale', left + 1, 4, width - 2, depth - 4)
        rect(a, 'gray', 10, 4, 3 if s != 3 else 4, depth - 4)
        if s in (1, 3):
            for x in (4, 8, 12):
                rect(a, 'gray', x, 5, 1, depth - 5)
        if s == 4:
            rect(a, 'ink', 9, 6, 5, 8)
            rect(a, 'gray', 10, 7, 3, 6)
            rect(a, 'pale', 10, 8, 1, 5)
            rect(a, 'white', 10, 7, 3, 1)
    elif f in (0, 2):
        rear_width = 4 if s in (0, 2) else 5
        rect(a, 'ink', left, 4, rear_width, 4 if s in (0, 2) else 9 if s == 3 else 6)
        rect(a, 'gray', left + 1, 4, rear_width - 2, 3 if s in (0, 2) else 8 if s == 3 else 5)
        if s == 1:
            rect(a, 'ink', 6, 4, 6, 2)
            rect(a, 'pale', 6, 4, 5, 1)
        elif s == 3:
            rect(a, 'pale', 2, 5, 1, 7)
            rect(a, 'ink', 6, 4, 5, 1)
        elif s == 4:
            rect(a, 'ink', 1, 7, 3, 7)
            rect(a, 'gray', 2, 8, 1, 5)
            rect(a, 'white', 1, 7, 3, 1)
    else:
        if s in (0, 2):
            rect(a, 'ink', 2, 4, 2, 3)
            rect(a, 'ink', 12, 4, 2, 3)
            rect(a, 'gray', 3, 4, 3, 1)
            rect(a, 'gray', 9, 4, 3, 1)
        elif s == 1:
            rect(a, 'ink', 2, 4, 3, 4)
            rect(a, 'gray', 3, 4, 1, 3)
            rect(a, 'ink', 5, 4, 6, 2)
            rect(a, 'pale', 5, 4, 5, 1)
            rect(a, 'ink', 5, 6, 2, 1)
        elif s == 3:
            for x in (1, 12):
                rect(a, 'ink', x, 4, 3, 10)
                rect(a, 'gray', x + 1, 4, 1, 9)
            rect(a, 'ink', 4, 4, 8, 1)
        elif s == 4:
            rect(a, 'ink', 2, 4, 2, 3)
            rect(a, 'ink', 12, 4, 2, 3)
            rect(a, 'ink', 12, 8, 3, 6)
            rect(a, 'gray', 13, 9, 1, 4)
            rect(a, 'white', 12, 8, 2, 1)
    return mirror(a, 16) if f == 2 else a


def torso(s, f, leather=False):
    a = []
    base, hi, lo = ('brown', 'tan', 'soil') if leather else ('pale', 'white', 'gray')
    rect(a, 'ink', 3, 2, 10, 12)
    rect(a, base, 4, 3, 8, 10)
    rect(a, hi, 4, 3, 2, 9)
    rect(a, lo, 10, 4, 2, 9)
    rect(a, lo, 4, 12, 8, 1)
    if f == 1:
        rect(a, lo, 4, 5, 8, 1)
        if s == 1:
            rect(a, lo, 7, 6, 1, 6)
            rect(a, hi, 5, 7, 2, 1)
        elif s == 2:
            rect(a, 'ink', 5, 2, 6, 5)
            rect(a, lo, 6, 3, 4, 3)
            rect(a, base, 6, 3, 3, 2)
            rect(a, hi, 7, 6, 2, 1)
    elif f in (0, 2):
        rect(a, lo, 4, 4, 3, 8)
        rect(a, hi, 8, 3, 3, 1)
        if s == 1:
            rect(a, 'soil' if leather else 'shade', 10, 4, 1, 8)
            rect(a, hi, 9, 7, 2, 1)
            rect(a, lo, 9, 8, 2, 2)
        elif s == 2:
            rect(a, 'ink', 4, 2, 3, 4)
            rect(a, lo, 5, 3, 2, 2)
            rect(a, hi, 10, 4, 1, 3)
            rect(a, lo, 8, 10, 3, 1)
    else:
        rect(a, 'ink', 6, 2, 4, 1)
        if s == 0:
            rect(a, lo, 6, 3, 4, 1)
        elif s == 1:
            rect(a, 'soil' if leather else 'shade', 8, 4, 1, 8)
            for x in (5, 9):
                rect(a, hi, x, 3, 2, 2)
                rect(a, lo, x, 8, 2, 2)
            rect(a, 'gold' if leather else 'white', 8, 7, 1, 1)
        else:
            rect(a, lo, 5, 3, 1, 3)
            rect(a, lo, 10, 3, 1, 3)
            rect(a, hi, 6, 4, 1, 3)
            rect(a, hi, 9, 4, 1, 3)
            rect(a, lo, 5, 10, 6, 1)
    return mirror(a, 16) if f == 2 else a


def arm(f, sleeve=None, leather=False):
    a = []
    height = 12 if sleeve is None else 5 if sleeve == 0 else 9
    base, hi, lo = ('brown', 'tan', 'soil') if leather else ('pale', 'white', 'gray')
    rect(a, 'ink', 2, 2, 4, height)
    rect(a, base, 3, 3, 2, height - 2)
    rect(a, hi, 3, 3, 1, max(1, height - 3))
    if f in (0, 2):
        rect(a, lo, 4, 4, 1, max(1, height - 3))
    elif f == 1:
        rect(a, lo, 3, 5, 2, 1)
    if sleeve is None:
        rect(a, base, 3, 11, 2, 2)
        rect(a, hi, 3, 11, 1, 1)
    elif sleeve == 2 or leather:
        rect(a, lo, 3, height, 2, 1)
    return mirror(a, 8) if f == 2 else a


def leg(f, work=False):
    a = []
    base, hi, lo = ('blue', 'steel', 'dark') if work else ('pale', 'white', 'gray')
    rect(a, 'ink', 2, 2, 5, 12)
    rect(a, base, 3, 3, 3, 10)
    rect(a, hi, 3, 3, 1, 9)
    rect(a, lo, 5, 4, 1, 9)
    rect(a, 'soil' if work else 'shade', 3, 2, 3, 1)
    if work:
        rect(a, 'sand', 4, 2, 1, 1)
        rect(a, 'dark', 3, 6, 3, 3)
        rect(a, 'steel', 3, 6, 3, 1)
        rect(a, 'light', 3, 6, 1, 1)
        rect(a, 'steel', 3, 10, 2, 2)
    elif f == 1:
        rect(a, 'gray', 3, 4, 3, 2)
    elif f in (0, 2):
        rect(a, 'shade', 5, 7, 1, 2)
    # Keep x=2..6 for every facing despite its asymmetric half-pixel center.
    return mirror(a, 9) if f == 2 else a


def shoe(f, gear=None):
    a = []
    base, hi, lo = (('brown', 'tan', 'soil') if gear == 13 else
                    ('shadow', 'steel', 'dark') if gear == 18 else
                    ('pale', 'white', 'gray'))
    rect(a, 'ink', 2, 2, 4, 4)
    rect(a, 'ink', 1, 3, 6, 3)
    rect(a, base, 2, 3, 4, 2)
    rect(a, hi, 2, 3, 2, 1)
    rect(a, lo, 5, 4, 1, 1)
    if f in (0, 2):
        rect(a, base, 5, 4, 1, 1)
        rect(a, hi, 5, 4, 1, 1)
    elif f == 1:
        rect(a, lo, 2, 4, 4, 1)
    if gear == 18:
        rect(a, 'light', 3, 3, 2, 1)
        rect(a, 'olive', 2, 4, 1, 1)
    elif gear == 13:
        rect(a, 'sand', 3, 4, 2, 1)
    return mirror(a, 8) if f == 2 else a


def helmet(gear, f):
    a = []
    base, hi, lo = {10: ('tan', 'sand', 'soil'), 14: ('steel', 'light', 'shadow'),
                    16: ('leaf', 'olive', 'shadow')}[gear]
    rect(a, 'ink', 1, 1, 14, 5)
    rect(a, base, 2, 2, 12, 3)
    rect(a, hi, 3, 2, 7, 1)
    rect(a, lo, 11, 3, 3, 2)
    rect(a, hi if gear == 10 else 'light', 2, 4, 12, 1)
    if f == 1:
        rect(a, 'ink', 1, 5, 14, 5 if gear == 16 else 3)
        rect(a, lo, 2, 5, 12, 4 if gear == 16 else 2)
        rect(a, base, 3, 5, 8, 2)
        if gear == 10:
            rect(a, 'ink', 10, 7, 3, 4)
            rect(a, 'sand', 11, 7, 1, 3)
        elif gear == 16:
            rect(a, 'dark', 3, 6, 10, 1)
            rect(a, 'steel', 7, 6, 2, 2)
    elif f in (0, 2):
        rect(a, 'ink', 1, 5, 4, 5 if gear == 16 else 3)
        rect(a, lo, 2, 5, 2, 4 if gear == 16 else 2)
        if gear == 10:
            rect(a, 'ink', 1, 7, 2, 4)
            rect(a, 'sand', 2, 7, 1, 3)
        elif gear == 16:
            rect(a, 'dark', 3, 6, 3, 3)
            rect(a, 'steel', 3, 6, 2, 2)
            rect(a, 'cyan', 12, 3, 2, 1)
    else:
        if gear in (10, 16):
            for x in (1, 13):
                rect(a, 'ink', x, 5, 2, 4 if gear == 16 else 2)
                rect(a, lo, x, 5, 1, 3 if gear == 16 else 1)
        if gear == 10:
            rect(a, 'cream', 4, 3, 6, 1)
        elif gear == 16:
            # Raised mount rather than a miniature icon: keep the eyes visible.
            rect(a, 'dark', 6, 1, 4, 3)
            rect(a, 'steel', 7, 2, 2, 1)
            rect(a, 'cyan', 12, 3, 2, 1)
    return mirror(a, 16) if f == 2 else a


def vest(gear, f):
    a = []
    tactical = gear == 17
    rect(a, 'ink', 3, 3, 10, 11)
    rect(a, 'shadow' if tactical else 'dark', 4, 4, 8, 9)
    for x in (3, 10):
        rect(a, 'ink', x, 2, 3, 3)
        rect(a, 'olive' if tactical else 'steel', x + 1, 2, 1, 3)
    if f in (0, 2):
        rect(a, 'dark', 4, 5, 3, 6)
        rect(a, 'steel', 8, 5, 3, 5)
        rect(a, 'light', 8, 5, 2, 1)
        rect(a, 'olive' if tactical else 'blue', 8, 7, 3, 1)
        rect(a, 'sand', 4, 10, 2, 1)
        if tactical:
            rect(a, 'leaf', 7, 10, 2, 3)
            rect(a, 'olive', 10, 10, 2, 3)
    else:
        rect(a, 'steel', 5, 5, 6, 5)
        rect(a, 'light', 5, 5, 5, 1)
        rect(a, 'olive' if tactical else 'blue', 6, 7, 4, 1)
        rect(a, 'dark', 4, 10, 8, 3)
        if f == 1:
            rect(a, 'steel', 5, 10, 6, 2)
            rect(a, 'olive' if tactical else 'blue', 6, 11, 4, 1)
        elif tactical:
            for x in (4, 7, 10):
                rect(a, 'leaf', x, 10, 2, 3)
                rect(a, 'olive', x, 10, 2, 1)
                rect(a, 'sand', x, 11, 1, 1)
        else:
            rect(a, 'steel', 5, 11, 6, 1)
            rect(a, 'sand', 4, 10, 1, 1)
            rect(a, 'sand', 11, 10, 1, 1)
    return mirror(a, 16) if f == 2 else a


def write_changed(path, content):
    data = content.encode('utf-8')
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def author():
    sprites, groups = [], []

    def add(name, part, f, art, **extra):
        w, h, px, py = PARTS[part]
        i = len(sprites)
        x, y = 2 + i % 16 * 18, 2 + i // 16 * 18
        s = dict(name=name, x=x, y=y, w=w, h=h, pixelsPerUnit=32,
                 pivotX=px, pivotY=py, part=part, facing=f, **extra)
        if part == 'face' and f == 1:
            s['allowEmpty'] = True
            s['emptyReason'] = 'Back view intentionally contains no face marks.'
        sprites.append(s)
        lines = [f'<g id="{name}" transform="translate({x} {y})">', f'<title>{name}</title>']
        for c, rx, ry, rw, rh in art:
            assert min(rx, ry) >= 1 and rx + rw <= w - 1 and ry + rh <= h - 1, name
            lines.append(f'<rect class="c-{c}" x="{rx}" y="{ry}" width="{rw}" height="{rh}"/>')
        lines.append('</g>')
        groups.append('\n'.join(lines))

    for f in range(4):
        add(f'avatar_head_{f}', 'head', f, head(f), tint='skin')
    for s, label in enumerate(FACES):
        for f in range(4):
            add(f'avatar_face_{s}_{f}', 'face', f, face(s, f), tint='none', style=s, label=label)
    for s, label in enumerate(HAIR):
        for f in range(4):
            add(f'avatar_hair_{s}_{f}', 'hair', f, hair(s, f), tint='hair', style=s, label=label)
    for s, label in enumerate(TOPS):
        for f in range(4):
            add(f'avatar_torso_{s}_{f}', 'torso', f, torso(s, f), tint='top', style=s, label=label)
    for f in range(4):
        add(f'avatar_arm_{f}', 'arm', f, arm(f), tint='skin')
    for s, label in enumerate(TOPS):
        for f in range(4):
            add(f'avatar_sleeve_{s}_{f}', 'sleeve', f, arm(f, s), tint='top', style=s, label=label)
    for f in range(4):
        add(f'avatar_leg_{f}', 'leg', f, leg(f), tint='pants')
    for f in range(4):
        add(f'avatar_shoe_{f}', 'shoe', f, shoe(f), tint='shoe')
    for gear, parts in GEAR.items():
        for part in parts:
            for f in range(4):
                art = (helmet(gear, f) if part == 'head' else
                       torso(1, f, True) if gear == 11 and part == 'torso' else
                       arm(f, 1, True) if part == 'arm' else
                       leg(f, True) if part == 'leg' else
                       shoe(f, gear) if part == 'shoe' else vest(gear, f))
                add(f'avatar_armor_{gear}_{part}_{f}', part, f, art, tint='none', gear=gear)
    refs = ['Tools/protagonist.svg', 'Tools/equipment-art/equipment.svg']
    catalog = dict(
        width=290, height=128, art_source='existing', route='SVG-authored/code-rendered',
        imageModelUsed=False,
        references=[dict(path=p, sha256=hashlib.sha256((ROOT / p).read_bytes()).hexdigest()) for p in refs],
        palette=PALETTE, tintRamp=['#ffffff', '#dedede', '#aeaeae', '#777777'],
        contract=dict(
            rectangleOrigin='top-left; normalized Unity pivots use bottom-left origin',
            facing={'0': 'right', '1': 'back', '2': 'left', '3': 'front'},
            pixelsPerUnit=32, minimumInternalPadding=1, minimumGutter=2,
            logicalPixel=1, sampling='Point/nearest; no mipmaps; no texture compression',
            baldStyle=5, baldHasSprite=False, reusableLimbs=True,
            hands='Last 3 SVG rows y=11..13; long sleeves end at y=11 boundary.',
            shoes='SVG top=2 and bottom boundary=6; soles land at world y=0.',
            helmetHidesHair=True, armorTint=False,
            pivots={k: [w * px, h * py] for k, (w, h, px, py) in PARTS.items()},
            anchorsPx=dict(neck=[0, 24], torso=[0, 24], hip=[0, 12],
                           armLeft=[-7, 23], armRight=[7, 23],
                           legLeft=[-3, 12], legRight=[2, 12],
                           shoeLeft=[-3, 4], shoeRight=[2, 4]),
            shoeOffsetFromLeg=[0, -8],
            layerOrder=['legs', 'pantsArmor', 'shoes', 'shoeArmor', 'arms', 'sleeves',
                        'jacketArmArmor', 'torso', 'torsoArmor', 'head', 'face',
                        'hairUnlessHeadgear', 'headArmor']),
        sprites=sprites)
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="290" height="128" '
           'viewBox="0 0 290 128" shape-rendering="crispEdges">\n'
           '<title>Dead Circuit modular block avatar parts</title>\n'
           '<desc>art_source=existing; adapted existing protagonist and equipment palette; '
           'SVG-authored/code-rendered; no image model; 112 independent four-facing parts; '
           'three intentionally empty back-facing face overlays.</desc>\n<style>' +
           ''.join(f'.c-{key}{{fill:{color}}}' for key, color in PALETTE.items()) +
           '</style>\n\n' + '\n\n'.join(groups) + '\n</svg>\n')
    write_changed(SOURCE / 'avatar-parts.svg', svg)
    write_changed(SOURCE / 'avatar-parts.catalog.json', json.dumps(catalog, indent=2) + '\n')
    print(json.dumps(dict(spriteCount=len(sprites), art_source='existing',
                         route='SVG-authored/code-rendered', imageModelUsed=False)))


if __name__ == '__main__':
    author()
