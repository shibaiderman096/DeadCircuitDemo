"""Offline assembly of real generated cells with the game's SVG-derived sprites.

This is a data/art review sheet, not a Unity or gameplay screenshot. Pillow
only composites existing sprites; no runtime texture is authored here.
"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
ART = PROJECT / 'Assets/DeadCircuit/Resources/Art'


def load_sprites():
    result = {}
    for stem in ['Expansion/modules', 'Expansion/world', 'Expansion/landmarks']:
        data = json.loads((ART / (stem + '.json')).read_text(encoding='utf-8-sig'))
        atlas = Image.open(ART / (stem + '.png')).convert('RGBA')
        for sprite in data['sprites']:
            x, y, w, h = [sprite[k] for k in ('x', 'y', 'w', 'h')]
            result[sprite['name']] = atlas.crop((x, y, x+w, y+h))
    data = json.loads((ART / 'atlas.json').read_text(encoding='utf-8-sig'))
    atlas = Image.open(ART / 'sprites.png').convert('RGBA')
    for sprite in data['sprites']:
        x, y, w, h = [sprite[k] for k in ('x', 'y', 'width', 'height')]
        result[sprite['name']] = atlas.crop((x, y, x+w, y+h))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    samples = json.loads(args.data.read_text(encoding='utf-8-sig'))
    sprites = load_sprites()
    assert len(samples) == 32
    font_path = Path('C:/Windows/Fonts/msyh.ttc')
    font = ImageFont.truetype(str(font_path), 17)
    small = ImageFont.truetype(str(font_path), 13)
    panels = []
    for sample in samples:
        site = sample['site']
        rows = sample['cells']
        width, height = site['width'], site['height']
        image = Image.new('RGBA', (464, 480), '#111d25')
        draw = ImageDraw.Draw(image)
        draw.text((14, 8), f"{site['kind']:02}  {sample['name']}", fill='#f5d58c', font=font)
        draw.text((14, 34), f"seed {sample['seed']} | ({site['x']},{site['y']}) | rotation {site['rotation']*90}", fill='#a8bbc2', font=small)
        ox, oy = (464-width*32)//2, 68+(384-height*32)//2
        def paste(sprite, x, y):
            image.alpha_composite(sprite, (ox+x*32, oy+y*32))
        for y, row in enumerate(rows):
            for x, cell in enumerate(row):
                if cell != ' ':
                    if cell == ',':
                        draw.rectangle((ox+x*32,oy+y*32,ox+x*32+31,oy+y*32+31),fill='#46544c')
                    else: paste(sprites[sample['floor']],x,y)
        walls = {(n['x']-site['x'], height-1-(n['y']-site['y'])) for n in sample['nodes'] if n['kind']==4}
        # Door cells participate in the same wall connection mask as Unity.
        walls.update((b['x']-site['x'],height-1-(b['y']-site['y'])) for b in sample['buildings'] if b['kind']==31)
        for y, row in enumerate(rows):
            for x, cell in enumerate(row):
                if cell == '#':
                    mask = sum(bit for dx,dy,bit in [(1,0,1),(0,-1,2),(-1,0,4),(0,1,8)] if (x+dx,y+dy) in walls)
                    paste(sprites[f'wall_{mask}'],x,y)
                elif cell == '+': paste(sprites['machine_31'],x,y)
                elif cell in 'FMATEBSK':
                    sprite = sprites.get('node_3', sprites['node_crate'])
                    paste(sprite.resize((32,32),Image.Resampling.NEAREST),x,y)
                elif cell in 'bsthnmpkglruvodfwea':
                    prop=sprites['landmark_prop_'+cell].rotate(site['rotation']*90,expand=False).resize((28,28),Image.Resampling.NEAREST)
                    image.alpha_composite(prop,(ox+x*32+2,oy+y*32+2))
        # One game-scale actor alongside the main entrance for scale review.
        actor=sprites['actor_0_0_0'].resize((51,51),Image.Resampling.NEAREST)
        dx,dy=site['doorX']-site['x'],height-1-(site['doorY']-site['y'])
        ax=max(0,min(image.width-51,ox+dx*32-10));ay=max(55,min(image.height-55,oy+dy*32-20))
        image.alpha_composite(actor,(ax,ay))
        image.alpha_composite(sprites[f"landmark_sign_{site['kind']}"],(414,8))
        draw.text((14,457),'离线数据 / SVG素材拼合；非游戏截图',font=small,fill='#7f969f')
        panels.append(image)
    args.output.mkdir(parents=True, exist_ok=True)
    sheet=Image.new('RGBA',(464*8,480*4),'#0c131a')
    for i,panel in enumerate(panels): sheet.alpha_composite(panel,((i%8)*464,(i//8)*480))
    sheet.convert('RGB').save(args.output/'all-32.png')
    for indices,name in [([4,18,19,23],'medical-research-bunker'),([5,24,28,30],'rural-industry-motel')]:
        sheet=Image.new('RGBA',(928,960),'#0c131a')
        for j,i in enumerate(indices): sheet.alpha_composite(panels[i],((j%2)*464,(j//2)*480))
        sheet.convert('RGB').save(args.output/(name+'.png'))
    print(json.dumps({'status':'PASS','samples':32,'kind':'offline-data-art-review','output':str(args.output)}))


if __name__=='__main__':
    main()
