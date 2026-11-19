"""Generate SVGs, export using codeart2d's headless renderer, then validate/publish.

Run with the project's supplied Python, -B. Never starts Unity or the game.
All subprocesses are bounded to 120 seconds; only this new batch subtree and
the four new runtime atlas files are written. No packages or external art used.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from pixel_svg import ROOT, PALETTE, RGB, export_svg
from svg_icons import all_icons
from svg_world import (all_world, FX, terrain_field, ENEMIES, ENEMY_NAMES_ZH,
                       ENEMY_PREFIXES, SITES, SITE_NAMES_ZH)
from svg_machines_survivor import all_extended, MACHINE_ZH, MACHINES, NODES

PROJECT=ROOT.parents[2]
RUNTIME=PROJECT/'Assets/DeadCircuit/Resources/Art/Expansion'
SKILLS=Path('C:/Users/man/.codex/skills')

def write_json(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')

def run_logged(args,log,env):
    result=subprocess.run([str(x) for x in args],capture_output=True,text=True,encoding='utf-8',
                          errors='replace',timeout=120,env=env,cwd=PROJECT)
    record={'argv':[str(x) for x in args],'exit':result.returncode,
            'stdout':result.stdout,'stderr':result.stderr}
    write_json(log,record)
    if result.returncode:raise RuntimeError(f'{log.name}: exit {result.returncode}: {result.stdout[-800:]} {result.stderr[-800:]}')
    return record

def expected_atlas(sprites,layout):
    expected=np.zeros((layout['height'],layout['width'],4),dtype=np.uint8)
    for s,e in zip(sprites,layout['sprites']):
        a=s.final();rgba=np.zeros((32,32,4),dtype=np.uint8)
        for n,rgb in enumerate(RGB):rgba[a==n]=rgb
        expected[e['y']:e['y']+32,e['x']:e['x']+32]=rgba
    return expected

def required_names():
    names=set()
    for prefix,count in [('food',31),('gear',19),('part',7),('item',25),('creature',12),('enemy',12),('site',12),('biome',6),('terrain',6),('machine',31),('node',8)]:
        names.update(f'{prefix}_{i}' for i in range(count))
    names.update(f'crop_{i}_{j}' for i in range(8) for j in range(2))
    names.update(f'survivor_{d}_{f}' for d in range(4) for f in range(4))
    names.update('fx_'+x for x in ['rain','snow','dust','fog','muzzle','spark','flame','ice','soil','poison','wave'])
    return names

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def check_enemy_catalog(sprites):
    """Bind art IDs/label prefixes to the real Core order, not an art-only list."""
    import re
    path=PROJECT/'Assets/DeadCircuit/Scripts/Core/EnemyCatalog.cs'
    text=path.read_text(encoding='utf-8-sig')
    block=re.search(r'string\[\]\s+Names\s*=\s*\{([^}]+)\}',text).group(1)
    core_names=re.findall(r'"([^"]+)"',block)
    variants=[(int(a),int(b)) for a,b in re.findall(r'new EnemyStats\(\s*(\d+)\s*,\s*Names\[(\d+)\]',text)]
    assert variants==[(i,i) for i in range(12)],'EnemyCatalog variant/Names index contract drifted'
    prefix_key={'普通':'Walker','快跑':'Sprinter','暴徒':'Brute','爬行':'Crawler',
                '喷吐':'Spitter','装甲':'Armored','燃烧':'Burning','尖啸':'Screamer',
                '防化':'Hazmat','冻伤':'Frost','自爆':'Bomber','夜行':'NightElite'}
    assert len(core_names)==len(ENEMIES)==len(ENEMY_NAMES_ZH)==12
    assert core_names==ENEMY_NAMES_ZH,'EnemyCatalog Chinese names/order mismatch'
    records=[]
    for i,core_name in enumerate(core_names):
        prefix=ENEMY_PREFIXES[i]
        assert core_name.startswith(prefix),(i,'Core name prefix mismatch')
        assert ENEMIES[i]==prefix_key[prefix],(i,'English role does not match Core name prefix')
        matches=[s for s in sprites if s.name==f'enemy_{i}']
        assert len(matches)==1,(i,'missing or duplicate enemy sprite')
        label=matches[0].label
        assert label==ENEMIES[i]+' / '+core_name,(i,'sprite label prefix/name mismatch')
        records.append({'key':matches[0].name,'variant':i,'englishPrefix':ENEMIES[i],
                        'coreNamePrefix':prefix,'coreName':core_name,'catalogLabel':label,'status':'pass'})
    return {'status':'pass','source':str(path),'sourceSHA256':sha(path),'count':12,
            'variantIndicesMatch':True,'namePrefixesMatch':True,'entries':records}


def check_site_catalog(sprites):
    """Verify enum ordinals, Sites entry order and facade label name prefixes."""
    import re
    path=PROJECT/'Assets/DeadCircuit/Scripts/Core/WorldGeneration.cs'
    text=path.read_text(encoding='utf-8-sig')
    enum=re.search(r'enum SiteKind\s*\{([^}]+)\}',text).group(1)
    ordinals=[(name,int(index)) for name,index in re.findall(r'(\w+)\s*=\s*(\d+)',enum)]
    sites=re.findall(r'new SiteDefinition\(SiteKind\.(\w+),\s*"([^"]+)"',text)
    assert len(ordinals)==len(sites)==len(SITES)==len(SITE_NAMES_ZH)==12
    assert ordinals==[(name,i) for i,name in enumerate(SITES)],'SiteKind ordinal mismatch'
    assert sites==list(zip(SITES,SITE_NAMES_ZH)),'WorldGenerationCatalog.Sites order/names mismatch'
    records=[]
    for i,(english,chinese) in enumerate(sites):
        matches=[s for s in sprites if s.name==f'site_{i}']
        assert len(matches)==1,(i,'missing or duplicate site sprite')
        label=matches[0].label
        assert label==english+' / '+chinese+' entrance',(i,'site facade label mismatch')
        records.append({'key':matches[0].name,'ordinal':i,'englishPrefix':english,
                        'coreName':chinese,'catalogLabel':label,'status':'pass'})
    return {'status':'pass','source':str(path),'sourceSHA256':sha(path),'count':12,
            'enumOrdinalsMatch':True,'sitesOrderMatch':True,'namePrefixesMatch':True,'entries':records}

def make_reviews(stem,im,layout,labels,review,native=True):
    font=ImageFont.load_default(size=11)
    entries=layout['sprites'];cols=8;cw=146;ch=162
    sheet=Image.new('RGB',(cols*cw,((len(entries)+cols-1)//cols)*ch),'#182327')
    d=ImageDraw.Draw(sheet)
    for i,e in enumerate(entries):
        x=(i%cols)*cw;y=(i//cols)*ch
        # 4x checker under true alpha for every frame.
        for yy in range(0,128,16):
            for xx in range(0,128,16):
                col='#324348' if ((xx+yy)//16)%2 else '#27363a'
                d.rectangle((x+xx,y+yy,x+xx+15,y+yy+15),fill=col)
        frame=im.crop((e['x'],e['y'],e['x']+32,e['y']+32)).resize((128,128),Image.Resampling.NEAREST)
        sheet.paste(frame,(x,y),frame)
        d.text((x+1,y+129),e['name'],font=font,fill='#80d8bc')
        label=labels[i].encode('ascii','ignore').decode().strip(' /')
        d.text((x+1,y+143),label[:23],font=font,fill='#ddd5ac')
    sheet.save(review/(stem+'_contact_4x.png'))
    if not native:return
    # Native sheet against two actual gameplay-like background values.
    native=Image.new('RGB',(im.width,im.height*2+20),'#e3ddc8')
    native.paste(im,(0,0),im);d=ImageDraw.Draw(native)
    d.rectangle((0,im.height+20,im.width,im.height*2+20),fill='#263b35')
    native.paste(im,(0,im.height+20),im)
    native.save(review/(stem+'_native_light_dark.png'))

def check_atlas(stem,im,layout,sprites):
    assert im.mode=='RGBA',(stem,'not native RGBA',im.mode)
    a=np.array(im);h,w=a.shape[:2]
    assert (w,h)==(layout['width'],layout['height'])
    assert set(layout)=={'width','height','sprites'}
    assert not np.any((a[:,:,3]!=0)&(a[:,:,3]!=255)),stem+' partial alpha'
    expected=expected_atlas(sprites,layout)
    # Transparent RGB is unspecified by SVG renderers; compare visible data + alpha.
    assert np.array_equal(a[:,:,3],expected[:,:,3]),stem+' alpha differs from SVG authoring grid'
    visible=a[:,:,3]>0
    assert np.array_equal(a[visible],expected[visible]),stem+' SVG renderer changed palette/geometry'
    allowed=set(RGB)
    assert all(tuple(c) in allowed for c in np.unique(a[visible],axis=0)),stem+' off palette'
    used=np.zeros((h,w),dtype=bool);rows=[];hashes={};silhouettes={};duplicates=[]
    for index,e in enumerate(layout['sprites']):
        assert set(e)=={'name','x','y','w','h'}
        assert all(type(e[k]) is int for k in ['x','y','w','h'])
        x,y=e['x'],e['y'];assert (x,y)==((index%16)*36+2,(index//16)*36+2)
        assert e['w']==e['h']==32 and x+32<=w and y+32<=h
        assert not used[y:y+32,x:x+32].any();used[y:y+32,x:x+32]=True
        frame=a[y:y+32,x:x+32];mask=frame[:,:,3]>0;pixels=int(mask.sum())
        assert pixels>0,e['name']+' empty'
        fhash=hashlib.sha256(frame.tobytes()).hexdigest()
        assert fhash not in hashes,(e['name'],'duplicate pixels',hashes.get(fhash))
        hashes[fhash]=e['name']
        mh=hashlib.sha256(mask.tobytes()).hexdigest()
        if mh in silhouettes and not e['name'].startswith('terrain_'):
            duplicates.append([silhouettes[mh],e['name']])
        silhouettes[mh]=e['name']
        if e['name'].startswith('terrain_'):
            assert mask.all(),e['name']+' not opaque/full'
            assert np.array_equal(frame[0],frame[-1]) and np.array_equal(frame[:,0],frame[:,-1]),e['name']+' edge mismatch'
        rows.append({'name':e['name'],'visiblePixels':pixels,'sha256':fhash})
    gutter=int(np.count_nonzero(a[:,:,3][~used]));assert gutter==0,(stem,'gutter spill',gutter)
    return {'status':'pass','sprites':len(rows),'width':w,'height':h,'rgba':True,
            'partialAlphaPixels':0,'offPalettePixels':0,'gutterPixels':gutter,
            'duplicatePixelFrames':0,'emptyFrames':0,'sameSilhouettePairs':duplicates,'frames':rows}

def terrain_review(world,layout,review):
    out=Image.new('RGB',(6*136,142),'#192829');d=ImageDraw.Draw(out)
    yy,xx=np.indices((96,96));checks=[]
    for i in range(6):
        e=next(e for e in layout['sprites'] if e['name']==f'terrain_{i}')
        tile=np.array(world.crop((e['x'],e['y'],e['x']+32,e['y']+32)))
        mosaic=np.tile(tile,(3,3,1))
        field=terrain_field(i,xx,yy);truth=np.array(RGB,dtype=np.uint8)[field]
        assert np.array_equal(mosaic,truth),f'terrain_{i} global periodic rendering mismatch'
        checks.append({'name':f'terrain_{i}','status':'pass','opposingEdgeMismatch':0,
                       'independentWorldPixelsCompared':96*96,'worldRenderMismatch':0})
        # 2x view of four adjacent tiles, no gutters, shows actual repeated edges.
        patch=Image.fromarray(mosaic[:64,:64]).resize((128,128),Image.Resampling.NEAREST)
        out.paste(patch,(i*136,0));d.text((i*136+2,130),f'terrain_{i}',fill='#ddd5ac')
    out.save(review/'terrain_repetition_2x.png')
    return {'status':'pass','adjacency':'same material only; cross-biome transitions not supplied','tiles':checks}

def survivor_review(world,layout,review):
    sheet=Image.new('RGB',(4*140,4*148),'#263b35');d=ImageDraw.Draw(sheet);checks=[]
    for direction in range(4):
        frames=[]
        for f in range(4):
            name=f'survivor_{direction}_{f}'
            e=next(e for e in layout['sprites'] if e['name']==name)
            frame=world.crop((e['x'],e['y'],e['x']+32,e['y']+32));frames.append(np.array(frame))
            # Regression guards for the previously baked barrels; these are
            # geometry tests, not a substitute for visual empty-hand review.
            alpha=np.array(frame)[:,:,3]
            if direction==0:assert not alpha[:,27:].any(),name+' east barrel remains'
            elif direction==2:assert not alpha[:,:5].any(),name+' west barrel remains'
            elif direction==1:assert not alpha[1:13,25:28].any(),name+' raised rifle remains'
            else:assert not alpha[26:30,17].any(),name+' lowered barrel remains'
            pic=frame.resize((128,128),Image.Resampling.NEAREST);sheet.paste(pic,(f*140,direction*148),pic)
            d.text((f*140,direction*148+131),name,fill='#ddd5ac')
        assert len({hashlib.sha256(x.tobytes()).hexdigest() for x in frames})==4
        # Feet-only geometry must change, not just colour or whole-body translation.
        feet=[x[22:,:,3]>0 for x in frames]
        assert len({x.tobytes() for x in feet})==4,('duplicate boot pose',direction)
        checks.append({'direction':direction,'directionName':['east','north','west','south'][direction],
                       'uniqueFrames':4,'uniqueBootMasks':4,'status':'pass'})
    sheet.save(review/'survivor_gait_4x.png')
    return {'status':'pass','frames':16,'checks':checks,
            'identity':'existing project rust cap/coat, steel armour, teal visor/core and radio pack; empty hands for runtime equipment overlays',
            'bakedEquipment':False,'oldBarrelRegionsClear':16,
            'visualReview':'Empty hands require native/enlarged visual review; region assertions alone do not prove semantics.',
            'limits':'Four authored poses per direction; timing and locomotion feel not tested in game.'}

def main():
    p=argparse.ArgumentParser();p.add_argument('--render-id',default='v1');p.add_argument('--publish',action='store_true')
    p.add_argument('--review-only',action='store_true',help='recheck existing render; no renderer/service retry')
    args=p.parse_args();assert args.render_id.replace('-','').isalnum()
    reports=ROOT/'Reports';renders=reports/'Renders'/args.render_id;review=ROOT/'Review'
    review.mkdir(exist_ok=True);(ROOT/'Temp').mkdir(exist_ok=True)
    env=os.environ.copy();env.update({'TEMP':str(ROOT/'Temp'),'TMP':str(ROOT/'Temp'),
        'PYTHONDONTWRITEBYTECODE':'1','PYTHONUTF8':'1'})
    batches={'icons':all_icons(),'world':all_world()+all_extended()}
    # Fixed source contract, checked without modifying the catalogs.
    import re
    core=(PROJECT/'Assets/DeadCircuit/Scripts/Core/DemoState.cs').read_text(encoding='utf-8-sig')
    build_names=re.search(r'BuildNames\s*=\s*\{([^}]+)\}',core).group(1)
    assert re.findall(r'"([^"]+)"',build_names)==MACHINE_ZH,'BuildNames order drifted'
    build_enum=re.search(r'enum BuildKind\s*\{([^}]+)\}',core).group(1)
    assert [x.strip().split('=')[0].strip() for x in build_enum.split(',')]==MACHINES
    runtime={};qa={};allnames=[]
    qa['enemyCatalog']=check_enemy_catalog(batches['world'])
    qa['siteCatalog']=check_site_catalog(batches['world'])
    for stem,sprites in batches.items():
        export_svg(stem,sprites)
        source=ROOT/'Sources'/(stem+'.svg');out=renders/stem
        if not args.review_only:
            run_logged([sys.executable,'-B',SKILLS/'codeart2d/scripts/svg_render.py','lint',
                        '--svg',source,'--profile','pixel'],reports/(stem+'_lint.json'),env)
            run_logged([sys.executable,'-B',SKILLS/'codeart2d/scripts/svg_render.py','render',
                        '--svg',source,'--output-dir',out,'--backend','chrome','--crisp',
                        '--zoom','4','--strict-qc'],reports/(stem+'_render_'+args.render_id+'.json'),env)
        png=out/'base'/(stem+'.png');im=Image.open(png);im.load()
        layout=json.loads((ROOT/'Sources'/(stem+'.layout.json')).read_text())
        qa[stem]=check_atlas(stem,im,layout,sprites)
        make_reviews(stem,im,layout,[s.label for s in sprites],review)
        allnames.extend(e['name'] for e in layout['sprites'])
        runtime[stem]=(png,layout)
        spillout=reports/('spill_'+stem+'_'+args.render_id)
        if not spillout.exists():
            run_logged([sys.executable,'-B',SKILLS/'generate2dsprite/scripts/sheet_qc.py','spill',
                        '--input',png,'--rows',str(layout['height']//36),'--cols','16','--expect',str(len(sprites)),
                        '--key','none','--band','2','--safe-margin',str(2/36),'--min-area','1',
                        '--output-dir',spillout],reports/(stem+'_spill_command_'+args.render_id+'.json'),env)
        if stem=='world':
            qa['terrain']=terrain_review(im,layout,review)
            qa['survivor']=survivor_review(im,layout,review)
            for group,prefixes in [('machines_nodes',('machine_','node_')),
                                    ('living_world',('crop_','creature_','enemy_')),
                                    ('environment_fx',('biome_','site_','deco_','fx_'))]:
                selected=[e for e in layout['sprites'] if e['name'].startswith(prefixes)]
                label_by_name={s.name:s.label for s in sprites}
                make_reviews(group,im,{'sprites':selected},[label_by_name[e['name']] for e in selected],review,False)
    assert len(allnames)==len(set(allnames)),'duplicate names across atlases'
    assert required_names().issubset(allnames),required_names()-set(allnames)
    assert len(allnames)==241,len(allnames)
    qa.update({'status':'pass','totalSprites':len(allnames),'requiredKeysMissing':[],
        'nameDuplicates':0,'art_source':'code','placeholder':False,'disclosure':'code-drawn, no image model',
        'route':'hand-authored pixel grid > palette integer-rectangle SVG > codeart2d svg_render.py chrome --headless',
        'renderer':'chrome 157.0.8103.79 (doctor report)',
        'content':{'size':[32,32],'cellStride':36,'gutterEachSide':2,'coordinates':'top-left PNG pixels',
                   'pivot':[.5,.5],'pixelsPerUnit':32},
        'scope':'Only DemoAudio.cs and new SvgBatch art files/runtime atlases. No Git, Unity/game, GUI, build, service call, package or loader.',
        'limits':['Static single poses/FX; survivor alone has 4 directions x 4 gait frames.','Six terrain materials repeat seamlessly with themselves; no cross-biome transition set.',
                  'Flat code-drawn pixel art needs main-agent aesthetic review.','No Unity import, running-game visual/audio test or compilation in this task.'],
        'runtime':{}})
    if args.publish:
        RUNTIME.mkdir(parents=True,exist_ok=True)
        for stem,(png,layout) in runtime.items():
            for ext in ['.png','.json']:
                target=RUNTIME/(stem+ext)
                if target.exists():
                    # Never overwrite external changes. Re-publishing is idempotent only.
                    if ext=='.png':assert sha(target)==sha(png),'existing runtime image differs: '+str(target)
                    else:assert json.loads(target.read_text())==layout,'existing runtime index differs: '+str(target)
                elif ext=='.png':shutil.copyfile(png,target)
                else:write_json(target,layout)
            qa['runtime'][stem]={'path':str(RUNTIME/(stem+'.png')),'sha256':sha(RUNTIME/(stem+'.png')),
                                 'jsonSha256':sha(RUNTIME/(stem+'.json')),'sprites':len(layout['sprites'])}
    write_json(reports/'atlas_qa.json',qa)
    print(json.dumps({'status':'pass','sprites':len(allnames),'atlases':{k:len(v[1]['sprites']) for k,v in runtime.items()},
                      'published':args.publish,'qa':str(reports/'atlas_qa.json')}))

if __name__=='__main__':main()
