"""Authored resource deposits and gatherable objects, on the shared 32px grid."""
from pixel_svg import Sprite


def rock(s, x, y, w, h, color, light):
    s.rect(x + 2, y, w - 4, 2, light).rect(x, y + 2, w, h - 4, color)
    s.rect(x + 2, y + h - 2, w - 4, 2, 'shadow')
    s.line(x + 2, y + 2, x + 4, y + h - 4, light)
    return s


def all_nodes():
    out = []
    s = Sprite('node_8', 'Stone / 石料堆')
    rock(s, 4, 15, 16, 11, 'steel', 'light')
    rock(s, 16, 10, 11, 12, 'light', 'cream')
    s.line(19, 15, 21, 20, 'steel').rect(9, 18, 5, 2, 'dark')
    out.append(s)

    s = Sprite('node_9', 'Clay / 黏土矿床')
    s.oval(3, 13, 26, 14, 'soil').oval(5, 12, 21, 12, 'rust')
    s.rect(8, 11, 14, 3, 'tan').rect(6, 17, 18, 2, 'rose')
    s.line(15, 14, 13, 22, 'brown').line(20, 20, 24, 22, 'soil')
    s.rect(5, 23, 6, 2, 'rust').rect(22, 24, 5, 2, 'brown')
    out.append(s)

    s = Sprite('node_10', 'Coal / 煤矿床')
    rock(s, 3, 16, 13, 10, 'dark', 'steel')
    rock(s, 12, 9, 12, 17, 'ink', 'steel')
    rock(s, 21, 17, 8, 9, 'dark', 'light')
    s.line(15, 12, 17, 16, 'light').line(7, 20, 10, 20, 'steel')
    s.rect(17, 23, 5, 2, 'steel')
    out.append(s)

    s = Sprite('node_11', 'Limestone / 石灰石矿床')
    rock(s, 4, 15, 23, 11, 'sand', 'cream')
    rock(s, 8, 9, 16, 10, 'cream', 'white')
    s.rect(8, 17, 15, 1, 'tan').rect(6, 21, 18, 1, 'tan')
    s.rect(11, 13, 10, 1, 'sand').line(18, 18, 20, 24, 'tan')
    out.append(s)

    s = Sprite('node_12', 'FireClay / 耐火黏土矿床')
    s.oval(3, 15, 26, 11, 'brown').rect(6, 11, 19, 12, 'rust')
    s.rect(9, 8, 13, 6, 'tan').rect(6, 15, 18, 2, 'sand')
    s.rect(5, 21, 21, 2, 'soil').rect(9, 12, 12, 1, 'cream')
    s.rect(11, 18, 2, 4, 'soil').rect(20, 14, 2, 6, 'brown')
    s.dot(8, 19, 'cream').dot(18, 18, 'cream').dot(23, 24, 'tan')
    out.append(s)

    s = Sprite('node_13', 'Oil / 原油渗漏')
    s.oval(3, 13, 26, 14, 'soil').oval(5, 14, 23, 11, 'ink')
    s.rect(8, 17, 13, 1, 'purple').rect(16, 21, 9, 1, 'blue')
    s.rect(10, 23, 7, 1, 'dark').rect(17, 18, 5, 1, 'steel')
    rock(s, 5, 9, 8, 7, 'brown', 'tan')
    rock(s, 23, 12, 5, 6, 'shadow', 'steel')
    out.append(s)

    s = Sprite('node_14', 'Quartz / 石英矿簇')
    rock(s, 4, 21, 24, 7, 'steel', 'light')
    # Separate tall, pointed, faceted crystals; not another recoloured ore pile.
    for x, y, w, h in ((6, 13, 6, 12), (12, 5, 7, 20), (20, 11, 6, 14)):
        s.rect(x + 2, y, w - 4, 2, 'white')
        s.rect(x + 1, y + 2, w - 2, 2, 'cream')
        s.rect(x, y + 4, w, h - 4, 'ice')
        s.rect(x + 1, y + 4, 2, h - 5, 'white')
        s.rect(x + w - 2, y + 4, 2, h - 4, 'blue')
    out.append(s)

    s = Sprite('node_15', 'Branch / 地面树枝')
    s.line(5, 24, 25, 10, 'brown', 2).line(7, 24, 24, 12, 'tan')
    s.line(14, 18, 12, 9, 'brown', 2).line(20, 14, 27, 16, 'tan')
    s.rect(5, 23, 2, 2, 'sand').rect(24, 9, 2, 2, 'sand')
    out.append(s)

    s = Sprite('node_16', 'LooseStone / 地面石头')
    rock(s, 6, 13, 19, 12, 'steel', 'light')
    s.rect(10, 12, 10, 2, 'light').line(9, 18, 14, 21, 'cream')
    s.rect(18, 21, 5, 2, 'dark')
    out.append(s)

    s = Sprite('node_17', 'DryGrass / 地面干草')
    for x, y in ((6, 10), (11, 6), (16, 8), (22, 5), (26, 13)):
        s.line(15, 26, x, y, 'tan')
        s.line(16, 25, x, y + 2, 'sand')
        s.line(x, y + 2, x - 2, y + 1, 'gold')
    s.line(8, 23, 23, 24, 'brown').line(12, 19, 21, 20, 'cream')
    out.append(s)
    return out
