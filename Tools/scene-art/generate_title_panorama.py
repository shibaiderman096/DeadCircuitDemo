"""Draw the Dead Circuit title scene on a native pixel grid (Python + Pillow)."""

from argparse import ArgumentParser
from io import BytesIO
from math import exp
from pathlib import Path
from random import Random

from PIL import Image, ImageDraw


WIDTH, HEIGHT = SIZE = (640, 360)
ART = Path(__file__).resolve().parents[2] / "Assets/DeadCircuit/Resources/Art"
LAYERS = ("TitlePanorama.png", "TitleForeground.png")
INK = "#141f21"
IRON = "#28383a"
STEEL = "#435152"
RUST = "#60483c"
ASH = "#666658"
GLINT = "#9b7650"


def mix(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def noise(x, y, cell_x, cell_y, salt):
    """Smooth interpolation of a fixed lattice, sampled on the pixel grid."""
    def corner(i, j):
        n = (i * 374761393 + j * 668265263 + salt * 69069) & 0xffffffff
        n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
        return ((n ^ (n >> 16)) & 255) / 255

    i, u = divmod(x, cell_x)
    j, v = divmod(y, cell_y)
    u, v = u / cell_x, v / cell_y
    u, v = u*u*(3 - 2*u), v*v*(3 - 2*v)
    return (corner(i, j)*(1-u) + corner(i+1, j)*u)*(1-v) + (
        corner(i, j+1)*(1-u) + corner(i+1, j+1)*u
    )*v


def draw_sky(image):
    pixels = image.load()
    horizon = 219
    top, bottom = (22, 40, 49), (68, 82, 83)
    for y in range(horizon + 37):
        base = mix(top, bottom, min(1, (y / horizon) ** 1.4))
        for x in range(WIDTH):
            falloff = ((x - 328) / 349) ** 2
            glow = exp(-((x - 395) / 118) ** 2 - ((y - 216) / 31) ** 2)
            # Long cloud banks with progressively smaller broken edges.
            stream_y = y + (x - 170) // 14
            cloud = 0.57 * noise(x, stream_y, 139, 41, 3)
            cloud += 0.30 * noise(x, stream_y, 66, 19, 5)
            cloud += 0.13 * noise(x, stream_y, 23, 7, 7)
            cloud = round(max(0, (cloud - 0.39)) * 17) / 17
            cloud *= 0.72 + 0.55 * min(1, falloff)
            shade = 1 - 0.19 * falloff
            cold = tuple(c * shade for c in base)
            color = mix(cold, (116, 96, 68), 0.67 * glow)
            color = mix(color, (18, 32, 39), cloud * (0.9 - 0.42 * glow))
            pixels[x, y] = tuple(2 * round(c / 2) for c in color)

    draw = ImageDraw.Draw(image)
    # Squall fragments frame the open sky. Their underside is cut into clusters.
    for shape, color, wisps in (
        ([(0, 30), (17, 32), (30, 39), (52, 40), (74, 53), (104, 57),
          (117, 64), (128, 65), (137, 70), (119, 71), (102, 67), (77, 66),
          (65, 61), (43, 61), (34, 57), (12, 57), (0, 50)], "#15262f",
         [(0, 52, 21), (33, 63, 30), (70, 69, 23), (99, 72, 22)]),
        ([(638, 27), (611, 33), (589, 34), (566, 42), (541, 44), (526, 50),
          (506, 52), (492, 57), (522, 59), (548, 55), (572, 55), (590, 49),
          (612, 47), (639, 45)], "#172830",
         [(500, 60, 38), (549, 57, 27), (602, 50, 31)]),
        ([(0, 106), (34, 105), (53, 112), (84, 116), (97, 123), (114, 123),
          (148, 133), (124, 134), (108, 131), (76, 128), (64, 124),
          (34, 124), (14, 120), (0, 120)], "#1f3038",
         [(3, 122, 21), (42, 127, 42), (101, 134, 15)]),
        ([(639, 119), (614, 122), (588, 127), (563, 130), (562, 133),
          (539, 135), (518, 141), (492, 143), (483, 147), (518, 149),
          (549, 144), (591, 143), (618, 134), (639, 134)], "#213139",
         [(496, 151, 31), (542, 147, 26), (589, 145, 20)]),
    ):
        draw.polygon(shape, fill=color)
        for x, y, width in wisps:
            draw.line((x, y, x + width, y), fill=color)
            draw.line((x + width//5, y+2, x + width*2//3, y+2), fill=color)

    # A few thin breaks lit from below, clustered around the fading light.
    for x, y, length, palette in (
        (88, 173, 92, ("#394c53", "#3f5055")),
        (346, 193, 77, ("#575952", "#605e52")),
        (388, 203, 131, ("#605b4d", "#6b624f")),
        (411, 213, 57, ("#6e644e", "#8b7454")),
        (361, 221, 82, ("#6c6555", "#807052")),
    ):
        draw.polygon([(x, y), (x + length//6, y-1), (x + length//2, y-2),
                      (x + length, y-2), (x + length*3//4, y),
                      (x + length//3, y+1)], fill=palette[0])
        draw.line((x + length//4, y, x + length*3//5, y), fill=palette[1])


def draw_ridge(draw, knots, color, edge, salt):
    ridge = []
    for (left, y0), (right, y1) in zip(knots, knots[1:]):
        for x in range(left, right + 1):
            y = y0 + (y1 - y0) * (x - left) / (right - left)
            y += 8 * (noise(x, 1, 19, 1, salt) - 0.5)
            y += 3 * (noise(x, 1, 5, 1, salt + 31) - 0.5)
            ridge.append((x, round(y)))
    draw.polygon(ridge + [(WIDTH - 1, 266), (0, 266)], fill=color)
    draw.line(ridge, fill=edge)
    return ridge


def draw_distance(draw):
    draw_ridge(draw, [(0, 192), (52, 181), (83, 186), (123, 199),
                      (163, 192), (230, 211), (285, 221), (361, 229),
                      (439, 220), (472, 208), (512, 219), (567, 194),
                      (616, 180), (639, 188)], "#435353", "#485858", 44)
    draw_ridge(draw, [(0, 224), (44, 207), (118, 218), (174, 211),
                      (209, 224), (267, 227), (323, 239), (378, 237),
                      (454, 226), (494, 232), (535, 218), (589, 222),
                      (639, 213)], "#36494a", "#3e5150", 66)
    # Low, half submerged utility buildings beyond the water.
    for x, y, width, height in ((141, 217, 43, 26), (181, 225, 38, 22),
                                (213, 230, 27, 17), (245, 234, 17, 14),
                                (426, 232, 31, 17), (462, 224, 45, 29),
                                (526, 213, 48, 41), (589, 208, 42, 48)):
        draw.polygon([(x, y+height), (x, y+5), (x+width//3, y),
                      (x+width-8, y), (x+width, y+5), (x+width, y+height)],
                     fill="#304243")
        draw.line((x+width//3, y, x+width-8, y), fill="#485855")
        for mullion in range(x+5, x+width-4, 8):
            draw.rectangle((mullion, y+7, mullion+3, y+9), fill="#3e4d4b")
    for x, height, width in ((163, 31, 4), (205, 26, 3), (228, 29, 2),
                              (459, 34, 3), (475, 46, 4), (489, 31, 3)):
        draw.polygon([(x, 243), (x+1, 243-height), (x+width, 243-height),
                      (x+width+1, 243)], fill="#344647")
        draw.line((x, 243-height, x+width+1, 243-height), fill="#485653")


def draw_water(draw):
    random = Random(727)
    for y in range(245, HEIGHT):
        depth = (y - 245) / (HEIGHT - 245)
        draw.line((0, y, WIDTH-1, y), fill=mix((53, 70, 69), (25, 40, 41), depth))
    for _ in range(1900):
        y = random.randrange(246, HEIGHT)
        depth = (y - 245) / (HEIGHT - 245)
        x = random.randrange(-35, WIDTH)
        length = random.randint(1, round(7 + 37 * depth))
        color = mix((53, 70, 69), (25, 40, 41), depth)
        shade = random.choice((-8, -6, -3, -2, 3, 4, 6, 8))
        if 270 < x < 440 and y < 291:
            shade //= 2
        draw.line((x, y, x + length, y), fill=tuple(c + shade for c in color))
    for _ in range(124):
        y = random.randrange(248, 320)
        spread = 17 + (y - 246) // 2
        x = 396 + random.randint(-spread, spread)
        t = (y - 248) / 72
        color = mix((101, 94, 70), (39, 53, 50), min(1, t + random.random() * 0.43))
        draw.line((x, y, x + random.randint(1, 12), y), fill=color)
    # Blocked reflections under the ruins, broken sideways by the water.
    for center, top, bottom, width in ((22, 262, 309, 55), (142, 258, 302, 36),
                                       (506, 264, 288, 25), (584, 268, 316, 49)):
        for y in range(top, bottom):
            t = (y - top) / (bottom - top)
            for _ in range(random.randint(0, 4)):
                x = center + random.randint(-width, width)
                length = random.randint(3, 19)
                color = mix((39, 53, 50), (38, 53, 52), t)
                if random.randrange(6) == 0:
                    color = mix((102, 88, 64), (39, 54, 51), min(1, t + 0.38))
                draw.line((x, y, x + length, y), fill=color)


def stack(draw, x, top, bottom, width):
    draw.polygon([(x-2, bottom), (x+1, top), (x+width-3, top),
                  (x+width+2, bottom)], fill=IRON)
    draw.polygon([(x+width-7, bottom), (x+width-6, top), (x+width-3, top),
                  (x+width+2, bottom)], fill=RUST)
    draw.line((x+width-2, top+7, x+width, bottom), fill="#80604b")
    draw.rectangle((x-1, top, x+width-1, top+4), fill=INK)
    draw.line((x, top, x+width-2, top), fill=ASH)
    for y in range(top+13, bottom, 14):
        right = x + width - 2 + (y - top) * 3 // (bottom - top)
        draw.line((x, y, right, y), fill="#354042")
        draw.line((right-4, y+1, right, y+1), fill="#715241")
    for y in (top+24, top+55):
        if y < bottom:
            draw.rectangle((x-4, y, x+width+3, y+2), fill=INK)
            draw.line((x-2, y, x+width+1, y), fill=STEEL)
    seed = Random(x)
    for _ in range((bottom-top)//3):
        y = seed.randrange(top+7, bottom)
        at = x + seed.randrange(2, width-1)
        tone = seed.choice(("#3b4240", "#665044", "#59493f"))
        draw.line((at, y, at, min(y + seed.randrange(1, 7), bottom)), fill=tone)


def corrode(draw, x, y, w, h, seed):
    """Downward rust stains with fractured edges and a few exposed plates."""
    random = Random(seed)
    draw.polygon([(x, y), (x+w, y), (x+w-1, y+h//3), (x+w*3//4, y+h//3),
                  (x+w//2, y+h), (x+w//3, y+h*2//3), (x+2, y+h//2)], fill="#564b3e")
    for _ in range(w+2):
        at = x + random.randrange(w)
        top = y + random.randrange(min(5, h//2))
        bottom = top + random.randrange(1, max(2, h - abs(at-x-w//2)))
        draw.line((at, top, at, bottom), fill=random.choice((
            "#655244", "#574c3e", "#43453d", "#4b4238", "#3b4440")))


def chipped_wall(draw, bounds, face, lit, shadow, seed):
    x0, y0, x1, y1 = bounds
    draw.rectangle(bounds, fill=face)
    random = Random(seed)
    for _ in range((x1-x0)*(y1-y0)//19):
        x, y = random.randrange(x0, x1), random.randrange(y0, y1)
        color = random.choice((face, shadow, shadow, lit))
        draw.line((x, y, min(x+random.randrange(1, 9), x1), y), fill=color)


def pipe(draw, points):
    draw.line(points, fill=INK, width=9, joint="curve")
    draw.line(points, fill="#4b4640", width=6, joint="curve")
    draw.line([(x, y-2) for x, y in points], fill="#765945", width=1)


def draw_factory(draw):
    stack(draw, 44, 68, 214, 20)
    stack(draw, 84, 99, 205, 14)
    stack(draw, 114, 119, 197, 10)
    draw.polygon([(0, 159), (22, 145), (40, 145), (40, 149), (46, 149),
                  (46, 152), (54, 150), (58, 145), (86, 145), (86, 172), (109, 153),
                  (132, 153), (132, 173), (150, 161), (163, 161), (172, 174),
                  (181, 175), (181, 241), (0, 252)], fill="#354344")
    draw.line((0, 159, 22, 145, 39, 145), fill="#62685c", width=2)
    draw.line((58, 145, 85, 145), fill="#62685c", width=2)
    for start, peak, end in (((86, 172), (109, 153), (132, 153)),
                             ((132, 173), (150, 161), (163, 161))):
        draw.line([start, peak, end], fill="#62685c", width=2)
        draw.line([start, (end[0], start[1]), end], fill="#4f5550")
    draw.polygon([(0, 160), (23, 147), (23, 199), (0, 207)], fill="#233437")
    draw.polygon([(22, 146), (39, 146), (39, 150), (46, 153), (54, 151),
                  (59, 146), (86, 146), (86, 151), (68, 153), (65, 158),
                  (60, 155), (48, 161), (39, 158), (39, 152), (20, 155)], fill="#242f31")
    draw.polygon([(24, 150), (36, 150), (36, 153), (55, 153), (51, 156),
                  (26, 157)], fill="#4d5349")
    draw.polygon([(65, 146), (70, 146), (71, 149), (77, 149), (75, 152),
                  (65, 155), (62, 152)], fill="#43514f")
    draw.polygon([(111, 154), (132, 154), (132, 159), (121, 159),
                  (118, 164), (112, 164), (112, 160), (103, 160)], fill="#232f30")
    for x, y, w, h in ((12, 158, 8, 17), (72, 158, 11, 15), (117, 162, 7, 9)):
        corrode(draw, x, y, w, h, x+y)
    draw.rectangle((0, 200, 165, 255), fill=INK)
    chipped_wall(draw, (1, 203, 134, 251), "#344240", "#404b44", "#2a3838", 81)
    chipped_wall(draw, (137, 192, 175, 254), "#484c43", "#625749", "#3d4340", 27)
    draw.polygon([(175, 192), (188, 184), (188, 244), (175, 254)], fill="#323e3d")
    draw.line((175, 193, 175, 245), fill="#79604b")
    draw.polygon([(128, 187), (175, 187), (188, 181), (190, 185), (174, 195),
                  (128, 195)], fill="#56564a")
    draw.line((137, 189, 171, 189, 187, 182), fill="#7d6c53")
    for x, y, w, h in ((3, 208, 10, 13), (32, 202, 21, 12), (80, 232, 6, 14),
                        (137, 196, 9, 17), (157, 216, 14, 25)):
        corrode(draw, x, y, w, h, x+y)
    for y in range(233, 252, 4):
        draw.line((5, y, 67, y), fill="#2b3837")
        for x in range(9 + (y % 8), 68, 9):
            draw.point((x, y+1), fill="#4c5045")
    # Punched clerestory, with some missing panes and a partly collapsed bay.
    for x, y, count in ((22, 174, 5), (105, 170, 3), (9, 214, 7), (142, 204, 2)):
        for i in range(count):
            at = x + 14*i
            if at < 98 or y < 175 or x > 130:
                draw.rectangle((at, y, at+10, y+12), fill="#1d292a")
                draw.line((at, y+13, at+11, y+13), fill="#5b6052")
                draw.line((at+4, y+1, at+4, y+12), fill="#424f4b")
                draw.line((at+1, y+6, at+10, y+6), fill="#424f4b")
                if (i + x) % 3 == 0:
                    draw.polygon([(at+5, y+1), (at+10, y+1), (at+10, y+7)], fill="#78664c")
    draw.polygon([(93, 249), (93, 225), (96, 221), (119, 221), (125, 224),
                  (125, 249)], fill=INK)
    for y in range(224, 245, 3):
        draw.line((97, y, 121, y), fill="#303c3b")
    for x in (24, 78, 132):
        draw.rectangle((x, 199, x+2, 254), fill="#4c5245")
        draw.line((x+3, 201, x+3, 254), fill="#273536")
    pipe(draw, [(1, 239), (13, 239), (23, 246), (59, 246), (70, 238), (70, 210)])
    pipe(draw, [(115, 253), (151, 253), (158, 246), (158, 224)])
    for x in (6, 37, 55, 132, 148):
        y = 240 if x < 12 else 246 if x < 80 else 253
        draw.rectangle((x, y-4, x+2, y+4), fill="#77604c")
        draw.line((x+2, y-4, x+2, y+4), fill="#343936")
    # Gantry ribs and damaged loading dock beyond the last bay.
    draw.line((187, 219, 213, 225, 228, 225), fill="#555a51", width=2)
    draw.line((187, 225, 215, 231, 223, 231), fill="#334345", width=2)
    for x, y in ((192, 220), (206, 223), (216, 225)):
        draw.line((x, y+5, x+8, y+1), fill="#626258")
        draw.line((x, y+6, x, 248), fill="#465051")
    draw.polygon([(0, 252), (63, 253), (78, 255), (127, 254), (151, 257),
                  (184, 250), (215, 252), (245, 257), (211, 262), (69, 266),
                  (0, 277)], fill="#283b3b")
    draw.line((12, 270, 72, 264, 123, 263), fill="#3c4d47")
    draw.line((131, 264, 187, 261, 210, 260), fill="#43554f")


def pylon_arms(cx, top, height):
    return [(cx - width, top + drop, cx + width) for drop, width in (
        (height // 6, height // 10), (height // 3, height // 6),
        (height * 5 // 11, height // 8),
    )]


def cable(draw, start, end, sag, color):
    x0, y0 = start
    x1, y1 = end
    result = []
    for x in range(x0, x1 + 1):
        t = (x - x0) / (x1 - x0)
        result.append((x, round(y0 + (y1 - y0) * t + 4 * sag * t * (1 - t))))
    draw.line(result, fill=color)


def pylon(draw, cx, top, height, dark, edge):
    top_width = max(1, height // 31)
    foot = height // 8
    levels = [(top, top_width), (top+height//6, top_width+2),
              (top+height//3, top_width+3), (top+height*5//11, top_width+3),
              (top+height*6//10, foot//2), (top+height*7//10, foot*2//3),
              (top+height*8//10, foot*3//4), (top+height*9//10, foot*7//8),
              (top+height, foot)]
    for (y0, w0), (y1, w1) in zip(levels, levels[1:]):
        draw.line((cx-w0, y0, cx+w1, y1), fill=dark)
        draw.line((cx+w0, y0, cx-w1, y1), fill=dark)
        draw.line((cx-w1, y1, cx+w1, y1), fill=dark)
    draw.line([(cx-w, y) for y, w in levels], fill=edge, width=2)
    draw.line([(cx+w, y) for y, w in levels], fill=dark, width=2)
    draw.line((cx-top_width-1, top, cx+top_width+1, top), fill=edge)
    for left, y, right in pylon_arms(cx, top, height):
        draw.line((left, y+3, cx, y-5, right, y+3), fill=edge)
        draw.line((left, y+3, right, y+3), fill=dark, width=2)
        for x in (left, right):
            draw.line((x, y+3, x, y+8), fill=dark)
            draw.point((x, y+6), fill=edge)


def draw_substation(draw):
    distant, main = (459, 164, 92), (549, 85, 169)
    for i, ((_, ya, right), (left, yb, _)) in enumerate(zip(
        pylon_arms(*distant), pylon_arms(*main)
    )):
        cable(draw, (right, ya+8), (left, yb+8), 8+i*4, "#394a4c")
    for i, (_, y, right) in enumerate(pylon_arms(*main)):
        cable(draw, (right, y+8), (646, 75+i*23), 11+i*2, "#354346")
    pylon(draw, *distant, "#394b4a", "#485450")
    pylon(draw, *main, "#243437", "#57605a")

    # The roof of the turbine house has peeled back above its dark interior.
    draw.polygon([(641, 156), (613, 155), (607, 160), (598, 156), (583, 170),
                  (572, 170), (552, 188), (542, 188), (540, 256),
                  (641, 264)], fill="#263536")
    draw.line((541, 189, 551, 189, 575, 169, 588, 168, 598, 158), fill="#666554", width=2)
    draw.line((607, 161, 615, 155, 639, 155), fill="#776951", width=2)
    for x, y in ((553, 188), (567, 177), (581, 173), (594, 165), (610, 164), (623, 159)):
        draw.line((x, y, x, 228), fill="#4d4b43", width=2)
        draw.line((x, y, x+9, y-2), fill="#596058")
        draw.line((x, y+13, x+10, y+9), fill="#3a4441")

    # A low tank and a closer switchgear hut give the right side different masses.
    draw.ellipse((557, 182, 619, 204), fill="#5c5c4e")
    draw.rectangle((557, 194, 619, 247), fill="#354442")
    draw.ellipse((557, 236, 619, 258), fill="#354442")
    draw.polygon([(561, 194), (569, 190), (575, 193), (575, 255),
                  (567, 253), (562, 248)], fill="#5d5546")
    draw.line((561, 196, 561, 247), fill="#807259")
    draw.rectangle((605, 194, 619, 247), fill="#2c3a3a")
    for y in (201, 219, 239):
        draw.arc((557, y-11, 619, y+4), 0, 180, fill="#252f2e", width=2)
        draw.arc((559, y-8, 618, y+5), 9, 179, fill="#535748")
    draw.arc((557, 182, 619, 203), 195, 330, fill="#82806a")
    corrode(draw, 572, 204, 11, 24, 107)
    corrode(draw, 602, 225, 13, 20, 122)
    for x, y in ((579, 198), (606, 201), (578, 235), (599, 244)):
        draw.line((x, y, x+5, y), fill="#596055")
        draw.point((x+1, y+1), fill="#7b7260")
    for x in (588, 594):
        draw.line((x, 191, x, 257), fill="#515850")
    for y in range(195, 257, 5):
        draw.line((588, y, 594, y), fill="#686859")
    pipe(draw, [(615, 210), (632, 210), (637, 215), (637, 247)])
    chipped_wall(draw, (490, 227, 550, 265), "#3b4640", "#4c5044", "#303f3d", 74)
    draw.polygon([(482, 228), (500, 214), (531, 214), (546, 218), (557, 229)],
                 fill="#4e544a")
    draw.line((482, 228, 500, 214, 531, 214, 545, 219), fill="#736951")
    draw.rectangle((496, 234, 521, 247), fill="#222e2e")
    for x in range(497, 521, 6):
        draw.line((x, 235, x, 246), fill="#4f5146")
    draw.rectangle((531, 232, 545, 264), fill="#6a5a46")
    draw.rectangle((533, 234, 544, 264), fill="#283533")
    draw.line((541, 248, 542, 248), fill=GLINT)
    draw.polygon([(441, 266), (471, 263), (479, 258), (497, 260), (530, 258),
                  (576, 260), (604, 256), (639, 256), (639, 286),
                  (584, 274), (540, 273), (506, 268), (473, 270)], fill="#263b3b")
    draw.line((448, 267, 472, 267, 499, 265), fill="#4c584e")
    draw.line((531, 269, 551, 270, 568, 271), fill="#47534b")


def draw_debris(draw):
    random = Random(1718)
    for x0, x1, y0, y1 in ((0, 211, 263, 309), (468, 640, 269, 316)):
        for _ in range(55):
            x, y = random.randrange(x0, x1), random.randrange(y0, y1)
            w = random.randint(4, 20)
            h = random.randint(1, 4)
            fill = random.choice(("#263837", "#314342", "#2c3f3d", "#344642"))
            draw.polygon([(x-w//2, y), (x-w//3, y-h), (x+w//4, y-h),
                          (x+w//2, y), (x+w//5, y+1)], fill=fill)
            if random.randrange(3) == 0:
                draw.line((x-w//3, y-h, x+w//5, y-h), fill="#4b574b")


def grass(draw, x, y, scale, random):
    for _ in range(random.randint(5, 10)):
        spread = random.randint(-scale, scale)
        height = random.randint(scale//3, scale)
        root = x + random.randint(-3, 3)
        tip = (x+spread, y-height)
        draw.line([(root, y), (root+spread//3, y-height*3//4), tip], fill=random.choice((
            "#263730", "#344337", "#3b4a39", "#4d5440", "#585741", "#2a3932",
        )))
        if height > 12 and random.randrange(4) == 0:
            draw.line((tip[0]-1, tip[1]-1, tip[0], tip[1]), fill="#8b7954")


def draw_rail(draw):
    # The track enters from bottom right and ends at the washout.
    for i in range(10):
        t = (i / 9) ** 1.65
        x, y = round(505 + 104*t), round(313 + 47*t)
        w = round(12 + 26*t)
        draw.polygon([(x-w, y), (x+w, y-12), (x+w+4, y-10),
                      (x+w+4, y-7), (x-w, y+3)], fill="#272a25")
        draw.line((x-w, y, x+w, y-12), fill="#564838")
    for rail in ([(498, 312), (526, 324), (562, 340), (603, 360)],
                 [(514, 309), (547, 320), (591, 335), (649, 354)]):
        draw.line(rail, fill="#151e1e", width=4)
        draw.line([(x, y-1) for x, y in rail], fill="#77583f", width=2)
        draw.line([(x, y-2) for x, y in rail[:2]], fill=GLINT)


def draw_foreground(image):
    draw = ImageDraw.Draw(image)
    # Cutout banks with a broad opening over the still water.
    bank = [(0, 285), (11, 288), (23, 286), (39, 296), (57, 296), (76, 301),
            (91, 304), (111, 312), (121, 310), (146, 317), (160, 323),
            (188, 324), (192, 329), (209, 327), (237, 337), (261, 339),
            (286, 346), (310, 344), (332, 350), (361, 347), (398, 344),
            (417, 337), (450, 335), (471, 324), (485, 324), (501, 313),
            (518, 314), (532, 306), (550, 308), (571, 300), (588, 298),
            (605, 290), (629, 285), (639, 282)]
    draw.polygon(bank + [(639, 359), (0, 359)], fill="#202d2b")
    draw.line(bank, fill="#4a5345")
    draw.polygon([(0, 312), (33, 305), (78, 313), (109, 325), (167, 332),
                  (218, 359), (0, 359)], fill="#192524")
    draw.polygon([(639, 301), (600, 308), (562, 322), (520, 345), (492, 359),
                  (639, 359)], fill="#1a2524")
    random = Random(113)
    for (a, y0), (b, y1) in zip(bank, bank[1:]):
        for _ in range((b-a)//2 + 1):
            x = random.randrange(a, b)
            edge_y = round(y0 + (y1-y0) * (x-a)/(b-a))
            y = random.randint(edge_y+2, HEIGHT)
            w = random.randint(2, 16)
            tone = random.choice(("#273831", "#172423", "#2b3830", "#3a4639", "#25302a"))
            draw.polygon([(x, y), (x+2, y-1), (x+w-1, y-1), (x+w, y),
                          (x+w//2, y+2)], fill=tone)

    draw_rail(draw)
    # Irregular foundation blocks and a discarded section of drainpipe.
    for x, y, w, h in ((-11, 292, 35, 28), (12, 300, 40, 19), (41, 308, 22, 18),
                        (74, 322, 34, 14), (119, 335, 29, 13), (169, 343, 17, 8),
                        (451, 338, 21, 10), (543, 324, 23, 17), (581, 305, 32, 20),
                        (609, 290, 36, 35)):
        draw.polygon([(x-6, y+4), (x+5, y-3), (x+w-7, y-4), (x+w, y),
                      (x+w-4, y+h), (x+1, y+h+2)], fill="#303b35")
        draw.polygon([(x-6, y+4), (x+5, y-3), (x+w-7, y-4), (x+w, y),
                      (x+w-14, y+6), (x+3, y+8)], fill="#515448")
        draw.line((x-6, y+4, x+3, y+8, x+11, y+8), fill="#666052")
        draw.line((x+3, y+9, x+5, y+13, x+4, y+h), fill="#212b27")
        draw.line((x+w-9, y+6, x+w-15, y+9), fill="#465043")
    draw.polygon([(18, 313), (42, 304), (66, 311), (74, 337), (64, 345),
                  (33, 350), (23, 346)], fill="#4d4438")
    draw.line((21, 315, 42, 305, 64, 312), fill="#796047", width=2)
    for x in (32, 40, 49, 57):
        draw.line((x, 310, x+6, 330, x+3, 346), fill="#2b322c", width=2)
    draw.ellipse((53, 311, 82, 346), fill="#675443")
    draw.ellipse((57, 314, 79, 343), fill="#192524")
    draw.arc((57, 314, 79, 343), 80, 260, fill="#3f3e34", width=2)
    draw.arc((53, 311, 82, 346), 182, 291, fill="#99805b")
    draw.line((68, 341, 73, 341, 78, 338), fill="#484a38")

    for (a, y0), (b, y1) in zip(bank, bank[1:]):
        weight = 7 if a < 223 or a > 432 else 20
        for _ in range((b-a)//weight + (random.randrange(3) == 0)):
            x = random.randrange(a, b)
            edge_y = round(y0 + (y1-y0) * (x-a)/(b-a))
            y = min(359, edge_y + random.randint(2, 21))
            size = random.randint(6, 25 if a < 162 or a > 481 else 12)
            grass(draw, x, y, size, random)
    for x, y, scale in ((10, 300, 31), (103, 349, 26), (152, 356, 18),
                         (477, 356, 22), (567, 341, 27), (620, 321, 32)):
        grass(draw, x, y, scale, random)


def render():
    panorama = Image.new("RGB", SIZE)
    draw_sky(panorama)
    draw = ImageDraw.Draw(panorama)
    draw_distance(draw)
    draw_water(draw)
    draw_substation(draw)
    draw_factory(draw)
    draw_debris(draw)
    foreground = Image.new("RGBA", SIZE)
    draw_foreground(foreground)
    return panorama, foreground


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Compare generated PNGs without writing files.")
    options = parser.parse_args()
    panorama, foreground = render()
    alpha = foreground.getchannel("A")
    assert panorama.size == foreground.size == SIZE
    assert {value for value, count in enumerate(alpha.histogram()) if count} == {0, 255}
    assert not alpha.crop((180, 0, 460, 280)).getbbox(), "Foreground covers the menu area"

    for name, image in zip(LAYERS, (panorama, foreground)):
        path = ART / name
        data = BytesIO()
        image.save(data, format="PNG", optimize=True)
        if options.check:
            if not path.is_file() or path.read_bytes() != data.getvalue():
                parser.exit(1, f"{path.name} differs from the generated PNG.\n")
        else:
            path.write_bytes(data.getvalue())
        print(f"{path.name} {image.width}x{image.height} {image.mode}")


if __name__ == "__main__":
    main()
