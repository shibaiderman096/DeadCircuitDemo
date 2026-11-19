"""Deterministic pixel-art atlas builder for Dead Circuit.

The playable survivor's four-direction walk cycle is authored in Tools/protagonist.svg
and rasterized into Tools/protagonist-sheet.png for the atlas builder. Terrain and
environment props are authored in Tools/scene-art/*.svg; remaining art uses Pillow.

Run: python Tools/generate_art.py
Outputs only Assets/DeadCircuit/Resources/Art. Atlas rectangles use PNG top-left
coordinates; atlas.json also records a bottom-left normalized Sprite pivot.
"""
from __future__ import annotations

import hashlib
import io
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[1] / 'Assets/DeadCircuit/Resources/Art'
CELL, COLS = 64, 16
INK = '#192829'
DARK = '#253b3b'
STEEL = '#536a68'
LIGHT = '#8ba39a'
CREAM = '#ddd5ac'
RUST = '#9b5338'
ORANGE = '#e59a50'
TEAL = '#80d8bc'
SCENE_ART = Path(__file__).with_name('scene-art')


def canvas():
    im = Image.new('RGBA', (CELL, CELL))
    return im, ImageDraw.Draw(im)


def plate(d, box, fill=STEEL, edge=INK):
    x0, y0, x1, y1 = box
    d.rectangle(box, fill=fill, outline=edge, width=2)
    d.line((x0 + 2, y0 + 2, x1 - 2, y0 + 2), fill=LIGHT)
    d.line((x0 + 2, y0 + 3, x0 + 2, y1 - 3), fill=LIGHT)
    d.line((x0 + 3, y1 - 2, x1 - 2, y1 - 2), fill=DARK, width=2)


def rivets(d, box):
    x0, y0, x1, y1 = box
    for x, y in ((x0 + 4, y0 + 4), (x1 - 4, y0 + 4),
                 (x0 + 4, y1 - 4), (x1 - 4, y1 - 4)):
        d.rectangle((x, y, x + 1, y + 1), fill=CREAM)


def actor(zombie, direction, frame):
    im, d = canvas()
    step = (0, 2, 0, -2)[frame]
    bob = 1 if frame in (1, 3) else 0
    skin = ('#cec297', '#9aa578', '#c5ad81')[zombie]
    coat = ('#c49550', '#52634c', '#8a5142')[zombie]
    darkcoat = ('#775536', '#39493e', '#543b36')[zombie]
    pants = '#334849' if not zombie else '#3d4140'
    # Legs remain registered to the feet, alternating rather than sliding the body.
    for x, s in ((25, step), (34, -step)):
        d.rectangle((x, 40, x + 5, 50 + s), fill=INK)
        d.rectangle((x + 1, 41, x + 4, 47 + s), fill=pants)
        d.rectangle((x - 1, 50 + s, x + 6, 52 + s), fill=INK)
    d.rectangle((24, 29 + bob, 39, 42 + bob), fill=INK)
    d.rectangle((25, 30 + bob, 38, 40 + bob), fill=coat)
    d.rectangle((25, 37 + bob, 38, 40 + bob), fill=darkcoat)
    d.line((28, 31 + bob, 28, 38 + bob), fill=CREAM)
    d.rectangle((25, 39 + bob, 38, 41 + bob), fill=INK)
    d.rectangle((31, 39 + bob, 33, 40 + bob), fill=ORANGE)
    if direction == 1:  # north: backpack and rear of helmet
        plate(d, (25, 28 + bob, 37, 39 + bob), darkcoat)
        d.line((28, 31 + bob, 34, 31 + bob), fill=coat)
    d.rectangle((25, 16 + bob, 38, 29 + bob), fill=INK)
    d.rectangle((26, 18 + bob, 37, 27 + bob), fill=skin)
    d.rectangle((27, 16 + bob, 37, 20 + bob), fill='#424137' if not zombie else darkcoat)
    if not zombie:
        d.rectangle((24, 16 + bob, 38, 19 + bob), fill='#825545')
        d.line((25, 16 + bob, 36, 16 + bob), fill=ORANGE)
        d.rectangle((25, 23 + bob, 38, 24 + bob), fill='#a05e45')
    if direction != 1:
        eye = '#f39b64' if zombie else INK
        ex = 36 if direction == 0 else 27 if direction == 2 else 28
        d.rectangle((ex, 21 + bob, ex + 1, 22 + bob), fill=eye)
        if direction == 3:
            d.rectangle((34, 21 + bob, 35, 22 + bob), fill=eye)
    # A shouldered carbine for survivors; extended torn sleeves for infected.
    if direction in (0, 2):
        side = 1 if direction == 0 else -1
        x = 38 if side > 0 else 19
        d.rectangle((x, 31 + bob, x + 7, 35 + bob), fill=INK)
        d.rectangle((x + 1, 31 + bob, x + 5, 33 + bob), fill=coat)
        d.rectangle((x + 4, 33 + bob, x + 8, 35 + bob), fill=skin)
        if not zombie:
            gx = 38 if side > 0 else 11
            d.rectangle((gx, 30 + bob, gx + 15, 32 + bob), fill=INK)
            d.line((gx + 1, 30 + bob, gx + 13, 30 + bob), fill=LIGHT)
            d.rectangle((gx + 4, 32 + bob, gx + 7, 36 + bob), fill=RUST)
    else:
        for x in (20, 39):
            d.rectangle((x, 30 + bob, x + 4, 38 + bob), fill=INK)
            d.rectangle((x + 1, 31 + bob, x + 3, 35 + bob), fill=coat)
            d.rectangle((x + 1, 36 + bob, x + 3, 39 + bob), fill=skin)
        if not zombie:
            gy = 10 if direction == 1 else 34
            d.rectangle((38, gy + bob, 40, gy + 15 + bob), fill=INK)
            d.line((39, gy + bob, 39, gy + 10 + bob), fill=LIGHT)
    if zombie:
        d.rectangle((32, 32 + bob, 34, 36 + bob), fill='#773d36')
        d.rectangle((26, 42, 28, 44), fill=skin)
    return im


def tree(variant):
    im, d = canvas()
    d.polygon([(25, 56), (28, 39), (25, 30), (35, 29), (38, 56),
               (44, 59), (31, 57), (23, 59)], fill=INK)
    d.polygon([(29, 35), (34, 33), (35, 54), (38, 57), (29, 55)], fill='#69523c')
    d.line((31, 37, 31, 53), fill='#a18152', width=2)
    if variant == 2:
        d.line((31, 39, 17, 25), fill=INK, width=5)
        d.line((32, 30, 43, 18), fill=INK, width=5)
        d.line((18, 27, 14, 16), fill='#6f7150', width=3)
        d.line((41, 21, 49, 20), fill='#6f7150', width=3)
    rng = random.Random(120 + variant)
    clusters = [(30, 18, 17), (18, 30, 15), (43, 29, 15), (31, 36, 18), (32, 10, 9)]
    if variant == 2:
        clusters = [(17, 22, 9), (44, 17, 10), (30, 11, 11)]
    for x, y, r in clusters:
        d.polygon([(x-r, y-2), (x-r+3, y-r+5), (x-3, y-r),
                   (x+r-4, y-r+4), (x+r, y+5), (x+5, y+r-1), (x-r+5, y+r-3)],
                  fill=INK)
        d.ellipse((x-r+3, y-r+3, x+r-3, y+r-3), fill='#3b5940')
        d.ellipse((x-r+4, y-r+3, x+r-6, y+4), fill='#526e48')
        for _ in range(9):
            px, py = rng.randint(x-r+5, x+r-5), rng.randint(y-r+5, y+5)
            d.rectangle((px, py, px+2, py+1), fill='#7b8d54' if variant != 1 else '#89945d')
    return im


def node(kind, variant=0):
    if kind == 'tree':
        return tree(variant)
    im, d = canvas()
    rng = random.Random(44 + variant)
    if kind == 'ore':
        for x, y, r in ((23, 39, 13), (40, 41, 12), (31, 30, 13)):
            d.polygon([(x-r, y+7), (x-r+3, y-5), (x+2, y-r), (x+r, y),
                       (x+r-2, y+9), (x-5, y+11)], fill=INK)
            d.polygon([(x-r+3, y+5), (x-r+5, y-4), (x+2, y-r+3), (x+r-3, y),
                       (x+r-4, y+7), (x-5, y+8)], fill=STEEL)
            d.line((x-r+6, y-3, x+1, y-r+5, x+6, y-3), fill=LIGHT, width=2)
            d.line((x+2, y, x+5, y+5), fill='#c68358', width=3)
            d.point((x+3, y+1), fill=ORANGE)
    elif kind == 'scrap':
        d.polygon([(10, 49), (17, 29), (43, 27), (55, 49), (39, 55)], fill=INK)
        for _ in range(10):
            x, y = rng.randint(14, 42), rng.randint(30, 46)
            d.rectangle((x, y, x+8, y+5), fill=rng.choice((RUST, STEEL, DARK, '#78654d')), outline=INK)
            d.line((x+1, y, x+6, y), fill=LIGHT)
        d.ellipse((25, 34, 41, 50), fill=INK, outline=STEEL, width=3)
        d.line((14, 48, 43, 30), fill=RUST, width=3)
    elif kind == 'crate':
        plate(d, (14, 24, 49, 50), '#8e6d43')
        d.rectangle((14, 42, 49, 51), fill='#614c37', outline=INK)
        d.polygon([(14, 24), (23, 18), (54, 18), (49, 25)], fill='#b09054', outline=INK)
        d.polygon([(49, 25), (54, 18), (54, 44), (49, 51)], fill='#5a4935', outline=INK)
        for x in (20, 41):
            d.rectangle((x, 25, x+3, 49), fill=STEEL)
        d.rectangle((28, 29, 36, 37), fill=CREAM)
        d.line((30, 33, 34, 33), fill=RUST, width=2)
    else:  # ruined concrete wall: broken silhouette and exposed rebar
        d.line((19, 15, 19, 37), fill=RUST, width=2)
        d.line((41, 12, 42, 35), fill=RUST, width=2)
        d.polygon([(8, 52), (8, 29), (18, 25), (24, 32), (34, 23),
                   (43, 28), (49, 23), (56, 30), (56, 52)], fill=INK)
        d.polygon([(10, 48), (10, 30), (18, 28), (24, 35), (34, 27),
                   (44, 32), (50, 27), (53, 31), (53, 48)], fill='#737467')
        d.rectangle((10, 43, 53, 49), fill='#52594f')
        d.line((12, 36, 25, 36, 28, 40, 41, 40), fill=INK)
        d.line((34, 29, 31, 35, 35, 42), fill=INK)
        d.line((13, 30, 17, 29), fill=CREAM, width=2)
    return im


def machine(kind, rotation):
    im, d = canvas()
    # Engine-facing convention is E,N,W,S. Belts and wall have truly rotated art;
    # other machines keep their readable top-down body with a directional output port.
    if kind == 'belt':
        plate(d, (9, 17, 54, 47), '#5c6659')
        d.rectangle((10, 22, 53, 42), fill=INK)
        for x in range(13, 53, 7):
            d.line((x, 24, x+2, 32, x, 40), fill='#6e7666', width=2)
        for x in (12, 50):
            for y in (20, 44):
                d.rectangle((x, y, x+2, y+1), fill=ORANGE)
        return im.rotate(rotation * 90, resample=Image.Resampling.NEAREST)
    if kind == 'wall':
        for x in (9, 49):
            plate(d, (x, 14, x+6, 51), '#7e7660')
        plate(d, (15, 20, 48, 47), '#8b7353')
        for x in range(18, 48, 7):
            d.line((x, 23, x, 43), fill='#4e4b3a', width=2)
        d.rectangle((16, 23, 47, 25), fill=STEEL)
        d.rectangle((16, 40, 47, 42), fill=STEEL)
        rivets(d, (13, 19, 51, 48))
        return im.rotate((rotation % 2) * 90, resample=Image.Resampling.NEAREST)
    plate(d, (8, 19, 55, 51), DARK)
    d.rectangle((10, 45, 53, 50), fill=INK)
    for x in (12, 47):
        d.rectangle((x, 49, x+4, 53), fill=INK)
    if kind == 'generator':
        plate(d, (13, 17, 47, 43), '#566a5f')
        d.rounded_rectangle((16, 11, 37, 30), radius=3, fill=RUST, outline=INK, width=2)
        d.line((20, 13, 32, 13), fill=ORANGE, width=2)
        for y in range(20, 37, 4):
            d.line((38, y, 43, y), fill=INK, width=2)
        d.rectangle((45, 8, 50, 27), fill=INK)
        d.rectangle((46, 9, 48, 25), fill=STEEL)
        d.rectangle((17, 34, 28, 40), fill=INK)
        d.line((20, 36, 23, 38, 25, 35), fill=TEAL)
    elif kind == 'miner':
        for x in (12, 44):
            plate(d, (x, 12, x+7, 46), RUST)
        plate(d, (17, 10, 47, 19), '#c3a260')
        d.rectangle((29, 18, 35, 38), fill=INK)
        for y in range(20, 38, 4):
            d.line((29, y, 35, y-2), fill=LIGHT, width=2)
        d.polygon([(25, 38), (39, 38), (32, 45)], fill=LIGHT, outline=INK)
        plate(d, (10, 36, 21, 47), '#88784e')
    elif kind == 'smelter':
        plate(d, (15, 17, 47, 45), '#92553c')
        d.ellipse((15, 9, 47, 24), fill=INK)
        d.ellipse((19, 12, 43, 22), fill='#c7753e')
        d.ellipse((23, 14, 39, 20), fill='#ffd179')
        d.rectangle((22, 29, 41, 41), fill=INK)
        d.rectangle((25, 31, 38, 38), fill='#c7763f')
        for x in (27, 32, 37):
            d.line((x, 30, x, 40), fill=INK, width=2)
        plate(d, (45, 4, 51, 25), STEEL)
    elif kind == 'assembler':
        plate(d, (12, 19, 51, 45), '#496b68')
        d.rectangle((17, 24, 46, 37), fill=INK)
        d.line((17, 28, 46, 28), fill=STEEL, width=2)
        d.ellipse((27, 27, 38, 37), fill=RUST, outline=ORANGE)
        plate(d, (14, 10, 22, 28), STEEL)
        d.line((20, 13, 35, 16, 39, 27), fill=INK, width=7)
        d.line((20, 12, 35, 15, 39, 25), fill=LIGHT, width=3)
        d.ellipse((32, 12, 37, 17), fill=ORANGE, outline=INK)
        d.rectangle((43, 12, 49, 19), fill=INK)
        d.rectangle((44, 13, 47, 17), fill=TEAL)
    elif kind == 'turret':
        d.ellipse((12, 19, 52, 49), fill=INK)
        d.ellipse((16, 22, 48, 45), fill=STEEL)
        d.ellipse((23, 26, 42, 42), fill=DARK, outline=LIGHT, width=2)
        d.ellipse((28, 28, 37, 37), fill=RUST)
        # The separate turret_barrel sprite tracks enemies at runtime.
        for x, y in ((18, 34), (31, 44), (46, 34)):
            d.rectangle((x, y, x+3, y+2), fill=ORANGE)
    else:  # chest
        plate(d, (12, 19, 51, 46), '#87653f')
        plate(d, (12, 17, 51, 31), '#a3854a')
        for x in (19, 41):
            d.rectangle((x, 19, x+3, 45), fill=STEEL)
        d.rectangle((28, 28, 35, 36), fill=INK)
        d.rectangle((30, 29, 33, 33), fill=ORANGE)
    rivets(d, (8, 20, 55, 51))
    if kind != 'turret':
        # Amber port at the exact edge matching the logical output coordinate.
        dx, dy = ((1, 0), (0, -1), (-1, 0), (0, 1))[rotation]
        px, py = 32 + dx * 23, 34 + dy * 20
        d.rectangle((px-3, py-3, px+3, py+3), fill=INK)
        d.rectangle((px-2, py-1, px+2, py+1), fill=ORANGE)
    return im


def decoration(kind):
    im, d = canvas()
    if kind == 'car':
        plate(d, (10, 18, 53, 45), '#8f5740')
        d.rectangle((19, 12, 43, 49), fill=INK)
        plate(d, (21, 15, 42, 46), '#9b6147')
        d.polygon([(24, 20), (39, 20), (41, 26), (22, 26)], fill='#4b7976', outline=INK)
        d.line((29, 20, 32, 26, 34, 23), fill=LIGHT)
        d.rectangle((25, 35, 39, 41), fill=DARK)
        for x in (10, 49):
            for y in (20, 39):
                d.rectangle((x, y, x+5, y+7), fill=INK)
        d.line((23, 30, 38, 32), fill=ORANGE)
    elif kind == 'tuft':
        for x, y in ((23, 38), (31, 43), (39, 35), (28, 30)):
            d.line((x, y+9, x-3, y, x, y+6, x+3, y-2), fill='#68794b', width=2)
            d.point((x+3, y-2), fill='#b2a26b')
    elif kind == 'rubble':
        for x, y, s in ((18, 35, 7), (34, 41, 10), (38, 28, 6), (22, 46, 5)):
            d.polygon([(x-s, y+2), (x-s+2, y-s), (x+3, y-s+1), (x+s, y+4)], fill=INK)
            d.polygon([(x-s+2, y), (x-s+4, y-s+2), (x+2, y-s+1), (x+s-2, y+1)], fill='#777965')
    elif kind == 'barrel':
        d.rectangle((24, 24, 40, 48), fill=INK)
        d.rectangle((26, 24, 38, 47), fill=RUST)
        d.ellipse((24, 19, 40, 29), fill=ORANGE, outline=INK, width=2)
        for y in (31, 42):
            d.line((25, y, 39, y), fill=STEEL, width=2)
        d.rectangle((29, 34, 35, 38), fill=CREAM)
    elif kind == 'decals':
        d.polygon([(7, 46), (14, 43), (22, 45), (29, 42), (38, 45),
                   (45, 40), (55, 43), (51, 48), (41, 49), (33, 52),
                   (21, 50), (13, 53), (7, 51)], fill=DARK)
        d.rectangle((12, 46, 20, 48), fill=STEEL)
        d.rectangle((28, 44, 37, 46), fill='#685841')
        d.rectangle((46, 42, 52, 44), fill=RUST)
        d.rectangle((19, 51, 24, 53), fill=CREAM)
        d.rectangle((39, 49, 43, 51), fill=ORANGE)
    elif kind == 'shadow':
        d.ellipse((9, 23, 55, 43), fill=(7, 18, 19, 100))
        d.ellipse((16, 27, 48, 39), fill=(7, 18, 19, 65))
    elif kind == 'arrow':
        d.polygon([(20, 28), (34, 28), (34, 22), (45, 32),
                   (34, 42), (34, 36), (20, 36)], fill=CREAM, outline=INK, width=2)
    elif kind == 'rotor':
        d.ellipse((23, 23, 41, 41), fill=INK)
        for a in range(0, 360, 90):
            x, y = 32 + math.cos(math.radians(a))*12, 32 + math.sin(math.radians(a))*12
            d.line((32, 32, x, y), fill=LIGHT, width=4)
        d.ellipse((29, 29, 35, 35), fill=RUST, outline=INK)
    elif kind == 'turret_barrel':
        plate(d, (26, 27, 52, 36), DARK)
        d.rectangle((45, 27, 56, 29), fill=INK)
        d.rectangle((45, 33, 56, 35), fill=INK)
        d.line((29, 28, 48, 28), fill=LIGHT)
        d.rectangle((28, 29, 36, 34), fill='#68786d')
    elif kind == 'glow':
        # Smooth illumination is intentional; all object silhouettes remain pixel sharp.
        for y in range(CELL):
            for x in range(CELL):
                r = math.hypot(x-31.5, y-31.5)/31.5
                im.putpixel((x, y), (255, 242, 187, int(max(0, 1-r)**2*110)))
    elif kind == 'ring':
        d.ellipse((3, 3, 60, 60), outline=CREAM, width=2)
        for x, y in ((0, 31), (59, 31), (31, 0), (31, 59)):
            d.rectangle((x, y, x+3, y+3), fill=CREAM)
    elif kind == 'hit':
        d.polygon([(32, 9), (35, 26), (48, 17), (40, 30), (57, 33),
                   (39, 36), (46, 50), (33, 40), (24, 54), (27, 38),
                   (10, 34), (26, 29), (18, 17), (30, 25)], fill=CREAM)
    elif kind == 'blood':
        for x, y, r in ((29, 32, 8), (39, 26, 4), (20, 40, 4), (42, 40, 3), (19, 24, 2)):
            d.ellipse((x-r, y-r, x+r, y+r), fill=(123, 54, 42, 160))
    elif kind == 'pixel':
        d.rectangle((0, 0, 63, 63), fill='white')
    return im


def item(index):
    im, d = canvas()
    if index == 0:
        d.polygon([(20, 37), (36, 21), (43, 28), (27, 44)], fill=INK)
        d.line((23, 37, 37, 24), fill='#a77a4d', width=7)
        d.ellipse((20, 34, 28, 43), fill=ORANGE, outline=INK)
    elif index == 1:
        d.polygon([(19, 39), (23, 25), (35, 21), (44, 35), (38, 43)], fill=INK)
        d.polygon([(22, 37), (26, 27), (35, 24), (40, 34), (36, 39)], fill=STEEL)
        d.line((27, 29, 34, 27, 37, 33), fill=ORANGE, width=2)
    elif index == 2:
        d.polygon([(19, 33), (25, 25), (44, 25), (39, 39), (19, 39)], fill=INK)
        d.polygon([(22, 32), (26, 27), (41, 27), (37, 33)], fill=LIGHT)
        d.rectangle((22, 34, 37, 37), fill=STEEL)
    elif index == 3:
        plate(d, (21, 24, 41, 41), RUST)
        d.line((25, 29, 37, 36), fill=LIGHT, width=2)
    elif index == 4:
        plate(d, (23, 21, 40, 43), '#b2974f')
        d.rectangle((26, 17, 34, 23), fill=INK)
        d.polygon([(32, 26), (28, 33), (32, 39), (36, 33)], fill=RUST)
    elif index == 5:
        for x in (22, 29, 36):
            d.rectangle((x, 28, x+4, 41), fill=INK)
            d.rectangle((x+1, 29, x+3, 39), fill=ORANGE)
            d.polygon([(x, 27), (x+2, 21), (x+4, 27)], fill=CREAM)
    else:
        d.ellipse((23, 20, 42, 29), fill=INK)
        d.rectangle((23, 24, 42, 40), fill=INK)
        d.rectangle((25, 25, 40, 39), fill='#8b934f')
        d.rectangle((25, 29, 40, 35), fill=CREAM)
        d.ellipse((26, 21, 39, 26), fill=LIGHT)
    return im


def terrain():
    im = Image.new('RGBA', (256, 256))
    bases = ((64, 81, 57), (110, 96, 65), (35, 72, 75), (66, 72, 68), (95, 99, 84))
    for kind, base in enumerate(bases):
        for variant in range(8):
            rng = random.Random(903 + kind*19 + variant)
            tile = Image.new('RGBA', (32, 32), base + (255,))
            d = ImageDraw.Draw(tile)
            for _ in range(90):
                x, y, shift = rng.randrange(32), rng.randrange(32), rng.choice((-10, -6, 6, 10))
                c = tuple(max(0, min(255, v+shift)) for v in base)
                d.line((x, y, min(x+rng.randrange(1, 3), 31), y), fill=c)
            if kind == 0:
                for _ in range(9):
                    x, y = rng.randrange(2, 29), rng.randrange(3, 30)
                    d.line((x, y, x-1, y-2, x, y-1, x+2, y-3), fill='#6d7d4e')
            elif kind == 1:
                for _ in range(5):
                    x, y = rng.randrange(28), rng.randrange(28)
                    d.line((x, y, x+2, y), fill='#a19368')
            elif kind == 2:
                for _ in range(7):
                    x, y = rng.randrange(26), rng.randrange(32)
                    d.line((x, y, x+5, y), fill='#406e6c')
                    d.point((x+1, y+1), fill='#1d4347')
            elif kind == 3:
                if variant in (1, 3, 5):
                    d.line((4, 5, 11, 9, 9, 14, 18, 20, 17, 26, 24, 31), fill='#313d37')
                if variant == 7:
                    d.rectangle((0, 13, 12, 15), fill='#bab080')
                    d.rectangle((22, 13, 31, 15), fill='#bab080')
            else:
                d.line((0, 0, 31, 0), fill='#afb09a')
                d.line((0, 1, 0, 31), fill='#afb09a')
                d.line((0, 31, 31, 31), fill='#414e45')
                for x, y in ((3, 3), (28, 3), (3, 28), (28, 28)):
                    d.point((x, y), fill='#39483f')
            im.alpha_composite(tile, (variant*32, kind*32))
    # Four irregular edge overlays per surface. Neighbour tests select these strips.
    for kind in range(5):
        for direction in range(4):
            edge = Image.new('RGBA', (32, 32))
            d = ImageDraw.Draw(edge)
            rng = random.Random(71 + kind*9)
            colors = ('#667b4c', '#b4a075', '#7b947d', '#8a8b6b', '#b1ad90')
            for y in range(32):
                width = rng.choice((2, 2, 3, 4))
                d.line((0, y, width, y), fill=colors[kind])
                d.point((width+1, y), fill=(20, 38, 33, 65))
            # direction 0=west,1=north,2=east,3=south in PNG coordinates
            edge = edge.rotate(-direction*90, resample=Image.Resampling.NEAREST)
            idx = kind*4+direction
            im.alpha_composite(edge, ((idx % 8)*32, (5+idx//8)*32))
    return im


def svg_image(path):
    """Render a portable authored SVG through resvg, preserving its native logical pixels."""
    import resvg_py

    rendered = resvg_py.svg_to_bytes(svg_string=path.read_text(encoding='utf-8'))
    with Image.open(io.BytesIO(rendered)) as image:
        return image.convert('RGBA')


def environment_overrides():
    """Load the 4x3 SVG prop sheet into the legacy 64-pixel sprite slots."""
    source = SCENE_ART / 'environment.svg'
    if not source.is_file():
        return {}
    sheet = svg_image(source)
    if sheet.size != (CELL * 4, CELL * 3):
        raise ValueError(f'environment.svg must render at 256x192, got {sheet.size}')
    names = ('tree_0', 'tree_1', 'tree_2', 'node_ore',
             'node_scrap', 'node_crate', 'node_wall', 'car',
             'tuft', 'rubble', 'barrel', 'decals')
    return {name: sheet.crop(((index % 4) * CELL, (index // 4) * CELL,
                              (index % 4 + 1) * CELL, (index // 4 + 1) * CELL)).copy()
            for index, name in enumerate(names)}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    assets = []
    svg_overrides = environment_overrides()

    def add(name, im, ppu=48, pivot=(.5, .5)):
        im = svg_overrides.get(name, im)
        assets.append((name, im, ppu, pivot))

    with Image.open(Path(__file__).with_name('protagonist-sheet.png')) as hero_source:
        hero_sheet = hero_source.convert('RGBA')
    if hero_sheet.size != (CELL * 4, CELL * 4):
        raise ValueError(f'protagonist-sheet.png must be {CELL * 4}x{CELL * 4}, got {hero_sheet.size}')
    for direction in range(4):
        for frame in range(4):
            x, y = frame * CELL, direction * CELL
            hero_frame = hero_sheet.crop((x, y, x + CELL, y + CELL))
            add(f'actor_0_{direction}_{frame}', hero_frame, 40, (.5, .1875))
    for variant in (1, 2):
        for direction in range(4):
            for frame in range(4):
                add(f'actor_{variant}_{direction}_{frame}', actor(variant, direction, frame), 40, (.5, .1875))
    for v in range(3):
        add(f'tree_{v}', node('tree', v), 32, (.5, .125))
    for kind in ('ore', 'scrap', 'crate', 'wall'):
        add('node_' + kind, node(kind), 48, (.5, .34375))
    for kind in ('wall', 'generator', 'miner', 'belt', 'smelter', 'assembler', 'turret', 'chest'):
        for rotation in range(4):
            add(f'build_{kind}_{rotation}', machine(kind, rotation), 52, (.5, .5))
    for name in ('car', 'tuft', 'rubble', 'barrel', 'decals', 'shadow', 'arrow', 'rotor',
                 'turret_barrel', 'glow', 'ring', 'hit', 'blood', 'pixel'):
        add(name, decoration(name))
    for i in range(7):
        add(f'item_{i}', item(i))
    rows = math.ceil(len(assets) / COLS)
    height = 1 << (rows * CELL - 1).bit_length()
    atlas = Image.new('RGBA', (COLS*CELL, height))
    entries = []
    for index, (name, im, ppu, pivot) in enumerate(assets):
        x, y = index % COLS * CELL, index // COLS * CELL
        atlas.alpha_composite(im, (x, y))
        entries.append(dict(name=name, x=x, y=y, width=CELL, height=CELL,
                            pixelsPerUnit=ppu, pivotX=pivot[0], pivotY=pivot[1]))
    atlas.save(OUT / 'sprites.png', optimize=True)
    terrain_svg = SCENE_ART / 'terrain.svg'
    (svg_image(terrain_svg) if terrain_svg.is_file() else terrain()).save(OUT / 'terrain.png', optimize=True)
    (OUT / 'atlas.json').write_text(json.dumps(dict(sprites=entries), ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    # Sanity checks are about published art, not game simulation.
    assert len({e['name'] for e in entries}) == len(entries)
    for _, im, _, _ in assets:
        assert im.getbbox() is not None and im.size == (64, 64)
    print(json.dumps(dict(art_source='SVG-authored, code-rendered, no image model', sprites=len(entries),
                          atlas_size=atlas.size, terrain_tiles=40, edge_tiles=20,
                          files={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in
                                 (OUT/'sprites.png', OUT/'terrain.png', OUT/'atlas.json')}), ensure_ascii=False))


if __name__ == '__main__':
    main()


