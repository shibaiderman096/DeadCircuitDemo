"""Original rect-only 32px landmark art. SVG is the sole art source.

No raster drawing, image model, Unity API or existing generator is invoked.
"""
from dataclasses import dataclass, field
from html import escape
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
RUNTIME = PROJECT / 'Assets/DeadCircuit/Resources/Art/Expansion'
SOURCE = ROOT / 'Sources'
CONTENT, PITCH, GUTTER, COLS = 32, 36, 2, 8
PALETTE_SOURCE = ROOT.parent / 'ModularArt/Sources/palette.json'
PALETTE = json.loads(PALETTE_SOURCE.read_text(encoding='utf-8'))['colors']
BUILDINGS = ('Residence Shop Warehouse Workshop Clinic Farmhouse GasStation '
             'PoliceStation School Apartment Greenhouse Substation Restaurant '
             'Pharmacy HardwareStore Supermarket Library FireStation Hospital '
             'ResearchLab PowerPlant WaterTreatment RadioStation Bunker LumberMill '
             'MineOffice TrainDepot Barn HuntingCabin RangerStation Motel RecyclingYard').split()
FLOORS = ['住宅木地板', '商业瓷砖', '公共灰地砖', '工业格栅', '乡村旧木泥地', '特殊金属']
PROP_CHARS = 'bsthnmpkglruvodfwea'
PROPS = ['床', '货架', '桌', '椅', '柜台', '机器', '泵', '罐', '种植槽', '书架',
         '无线电', '发电机', '面板', '炉灶', '病床', '橱柜', '武器架', '终端', '水槽']
MOTIFS = [
    'House-shaped key plaque; brass key and door',
    'Striped canopy and stocked shopping basket',
    'Three shipping crates on a loading pallet',
    'Open-jaw wrench, socket and workbench',
    'Low medical cross plaque with pulse blocks',
    'Wheat sheaf and pitched timber farm plaque',
    'Freestanding fuel pump, meter and hanging hose',
    'Stepped blue shield with brass badge',
    'Open exercise book and school bell',
    'Tall brick intercom with six apartment windows',
    'Stepped glasshouse frame and climbing seedling',
    'Twin ceramic insulators and live busbar',
    'Dinner plate between fork and knife',
    'Green pharmacy cross and two-tone capsule',
    'Claw hammer, loose nut and hardware pegboard',
    'Wheeled trolley containing three groceries',
    'Open folio below three upright library books',
    'Fire helmet and nozzle on red equipment board',
    'Tall freestanding hospital cross and stretcher',
    'Laboratory flask and orthogonal molecular bonds',
    'Cooling stacks and stepped high-voltage bolt',
    'Water clarification tank, inlet and clean outlet',
    'Broadcast mast with two pairs of stepped radio waves',
    'Riveted armoured hatch, view slit and locking wheel',
    'Two cut log ends over a toothed mill blade',
    'Mine headframe, winding pulley and ore trolley',
    'Parallel rails, sleepers and red-green semaphore',
    'Red timber barn face, hayloft and hay bale',
    'Antler trophy above a brass camping lantern',
    'Three stepped pines and forest trail marker',
    'Roadside bed pictogram and separate vacancy lamp',
    'Recycling skip with three orthogonal chasing arrows',
]


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


@dataclass
class Sprite:
    name: str
    label: str
    role: str
    motif: str
    rects: list = field(default_factory=list)

    def r(self, x, y, w, h, color):
        assert all(type(v) is int for v in (x, y, w, h)), (self.name, x, y, w, h)
        assert 0 <= x < x+w <= 32 and 0 <= y < y+h <= 32, (self.name, x, y, w, h)
        assert color in PALETTE, color
        self.rects.append((x, y, w, h, color))
        return self

    def dot(self, x, y, color):
        return self.r(x, y, 1, 1, color)


def box(s, x, y, w, h, fill='steel', edge='ink', hi='light'):
    s.r(x, y, w, h, edge).r(x+1, y+1, w-2, h-2, fill)
    s.r(x+1, y+1, w-2, 1, hi)


def bolts(s, x, y, w, h):
    for xx in (x, x+w-1):
        for yy in (y, y+h-1):
            s.dot(xx, yy, 'cream')


def cross(s, x, y, color='white', size=3):
    s.r(x+size, y, size, size*3, color).r(x, y+size, size*3, size, color)


def crate(s, x, y, w=10, h=10):
    box(s, x, y, w, h, 'brown', hi='sand')
    s.r(x+2, y+2, w-4, 1, 'tan').r(x+2, y+h-3, w-4, 1, 'soil')
    s.r(x+2, y+2, 1, h-4, 'tan').r(x+w-3, y+2, 1, h-4, 'soil')
    if w >= 9:
        s.r(x+w//2, y+2, 1, h-4, 'soil')


def book(s, x, y, w, h, color):
    box(s, x, y, w, h, color, hi='cream')
    s.r(x+1, y+h-3, w-2, 1, 'sand')


def sign(i):
    s = Sprite(f'landmark_sign_{i}', BUILDINGS[i], 'sign', MOTIFS[i])
    r = s.r
    if i == 0:  # Key plaque: a stepped roof, never a whole building sprite.
        r(12, 2, 8, 3, 'ink'); r(8, 5, 16, 3, 'ink'); box(s, 4, 8, 24, 20, 'brown', hi='sand')
        r(8, 10, 16, 2, 'tan'); r(7, 24, 18, 2, 'soil')
        r(9, 14, 7, 7, 'gold'); r(11, 16, 3, 3, 'brown')
        r(15, 17, 9, 3, 'gold'); r(20, 20, 2, 3, 'gold'); r(23, 19, 2, 3, 'gold')
        bolts(s, 6, 10, 20, 16)
    elif i == 1:
        r(4, 7, 24, 4, 'ink'); r(3, 11, 26, 6, 'ink')
        for x in range(4, 28, 4):
            r(x, 8, 4, 7, 'cream' if x % 8 == 4 else 'red')
        r(5, 17, 2, 10, 'tan'); r(25, 17, 2, 10, 'tan')
        box(s, 9, 19, 14, 9, 'leaf', hi='green'); r(12, 16, 8, 2, 'cream')
        r(11, 18, 2, 2, 'cream'); r(19, 18, 2, 2, 'cream')
        r(12, 22, 1, 4, 'sand'); r(16, 22, 1, 4, 'sand'); r(20, 22, 1, 4, 'sand')
    elif i == 2:
        r(3, 26, 26, 4, 'ink'); r(4, 26, 24, 1, 'tan')
        r(6, 28, 4, 3, 'soil'); r(22, 28, 4, 3, 'soil')
        crate(s, 5, 15, 11, 11); crate(s, 17, 15, 11, 11); crate(s, 11, 3, 11, 12)
        r(14, 6, 5, 3, 'cream'); r(9, 18, 4, 2, 'sand')
    elif i == 3:
        box(s, 3, 4, 26, 23, 'dark', hi='steel'); bolts(s, 5, 6, 22, 19)
        r(9, 7, 3, 7, 'light'); r(17, 7, 3, 7, 'light')
        r(10, 13, 9, 5, 'light'); r(12, 17, 4, 7, 'light'); r(13, 19, 2, 3, 'steel')
        r(21, 15, 4, 7, 'tan'); r(20, 14, 6, 3, 'gold')
        r(7, 27, 18, 2, 'tan'); r(8, 29, 3, 2, 'soil'); r(22, 29, 3, 2, 'soil')
    elif i == 4:
        box(s, 3, 8, 26, 19, 'cream', hi='white'); cross(s, 8, 11, 'red', 4)
        r(5, 22, 3, 1, 'cyan'); r(8, 21, 2, 2, 'cyan'); r(10, 23, 3, 1, 'cyan')
        r(20, 22, 6, 1, 'cyan'); bolts(s, 5, 10, 22, 15)
    elif i == 5:
        r(11, 3, 10, 3, 'soil'); r(7, 6, 18, 3, 'soil'); box(s, 4, 9, 24, 19, 'brown', hi='tan')
        r(14, 11, 3, 14, 'gold'); r(10, 22, 12, 2, 'sand')
        for x, y in ((9, 12), (18, 10), (8, 17), (19, 15)):
            r(x, y, 4, 3, 'gold'); r(x+1, y, 2, 1, 'cream')
        r(12, 8, 3, 4, 'gold'); r(16, 7, 3, 4, 'sand')
        r(5, 26, 22, 2, 'soil')
    elif i == 6:
        r(5, 28, 21, 3, 'ink'); box(s, 6, 5, 15, 24, 'red', hi='rose')
        box(s, 8, 7, 11, 8, 'dark', hi='light'); r(10, 10, 7, 2, 'teal')
        r(10, 19, 7, 7, 'rust'); r(12, 18, 3, 8, 'gold'); r(11, 22, 5, 3, 'gold')
        r(21, 9, 5, 3, 'ink'); r(25, 11, 3, 14, 'ink'); r(22, 24, 5, 3, 'ink')
        r(21, 17, 3, 8, 'steel'); r(22, 14, 3, 5, 'light')
    elif i == 7:
        r(4, 5, 24, 16, 'ink'); r(7, 21, 18, 4, 'ink'); r(11, 25, 10, 3, 'ink')
        r(5, 6, 22, 14, 'blue'); r(8, 20, 16, 4, 'blue'); r(12, 24, 8, 3, 'blue')
        r(6, 6, 20, 2, 'ice'); r(10, 11, 12, 9, 'gold'); r(13, 8, 6, 15, 'gold')
        r(14, 12, 4, 7, 'cream'); r(12, 14, 8, 3, 'cream'); r(15, 14, 2, 3, 'amber')
    elif i == 8:
        r(3, 16, 26, 13, 'ink'); r(4, 17, 11, 10, 'cream'); r(17, 17, 11, 10, 'sand')
        r(15, 17, 2, 12, 'brown'); r(6, 20, 7, 1, 'tan'); r(6, 23, 7, 1, 'tan')
        r(19, 20, 7, 1, 'brown'); r(19, 23, 7, 1, 'brown')
        r(13, 3, 6, 3, 'ink'); r(10, 6, 12, 7, 'ink'); r(8, 12, 16, 3, 'ink')
        r(11, 7, 10, 5, 'gold'); r(9, 12, 14, 2, 'gold'); r(14, 14, 4, 2, 'cream')
    elif i == 9:
        box(s, 6, 2, 20, 28, 'rust', hi='rose')
        for y in (6, 12, 18):
            for x in (9, 18):
                box(s, x, y, 5, 4, 'blue', hi='ice')
        r(10, 25, 6, 3, 'dark'); r(18, 25, 3, 2, 'gold')
        r(6, 10, 20, 1, 'brown'); r(6, 16, 20, 1, 'brown'); r(6, 22, 20, 1, 'brown')
    elif i == 10:
        r(12, 2, 8, 3, 'ink'); r(7, 5, 18, 4, 'ink'); r(4, 9, 24, 20, 'ink')
        r(13, 3, 6, 3, 'ice'); r(8, 6, 16, 4, 'cyan'); r(5, 10, 22, 17, 'blue')
        r(7, 11, 7, 12, 'cyan'); r(18, 11, 7, 12, 'cyan')
        r(15, 5, 2, 22, 'light'); r(5, 15, 22, 2, 'light'); r(5, 24, 22, 3, 'soil')
        r(14, 18, 4, 8, 'green'); r(10, 19, 5, 3, 'leaf'); r(18, 17, 5, 3, 'green')
    elif i == 11:
        box(s, 3, 24, 26, 5, 'steel'); r(7, 9, 3, 15, 'steel'); r(23, 9, 3, 15, 'steel')
        for x in (5, 21):
            for y in (6, 10, 14):
                r(x, y, 7, 2, 'cream'); r(x+1, y, 5, 1, 'white')
        r(7, 4, 18, 2, 'ink'); r(8, 4, 16, 1, 'amber')
        r(16, 13, 4, 5, 'gold'); r(13, 17, 5, 4, 'gold'); r(15, 20, 3, 5, 'amber')
        r(9, 26, 14, 1, 'dark')
    elif i == 12:
        r(10, 7, 12, 2, 'ink'); r(6, 11, 20, 12, 'ink'); r(9, 8, 14, 18, 'ink')
        r(10, 10, 12, 14, 'cream'); r(7, 12, 18, 10, 'cream'); r(10, 12, 12, 10, 'white')
        r(12, 14, 8, 6, 'sand'); r(13, 14, 6, 4, 'cream')
        r(2, 5, 1, 8, 'light'); r(4, 5, 1, 8, 'light'); r(6, 5, 1, 8, 'light')
        r(2, 12, 5, 3, 'light'); r(4, 15, 2, 13, 'light')
        r(27, 5, 3, 12, 'light'); r(28, 17, 2, 11, 'light')
    elif i == 13:
        r(12, 2, 8, 17, 'ink'); r(7, 7, 18, 7, 'ink'); cross(s, 8, 3, 'green', 4)
        r(7, 23, 3, 6, 'ink'); r(10, 20, 12, 11, 'ink'); r(22, 23, 3, 6, 'ink')
        r(8, 23, 8, 6, 'white'); r(11, 21, 5, 9, 'white')
        r(16, 21, 5, 9, 'red'); r(21, 23, 3, 6, 'red'); r(16, 22, 1, 7, 'ink')
    elif i == 14:
        box(s, 4, 4, 24, 25, 'brown', hi='tan'); bolts(s, 6, 6, 20, 21)
        for x in (9, 15, 21):
            for y in (10, 16, 22):
                s.dot(x, y, 'soil')
        r(11, 10, 4, 15, 'gold'); r(8, 7, 13, 5, 'light'); r(8, 11, 4, 3, 'steel')
        r(20, 18, 5, 7, 'steel'); r(18, 20, 9, 3, 'steel'); r(21, 20, 3, 3, 'ink')
        r(12, 13, 2, 10, 'tan')
    elif i == 15:
        r(3, 7, 5, 3, 'ink'); r(6, 10, 3, 13, 'ink'); r(8, 11, 20, 12, 'ink')
        r(9, 12, 17, 8, 'light'); r(10, 14, 15, 1, 'dark'); r(10, 17, 15, 1, 'dark')
        for x in (13, 18, 23):
            r(x, 13, 1, 7, 'dark')
        r(8, 23, 17, 2, 'steel'); r(10, 26, 4, 4, 'ink'); r(21, 26, 4, 4, 'ink')
        r(11, 27, 2, 2, 'light'); r(22, 27, 2, 2, 'light')
        r(10, 6, 5, 6, 'red'); r(16, 4, 4, 8, 'gold'); r(21, 7, 5, 5, 'green')
        r(17, 3, 2, 2, 'cream')
    elif i == 16:
        r(3, 26, 26, 3, 'brown'); book(s, 5, 4, 6, 14, 'red')
        book(s, 12, 2, 6, 16, 'blue'); book(s, 19, 5, 7, 13, 'leaf')
        r(4, 20, 24, 6, 'ink'); r(5, 20, 10, 5, 'cream'); r(17, 20, 10, 5, 'sand')
        r(15, 19, 2, 9, 'soil'); r(7, 22, 6, 1, 'tan'); r(19, 22, 6, 1, 'brown')
    elif i == 17:
        box(s, 3, 5, 26, 23, 'red', hi='rose'); bolts(s, 5, 7, 22, 19)
        r(12, 8, 9, 3, 'ink'); r(8, 11, 17, 7, 'ink'); r(6, 17, 21, 3, 'ink')
        r(13, 9, 7, 2, 'gold'); r(9, 12, 15, 5, 'gold'); r(7, 17, 19, 2, 'gold')
        r(14, 12, 4, 5, 'cream'); r(10, 21, 12, 3, 'steel'); r(21, 20, 4, 5, 'light')
        r(7, 22, 3, 3, 'dark')
    elif i == 18:
        r(14, 8, 3, 23, 'steel'); r(10, 29, 12, 2, 'ink')
        box(s, 4, 2, 24, 18, 'white', hi='cream'); cross(s, 10, 4, 'red', 4)
        r(5, 23, 2, 5, 'light'); r(5, 25, 22, 3, 'light'); r(9, 22, 12, 3, 'ice')
        r(7, 28, 2, 3, 'dark'); r(24, 28, 2, 3, 'dark')
    elif i == 19:
        box(s, 2, 4, 28, 25, 'dark', hi='steel'); r(11, 7, 9, 3, 'light')
        r(12, 10, 3, 8, 'ice'); r(17, 10, 3, 8, 'ice')
        r(9, 17, 14, 4, 'ice'); r(7, 21, 18, 5, 'ice'); r(9, 21, 14, 4, 'cyan')
        r(11, 18, 10, 3, 'teal'); r(12, 22, 3, 2, 'ice')
        r(23, 9, 4, 4, 'purple'); r(25, 13, 1, 4, 'light'); r(24, 17, 3, 3, 'teal')
        r(5, 9, 4, 3, 'rose'); r(6, 12, 1, 4, 'light'); r(4, 16, 4, 4, 'cyan')
    elif i == 20:
        r(3, 27, 26, 3, 'ink'); r(4, 23, 24, 4, 'steel')
        for x in (5, 20):
            r(x, 7, 7, 16, 'ink'); r(x+1, 8, 5, 14, 'steel')
            r(x-1, 5, 9, 4, 'light'); r(x+1, 10, 2, 11, 'light')
        r(13, 10, 5, 7, 'gold'); r(10, 16, 8, 4, 'gold')
        r(14, 19, 5, 6, 'gold'); r(15, 24, 2, 4, 'amber')
        r(14, 3, 4, 3, 'cream')
    elif i == 21:
        r(2, 9, 7, 5, 'ink'); r(2, 10, 7, 3, 'steel'); r(3, 10, 6, 1, 'light')
        box(s, 7, 6, 18, 22, 'steel'); r(9, 12, 14, 12, 'blue')
        r(9, 12, 5, 2, 'ice'); r(14, 14, 6, 2, 'ice'); r(20, 12, 3, 2, 'ice')
        r(9, 18, 7, 2, 'cyan'); r(16, 20, 7, 2, 'cyan')
        r(25, 19, 5, 6, 'ink'); r(25, 20, 5, 3, 'light'); r(28, 23, 2, 5, 'ice')
        r(11, 3, 10, 3, 'light'); r(14, 2, 4, 2, 'cream')
    elif i == 22:
        r(14, 4, 4, 22, 'ink'); r(15, 5, 2, 21, 'light')
        r(11, 24, 10, 3, 'steel'); r(7, 27, 18, 3, 'ink'); r(10, 27, 12, 1, 'light')
        r(9, 17, 14, 2, 'steel'); r(11, 12, 10, 2, 'steel'); r(14, 2, 4, 4, 'red')
        for x in (5, 24):
            r(x, 7, 3, 11, 'cyan')
        for x in (1, 29):
            r(x, 9, 2, 7, 'teal')
        r(8, 5, 3, 2, 'cyan'); r(21, 5, 3, 2, 'cyan')
    elif i == 23:
        box(s, 3, 3, 26, 27, 'shadow', hi='light'); box(s, 6, 6, 20, 20, 'steel')
        bolts(s, 5, 5, 22, 23); r(10, 9, 12, 4, 'ink'); r(11, 10, 10, 1, 'blue')
        r(13, 16, 6, 7, 'dark'); r(11, 18, 10, 3, 'dark')
        r(15, 16, 2, 7, 'light'); r(11, 19, 10, 1, 'light')
        r(15, 18, 2, 3, 'gold'); r(8, 27, 4, 2, 'gold'); r(20, 27, 4, 2, 'gold')
    elif i == 24:
        for x in (4, 17):
            r(x, 5, 10, 13, 'ink'); r(x+1, 6, 8, 11, 'brown')
            r(x+2, 7, 6, 8, 'sand'); r(x+3, 9, 4, 4, 'brown'); r(x+4, 10, 2, 2, 'tan')
        r(3, 21, 26, 7, 'steel'); r(4, 21, 24, 2, 'light')
        for x in range(4, 29, 4):
            r(x, 27, 2, 3, 'light'); r(x, 19, 2, 2, 'light')
        r(8, 24, 16, 1, 'dark')
    elif i == 25:
        r(7, 2, 18, 4, 'ink'); r(8, 3, 16, 2, 'tan'); r(8, 6, 3, 14, 'brown')
        r(21, 6, 3, 14, 'brown'); r(14, 4, 5, 6, 'light'); r(16, 10, 1, 9, 'steel')
        r(8, 13, 16, 3, 'tan'); box(s, 7, 20, 18, 8, 'steel')
        r(10, 18, 5, 5, 'shadow'); r(17, 17, 5, 6, 'light'); r(13, 16, 4, 6, 'steel')
        r(9, 28, 4, 3, 'ink'); r(20, 28, 4, 3, 'ink')
    elif i == 26:
        for y in (8, 15, 22, 28):
            r(6, y, 19, 3, 'brown'); r(6, y, 19, 1, 'tan')
        for x in (9, 21):
            r(x, 5, 3, 26, 'ink'); r(x+1, 5, 1, 26, 'light')
        r(3, 2, 4, 20, 'steel'); r(3, 3, 16, 2, 'light')
        r(13, 1, 8, 8, 'ink'); r(15, 3, 4, 2, 'red'); r(15, 6, 4, 2, 'green')
    elif i == 27:
        r(12, 2, 8, 3, 'ink'); r(8, 5, 16, 3, 'ink'); r(4, 8, 24, 21, 'ink')
        r(9, 6, 14, 3, 'rust'); r(5, 9, 22, 18, 'red')
        box(s, 12, 8, 8, 7, 'dark', hi='tan'); box(s, 8, 17, 16, 11, 'brown', hi='sand')
        r(15, 18, 2, 10, 'soil'); r(9, 21, 14, 2, 'tan')
        r(3, 23, 7, 6, 'gold'); r(4, 24, 5, 1, 'sand'); r(6, 23, 1, 6, 'brown')
    elif i == 28:
        r(4, 3, 2, 8, 'tan'); r(9, 4, 2, 9, 'tan'); r(6, 9, 6, 3, 'tan')
        r(26, 3, 2, 8, 'tan'); r(21, 4, 2, 9, 'tan'); r(20, 9, 6, 3, 'tan')
        r(11, 11, 3, 4, 'sand'); r(18, 11, 3, 4, 'sand'); r(13, 14, 6, 4, 'brown')
        r(12, 19, 8, 3, 'ink'); box(s, 10, 22, 12, 7, 'gold', hi='cream')
        r(13, 23, 6, 4, 'amber'); r(15, 23, 2, 4, 'cream'); r(9, 29, 14, 2, 'soil')
    elif i == 29:
        for x, y in ((4, 7), (18, 4), (11, 1)):
            r(x+3, y, 3, 4, 'green'); r(x+1, y+4, 7, 4, 'leaf')
            r(x, y+8, 9, 4, 'leaf'); r(x+3, y+12, 3, 4, 'brown')
        box(s, 5, 22, 22, 6, 'tan', hi='sand'); r(13, 28, 3, 4, 'soil')
        r(8, 24, 13, 2, 'ink'); r(20, 23, 3, 4, 'ink')
    elif i == 30:
        r(5, 9, 3, 21, 'brown'); r(25, 9, 3, 21, 'brown'); box(s, 3, 5, 27, 18, 'blue', hi='ice')
        r(7, 12, 2, 8, 'cream'); r(9, 15, 17, 4, 'cream'); r(23, 13, 3, 7, 'cream')
        r(10, 12, 5, 3, 'white'); r(16, 13, 7, 2, 'teal')
        box(s, 10, 25, 14, 5, 'dark'); r(12, 26, 10, 2, 'green')
    elif i == 31:
        r(5, 7, 22, 3, 'ink'); box(s, 6, 10, 20, 18, 'leaf', hi='green')
        r(8, 5, 16, 3, 'steel'); r(11, 3, 10, 2, 'light')
        r(12, 13, 8, 3, 'cream'); r(18, 11, 4, 4, 'cream'); r(20, 15, 3, 5, 'cream')
        r(18, 19, 5, 3, 'cream'); r(18, 21, 3, 3, 'cream'); r(12, 22, 8, 3, 'cream')
        r(9, 20, 4, 5, 'cream'); r(8, 16, 3, 6, 'cream'); r(9, 14, 4, 3, 'cream')
        r(8, 27, 4, 3, 'ink'); r(20, 27, 4, 3, 'ink')
    return s


def floor(i):
    s = Sprite(f'landmark_floor_{i}', FLOORS[i], 'floor', 'Seamless 32px top-down surface')
    r = s.r
    if i == 0:
        r(0, 0, 32, 32, 'soil')
        for row, c in enumerate(('brown', 'tan', 'brown', 'brown')):
            y = row*8
            r(0, y+1, 32, 6, c); r(0, y+1, 32, 1, 'tan')
            x = 8 if row % 2 == 0 else 23
            r(x, y+2, 1, 5, 'soil'); r(3, y+4, 3, 1, 'brown'); r(26, y+5, 3, 1, 'soil')
            s.dot(x-2, y+3, 'sand'); s.dot(x+2, y+5, 'soil')
    elif i == 1:
        r(0, 0, 32, 32, 'shadow')
        for y in (0, 16):
            for x in (0, 16):
                r(x+1, y+1, 14, 14, 'sand' if x == y else 'cream')
                r(x+2, y+2, 12, 1, 'white'); r(x+14, y+3, 1, 11, 'tan')
    elif i == 2:
        r(0, 0, 32, 32, 'dark')
        for y in range(0, 32, 8):
            for x in range(0, 32, 8):
                r(x+1, y+1, 6, 6, 'steel' if (x+y) % 16 else 'shadow')
                r(x+1, y+1, 6, 1, 'light')
        r(11, 20, 2, 1, 'dark'); r(12, 21, 1, 2, 'dark'); r(25, 11, 3, 1, 'shadow')
    elif i == 3:
        r(0, 0, 32, 32, 'ink'); r(1, 1, 30, 30, 'dark')
        for x in range(3, 31, 4):
            r(x, 2, 2, 28, 'steel'); r(x, 2, 1, 28, 'light')
        for y in (5, 15, 25):
            r(2, y, 28, 2, 'shadow'); r(2, y, 28, 1, 'steel')
        bolts(s, 2, 2, 28, 28)
    elif i == 4:
        r(0, 0, 32, 32, 'soil')
        for x, y, w, h in ((2, 3, 21, 6), (8, 12, 22, 6), (1, 21, 23, 7)):
            r(x, y, w, h, 'brown'); r(x, y, w, 1, 'tan'); r(x+2, y+3, w-5, 1, 'soil')
            s.dot(x+1, y+2, 'sand'); s.dot(x+w-2, y+h-2, 'dark')
        r(25, 3, 4, 3, 'shadow'); r(3, 13, 3, 2, 'olive'); r(26, 24, 3, 2, 'olive')
        r(10, 29, 4, 1, 'tan'); s.dot(29, 10, 'sand')
    elif i == 5:
        r(0, 0, 32, 32, 'ink'); r(1, 1, 30, 30, 'steel'); r(2, 2, 28, 1, 'light')
        r(2, 3, 1, 26, 'light'); r(29, 3, 1, 27, 'shadow'); r(3, 29, 26, 1, 'shadow')
        r(15, 3, 2, 26, 'dark'); r(3, 15, 26, 2, 'dark')
        for x in (4, 24):
            for y in (4, 24):
                r(x, y, 4, 4, 'dark'); r(x+1, y+1, 2, 2, 'light')
        r(5, 13, 5, 1, 'cyan'); r(20, 18, 6, 1, 'cyan')
    return s


def prop(ch):
    i = PROP_CHARS.index(ch)
    s = Sprite(f'landmark_prop_{ch}', PROPS[i], 'prop', 'Top-down furniture; north/top lighting')
    r = s.r
    if ch == 'b':
        box(s, 7, 3, 18, 27, 'brown', hi='sand'); r(9, 5, 14, 22, 'cream')
        r(11, 6, 10, 5, 'white'); r(9, 13, 14, 14, 'blue'); r(9, 13, 14, 2, 'ice')
        r(10, 16, 2, 10, 'cyan'); r(21, 16, 2, 11, 'dark'); r(8, 28, 16, 2, 'soil')
    elif ch == 's':
        box(s, 3, 5, 26, 23, 'soil', hi='tan')
        for y in (7, 14, 21):
            r(5, y+4, 22, 2, 'steel'); r(5, y+4, 22, 1, 'light')
            for x, c in ((6, 'rust'), (13, 'olive'), (20, 'sand')):
                r(x, y, 5, 4, c); r(x+1, y, 3, 1, 'cream')
        r(4, 8, 1, 18, 'tan'); r(27, 8, 1, 18, 'brown')
    elif ch == 't':
        r(5, 9, 23, 18, 'ink'); r(4, 6, 24, 19, 'brown'); r(5, 7, 22, 16, 'tan')
        r(5, 7, 22, 2, 'sand'); r(6, 14, 20, 1, 'brown'); r(25, 9, 2, 14, 'soil')
        r(8, 10, 5, 5, 'cream'); r(9, 11, 3, 2, 'white'); r(20, 17, 4, 4, 'steel')
        r(21, 18, 2, 2, 'dark')
    elif ch == 'h':
        box(s, 8, 3, 16, 8, 'brown', hi='sand'); r(9, 11, 3, 17, 'soil'); r(21, 11, 3, 17, 'soil')
        box(s, 7, 12, 18, 13, 'tan', hi='sand'); r(10, 15, 12, 7, 'brown')
        r(11, 15, 10, 1, 'tan'); r(7, 25, 3, 4, 'ink'); r(22, 25, 3, 4, 'ink')
    elif ch == 'n':
        box(s, 3, 5, 26, 24, 'brown', hi='tan'); r(5, 7, 22, 9, 'sand')
        r(5, 7, 22, 2, 'cream'); r(5, 17, 22, 10, 'soil'); r(6, 18, 20, 2, 'tan')
        r(6, 25, 20, 1, 'brown'); box(s, 18, 8, 7, 6, 'dark'); r(19, 9, 5, 2, 'teal')
        r(7, 10, 7, 4, 'cream'); r(8, 11, 5, 1, 'tan')
    elif ch == 'm':
        box(s, 3, 5, 26, 23, 'steel'); box(s, 6, 8, 13, 13, 'dark')
        r(10, 7, 5, 15, 'shadow'); r(5, 12, 15, 5, 'shadow'); r(10, 12, 5, 5, 'light')
        r(11, 13, 3, 3, 'dark'); r(22, 9, 3, 4, 'teal'); r(22, 15, 3, 2, 'red')
        for y in (21, 23, 25):
            r(20, y, 6, 1, 'dark')
        bolts(s, 5, 7, 22, 19)
    elif ch == 'p':
        r(1, 13, 30, 6, 'ink'); r(2, 14, 28, 4, 'steel'); r(2, 14, 28, 1, 'light')
        box(s, 8, 6, 15, 20, 'blue', hi='ice'); box(s, 10, 10, 11, 11, 'steel')
        r(13, 12, 5, 7, 'dark'); r(12, 14, 7, 3, 'dark'); r(14, 14, 3, 3, 'gold')
        r(10, 26, 3, 3, 'ink'); r(19, 26, 3, 3, 'ink'); r(12, 7, 7, 2, 'cyan')
    elif ch == 'k':
        r(9, 2, 14, 3, 'ink'); r(5, 5, 22, 22, 'ink'); r(8, 27, 16, 3, 'ink')
        r(9, 3, 14, 2, 'light'); r(6, 6, 20, 20, 'steel'); r(9, 26, 14, 3, 'shadow')
        r(8, 7, 3, 17, 'light'); r(23, 7, 2, 17, 'shadow'); r(7, 12, 18, 2, 'dark')
        r(7, 21, 18, 2, 'dark'); box(s, 12, 6, 9, 6, 'dark'); r(14, 8, 5, 2, 'cyan')
        r(21, 2, 4, 4, 'steel'); r(24, 1, 5, 3, 'light')
    elif ch == 'g':
        box(s, 2, 6, 28, 21, 'brown', hi='sand'); r(4, 9, 24, 14, 'soil')
        for x, y in ((6, 10), (14, 13), (22, 10)):
            r(x, y+2, 3, 7, 'leaf'); r(x-2, y, 4, 4, 'green'); r(x+2, y+3, 4, 4, 'leaf')
            r(x, y+1, 2, 3, 'olive')
        r(4, 24, 24, 1, 'tan'); r(4, 7, 24, 1, 'cream')
    elif ch == 'l':
        box(s, 4, 3, 24, 27, 'soil', hi='sand')
        for row, y in enumerate((5, 13, 21)):
            for j, x in enumerate((6, 10, 14, 19, 23)):
                book(s, x, y, 3 if j != 2 else 4, 6, ('red', 'blue', 'olive', 'purple', 'tan')[(j+row) % 5])
            r(5, y+6, 22, 1, 'tan')
    elif ch == 'r':
        r(5, 1, 2, 13, 'light'); r(6, 2, 1, 9, 'cream'); box(s, 3, 10, 26, 19, 'leaf', hi='green')
        box(s, 6, 13, 11, 12, 'dark')
        for y in (15, 18, 21):
            r(8, y, 7, 1, 'steel')
        box(s, 19, 13, 7, 5, 'dark'); r(20, 14, 5, 2, 'teal')
        r(20, 21, 5, 5, 'ink'); r(21, 22, 3, 3, 'light')
        r(10, 7, 12, 3, 'ink'); r(12, 8, 8, 1, 'tan')
    elif ch == 'u':
        box(s, 2, 7, 28, 20, 'ink', hi='steel'); box(s, 5, 10, 16, 13, 'steel')
        for x in (7, 11, 15):
            r(x, 12, 2, 9, 'dark'); r(x, 12, 1, 9, 'light')
        box(s, 22, 10, 5, 13, 'rust', hi='amber'); r(23, 11, 3, 2, 'gold')
        r(6, 3, 6, 4, 'ink'); r(7, 3, 4, 1, 'light'); r(7, 27, 4, 3, 'ink'); r(23, 27, 4, 3, 'ink')
        r(17, 5, 7, 3, 'steel'); r(20, 4, 3, 2, 'light')
    elif ch == 'v':
        box(s, 5, 2, 22, 28, 'steel'); bolts(s, 7, 4, 18, 24)
        box(s, 8, 6, 16, 9, 'ink'); r(10, 8, 12, 2, 'teal'); r(10, 12, 7, 1, 'cyan')
        for x in (10, 16, 22):
            r(x, 19, 2, 5, 'dark'); r(x, 18, 2, 2, 'red' if x == 22 else 'gold')
        r(9, 26, 14, 1, 'dark')
    elif ch == 'o':
        box(s, 4, 3, 24, 26, 'cream', hi='white')
        for x in (7, 18):
            for y in (7, 17):
                r(x, y, 7, 7, 'dark'); r(x+2, y+1, 3, 5, 'steel'); r(x+1, y+2, 5, 3, 'steel')
                s.dot(x+3, y+3, 'ink')
        r(7, 4, 2, 1, 'red'); r(12, 4, 2, 1, 'steel'); r(18, 4, 2, 1, 'steel'); r(23, 4, 2, 1, 'steel')
    elif ch == 'd':
        box(s, 6, 3, 20, 27, 'steel'); r(8, 5, 16, 22, 'white'); r(10, 6, 12, 5, 'cream')
        r(8, 14, 16, 13, 'ice'); r(9, 14, 14, 2, 'white'); cross(s, 12, 18, 'red', 2)
        r(3, 9, 3, 15, 'ink'); r(4, 10, 1, 13, 'light'); r(26, 9, 3, 15, 'ink'); r(27, 10, 1, 13, 'light')
        r(8, 28, 4, 3, 'dark'); r(21, 28, 4, 3, 'dark')
    elif ch == 'f':
        box(s, 4, 3, 24, 26, 'brown', hi='sand'); r(6, 7, 20, 18, 'tan')
        r(15, 6, 2, 21, 'soil'); r(7, 7, 7, 1, 'sand'); r(18, 7, 7, 1, 'sand')
        r(12, 14, 2, 6, 'gold'); r(18, 14, 2, 6, 'gold')
        r(7, 24, 7, 1, 'brown'); r(18, 24, 7, 1, 'brown'); r(5, 27, 22, 2, 'soil')
    elif ch == 'w':
        box(s, 4, 4, 24, 24, 'soil', hi='tan'); r(7, 8, 18, 2, 'steel'); r(7, 22, 18, 2, 'steel')
        for x in (9, 17):
            r(x, 7, 3, 13, 'steel'); r(x+1, 7, 1, 11, 'light'); r(x-1, 18, 5, 7, 'brown')
            r(x+2, 16, 3, 3, 'dark'); r(x+2, 17, 2, 1, 'light')
        r(23, 11, 2, 10, 'light'); r(22, 21, 4, 3, 'tan')
    elif ch == 'e':
        box(s, 5, 3, 22, 18, 'steel'); box(s, 8, 6, 16, 12, 'ink')
        r(10, 8, 8, 2, 'teal'); r(10, 12, 11, 1, 'cyan'); r(10, 15, 5, 1, 'green')
        r(12, 21, 8, 2, 'dark'); box(s, 4, 23, 24, 6, 'steel')
        for x in range(7, 25, 3):
            r(x, 25, 2, 1, 'light'); r(x, 27, 2, 1, 'dark')
    elif ch == 'a':
        box(s, 3, 5, 26, 24, 'light', hi='white'); box(s, 6, 11, 20, 14, 'steel')
        r(8, 13, 16, 9, 'dark'); r(9, 14, 14, 6, 'blue'); r(10, 14, 11, 1, 'ice')
        r(15, 5, 3, 11, 'ink'); r(15, 6, 2, 9, 'light'); r(12, 6, 5, 2, 'light')
        r(8, 7, 3, 2, 'red'); r(21, 7, 3, 2, 'blue'); r(16, 19, 2, 2, 'light')
    return s


def sprites():
    return [sign(i) for i in range(32)] + [floor(i) for i in range(6)] + [prop(c) for c in PROP_CHARS]


def style():
    return '<style>' + ''.join(f'.c-{k}{{fill:{v}}}' for k, v in PALETTE.items()) + '</style>'


def svg(width, height, contents):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
            f'viewBox="0 0 {width} {height}" shape-rendering="crispEdges" data-codeart="code">\n'
            + style() + '\n' + '\n'.join(contents) + '\n</svg>\n')


def rect_xml(x, y, w, h, color):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" class="c-{color}"/>'


def sprite_xml(s, ox=0, oy=0, group_id=None):
    return (f'<g id="{escape(group_id or s.name)}">\n'
            + '\n'.join(rect_xml(x+ox, y+oy, w, h, c) for x, y, w, h, c in s.rects)
            + '\n</g>')


def write_sources():
    SOURCE.mkdir(parents=True, exist_ok=True)
    all_sprites = sprites()
    height = ((len(all_sprites)+COLS-1)//COLS)*PITCH
    entries, catalog, groups = [], [], []
    for i, s in enumerate(all_sprites):
        x, y = (i % COLS)*PITCH+GUTTER, (i//COLS)*PITCH+GUTTER
        entries.append(dict(name=s.name, x=x, y=y, w=32, h=32))
        catalog.append(dict(name=s.name, label=s.label, role=s.role, motif=s.motif, index=i,
                            row=i//COLS, column=i % COLS, content=[32, 32]))
        groups.append(sprite_xml(s, x, y))
        (SOURCE / f'{s.name}.svg').write_text(svg(32, 32, [sprite_xml(s)]), encoding='utf-8')
    (SOURCE / 'landmarks.svg').write_text(svg(COLS*PITCH, height, groups), encoding='utf-8')
    layout = dict(width=COLS*PITCH, height=height, sprites=entries)
    write_json(SOURCE / 'landmarks.layout.json', layout)
    write_json(SOURCE / 'palette.json', dict(colors=PALETTE))
    write_json(SOURCE / 'manifest.json', dict(schema='deadcircuit.landmark_art.v1',
               art_source='code', disclosure='code-drawn, no image model', placeholder=False,
               canvas=[32, 32], atlas=[COLS*PITCH, height], pitch=36, gutter=2, columns=8,
               rowOrigin='top-left', rotation=False, trim=False,
               runtimeJson='width,height,sprites[{name,x,y,w,h}]',
               categories=dict(sign=32, floor=6, prop=19),
               buildingOrder=BUILDINGS, propCharacterOrder=PROP_CHARS, sprites=catalog))
    # Review graphics are SVG too. They contain no text or embedded bitmap.
    review = []
    for i, s in enumerate(all_sprites):
        x, y = (i % 8)*76, (i//8)*44
        for j, bg in enumerate(('dark', 'cream')):
            xx = x+j*36
            review.append(rect_xml(xx, y, 34, 34, bg))
            review.append(sprite_xml(s, xx+1, y+1, f'review-{i}-{j}'))
        # Category ticks: blue=sign, tan=floor, green=prop; no lettering.
        review.append(rect_xml(x, y+37, 68, 2, {'sign':'blue', 'floor':'tan', 'prop':'green'}[s.role]))
    (SOURCE / 'landmarks_review.svg').write_text(svg(608, 352, review), encoding='utf-8')
    tiles = []
    for i in range(6):
        s = all_sprites[32+i]
        for yy in range(3):
            for xx in range(3):
                tiles.append(sprite_xml(s, (i % 3)*104+xx*32, (i//3)*104+yy*32, f'floor-{i}-{xx}-{yy}'))
    (SOURCE / 'floor_tiling_review.svg').write_text(svg(312, 208, tiles), encoding='utf-8')
    return dict(spriteCount=len(all_sprites), svgCount=len(list(SOURCE.glob('*.svg'))), atlas=[288, height])


if __name__ == '__main__':
    print(json.dumps(write_sources()))
