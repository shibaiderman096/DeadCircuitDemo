"""Layered radiation-suit avatar art aligned to the existing modular rig."""

from pixel_svg import Sprite


PARTS = (
    ('head', 'head', 'RadiationSuitHead', '核作业防护服头罩', 16, 16, 8, 8, 16),
    ('torso', 'torso', 'RadiationSuitTorso', '核作业防护服躯干', 16, 16, 8, 8, 16),
    ('arm', 'arm', 'RadiationSuitArm', '核作业防护服手臂', 8, 16, 12, 8, 8),
    ('leg', 'leg', 'RadiationSuitLeg', '核作业防护服腿部', 8, 16, 12, 8, 9),
    ('shoe', 'shoe', 'RadiationSuitShoe', '核作业防护服靴子', 8, 8, 12, 12, 8),
)


def _rect(sprite, part, x, y, width, height, color):
    _, _, _, _, local_width, local_height, offset_x, offset_y, _ = part
    assert 1 <= x < x + width <= local_width - 1
    assert 1 <= y < y + height <= local_height - 1
    sprite.rect(offset_x + x, offset_y + y, width, height, color)


def _side_x(part, facing, x, width):
    return part[8] - x - width if facing == 2 else x


def _head(sprite, part, facing):
    r = lambda x, y, w, h, c: _rect(sprite, part, x, y, w, h, c)
    sx = lambda x, w: _side_x(part, facing, x, w)

    r(4, 3, 8, 8, 'gold')
    r(3, 9, 10, 4, 'gold')
    r(4, 4, 5, 2, 'sand')
    r(3, 9, 3, 3, 'sand')
    r(10, 6, 2, 5, 'brown')
    r(5, 11, 6, 2, 'sand')

    if facing == 1:
        r(7, 4, 2, 8, 'brown')
        r(4, 6, 2, 2, 'sand')
        r(10, 8, 2, 4, 'brown')
        r(5, 11, 6, 1, 'dark')
    else:
        if facing == 3:
            r(5, 5, 6, 6, 'dark')
            r(6, 6, 4, 4, 'cyan')
            r(6, 6, 2, 1, 'white')
            r(9, 8, 1, 2, 'teal')
            r(6, 10, 4, 2, 'steel')
            r(7, 11, 2, 1, 'dark')
        else:
            r(sx(6, 6), 5, 6, 6, 'dark')
            r(sx(7, 4), 6, 4, 4, 'cyan')
            r(sx(7, 2), 6, 2, 1, 'white')
            r(sx(9, 1), 8, 1, 2, 'teal')
            r(sx(9, 3), 10, 3, 2, 'steel')
            r(sx(10, 1), 11, 1, 1, 'dark')
            r(sx(3, 2), 4, 2, 5, 'sand')
    return sprite


def _torso(sprite, part, facing):
    r = lambda x, y, w, h, c: _rect(sprite, part, x, y, w, h, c)
    sx = lambda x, w: _side_x(part, facing, x, w)

    r(5, 3, 6, 3, 'gold')
    r(4, 5, 8, 8, 'gold')
    r(4, 5, 2, 7, 'sand')
    r(10, 5, 2, 8, 'brown')
    r(5, 11, 7, 2, 'sand')
    r(4, 10, 8, 2, 'dark')
    r(6, 10, 4, 1, 'steel')

    if facing == 1:
        r(7, 3, 2, 8, 'brown')
        r(5, 5, 2, 4, 'dark')
        r(9, 5, 2, 3, 'steel')
    elif facing == 3:
        r(7, 3, 2, 9, 'brown')
        r(6, 7, 4, 3, 'dark')
        r(7, 8, 2, 1, 'teal')
        r(5, 5, 1, 5, 'white')
    else:
        r(sx(10, 2), 4, 2, 7, 'brown')
        r(sx(8, 2), 6, 2, 3, 'steel')
        r(sx(6, 1), 7, 1, 3, 'dark')
    return sprite


def _arm(sprite, part, facing):
    r = lambda x, y, w, h, c: _rect(sprite, part, x, y, w, h, c)
    sx = lambda x, w: _side_x(part, facing, x, w)

    r(3, 3, 2, 10, 'gold')
    r(3, 3, 1, 6, 'sand')
    r(4, 5, 1, 6, 'brown')
    r(3, 9, 2, 1, 'steel')
    r(3, 11, 2, 2, 'dark')

    if facing == 1:
        r(3, 4, 1, 5, 'brown')
        r(4, 10, 1, 1, 'sand')
        r(3, 12, 2, 1, 'brown')
    elif facing == 3:
        r(3, 6, 2, 1, 'brown')
        r(3, 10, 2, 1, 'sand')
        r(4, 11, 1, 2, 'shadow')
    else:
        r(sx(3, 1), 5, 1, 3, 'white')
        r(sx(4, 1), 7, 1, 3, 'brown')
        r(sx(3, 1), 10, 1, 1, 'sand')
        r(sx(4, 1), 12, 1, 1, 'brown')
    return sprite


def _leg(sprite, part, facing):
    r = lambda x, y, w, h, c: _rect(sprite, part, x, y, w, h, c)
    sx = lambda x, w: _side_x(part, facing, x, w)

    r(3, 3, 3, 10, 'gold')
    r(3, 3, 1, 8, 'sand')
    r(5, 4, 1, 8, 'brown')
    r(3, 9, 3, 2, 'steel')
    r(3, 10, 3, 1, 'sand')
    r(3, 12, 3, 1, 'brown')

    if facing == 1:
        r(4, 3, 1, 7, 'brown')
        r(3, 8, 3, 1, 'dark')
        r(3, 11, 3, 1, 'sand')
    elif facing == 3:
        r(4, 4, 1, 5, 'dark')
        r(3, 7, 3, 2, 'sand')
        r(4, 7, 1, 1, 'brown')
    else:
        r(sx(3, 1), 4, 1, 3, 'white')
        r(sx(4, 1), 5, 1, 3, 'brown')
        r(sx(3, 3), 8, 3, 1, 'dark')
        r(sx(3, 1), 11, 1, 1, 'sand')
    return sprite


def _shoe(sprite, part, facing):
    r = lambda x, y, w, h, c: _rect(sprite, part, x, y, w, h, c)
    sx = lambda x, w: _side_x(part, facing, x, w)

    r(2, 3, 4, 1, 'gold')
    r(2, 4, 4, 1, 'dark')
    r(2, 3, 2, 1, 'sand')
    r(5, 4, 1, 1, 'brown')

    if facing == 1:
        r(3, 4, 2, 1, 'brown')
        r(4, 3, 1, 1, 'brown')
    elif facing == 3:
        r(4, 4, 2, 1, 'steel')
        r(3, 3, 2, 1, 'white')
    else:
        r(sx(5, 1), 3, 1, 1, 'white')
        r(sx(2, 2), 4, 2, 1, 'steel')
    return sprite


def _radiation_part(part, facing):
    key, target, english, chinese, _, _, _, _, _ = part
    sprite = Sprite(f'avatar_armor_19_{key}_{facing}', f'{english} / {chinese}')
    {'head': _head, 'torso': _torso, 'arm': _arm, 'leg': _leg, 'shoe': _shoe}[target](
        sprite, part, facing)
    return sprite


def all_radiation() -> list[Sprite]:
    """Return the five layered suit parts in four catalog facing orders."""
    return [_radiation_part(part, facing) for part in PARTS for facing in range(4)]
