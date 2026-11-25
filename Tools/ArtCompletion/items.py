"""Hand-authored 32px SVG sprite grids for the remaining items and gear."""

from pixel_svg import Sprite


ITEMS = (
    (25, 'Stone', '石材'),
    (26, 'Clay', '黏土'),
    (27, 'Charcoal', '木炭'),
    (28, 'Brick', '陶砖'),
    (29, 'IronPlate', '铁板'),
    (30, 'Gear', '齿轮'),
    (31, 'Bearing', '轴承'),
    (32, 'Coal', '煤'),
    (33, 'Limestone', '石灰石'),
    (34, 'FireClay', '耐火黏土'),
    (35, 'Coke', '焦炭'),
    (36, 'RefractoryBrick', '耐火砖'),
    (37, 'PigIron', '生铁'),
    (38, 'SteelPlate', '钢板'),
    (39, 'PipeFitting', '耐压管件'),
    (40, 'CopperWire', '铜线'),
    (41, 'Insulator', '陶瓷绝缘件'),
    (42, 'Cable', '绝缘电缆'),
    (43, 'Coil', '线圈'),
    (44, 'Motor', '电机'),
    (45, 'Relay', '继电器'),
    (46, 'CrudeOil', '原油'),
    (47, 'LightFraction', '轻质馏分'),
    (48, 'ChemicalFeedstock', '化工原料'),
    (49, 'HeavyFraction', '重质馏分'),
    (50, 'Diesel', '柴油'),
    (51, 'Lubricant', '润滑油'),
    (52, 'ProcessWater', '工艺水'),
    (53, 'HotCoolant', '热冷却液'),
    (54, 'Quartz', '石英'),
    (55, 'IndustrialSilicon', '工业硅'),
    (56, 'PureSilicon', '高纯硅'),
    (57, 'Wafer', '晶圆'),
    (58, 'Semiconductor', '半导体器件'),
    (59, 'PrecisionPart', '精密部件'),
    (60, 'ControlModule', '控制模块'),
    (61, 'HeatAlloy', '耐热合金'),
    (62, 'UraniumConcentrate', '铀精矿'),
    (63, 'Branch', '树枝'),
    (64, 'LooseStone', '石头'),
    (65, 'DryGrass', '干草'),
)

GEAR = (
    (19, 'RadiationSuit', '核作业防护服'),
    (20, 'GrassBackpack', '草绳背包'),
    (21, 'ReinforcedBackpack', '加固背包'),
    (22, 'EngineerBackpack', '工程背包'),
)


def _sprite(kind, index, english, chinese):
    return Sprite(f'{kind}_{index}', f'{english} / {chinese}')


def _stacked_brick(s, y=10, base='amber', light='gold', seam='brown'):
    s.rect(5, y + 8, 23, 8, base)
    s.rect(5, y + 8, 23, 2, light)
    s.rect(9, y + 12, 3, 1, seam)
    s.rect(19, y + 12, 3, 1, seam)
    s.rect(9, y, 15, 7, base)
    s.rect(9, y, 15, 2, light)
    s.rect(11, y + 5, 4, 1, seam)
    s.rect(21, y + 4, 1, 3, seam)


def _metal_plate(s, base, light, shade, rivets=True):
    s.rect(5, 9, 22, 15, base)
    s.rect(5, 9, 22, 3, light)
    s.rect(5, 21, 22, 3, shade)
    s.rect(8, 7, 4, 2, light)
    s.rect(23, 7, 3, 2, shade)
    if rivets:
        for x, y in ((8, 13), (23, 13), (8, 20), (23, 20)):
            s.rect(x, y, 2, 2, 'cream')


def _gear(s):
    for x, y, w, h in (
        (13, 2, 6, 5), (13, 25, 6, 5),
        (3, 13, 5, 6), (24, 13, 5, 6),
        (6, 6, 5, 5), (21, 6, 5, 5),
        (6, 21, 5, 5), (21, 21, 5, 5),
    ):
        s.rect(x, y, w, h, 'steel')
        s.rect(x, y, max(2, w - 2), 2, 'light')
    s.rect(7, 9, 18, 14, 'steel')
    s.rect(9, 7, 14, 18, 'steel')
    s.rect(9, 9, 14, 13, 'light')
    s.oval(11, 11, 10, 10, 'dark')
    s.oval(13, 13, 6, 6, None)
    s.rect(10, 12, 3, 2, 'cream')
    s.rect(19, 19, 3, 2, 'shadow')


def _bearing(s):
    s.rect(5, 9, 22, 15, 'steel')
    s.rect(7, 6, 18, 3, 'light')
    s.rect(7, 24, 18, 3, 'shadow')
    s.oval(8, 8, 16, 16, 'dark')
    s.oval(10, 10, 12, 12, 'light')
    s.oval(13, 13, 6, 6, None)
    for x, y in ((9, 14), (14, 8), (21, 13), (15, 21), (21, 20), (8, 20)):
        s.oval(x, y, 3, 3, 'gold')
    s.rect(12, 10, 3, 1, 'cream')


def _lump(s, left, top, rows, base, light, shade):
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch != ' ':
                s.dot(left + dx, top + dy, base)
    # Highlights follow the authored rock contour; never grow disconnected bars.
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch != ' ' and (dy == 0 or dx == 0 or row[dx - 1] == ' '):
                s.dot(left + dx, top + dy, light)
            elif ch != ' ' and dy == len(rows) - 1:
                s.dot(left + dx, top + dy, shade)


def _vial(s, liquid, glass='ice', cap='steel', level=11):
    s.rect(10, 7, 12, 19, glass)
    s.rect(12, 9, 8, level, liquid)
    s.rect(12, 9 + level - 2, 8, 2, 'white')
    s.rect(8, 5, 16, 4, cap)
    s.rect(9, 4, 14, 2, 'light')
    s.rect(10, 25, 12, 2, 'steel')
    s.rect(11, 10, 2, level - 2, 'white')


def _barrel(s, liquid, highlight, band='steel', wide=False):
    x = 7 if wide else 9
    w = 18 if wide else 14
    h = 18 if wide else 18
    y = 8 if wide else 8
    s.rect(x, y + 2, w, h, 'steel')
    s.rect(x + 2, y, w - 4, h + 4, 'light')
    s.rect(x + 1, y + 6, w - 2, 3, band)
    s.rect(x + 1, y + h - 1, w - 2, 3, 'shadow')
    s.rect(x + 4, y + 11, w - 8, 7, liquid)
    s.rect(x + 4, y + 11, w - 8, 2, highlight)
    s.rect(x + 3, y + 3, 2, h - 1, 'cream')


def _item(i):
    english, chinese = next((name, zh) for n, name, zh in ITEMS if n == i)
    s = _sprite('item', i, english, chinese)

    if i == 25:
        _lump(s, 4, 16, ('   #####   ', ' ######### ', '###########', '###########',
                           '###########', ' ######### ', '  #######  '), 'light', 'white', 'steel')
        _lump(s, 16, 10, ('  #####  ', ' ####### ', '#########', '#########',
                            '#########', ' ####### ', '  #####  '), 'steel', 'light', 'shadow')
        s.rect(8, 18, 3, 2, 'cream').rect(20, 12, 2, 2, 'cream')
    elif i == 26:
        s.oval(4, 16, 24, 10, 'tan')
        s.rect(7, 13, 18, 7, 'sand')
        s.rect(10, 11, 12, 5, 'tan')
        s.rect(10, 12, 7, 2, 'cream')
        s.line(9, 20, 13, 23, 'brown')
        s.line(18, 18, 16, 23, 'brown')
        s.rect(20, 20, 5, 2, 'brown')
    elif i == 27:
        s.line(4, 25, 16, 7, 'dark', 4)
        s.line(8, 26, 24, 11, 'shadow', 3)
        s.line(12, 9, 24, 25, 'brown', 3)
        s.rect(6, 20, 8, 5, 'dark')
        s.rect(18, 15, 8, 5, 'shadow')
        s.rect(12, 11, 3, 9, 'rust')
        s.rect(9, 20, 2, 5, 'amber')
        s.rect(20, 17, 2, 5, 'amber')
    elif i == 28:
        _stacked_brick(s, 10)
    elif i == 29:
        _metal_plate(s, 'steel', 'light', 'dark')
        s.rect(11, 12, 10, 8, 'shadow')
        s.rect(13, 13, 6, 6, None)
        s.line(23, 10, 23, 22, 'cream')
    elif i == 30:
        _gear(s)
    elif i == 31:
        _bearing(s)
    elif i == 32:
        _lump(s, 4, 11, ('   ####    ', '  #######  ', ' ######### ', '###########',
                           '###########', ' ######### ', '  #######  ', '   #####   '), 'dark', 'steel', 'shadow')
        s.rect(9, 16, 5, 2, 'light')
        s.rect(17, 20, 4, 2, 'steel')
        _lump(s, 17, 19, ('  ####  ', ' ###### ', '########', '########', ' ###### '), 'dark', 'steel', 'shadow')
    elif i == 33:
        s.rect(6, 9, 17, 16, 'cream')
        s.rect(8, 7, 13, 3, 'white')
        s.rect(6, 21, 17, 4, 'sand')
        s.rect(11, 12, 4, 5, 'light')
        s.line(19, 10, 21, 15, 'tan')
        s.line(10, 19, 15, 21, 'tan')
        s.rect(20, 17, 7, 8, 'light')
        s.rect(23, 14, 5, 8, 'sand')
    elif i == 34:
        s.rect(7, 9, 19, 15, 'rust')
        s.rect(9, 7, 15, 3, 'rose')
        s.rect(7, 21, 19, 4, 'brown')
        s.rect(13, 13, 7, 8, 'amber')
        s.rect(15, 11, 3, 7, 'gold')
        s.rect(11, 17, 3, 3, 'red')
        s.rect(20, 17, 3, 3, 'red')
    elif i == 35:
        _lump(s, 4, 10, ('    #####   ', '  ########  ', ' ########## ', '############',
                           '############', '########### ', ' #########  ', '  #######   '), 'shadow', 'steel', 'dark')
        s.rect(7, 15, 3, 3, None)
        s.rect(13, 18, 4, 2, None)
        s.rect(20, 14, 2, 3, None)
        _lump(s, 17, 19, (' ####### ', '#########', '#########', '#########', ' ####### '), 'shadow', 'steel', 'dark')
        s.rect(20, 21, 2, 1, 'ink').dot(23, 22, 'ink')
    elif i == 36:
        s.rect(8, 6, 16, 20, 'red')
        s.rect(8, 6, 16, 3, 'rose')
        s.rect(8, 23, 16, 3, 'brown')
        s.rect(11, 10, 10, 2, 'sand')
        s.rect(11, 20, 10, 2, 'sand')
        s.rect(14, 13, 4, 2, 'gold')
        s.rect(13, 16, 2, 3, 'amber')
        s.rect(17, 15, 2, 4, 'amber')
        s.rect(16, 19, 3, 2, 'rust')
    elif i == 37:
        s.rect(4, 17, 24, 8, 'steel')
        s.rect(7, 14, 8, 5, 'steel')
        s.rect(17, 14, 8, 5, 'steel')
        s.rect(13, 9, 6, 12, 'steel')
        s.rect(5, 18, 22, 2, 'light')
        s.rect(8, 15, 5, 1, 'cream')
        s.rect(20, 15, 3, 1, 'light')
        s.rect(4, 24, 24, 2, 'dark')
    elif i == 38:
        s.rect(4, 8, 24, 5, 'light')
        s.rect(6, 13, 22, 5, 'cream')
        s.rect(5, 18, 22, 5, 'steel')
        s.rect(8, 9, 16, 1, 'white')
        s.rect(10, 14, 14, 1, 'ice')
        s.rect(8, 19, 14, 1, 'light')
        s.rect(26, 8, 2, 15, 'shadow')
        s.rect(3, 23, 25, 2, 'dark')
    elif i == 39:
        s.rect(5, 7, 9, 18, 'steel')
        s.rect(5, 18, 20, 8, 'steel')
        s.rect(7, 9, 5, 14, 'light')
        s.rect(8, 7, 4, 3, 'dark')
        s.rect(5, 9, 3, 4, None)
        s.rect(23, 20, 4, 4, 'dark')
        s.rect(21, 18, 3, 3, None)
        s.rect(9, 23, 13, 1, 'cream')
        s.rect(14, 18, 3, 2, 'gold')
    elif i == 40:
        s.rect(7, 6, 18, 4, 'brown')
        s.rect(7, 23, 18, 4, 'brown')
        s.rect(9, 9, 14, 15, 'amber')
        for y in range(10, 23, 3):
            s.rect(9, y, 14, 2, 'rust')
            s.rect(11, y, 10, 1, 'gold')
        s.rect(5, 10, 3, 13, 'brown')
        s.rect(24, 10, 3, 13, 'tan')
        s.rect(9, 27, 14, 2, 'brown')
        s.rect(12, 12, 2, 10, 'cream')
    elif i == 41:
        s.rect(12, 4, 8, 23, 'white')
        s.oval(9, 6, 14, 7, 'light')
        s.oval(7, 11, 18, 8, 'white')
        s.oval(7, 17, 18, 8, 'light')
        s.oval(9, 23, 14, 5, 'cream')
        s.rect(14, 2, 4, 4, 'steel')
        s.rect(13, 13, 7, 2, 'ice')
        s.rect(13, 19, 7, 2, 'ice')
        s.rect(10, 24, 2, 4, 'shadow')
        s.rect(20, 24, 2, 4, 'shadow')
    elif i == 42:
        for x, y, w, h, c in (
            (5, 11, 19, 13, 'shadow'),
            (7, 9, 18, 16, 'olive'),
            (9, 8, 15, 17, 'green'),
        ):
            s.rect(x, y, w, h, c)
        s.rect(10, 9, 9, 7, None)
        s.rect(14, 16, 4, 10, None)
        s.rect(12, 13, 8, 2, 'gold')
        s.rect(4, 14, 5, 3, 'amber')
        s.rect(3, 15, 3, 2, 'rust')
        s.rect(23, 22, 5, 3, 'rust')
        s.rect(25, 23, 3, 2, 'gold')
        s.rect(11, 9, 2, 10, 'light')
    elif i == 43:
        s.rect(5, 7, 22, 18, 'brown')
        s.rect(7, 6, 18, 4, 'tan')
        s.rect(7, 22, 18, 4, 'tan')
        s.rect(9, 10, 14, 12, 'steel')
        for y in range(9, 23, 2):
            s.rect(7, y, 18, 2, 'rust')
            s.rect(9, y, 14, 1, 'gold')
        s.rect(3, 12, 4, 8, 'dark')
        s.rect(25, 12, 4, 8, 'dark')
        s.rect(6, 12, 2, 8, 'sand')
        s.rect(24, 12, 2, 8, 'brown')
    elif i == 44:
        s.rect(6, 9, 20, 15, 'steel')
        s.oval(4, 8, 9, 17, 'light')
        s.rect(9, 7, 14, 3, 'cream')
        s.rect(9, 23, 14, 3, 'dark')
        for x in (11, 16, 21):
            s.rect(x, 12, 2, 9, 'shadow')
        s.rect(24, 14, 5, 5, 'steel')
        s.rect(27, 15, 3, 3, 'light')
        s.rect(3, 15, 4, 3, 'gold')
        s.rect(10, 10, 9, 2, 'white')
        s.rect(12, 25, 5, 3, 'dark')
    elif i == 45:
        s.rect(5, 7, 22, 19, 'ice')
        s.rect(7, 9, 18, 15, 'light')
        s.rect(8, 11, 7, 11, 'rust')
        for y in range(12, 21, 2):
            s.rect(9, y, 5, 1, 'gold')
        s.rect(17, 11, 6, 3, 'steel')
        s.line(21, 13, 19, 18, 'dark', 2)
        s.rect(18, 19, 3, 3, 'steel')
        s.rect(15, 18, 2, 2, 'amber')
        s.rect(8, 3, 2, 4, 'gold')
        s.rect(22, 3, 2, 4, 'gold')
        s.rect(8, 26, 2, 3, 'gold')
        s.rect(22, 26, 2, 3, 'gold')
    elif i == 46:
        _barrel(s, 'ink', 'steel')
        s.oval(12, 3, 8, 6, 'dark')
        s.oval(13, 4, 6, 4, None)
        s.rect(11, 11, 3, 8, 'shadow')
        s.rect(20, 13, 1, 8, 'ink')
        s.rect(8, 18, 2, 2, 'cream')
    elif i == 47:
        _vial(s, 'amber', level=11)
        s.rect(13, 13, 2, 2, 'gold')
        s.rect(17, 17, 2, 2, 'cream')
        s.rect(12, 22, 8, 1, 'brown')
        s.rect(8, 27, 16, 2, 'brown')
    elif i == 48:
        s.rect(7, 8, 18, 17, 'green')
        s.rect(5, 22, 22, 5, 'green')
        s.rect(9, 12, 14, 9, 'teal')
        s.rect(10, 13, 12, 2, 'ice')
        s.rect(12, 4, 8, 5, 'light')
        s.rect(14, 2, 4, 4, 'steel')
        s.rect(9, 18, 3, 3, 'white')
        s.rect(15, 17, 3, 3, 'gold')
        s.rect(21, 16, 2, 3, 'cream')
        s.rect(8, 25, 16, 1, 'olive')
    elif i == 49:
        _barrel(s, 'rust', 'rose', wide=True)
        s.rect(12, 2, 8, 6, 'shadow')
        s.rect(13, 3, 6, 4, 'dark')
        s.rect(9, 16, 4, 2, 'amber')
        s.rect(20, 19, 3, 2, 'brown')
    elif i == 50:
        s.rect(7, 7, 19, 20, 'olive')
        s.rect(9, 5, 15, 4, 'dark')
        s.rect(7, 24, 19, 4, 'shadow')
        s.rect(11, 10, 11, 10, 'amber')
        s.rect(13, 11, 7, 2, 'gold')
        s.rect(9, 11, 2, 9, 'sand')
        s.rect(23, 12, 2, 9, 'tan')
        s.rect(4, 14, 3, 5, 'steel')
        s.rect(25, 10, 3, 3, 'steel')
        s.line(11, 23, 23, 15, 'dark')
    elif i == 51:
        s.oval(5, 13, 23, 13, 'cyan')
        s.rect(5, 14, 18, 9, 'blue')
        s.rect(7, 11, 12, 4, 'light')
        s.line(20, 14, 27, 7, 'steel', 3)
        s.rect(25, 5, 5, 3, 'dark')
        s.oval(15, 17, 5, 7, 'gold')
        s.rect(8, 16, 3, 4, 'ice')
        s.rect(7, 26, 19, 2, 'shadow')
    elif i == 52:
        s.rect(9, 6, 14, 21, 'ice')
        s.rect(11, 13, 10, 11, 'blue')
        s.rect(12, 14, 8, 2, 'cyan')
        s.rect(9, 4, 14, 4, 'steel')
        s.rect(12, 2, 8, 3, 'light')
        s.rect(8, 21, 5, 4, 'steel')
        s.rect(5, 23, 7, 3, 'light')
        s.oval(14, 16, 6, 7, 'white')
        s.rect(16, 17, 2, 5, 'blue')
        s.rect(12, 24, 8, 1, 'cyan')
    elif i == 53:
        s.rect(6, 7, 20, 20, 'white')
        s.rect(8, 9, 16, 16, 'light')
        s.rect(9, 16, 14, 8, 'rose')
        s.rect(10, 17, 12, 2, 'gold')
        s.rect(7, 4, 18, 3, 'red')
        s.rect(12, 2, 8, 3, 'steel')
        s.rect(9, 11, 2, 13, 'ice')
        s.line(16, 11, 13, 6, 'red')
        s.line(18, 10, 21, 5, 'rose')
        s.rect(13, 20, 6, 2, 'white')
        s.rect(9, 26, 15, 2, 'dark')
    elif i == 54:
        s.rect(11, 4, 5, 23, 'teal')
        s.rect(6, 10, 5, 17, 'cyan')
        s.rect(18, 7, 5, 20, 'ice')
        s.rect(23, 14, 4, 13, 'teal')
        s.rect(4, 17, 4, 10, 'cyan')
        s.rect(12, 5, 2, 13, 'white')
        s.rect(19, 8, 1, 12, 'white')
        s.rect(7, 12, 1, 8, 'ice')
        s.rect(6, 26, 21, 2, 'blue')
        s.rect(24, 18, 1, 5, 'cyan')
    elif i == 55:
        s.rect(6, 9, 21, 8, 'steel')
        s.rect(4, 15, 25, 11, 'light')
        s.rect(8, 6, 8, 5, 'steel')
        s.rect(17, 7, 7, 4, 'shadow')
        s.rect(7, 11, 13, 3, 'cream')
        s.rect(6, 16, 20, 2, 'white')
        s.rect(5, 23, 23, 3, 'steel')
        s.rect(12, 8, 4, 2, 'light')
        s.rect(20, 18, 4, 3, 'shadow')
    elif i == 56:
        s.rect(12, 3, 7, 7, 'cyan')
        s.rect(9, 7, 13, 10, 'teal')
        s.rect(6, 15, 19, 10, 'cyan')
        s.rect(10, 23, 11, 5, 'blue')
        s.rect(14, 4, 3, 10, 'white')
        s.rect(10, 10, 3, 10, 'ice')
        s.rect(18, 15, 3, 6, 'ice')
        s.rect(7, 17, 3, 4, 'teal')
        s.rect(8, 27, 17, 2, 'shadow')
    elif i == 57:
        s.oval(4, 5, 24, 22, 'light')
        s.oval(6, 7, 20, 18, 'ice')
        s.rect(15, 4, 2, 2, None)
        for x in range(8, 25, 4):
            s.rect(x, 9, 1, 14, 'cyan')
        for y in range(9, 23, 4):
            s.rect(8, y, 16, 1, 'cyan')
        s.rect(10, 9, 4, 3, 'white')
        s.rect(7, 14, 3, 7, 'teal')
    elif i == 58:
        s.rect(7, 9, 18, 14, 'dark')
        s.rect(10, 11, 12, 10, 'shadow')
        s.rect(12, 13, 8, 6, 'teal')
        s.rect(14, 14, 4, 4, 'cyan')
        for x in range(8, 25, 3):
            s.rect(x, 6, 1, 3, 'gold')
            s.rect(x, 23, 1, 3, 'gold')
        for y in range(11, 21, 3):
            s.rect(4, y, 3, 1, 'gold')
            s.rect(25, y, 3, 1, 'gold')
        s.rect(13, 19, 7, 1, 'white')
    elif i == 59:
        s.oval(5, 5, 22, 22, 'steel')
        s.oval(8, 8, 16, 16, 'light')
        s.oval(12, 12, 8, 8, 'dark')
        s.rect(14, 14, 4, 4, 'cream')
        s.rect(15, 2, 2, 6, 'gold')
        s.rect(15, 24, 2, 6, 'gold')
        s.rect(2, 15, 6, 2, 'gold')
        s.rect(24, 15, 6, 2, 'gold')
        s.rect(9, 9, 4, 2, 'white')
        s.rect(20, 20, 3, 2, 'shadow')
    elif i == 60:
        s.rect(5, 7, 22, 18, 'leaf')
        s.rect(7, 9, 18, 14, 'green')
        s.rect(10, 11, 8, 7, 'dark')
        for x in range(11, 18, 2):
            s.rect(x, 12, 1, 5, 'gold')
        s.rect(20, 11, 3, 3, 'teal')
        s.rect(20, 16, 3, 4, 'red')
        for x in range(8, 24, 3):
            s.rect(x, 4, 1, 3, 'gold')
            s.rect(x, 25, 1, 3, 'gold')
        s.rect(7, 22, 16, 1, 'light')
    elif i == 61:
        s.rect(4, 13, 24, 11, 'purple')
        s.rect(7, 10, 18, 4, 'rose')
        s.rect(9, 9, 14, 2, 'gold')
        s.rect(6, 14, 20, 2, 'red')
        s.rect(5, 22, 22, 2, 'dark')
        s.rect(10, 16, 12, 4, 'tan')
        for x in (10, 15, 20):
            s.rect(x, 4, 2, 5, 'amber')
            s.rect(x, 17, 2, 2, 'gold')
        s.rect(13, 26, 7, 2, 'rust')
    elif i == 62:
        s.rect(8, 7, 16, 20, 'olive')
        s.rect(10, 4, 12, 5, 'gold')
        s.rect(9, 9, 14, 15, 'sand')
        s.rect(11, 11, 10, 3, 'cream')
        s.oval(14, 16, 4, 4, 'red')
        s.rect(15, 13, 2, 2, 'gold')
        s.rect(12, 20, 2, 2, 'gold')
        s.rect(18, 20, 2, 2, 'gold')
        s.rect(15, 19, 2, 5, 'gold')
        s.rect(6, 24, 4, 4, 'dark')
        s.rect(22, 24, 4, 4, 'dark')
    elif i == 63:
        s.line(7, 27, 17, 4, 'brown', 3)
        s.line(16, 17, 26, 8, 'tan', 2)
        s.line(14, 20, 8, 9, 'soil', 2)
        s.rect(16, 3, 3, 3, 'tan')
        s.rect(24, 6, 3, 3, 'brown')
        s.rect(6, 10, 3, 3, 'tan')
        s.rect(11, 12, 4, 2, 'brown')
        s.rect(18, 14, 4, 2, 'brown')
        s.rect(13, 20, 4, 2, 'tan')
    elif i == 64:
        s.oval(4, 15, 10, 11, 'light')
        s.oval(8, 10, 8, 10, 'steel')
        s.oval(14, 16, 8, 10, 'white')
        s.oval(18, 9, 9, 10, 'light')
        s.oval(21, 19, 7, 8, 'cream')
        s.rect(7, 18, 4, 2, 'white')
        s.rect(16, 19, 3, 2, 'ice')
        s.rect(20, 12, 3, 2, 'white')
        s.rect(6, 26, 22, 2, 'shadow')
    elif i == 65:
        for x, top, bottom in ((8, 7, 27), (12, 4, 28), (16, 6, 29), (20, 3, 27), (24, 8, 26)):
            s.line(x, bottom, x - 2, top, 'sand')
            s.rect(x - 2, top, 3, 3, 'gold')
        s.rect(9, 20, 16, 5, 'tan')
        s.rect(11, 19, 12, 2, 'brown')
        s.line(6, 18, 12, 15, 'olive')
        s.line(23, 16, 27, 19, 'olive')
        s.rect(13, 23, 7, 2, 'gold')
        s.rect(7, 28, 20, 2, 'brown')

    return s


def _radiation_suit(s):
    s.oval(8, 3, 17, 14, 'gold')
    s.rect(10, 5, 12, 8, 'dark')
    s.rect(11, 6, 10, 6, 'cyan')
    s.rect(12, 7, 4, 3, 'white')
    s.rect(18, 9, 3, 2, 'teal')
    s.rect(11, 12, 9, 3, 'gold')
    s.rect(8, 15, 17, 10, 'gold')
    s.rect(10, 17, 13, 7, 'sand')
    s.rect(5, 15, 3, 10, 'gold')
    s.rect(24, 15, 3, 10, 'gold')
    s.rect(5, 23, 4, 5, 'dark')
    s.rect(23, 23, 4, 5, 'dark')
    s.rect(11, 24, 4, 5, 'gold')
    s.rect(17, 24, 4, 5, 'gold')
    s.rect(9, 27, 7, 3, 'dark')
    s.rect(17, 27, 7, 3, 'dark')
    s.rect(15, 14, 2, 10, 'brown')
    s.oval(13, 17, 6, 6, 'dark')
    s.oval(14, 18, 4, 4, 'sand')
    s.rect(15, 17, 2, 2, 'red')
    s.rect(12, 19, 2, 1, 'red')
    s.rect(17, 19, 2, 1, 'red')
    s.rect(14, 16, 1, 3, 'red')
    s.rect(18, 5, 2, 1, 'blue')
    s.rect(10, 15, 4, 2, 'cream')
    s.rect(20, 20, 2, 3, 'rust')


def _grass_pack(s):
    s.oval(6, 7, 20, 22, 'tan')
    s.rect(8, 8, 16, 18, 'sand')
    s.rect(8, 6, 16, 6, 'light')
    s.rect(10, 4, 12, 3, 'brown')
    s.rect(5, 13, 4, 11, 'tan')
    s.rect(23, 12, 4, 12, 'tan')
    s.rect(10, 9, 12, 2, 'gold')
    s.rect(14, 16, 4, 5, 'brown')
    s.rect(15, 17, 2, 2, 'gold')
    s.line(9, 10, 9, 24, 'brown')
    s.line(14, 12, 14, 24, 'sand')
    s.line(19, 10, 19, 25, 'brown')
    s.line(23, 12, 23, 24, 'sand')
    s.line(8, 14, 23, 14, 'gold')
    s.line(8, 20, 23, 18, 'gold')
    s.rect(8, 26, 16, 2, 'brown')


def _reinforced_pack(s):
    s.rect(6, 9, 20, 19, 'olive')
    s.rect(8, 6, 16, 5, 'sand')
    s.rect(9, 4, 14, 3, 'brown')
    s.rect(5, 12, 4, 12, 'olive')
    s.rect(23, 12, 4, 12, 'olive')
    s.rect(9, 11, 14, 11, 'tan')
    s.rect(10, 12, 12, 8, 'sand')
    s.rect(14, 13, 4, 5, 'steel')
    s.rect(15, 14, 2, 3, 'dark')
    s.rect(8, 23, 16, 4, 'shadow')
    s.rect(8, 9, 16, 2, 'steel')
    s.rect(7, 17, 3, 2, 'steel')
    s.rect(22, 17, 3, 2, 'steel')
    s.rect(10, 22, 3, 2, 'cream')
    s.rect(19, 22, 3, 2, 'cream')
    s.line(11, 10, 11, 25, 'dark')
    s.line(21, 10, 21, 25, 'dark')


def _engineer_pack(s):
    s.rect(4, 10, 24, 17, 'dark')
    s.rect(6, 8, 20, 4, 'steel')
    s.rect(10, 5, 12, 4, 'shadow')
    s.rect(12, 4, 8, 2, None)
    s.rect(7, 12, 18, 12, 'teal')
    s.rect(8, 13, 16, 3, 'cyan')
    s.rect(9, 17, 14, 6, 'blue')
    s.rect(7, 22, 18, 4, 'shadow')
    s.rect(4, 13, 4, 10, 'steel')
    s.rect(24, 13, 4, 10, 'steel')
    s.rect(12, 17, 8, 2, 'gold')
    s.oval(10, 16, 5, 5, None)
    s.rect(12, 18, 2, 4, 'gold')
    s.rect(14, 18, 3, 2, 'gold')
    s.rect(20, 12, 3, 2, 'amber')
    s.rect(20, 15, 3, 2, 'red')
    s.rect(20, 18, 3, 2, 'green')
    s.rect(11, 24, 10, 2, 'dark')
    s.rect(16, 26, 4, 3, 'brown')


def _gear_item(i):
    english, chinese = next((name, zh) for n, name, zh in GEAR if n == i)
    s = _sprite('gear', i, english, chinese)
    if i == 19:
        _radiation_suit(s)
    elif i == 20:
        _grass_pack(s)
    elif i == 21:
        _reinforced_pack(s)
    elif i == 22:
        _engineer_pack(s)
    return s


def all_items() -> list[Sprite]:
    """Return item_25..65 followed by gear_19..22 in catalog order."""
    return [_item(i) for i in range(25, 66)] + [_gear_item(i) for i in range(19, 23)]
