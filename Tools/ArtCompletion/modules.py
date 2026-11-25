"""Dedicated 32 px module-role mechanisms for modular facilities.

These are functional role illustrations rather than miniature copies of whole
buildings. Each sprite is transparent outside its silhouette and uses only the
shared pixel_svg palette so it can join the completion atlas unchanged.
"""
from pixel_svg import Sprite


MODULES = (
    ("module_controller", "Controller / 控制器"),
    ("module_casing", "Casing / 机壳"),
    ("module_input", "Input bus / 输入总线"),
    ("module_output", "Output bus / 输出总线"),
    ("module_maintenance", "Maintenance / 维护舱"),
    ("module_exhaust", "Exhaust / 排气口"),
    ("module_energy_steam", "Steam energy / 蒸汽能源舱"),
    ("module_energy_electric", "Electric energy / 电力能源舱"),
    ("module_process_furnace", "Furnace process / 炉膛工艺核心"),
    ("module_process_assembly", "Assembly process / 装配工艺核心"),
    ("module_process_ore", "Ore process / 矿石工艺核心"),
)


def _bolts(s, left, top, right, bottom, color="sand"):
    for x, y in ((left, top), (right, top), (left, bottom), (right, bottom)):
        s.rect(x, y, 2, 2, color)


def _controller(s):
    s.rect(4, 5, 24, 21, "dark")
    s.rect(5, 6, 22, 19, "steel")
    s.rect(6, 7, 20, 3, "light")
    s.rect(7, 11, 13, 9, "ink")
    s.rect(8, 12, 11, 7, "blue")
    s.rect(9, 13, 4, 4, "dark")
    s.rect(10, 14, 1, 2, "red")
    s.rect(9, 16, 4, 1, "cyan")
    s.rect(14, 13, 4, 4, "shadow")
    s.rect(15, 14, 1, 2, "gold")
    s.rect(14, 16, 4, 1, "teal")
    s.rect(8, 21, 11, 2, "shadow")
    for x, color in ((9, "red"), (13, "amber"), (17, "teal")):
        s.rect(x, 22, 3, 2, color)
    s.rect(21, 10, 4, 12, "shadow")
    for y, color in ((11, "red"), (14, "gold"), (17, "teal")):
        s.rect(22, y, 2, 2, color)
    s.rect(7, 26, 3, 3, "dark")
    s.rect(22, 26, 3, 3, "dark")
    s.rect(12, 3, 8, 2, "steel")
    s.rect(14, 2, 4, 2, "light")


def _casing(s):
    s.rect(3, 3, 26, 26, "dark")
    s.rect(4, 4, 24, 24, "steel")
    s.rect(5, 5, 22, 3, "light")
    s.rect(5, 24, 22, 3, "shadow")
    s.rect(8, 9, 16, 14, "shadow")
    s.rect(10, 11, 12, 10, "dark")
    s.rect(12, 13, 8, 6, "tan")
    s.rect(14, 15, 4, 2, "gold")
    s.rect(2, 8, 2, 16, "steel")
    s.rect(28, 8, 2, 16, "steel")
    s.rect(8, 2, 16, 2, "light")
    s.rect(8, 28, 16, 2, "shadow")
    for x, y in ((7, 7), (23, 7), (7, 23), (23, 23)):
        s.rect(x, y, 2, 2, "sand")
    s.rect(18, 10, 2, 12, "light")
    s.rect(11, 20, 10, 1, "dark")


def _input(s):
    s.rect(3, 5, 17, 4, "light")
    s.rect(4, 9, 15, 4, "steel")
    s.rect(6, 13, 11, 4, "shadow")
    s.rect(8, 17, 7, 5, "dark")
    s.rect(9, 22, 12, 4, "steel")
    s.rect(11, 20, 8, 2, "amber")
    s.oval(13, 11, 9, 9, "steel")
    s.oval(15, 13, 5, 5, "ink")
    s.rect(17, 14, 1, 3, "gold")
    s.rect(20, 18, 8, 5, "dark")
    s.rect(21, 19, 6, 2, "light")
    for x in (22, 25):
        s.rect(x, 21, 1, 2, "cyan")
    s.rect(5, 24, 3, 3, "dark")
    s.rect(24, 24, 3, 3, "dark")
    s.line(4, 15, 8, 19, "tan", 2)
    s.rect(3, 18, 3, 2, "gold")


def _output(s):
    s.rect(3, 12, 9, 9, "dark")
    s.rect(4, 13, 7, 7, "steel")
    s.rect(6, 15, 3, 3, "shadow")
    s.rect(11, 13, 10, 7, "steel")
    s.rect(12, 14, 8, 2, "light")
    for x in (13, 16, 19):
        s.rect(x, 17, 2, 1, "cyan")
    s.rect(21, 10, 7, 11, "dark")
    s.rect(23, 9, 5, 3, "steel")
    s.rect(24, 12, 3, 6, "amber")
    s.rect(22, 20, 8, 4, "steel")
    s.rect(24, 21, 6, 1, "light")
    s.oval(13, 5, 9, 8, "steel")
    s.oval(15, 7, 5, 4, "ink")
    s.line(18, 12, 21, 15, "tan", 2)
    s.rect(5, 23, 3, 3, "dark")
    s.rect(25, 23, 3, 3, "dark")


def _maintenance(s):
    s.rect(4, 4, 24, 23, "dark")
    s.rect(5, 5, 22, 21, "steel")
    s.rect(6, 6, 20, 3, "light")
    s.rect(8, 10, 16, 11, None)
    s.rect(9, 11, 14, 9, "shadow")
    s.rect(11, 13, 10, 5, "ink")
    s.rect(12, 14, 8, 3, "dark")
    _bolts(s, 7, 7, 23, 23)
    s.line(5, 22, 25, 8, "tan", 3)
    s.line(6, 23, 24, 10, "sand")
    s.oval(22, 4, 6, 7, "steel")
    s.oval(24, 6, 3, 3, "ink")
    s.oval(4, 20, 6, 7, "steel")
    s.rect(6, 22, 2, 3, "ink")
    s.rect(12, 26, 9, 2, "rust")
    s.rect(15, 25, 3, 3, "gold")


def _exhaust(s):
    s.rect(3, 18, 26, 6, "dark")
    s.rect(5, 19, 22, 4, "steel")
    s.rect(7, 20, 18, 1, "light")
    s.rect(12, 6, 8, 13, "shadow")
    s.rect(13, 7, 6, 12, "steel")
    s.rect(14, 8, 2, 10, "light")
    s.rect(11, 4, 10, 4, "dark")
    s.rect(12, 3, 8, 3, "steel")
    s.oval(7, 2, 7, 5, "light")
    s.oval(17, 2, 7, 5, "white")
    s.rect(15, 10, 2, 8, "rust")
    for y in (12, 15, 18):
        s.rect(4, y, 4, 2, "steel")
        s.rect(24, y, 4, 2, "steel")
    s.rect(6, 25, 4, 2, "dark")
    s.rect(22, 25, 4, 2, "dark")


def _energy_steam(s):
    s.rect(2, 15, 28, 5, "steel")
    s.rect(3, 16, 26, 2, "light")
    s.rect(6, 20, 20, 5, "dark")
    s.rect(8, 21, 16, 3, "steel")
    s.oval(7, 4, 17, 16, "tan")
    s.oval(9, 6, 13, 12, "dark")
    s.oval(12, 9, 7, 6, "steel")
    s.rect(15, 2, 2, 6, "steel")
    s.rect(12, 8, 8, 2, "light")
    s.rect(15, 6, 2, 10, "light")
    s.rect(11, 10, 3, 2, "light")
    s.rect(18, 10, 3, 2, "light")
    s.rect(24, 6, 5, 8, "dark")
    s.rect(25, 7, 3, 5, "cream")
    s.rect(26, 8, 1, 3, "red")
    s.rect(3, 13, 4, 3, "rust")
    s.rect(27, 13, 3, 3, "rust")
    s.rect(5, 25, 4, 3, "dark")
    s.rect(23, 25, 4, 3, "dark")


def _energy_electric(s):
    s.rect(6, 5, 21, 20, "dark")
    s.rect(7, 6, 19, 18, "steel")
    s.rect(8, 7, 17, 3, "light")
    s.rect(11, 11, 11, 9, "shadow")
    for x in (12, 15, 18):
        s.rect(x, 12, 1, 3, "ink")
    s.rect(12, 17, 9, 1, "dark")
    s.line(25, 9, 28, 13, "cyan")
    s.line(28, 13, 26, 18, "cyan")
    s.rect(25, 18, 4, 3, "blue")
    s.rect(4, 11, 4, 9, "rust")
    s.rect(2, 13, 3, 5, "steel")
    s.rect(3, 14, 2, 1, "gold")
    s.line(5, 20, 8, 25, "tan", 2)
    s.rect(7, 24, 5, 3, "steel")
    s.rect(13, 22, 9, 2, "teal")
    s.rect(8, 27, 4, 2, "dark")
    s.rect(23, 27, 3, 2, "dark")


def _process_furnace(s):
    s.rect(4, 8, 24, 20, "dark")
    s.rect(5, 9, 22, 18, "brown")
    s.rect(6, 10, 20, 3, "tan")
    s.rect(8, 14, 16, 10, "ink")
    s.rect(10, 17, 12, 7, "amber")
    s.rect(12, 18, 8, 5, "gold")
    s.rect(14, 19, 4, 4, "white")
    s.oval(5, 4, 7, 8, "shadow")
    s.rect(6, 3, 5, 5, "steel")
    s.rect(22, 5, 4, 8, "rust")
    s.rect(23, 3, 2, 3, "dark")
    for x, y in ((7, 12), (23, 12), (7, 25), (23, 25)):
        s.rect(x, y, 2, 2, "sand")
    s.rect(3, 26, 26, 3, "shadow")
    s.rect(8, 27, 6, 2, "dark")
    s.rect(18, 27, 6, 2, "dark")


def _process_assembly(s):
    s.rect(3, 5, 26, 4, "dark")
    s.rect(4, 6, 24, 2, "light")
    s.rect(5, 9, 4, 15, "steel")
    s.rect(23, 9, 4, 15, "steel")
    s.rect(8, 9, 16, 3, "shadow")
    s.rect(10, 12, 3, 8, "rust")
    s.rect(19, 12, 3, 8, "steel")
    s.rect(12, 13, 9, 3, "dark")
    s.rect(15, 16, 3, 4, "amber")
    s.rect(14, 20, 5, 3, "steel")
    s.rect(12, 23, 3, 2, "gold")
    s.rect(18, 23, 3, 2, "gold")
    s.rect(8, 24, 16, 3, "dark")
    s.rect(10, 25, 12, 1, "teal")
    s.rect(3, 26, 5, 2, "steel")
    s.rect(24, 26, 5, 2, "steel")
    for x in (7, 25):
        s.rect(x, 16, 2, 3, "amber")


def _process_ore(s):
    s.rect(4, 5, 24, 5, "light")
    s.rect(6, 10, 20, 4, "steel")
    s.rect(9, 14, 14, 4, "shadow")
    s.rect(5, 18, 22, 7, "dark")
    s.rect(7, 19, 18, 5, "steel")
    s.oval(7, 13, 9, 10, "steel")
    s.oval(17, 13, 9, 10, "steel")
    for x, y in ((8, 16), (11, 18), (18, 16), (21, 18)):
        s.rect(x, y, 2, 2, "dark")
    for x, y in ((10, 8), (16, 9), (21, 7), (12, 15), (20, 14)):
        s.rect(x, y, 2, 2, "tan")
    s.rect(13, 12, 6, 3, "rust")
    s.rect(14, 22, 4, 3, "amber")
    s.rect(6, 25, 4, 3, "dark")
    s.rect(22, 25, 4, 3, "dark")
    s.rect(11, 28, 10, 2, "shadow")


_DRAW = {
    "module_controller": _controller,
    "module_casing": _casing,
    "module_input": _input,
    "module_output": _output,
    "module_maintenance": _maintenance,
    "module_exhaust": _exhaust,
    "module_energy_steam": _energy_steam,
    "module_energy_electric": _energy_electric,
    "module_process_furnace": _process_furnace,
    "module_process_assembly": _process_assembly,
    "module_process_ore": _process_ore,
}


def all_modules():
    """Return module role art in the fixed completion-atlas order."""
    sprites = []
    for name, label in MODULES:
        sprite = Sprite(name, label)
        _DRAW[name](sprite)
        sprites.append(sprite)

    assert len(sprites) == 11
    assert [sprite.name for sprite in sprites] == [name for name, _ in MODULES]
    pixels = set()
    silhouettes = set()
    for sprite in sprites:
        final = sprite.final()
        assert final.shape == (32, 32), sprite.name + " is not 32x32"
        assert (final >= 0).any(), sprite.name + " is empty"
        pixel_key = tuple(int(value) for value in final.ravel())
        silhouette_key = tuple(bool(value >= 0) for value in final.ravel())
        assert pixel_key not in pixels, sprite.name + " duplicates another frame"
        assert silhouette_key not in silhouettes, sprite.name + " duplicates another silhouette"
        pixels.add(pixel_key)
        silhouettes.add(silhouette_key)
    return sprites
