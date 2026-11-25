"""Late-game DeadCircuit machine sprites drawn on the shared 32 px grid.

The keys follow BuildKind 36..66 and labels follow DemoCatalog.BuildNames.
These are top-down thumbnails: each machine has a readable world-scale
silhouette, a slight front face, and top-left lighting.  The three modular
facilities are catalogue/preview thumbnails only; runtime construction still
renders their individual cells.
"""
from pixel_svg import Sprite


MACHINES = [
    "CharcoalKiln",
    "ClayKiln",
    "BlastFurnace",
    "MechanicalPress",
    "MechanicalWorkshop",
    "WaterMill",
    "IrrigationPump",
    "MechanicalHopper",
    "CokeOven",
    "RefractoryKiln",
    "IronBlastFurnace",
    "RefiningForge",
    "MachineTool",
    "WindingBench",
    "FilterSplitter",
    "CircuitBreaker",
    "ElectricCrusher",
    "OilPump",
    "LiquidPipe",
    "LiquidTank",
    "DistillationUnit",
    "ChemicalReactor",
    "ProcessWaterPump",
    "SiliconRefiner",
    "ElectronicsBench",
    "ReactorControlConsole",
    "NuclearWasteVault",
    "LargeSteamFurnace",
    "LargeAssemblyPlant",
    "LargeOreProcessor",
    "ResearchBench",
]

MACHINE_ZH = [
    "炭窑",
    "陶窑",
    "鼓风炉",
    "机械压机",
    "机械工坊",
    "水力磨坊",
    "灌溉泵",
    "机械装卸器",
    "焦炉",
    "耐火窑",
    "炼铁高炉",
    "精炼炉",
    "机床",
    "机械绕线台",
    "过滤分流器",
    "低压断路器",
    "电动破碎机",
    "抽油机",
    "液体管道",
    "储液罐",
    "分馏装置",
    "基础化学反应器",
    "工艺取水泵",
    "硅材精炼机",
    "电子工台",
    "反应堆控制台",
    "核废料专用仓",
    "大型蒸汽高炉",
    "基础装配厂",
    "矿物处理厂",
    "研究台",
]


def _base(s, x=3, y=25, w=26, h=3):
    s.rect(x, y, w, h, "dark")
    s.rect(x + 1, y, w - 2, 1, "light")
    s.rect(x + 2, y + h, 3, 1, "ink")
    s.rect(x + w - 5, y + h, 3, 1, "ink")


def _top_face(s, x, y, w, h, face, top, dark="dark"):
    s.rect(x, y, w, h, face)
    s.rect(x, y, w, 2, top)
    s.rect(x, y + h - 2, w, 2, dark)
    s.rect(x, y + 1, 2, h - 2, "light")


def _pipe(s, x, y, w, h, c="steel"):
    s.rect(x, y, w, h, c)
    if w >= h:
        s.rect(x, y, w, 1, "light")
        s.rect(x, y + h - 1, w, 1, "dark")
    else:
        s.rect(x, y, 1, h, "light")
        s.rect(x + w - 1, y, 1, h, "dark")


def _flange(s, x, y, w, h, c="light"):
    s.rect(x, y, w, h, c)
    s.rect(x + 1, y + 1, max(1, w - 2), max(1, h - 2), "steel")


def _tank(s, x, y, w, h, body="steel"):
    s.oval(x, y, w, min(6, h), body)
    s.rect(x, y + 2, w, h - 4, body)
    s.oval(x, y + h - min(6, h), w, min(6, h), body)
    s.rect(x + 2, y + 3, 1, h - 6, "light")
    s.rect(x + w - 2, y + 4, 1, h - 8, "dark")


def _gear(s, x, y, z, c="tan"):
    s.oval(x, y, z, z, c)
    s.oval(x + 3, y + 3, z - 6, z - 6, "dark")
    s.rect(x + z // 2 - 1, y - 1, 3, z + 2, c)
    s.rect(x - 1, y + z // 2 - 1, z + 2, 3, c)
    s.rect(x + z // 2 - 1, y + z // 2 - 1, 3, 3, "light")


def _wheel(s, x, y, z, c="brown"):
    s.oval(x, y, z, z, c)
    s.oval(x + 3, y + 3, z - 6, z - 6, "dark")
    mid = z // 2
    s.rect(x + mid - 1, y, 3, z, c)
    s.rect(x, y + mid - 1, z, 3, c)
    s.line(x + 3, y + 3, x + z - 4, y + z - 4, c)
    s.line(x + z - 4, y + 3, x + 3, y + z - 4, c)
    s.rect(x + mid - 1, y + mid - 1, 3, 3, "light")


def _vent(s, x, y, w, h):
    s.rect(x, y, w, h, "dark")
    for yy in range(y + 1, y + h, 3):
        s.rect(x + 1, yy, w - 2, 1, "light")


def _bolts(s, x, y, w, h):
    for bx, by in ((x + 1, y + 1), (x + w - 2, y + 1),
                   (x + 1, y + h - 2), (x + w - 2, y + h - 2)):
        s.dot(bx, by, "sand")


def _charcoal_kiln(s):
    s.rect(4, 25, 24, 4, "soil")
    s.rect(6, 20, 20, 7, "brown")
    s.oval(7, 9, 18, 15, "brown")
    s.oval(9, 11, 14, 10, "tan")
    s.rect(8, 12, 13, 3, "amber")
    s.rect(11, 19, 8, 7, "dark")
    s.oval(13, 20, 4, 5, "amber")
    s.rect(15, 3, 3, 8, "shadow")
    s.rect(14, 2, 5, 3, "steel")
    for x, y, w in ((2, 24, 7), (24, 23, 5), (22, 26, 7)):
        s.rect(x, y, w, 2, "tan")
        s.dot(x + 1, y - 1, "sand")
    s.dot(11, 12, "gold")
    s.dot(20, 15, "gold")


def _clay_kiln(s):
    _base(s)
    s.rect(5, 12, 22, 14, "rust")
    s.rect(5, 10, 22, 4, "tan")
    s.rect(4, 9, 24, 2, "amber")
    s.rect(10, 3, 12, 7, "rust")
    s.rect(12, 2, 8, 2, "tan")
    s.rect(12, 16, 9, 9, "dark")
    s.rect(14, 20, 5, 4, "amber")
    s.rect(22, 14, 3, 10, "brown")
    for x in (7, 12, 17, 22):
        s.rect(x, 12, 1, 9, "dark")
    s.rect(7, 16, 4, 4, "sand")
    s.rect(6, 18, 4, 1, "white")
    s.rect(24, 7, 3, 4, "steel")


def _blast_furnace(s):
    _base(s)
    s.rect(8, 12, 17, 14, "shadow")
    s.rect(10, 7, 13, 9, "rust")
    s.rect(12, 3, 9, 6, "steel")
    s.rect(14, 2, 5, 3, "dark")
    s.rect(4, 15, 5, 9, "rust")
    s.rect(2, 17, 4, 5, "steel")
    s.rect(13, 18, 8, 7, "dark")
    s.rect(15, 21, 4, 3, "gold")
    s.rect(25, 15, 4, 8, "brown")
    _pipe(s, 1, 12, 3, 3, "steel")
    _pipe(s, 24, 6, 3, 10, "rust")
    s.rect(18, 4, 4, 1, "light")
    s.rect(8, 14, 3, 6, "amber")
    s.rect(21, 20, 2, 3, "amber")


def _mechanical_press(s):
    _base(s)
    s.rect(3, 8, 5, 19, "steel")
    s.rect(24, 8, 5, 19, "steel")
    s.rect(4, 6, 24, 4, "light")
    s.rect(6, 10, 20, 3, "dark")
    s.rect(14, 10, 4, 9, "rust")
    s.rect(11, 18, 10, 5, "dark")
    s.rect(10, 20, 12, 2, "light")
    s.rect(13, 22, 7, 4, "steel")
    _wheel(s, 5, 11, 10, "steel")
    s.rect(24, 13, 4, 10, "brown")
    s.rect(25, 15, 2, 5, "amber")
    s.rect(9, 7, 3, 3, "gold")
    s.rect(20, 7, 3, 3, "gold")


def _mechanical_workshop(s):
    _base(s, 2, 25, 28)
    s.rect(5, 22, 23, 4, "tan")
    s.rect(5, 11, 3, 12, "brown")
    s.rect(25, 8, 3, 16, "brown")
    s.rect(4, 7, 25, 4, "rust")
    s.rect(7, 8, 19, 2, "amber")
    s.rect(9, 14, 3, 9, "steel")
    s.rect(10, 10, 7, 4, "steel")
    s.rect(15, 11, 2, 8, "light")
    s.rect(15, 18, 5, 2, "dark")
    _gear(s, 21, 15, 7, "tan")
    s.rect(20, 21, 6, 2, "brown")
    s.rect(12, 23, 5, 2, "cream")
    s.rect(16, 22, 4, 3, "steel")
    s.dot(6, 24, "amber")


def _water_mill(s):
    s.rect(1, 25, 30, 4, "blue")
    s.rect(2, 28, 28, 1, "cyan")
    s.rect(17, 15, 11, 11, "tan")
    s.rect(18, 13, 9, 3, "rust")
    s.rect(20, 16, 6, 7, "brown")
    s.rect(22, 17, 3, 4, "amber")
    _wheel(s, 3, 4, 19, "brown")
    s.rect(14, 12, 11, 3, "steel")
    s.rect(16, 10, 5, 4, "dark")
    s.rect(26, 10, 3, 6, "steel")
    s.rect(2, 22, 12, 2, "cream")
    s.rect(2, 24, 18, 1, "white")


def _irrigation_pump(s):
    s.rect(1, 24, 30, 6, "blue")
    s.rect(2, 27, 28, 1, "cyan")
    s.rect(4, 19, 24, 6, "steel")
    s.oval(11, 9, 12, 12, "rust")
    s.oval(14, 12, 6, 6, "tan")
    s.rect(15, 14, 4, 3, "amber")
    s.rect(7, 13, 5, 9, "steel")
    s.rect(21, 8, 3, 14, "steel")
    s.rect(22, 6, 7, 3, "light")
    s.rect(26, 8, 3, 7, "amber")
    _pipe(s, 23, 14, 7, 3, "rust")
    s.rect(10, 22, 5, 3, "brown")
    s.rect(24, 21, 4, 3, "brown")
    s.rect(5, 22, 2, 3, "light")


def _mechanical_hopper(s):
    _base(s)
    s.rect(7, 8, 18, 2, "light")
    s.rect(8, 9, 16, 3, "steel")
    s.rect(10, 12, 12, 2, "dark")
    s.rect(12, 14, 8, 3, "steel")
    s.rect(14, 17, 4, 4, "rust")
    s.rect(5, 21, 8, 4, "dark")
    s.rect(19, 21, 8, 4, "dark")
    _gear(s, 20, 11, 7, "tan")
    s.rect(4, 19, 3, 6, "brown")
    s.rect(26, 18, 3, 7, "brown")
    s.rect(15, 20, 11, 2, "steel")
    s.rect(16, 21, 8, 1, "light")
    s.rect(23, 23, 3, 2, "amber")


def _coke_oven(s):
    _base(s)
    s.rect(4, 13, 25, 12, "brown")
    for x in (5, 12, 19):
        s.oval(x, 12, 7, 12, "rust")
        s.rect(x + 2, 17, 3, 5, "dark")
        s.rect(x + 3, 19, 1, 3, "amber")
    s.rect(4, 10, 25, 4, "dark")
    s.rect(7, 4, 4, 8, "rust")
    s.rect(8, 2, 2, 3, "dark")
    s.rect(23, 5, 4, 7, "rust")
    s.rect(24, 3, 2, 3, "dark")
    s.rect(5, 23, 23, 2, "rust")
    for x in (8, 15, 22):
        s.rect(x, 11, 1, 3, "sand")


def _refractory_kiln(s):
    _base(s)
    s.rect(4, 11, 24, 14, "cream")
    s.rect(5, 8, 22, 5, "steel")
    s.rect(7, 6, 18, 2, "light")
    s.rect(8, 15, 17, 8, "dark")
    s.rect(10, 18, 13, 3, "amber")
    s.rect(10, 18, 2, 3, "white")
    s.rect(21, 21, 1, 2, "red")
    for x in (7, 13, 19, 25):
        s.rect(x, 12, 1, 13, "steel")
    s.rect(24, 2, 4, 7, "rust")
    s.rect(25, 1, 2, 2, "dark")
    s.rect(6, 22, 5, 3, "sand")
    s.rect(20, 24, 7, 1, "rust")
    _bolts(s, 5, 10, 22, 3)


def _iron_blast_furnace(s):
    _base(s)
    s.rect(8, 13, 18, 13, "shadow")
    s.rect(10, 8, 14, 9, "steel")
    s.rect(7, 5, 20, 4, "rust")
    s.rect(10, 2, 14, 4, "steel")
    s.rect(12, 1, 10, 2, "dark")
    s.rect(12, 16, 10, 8, "dark")
    s.rect(14, 20, 6, 4, "amber")
    s.rect(16, 19, 2, 5, "gold")
    _pipe(s, 2, 11, 7, 4, "steel")
    _pipe(s, 25, 7, 3, 11, "rust")
    _pipe(s, 25, 17, 5, 3, "steel")
    s.rect(16, 5, 3, 4, "cream")
    s.rect(9, 13, 2, 6, "light")
    _bolts(s, 9, 8, 15, 9)


def _refining_forge(s):
    _base(s)
    s.oval(5, 9, 23, 15, "rust")
    s.oval(8, 11, 17, 9, "dark")
    s.oval(11, 13, 11, 6, "amber")
    s.rect(13, 15, 7, 3, "gold")
    s.rect(4, 16, 5, 9, "steel")
    s.rect(25, 14, 4, 11, "rust")
    _gear(s, 21, 7, 8, "steel")
    s.rect(7, 5, 9, 5, "brown")
    s.rect(9, 4, 5, 2, "tan")
    s.rect(16, 6, 3, 8, "dark")
    s.rect(3, 24, 7, 2, "brown")
    s.rect(23, 24, 6, 2, "brown")


def _machine_tool(s):
    _base(s, 2, 24, 28)
    s.rect(3, 18, 26, 5, "steel")
    s.rect(4, 16, 25, 2, "light")
    s.rect(3, 11, 7, 7, "dark")
    s.rect(7, 12, 4, 5, "rust")
    _wheel(s, 4, 12, 6, "steel")
    s.rect(12, 15, 2, 4, "dark")
    s.rect(13, 14, 12, 2, "light")
    s.rect(18, 12, 4, 3, "cream")
    s.rect(22, 12, 5, 2, "steel")
    s.rect(17, 18, 4, 3, "brown")
    s.rect(24, 8, 3, 9, "amber")
    s.rect(26, 6, 2, 3, "rust")
    s.rect(5, 23, 3, 2, "gold")


def _winding_bench(s):
    _base(s, 2, 24, 28)
    s.rect(4, 18, 25, 5, "tan")
    s.rect(4, 16, 25, 2, "sand")
    s.rect(5, 20, 3, 6, "brown")
    s.rect(25, 20, 3, 6, "brown")
    _wheel(s, 6, 6, 13, "amber")
    s.oval(9, 9, 7, 7, "rust")
    s.rect(11, 8, 3, 9, "amber")
    s.rect(19, 10, 7, 4, "steel")
    s.rect(20, 9, 5, 2, "light")
    s.rect(21, 14, 3, 4, "dark")
    s.rect(20, 17, 7, 2, "teal")
    s.rect(17, 7, 2, 9, "dark")
    s.rect(24, 17, 3, 2, "cream")
    s.dot(6, 18, "light")


def _filter_splitter(s):
    _base(s)
    s.rect(5, 11, 14, 13, "steel")
    s.rect(6, 9, 12, 4, "light")
    s.rect(8, 14, 9, 6, "dark")
    for y in (15, 18):
        s.rect(9, y, 7, 2, "cyan")
    s.rect(19, 15, 3, 5, "dark")
    s.rect(22, 8, 7, 4, "steel")
    s.rect(22, 12, 3, 6, "dark")
    s.rect(26, 17, 4, 4, "rust")
    s.rect(1, 16, 4, 4, "steel")
    s.rect(2, 17, 3, 2, "light")
    s.rect(22, 9, 4, 1, "teal")
    s.rect(11, 21, 7, 2, "sand")
    s.rect(14, 20, 2, 4, "gold")
    s.dot(24, 18, "red")


def _circuit_breaker(s):
    _base(s)
    s.rect(6, 6, 20, 19, "steel")
    s.rect(5, 5, 22, 3, "light")
    s.rect(8, 9, 16, 12, "dark")
    s.rect(10, 11, 12, 4, "cream")
    s.rect(11, 12, 4, 2, "red")
    s.rect(17, 12, 4, 2, "green")
    s.rect(14, 16, 4, 5, "rust")
    s.rect(15, 15, 2, 7, "amber")
    s.rect(8, 22, 16, 2, "sand")
    for x in (9, 14, 19, 24):
        s.rect(x, 23, 1, 3, "tan")
    _pipe(s, 2, 9, 4, 3, "blue")
    _pipe(s, 26, 9, 4, 3, "blue")
    s.rect(9, 4, 3, 2, "cream")
    s.rect(20, 4, 3, 2, "cream")
    s.rect(12, 18, 8, 1, "light")


def _electric_crusher(s):
    _base(s)
    s.rect(5, 9, 22, 15, "steel")
    s.rect(7, 7, 18, 4, "light")
    s.rect(9, 12, 14, 3, "dark")
    s.rect(10, 15, 5, 5, "rust")
    s.rect(17, 15, 5, 5, "rust")
    s.rect(15, 20, 2, 4, "dark")
    s.rect(12, 18, 3, 2, "gold")
    s.rect(18, 18, 3, 2, "gold")
    _gear(s, 22, 14, 8, "steel")
    s.rect(3, 16, 5, 8, "brown")
    s.rect(2, 17, 2, 5, "amber")
    _pipe(s, 24, 8, 3, 7, "blue")
    s.rect(7, 13, 2, 5, "light")
    s.dot(13, 22, "gold")
    s.dot(21, 23, "gold")


def _oil_pump(s):
    _base(s, 2, 26, 28)
    s.rect(4, 21, 24, 4, "dark")
    s.rect(6, 18, 8, 5, "steel")
    s.rect(7, 14, 6, 5, "rust")
    s.line(13, 21, 8, 8, "steel", 2)
    s.line(24, 21, 15, 7, "steel", 2)
    s.rect(6, 6, 19, 3, "light")
    s.rect(5, 7, 4, 5, "dark")
    s.rect(23, 7, 4, 7, "rust")
    s.rect(25, 13, 3, 7, "amber")
    s.rect(7, 5, 4, 2, "steel")
    s.rect(19, 4, 5, 3, "dark")
    _pipe(s, 1, 23, 5, 3, "brown")
    s.rect(12, 20, 7, 2, "tan")
    s.rect(15, 22, 2, 4, "dark")


def _liquid_pipe(s):
    s.rect(2, 20, 25, 7, "steel")
    s.rect(3, 19, 23, 2, "light")
    s.rect(24, 9, 7, 18, "steel")
    s.rect(23, 8, 3, 20, "light")
    s.rect(4, 22, 20, 3, "blue")
    s.rect(25, 11, 3, 12, "blue")
    s.rect(6, 22, 2, 3, "cyan")
    s.rect(12, 22, 2, 3, "cyan")
    s.rect(18, 22, 2, 3, "cyan")
    s.rect(25, 13, 3, 2, "cyan")
    s.rect(25, 19, 3, 2, "cyan")
    _flange(s, 2, 18, 4, 11, "light")
    _flange(s, 25, 7, 5, 4, "light")
    _flange(s, 25, 25, 5, 4, "light")
    s.rect(14, 18, 3, 4, "rust")
    s.rect(15, 17, 1, 6, "amber")
    s.rect(4, 29, 20, 1, "dark")


def _liquid_tank(s):
    _base(s)
    _tank(s, 4, 7, 24, 18, "steel")
    s.rect(5, 14, 22, 3, "rust")
    s.rect(7, 11, 18, 2, "cyan")
    s.rect(9, 10, 2, 4, "light")
    s.rect(22, 18, 3, 5, "dark")
    _pipe(s, 1, 18, 21, 3, "steel")
    _pipe(s, 20, 17, 3, 10, "steel")
    _flange(s, 2, 17, 3, 5, "light")
    s.rect(4, 22, 3, 4, "brown")
    s.rect(22, 22, 3, 4, "brown")
    for x in (9, 13, 9 + 8):
        s.rect(x, 21, 2, 1, "sand")
    s.rect(24, 13, 2, 5, "teal")


def _distillation_unit(s):
    _base(s, 2, 26, 28)
    s.rect(3, 22, 18, 4, "rust")
    s.rect(5, 19, 14, 4, "dark")
    s.rect(7, 21, 10, 1, "amber")
    _tank(s, 5, 4, 9, 17, "steel")
    for y in (8, 11, 14, 17):
        s.rect(6, y, 7, 1, "light")
    _tank(s, 16, 8, 7, 14, "rust")
    s.rect(18, 6, 3, 5, "dark")
    _pipe(s, 12, 8, 5, 3, "steel")
    _pipe(s, 22, 12, 5, 3, "steel")
    _pipe(s, 25, 12, 3, 13, "steel")
    s.rect(8, 2, 3, 4, "dark")
    s.rect(18, 4, 3, 4, "amber")
    s.rect(23, 20, 5, 4, "steel")
    s.rect(24, 21, 3, 1, "cyan")
    s.rect(1, 22, 3, 4, "brown")


def _chemical_reactor(s):
    _base(s)
    _tank(s, 4, 9, 12, 16, "steel")
    _tank(s, 18, 5, 10, 20, "cream")
    s.rect(7, 14, 6, 7, "teal")
    s.rect(20, 10, 6, 10, "cyan")
    s.rect(21, 12, 4, 2, "light")
    s.rect(21, 17, 4, 2, "light")
    s.rect(8, 5, 4, 6, "dark")
    s.rect(9, 3, 2, 3, "rust")
    s.rect(23, 2, 2, 5, "dark")
    s.rect(21, 1, 6, 2, "steel")
    _pipe(s, 14, 11, 6, 3, "steel")
    _pipe(s, 3, 20, 2, 7, "rust")
    _pipe(s, 27, 18, 3, 8, "rust")
    s.rect(10, 22, 3, 2, "amber")
    s.rect(24, 24, 2, 2, "gold")


def _process_water_pump(s):
    s.rect(2, 24, 28, 5, "blue")
    s.rect(4, 27, 25, 1, "cyan")
    s.oval(4, 9, 16, 16, "steel")
    s.oval(7, 11, 10, 12, "rust")
    s.rect(8, 15, 2, 5, "light")
    s.rect(13, 16, 15, 5, "steel")
    s.rect(17, 13, 6, 8, "rust")
    s.rect(19, 11, 3, 4, "light")
    s.rect(20, 5, 8, 3, "steel")
    s.rect(26, 6, 3, 9, "steel")
    _pipe(s, 16, 22, 15, 3, "blue")
    s.rect(19, 21, 9, 2, "cyan")
    s.rect(5, 22, 5, 2, "brown")
    s.rect(13, 20, 4, 2, "amber")


def _silicon_refiner(s):
    _base(s)
    s.rect(5, 12, 15, 14, "rust")
    s.oval(6, 8, 13, 11, "steel")
    s.oval(9, 10, 7, 6, "amber")
    s.oval(11, 11, 3, 3, "gold")
    s.rect(17, 5, 10, 8, "dark")
    s.rect(19, 6, 6, 4, "ice")
    s.rect(21, 7, 2, 2, "cyan")
    _pipe(s, 20, 13, 7, 3, "steel")
    s.rect(7, 16, 11, 7, "dark")
    s.rect(9, 18, 7, 4, "teal")
    for x, y in ((8, 16), (14, 18), (11, 20)):
        s.rect(x, y, 2, 2, "ice")
    s.rect(22, 17, 5, 9, "steel")
    s.rect(23, 19, 3, 5, "cream")
    s.rect(4, 23, 4, 3, "rust")
    s.rect(14, 23, 9, 2, "gold")


def _electronics_bench(s):
    _base(s, 2, 24, 28)
    s.rect(3, 18, 26, 5, "tan")
    s.rect(4, 20, 3, 6, "brown")
    s.rect(25, 20, 3, 6, "brown")
    s.rect(6, 13, 10, 6, "dark")
    s.rect(7, 14, 8, 3, "leaf")
    s.rect(8, 15, 2, 1, "gold")
    s.rect(12, 14, 2, 1, "gold")
    s.rect(7, 17, 8, 1, "cyan")
    s.rect(18, 9, 8, 10, "steel")
    s.rect(19, 10, 6, 4, "blue")
    s.rect(20, 11, 4, 2, "ice")
    s.rect(21, 15, 2, 3, "light")
    s.rect(18, 17, 8, 2, "cream")
    s.rect(17, 6, 2, 8, "dark")
    s.line(17, 6, 13, 4, "amber")
    s.rect(12, 3, 2, 2, "gold")
    for x in (6, 9, 12, 15):
        s.rect(x, 18, 1, 2, "rust")
    s.rect(21, 18, 5, 2, "teal")


def _reactor_control_console(s):
    _base(s)
    s.rect(3, 10, 24, 15, "steel")
    s.rect(4, 8, 22, 4, "light")
    s.rect(6, 12, 19, 8, "dark")
    s.oval(8, 13, 5, 5, "cream")
    s.rect(10, 13, 1, 4, "red")
    s.oval(16, 13, 5, 5, "blue")
    s.rect(18, 14, 1, 3, "cyan")
    s.rect(23, 13, 3, 6, "amber")
    s.rect(8, 20, 16, 2, "sand")
    for x in (9, 14, 19):
        s.rect(x, 21, 2, 3, "dark")
    s.line(23, 12, 25, 10, "red", 2)
    s.rect(24, 7, 3, 3, "gold")
    s.rect(5, 5, 5, 3, "teal")
    s.rect(12, 5, 9, 3, "steel")
    s.rect(13, 6, 7, 1, "white")
    s.rect(4, 22, 22, 1, "rust")


def _nuclear_waste_vault(s):
    _base(s)
    s.rect(4, 8, 24, 18, "cream")
    s.rect(3, 6, 26, 4, "steel")
    s.rect(5, 10, 22, 13, "sand")
    s.rect(7, 12, 18, 9, "dark")
    s.oval(12, 12, 8, 8, "gold")
    s.oval(14, 14, 4, 4, "dark")
    for x, y, w, h in ((12, 13, 2, 6), (17, 13, 2, 6),
                       (14, 11, 4, 2), (14, 19, 4, 2)):
        s.rect(x, y, w, h, "dark")
    s.rect(6, 22, 4, 3, "red")
    s.rect(20, 22, 5, 3, "red")
    s.rect(9, 5, 4, 2, "tan")
    s.rect(19, 5, 4, 2, "tan")
    _pipe(s, 1, 15, 4, 3, "steel")
    _pipe(s, 27, 15, 4, 3, "steel")
    _bolts(s, 4, 8, 24, 18)


def _large_steam_furnace(s):
    s.rect(3, 24, 26, 4, "dark")
    _tank(s, 4, 13, 9, 12, "steel")
    s.rect(7, 8, 18, 12, "rust")
    s.rect(10, 3, 11, 7, "steel")
    s.rect(12, 1, 7, 2, "dark")
    s.rect(18, 5, 10, 6, "steel")
    s.rect(23, 2, 3, 4, "dark")
    s.rect(21, 10, 8, 4, "rust")
    _pipe(s, 2, 19, 9, 3, "steel")
    _pipe(s, 13, 14, 18, 3, "steel")
    _pipe(s, 27, 9, 2, 9, "steel")
    s.rect(9, 16, 8, 5, "dark")
    s.rect(11, 18, 4, 2, "amber")
    _vent(s, 6, 14, 5, 6)
    _bolts(s, 7, 8, 18, 9)


def _large_assembly_plant(s):
    s.rect(2, 25, 28, 3, "dark")
    s.rect(4, 7, 24, 18, "steel")
    s.rect(3, 5, 26, 4, "light")
    s.rect(6, 9, 20, 13, "tan")
    for x in (7, 15, 23):
        s.rect(x, 8, 2, 15, "steel")
    s.rect(8, 14, 17, 5, "dark")
    for x in (9, 14, 19):
        s.rect(x, 15, 3, 2, "amber")
    s.rect(8, 10, 2, 4, "rust")
    s.line(10, 11, 15, 13, "steel", 2)
    s.rect(19, 8, 2, 6, "rust")
    s.line(21, 10, 25, 13, "steel", 2)
    s.rect(4, 20, 24, 3, "teal")
    s.rect(5, 22, 22, 1, "cyan")
    for x in (7, 13, 19, 25):
        s.dot(x, 11, "gold")
    s.rect(3, 17, 4, 6, "steel")
    s.rect(25, 16, 4, 7, "rust")


def _large_ore_processor(s):
    s.rect(2, 25, 28, 3, "dark")
    s.rect(3, 11, 15, 14, "steel")
    s.rect(5, 8, 11, 5, "light")
    s.rect(7, 12, 7, 5, "dark")
    s.rect(8, 13, 2, 3, "shadow")
    s.rect(12, 13, 2, 3, "rust")
    _gear(s, 4, 16, 7, "tan")
    s.rect(17, 18, 12, 5, "dark")
    s.rect(18, 19, 10, 2, "steel")
    for x in (19, 22, 25):
        s.rect(x, 19, 1, 2, "amber")
    s.rect(19, 8, 9, 9, "dark")
    s.oval(21, 4, 6, 7, "shadow")
    for x, y in ((20, 7), (24, 5), (25, 9)):
        s.rect(x, y, 2, 2, "steel")
    _pipe(s, 15, 12, 6, 3, "steel")
    s.rect(27, 18, 3, 7, "brown")
    s.rect(3, 22, 13, 2, "rust")


def _research_bench(s):
    _base(s, 2, 25, 28)
    s.rect(4, 18, 24, 5, "tan")
    s.rect(5, 17, 23, 2, "sand")
    s.rect(6, 21, 3, 6, "brown")
    s.rect(24, 21, 3, 6, "brown")
    s.rect(6, 11, 9, 7, "cream")
    s.rect(7, 12, 3, 5, "white")
    s.rect(11, 12, 3, 5, "white")
    s.rect(7, 13, 2, 1, "steel")
    s.rect(12, 14, 1, 2, "rust")
    s.rect(15, 14, 8, 4, "ice")
    s.rect(16, 15, 6, 2, "blue")
    s.rect(18, 12, 1, 2, "steel")
    s.rect(21, 13, 1, 5, "steel")
    s.rect(18, 5, 8, 8, "steel")
    s.rect(19, 6, 6, 5, "dark")
    s.rect(20, 7, 4, 2, "cyan")
    s.rect(27, 8, 2, 9, "brown")
    s.rect(9, 6, 3, 7, "rust")
    s.rect(10, 5, 1, 3, "amber")
    s.rect(15, 20, 4, 1, "dark")
    s.rect(20, 19, 4, 1, "gold")


_DRAW = {
    36: _charcoal_kiln,
    37: _clay_kiln,
    38: _blast_furnace,
    39: _mechanical_press,
    40: _mechanical_workshop,
    41: _water_mill,
    42: _irrigation_pump,
    43: _mechanical_hopper,
    44: _coke_oven,
    45: _refractory_kiln,
    46: _iron_blast_furnace,
    47: _refining_forge,
    48: _machine_tool,
    49: _winding_bench,
    50: _filter_splitter,
    51: _circuit_breaker,
    52: _electric_crusher,
    53: _oil_pump,
    54: _liquid_pipe,
    55: _liquid_tank,
    56: _distillation_unit,
    57: _chemical_reactor,
    58: _process_water_pump,
    59: _silicon_refiner,
    60: _electronics_bench,
    61: _reactor_control_console,
    62: _nuclear_waste_vault,
    63: _large_steam_furnace,
    64: _large_assembly_plant,
    65: _large_ore_processor,
    66: _research_bench,
}


def machine(index):
    name = "machine_" + str(index)
    label = MACHINE_ZH[index - 36] + " / " + MACHINES[index - 36]
    sprite = Sprite(name, label)
    _DRAW[index](sprite)
    return sprite


def all_machines():
    sprites = [machine(index) for index in range(36, 67)]
    assert len(sprites) == 31
    assert [sprite.name for sprite in sprites] == [
        "machine_" + str(index) for index in range(36, 67)
    ]
    assert [sprite.label for sprite in sprites] == [
        chinese + " / " + english
        for chinese, english in zip(MACHINE_ZH, MACHINES)
    ]
    pixels = set()
    silhouettes = set()
    for sprite in sprites:
        final = sprite.final()
        assert (final >= 0).any(), sprite.name + " is empty"
        pixel_key = tuple(int(value) for value in final.ravel())
        assert pixel_key not in pixels, sprite.name + " duplicates another sprite"
        pixels.add(pixel_key)
        silhouette_key = tuple(int(value) for value in (final >= 0).ravel())
        assert silhouette_key not in silhouettes, (
            sprite.name + " duplicates another machine silhouette"
        )
        silhouettes.add(silhouette_key)
    return sprites
