"""Review round 1: fix enemy/site contracts and remove baked survivor weapons.

No Git, Unity/game, GUI, service request, audio or icons edits. All subprocess
logs/temp files stay in this batch; each subprocess has a 120 second deadline.
"""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

import numpy as np
from PIL import Image
from pixel_svg import ROOT, export_svg
from svg_world import all_world
from svg_machines_survivor import all_extended
from export_and_qc import (PROJECT,RUNTIME,SKILLS,write_json,run_logged,sha,
                           check_atlas,check_enemy_catalog,check_site_catalog,
                           survivor_review,make_reviews,required_names)

REV='visualreview1'

def main():
    p=argparse.ArgumentParser();p.add_argument('--review-only',action='store_true');p.add_argument('--publish',action='store_true')
    args=p.parse_args();reports=ROOT/'Reports';review=ROOT/'Review'
    syntax=[]
    for source in sorted(ROOT.glob('*.py')):
        ast.parse(source.read_text(encoding='utf-8-sig'),filename=str(source))
        syntax.append(source.name)
    baseline_dir=reports/'Renders/final';out=reports/'Renders'/REV/'world'
    original=baseline_dir/'world/base/world.png';published=RUNTIME/'world.png'
    runtime_json=RUNTIME/'world.json'
    before_files={p.relative_to(PROJECT).as_posix():sha(p) for p in RUNTIME.iterdir() if p.is_file()}
    old_layout=json.loads(runtime_json.read_text(encoding='utf-8'))
    assert sha(RUNTIME/'icons.png')==sha(baseline_dir/'icons/base/icons.png'),'icons changed externally'
    icons_before=sha(RUNTIME/'icons.png');icons_json_before=sha(RUNTIME/'icons.json')
    sprites=all_world()+all_extended();catalog_check=check_enemy_catalog(sprites)
    site_check=check_site_catalog(sprites)
    env=os.environ.copy();env.update({'TEMP':str(ROOT/'Temp'),'TMP':str(ROOT/'Temp'),
                                    'PYTHONDONTWRITEBYTECODE':'1','PYTHONUTF8':'1'})
    export_svg('world',sprites)
    new_layout=json.loads((ROOT/'Sources/world.layout.json').read_text(encoding='utf-8'))
    assert old_layout==new_layout,'visual fix must preserve every key and atlas rectangle'
    if not args.review_only:
        run_logged([sys.executable,'-B',SKILLS/'codeart2d/scripts/svg_render.py','lint',
                    '--svg',ROOT/'Sources/world.svg','--profile','pixel'],reports/'world_lint.json',env)
        run_logged([sys.executable,'-B',SKILLS/'codeart2d/scripts/svg_render.py','render',
                    '--svg',ROOT/'Sources/world.svg','--output-dir',out,'--backend','chrome',
                    '--crisp','--zoom','4','--strict-qc'],reports/('world_render_'+REV+'.json'),env)
    png=out/'base/world.png';im=Image.open(png);im.load()
    world_qa=check_atlas('world',im,new_layout,sprites)
    old=np.array(Image.open(original).convert('RGBA'));new=np.array(im)
    assert old.shape==new.shape==(360,576,4)
    permitted=np.zeros(old.shape[:2],dtype=bool);changed=[];preserved=[]
    allowed={f'enemy_{i}' for i in range(3,12)}|{f'site_{i}' for i in range(12)}
    allowed|={f'survivor_{d}_{f}' for d in range(4) for f in range(4)}
    for entry in new_layout['sprites']:
        name=entry['name'];x,y=entry['x'],entry['y']
        same=np.array_equal(old[y:y+32,x:x+32],new[y:y+32,x:x+32])
        (preserved if same else changed).append(name)
        if name in allowed:permitted[y:y+32,x:x+32]=True
        else:assert same,'unrelated sprite changed: '+name
    assert set(changed)==allowed and len(changed)==37,changed
    assert np.array_equal(old[~permitted],new[~permitted]),'pixels changed outside approved 37 rectangles'
    assert sha(published) in (sha(original),sha(png)),'runtime world PNG was externally modified; refusing overwrite'
    spillout=reports/('spill_world_'+REV)
    if not spillout.exists():
        run_logged([sys.executable,'-B',SKILLS/'generate2dsprite/scripts/sheet_qc.py','spill',
                    '--input',png,'--rows','10','--cols','16','--expect','151','--key','none',
                    '--band','2','--safe-margin',str(2/36),'--min-area','1','--output-dir',spillout],
                   reports/('world_spill_command_'+REV+'.json'),env)
    metadata=json.loads((out/'codeart-meta.json').read_text(encoding='utf-8'))
    spill=json.loads((spillout/'sheet-qc.json').read_text(encoding='utf-8'))
    assert metadata['qa']['status']==spill['status']=='pass'
    issues=[c for envelope in (metadata['qa'],spill) for c in envelope['checks'] if c['status'] in ('warn','fail')]
    assert not issues,issues
    label_by_name={s.name:s.label for s in sprites}
    # Explicitly read back the generated labels, not only in-memory authoring objects.
    catalog=json.loads((ROOT/'Sources/world.catalog.json').read_text(encoding='utf-8'))
    actual_labels={s['name']:s['label'] for s in catalog}
    for entry in catalog_check['entries']+site_check['entries']:
        assert actual_labels[entry['key']]==entry['catalogLabel']
    icons_layout=json.loads((RUNTIME/'icons.json').read_text(encoding='utf-8'))
    allnames=[e['name'] for layout in [icons_layout,new_layout] for e in layout['sprites']]
    assert len(allnames)==len(set(allnames))==241
    assert required_names().issubset(allnames)
    pixel_hashes={}
    for stem,texture,layout in [('icons',Image.open(RUNTIME/'icons.png'),icons_layout),('world',im,new_layout)]:
        assert texture.mode=='RGBA' and texture.size==(layout['width'],layout['height'])
        for entry in layout['sprites']:
            x,y=entry['x'],entry['y'];frame=texture.crop((x,y,x+32,y+32))
            digest=hashlib.sha256(frame.tobytes()).hexdigest()
            assert digest not in pixel_hashes,(entry['name'],'duplicate across atlases',pixel_hashes.get(digest))
            pixel_hashes[digest]=entry['name']
    survivor_qa=survivor_review(im,new_layout,review)
    make_reviews('world',im,new_layout,[s.label for s in sprites],review)
    for group,prefixes in [('living_world',('crop_','creature_','enemy_')),('enemy',('enemy_',)),
                           ('sites',('site_',)),('environment_fx',('biome_','site_','deco_','fx_'))]:
        selected=[e for e in new_layout['sprites'] if e['name'].startswith(prefixes)]
        make_reviews(group,im,{'sprites':selected},[label_by_name[e['name']] for e in selected],review,False)
    # Compact 1x light/dark strips supplement enlarged review sheets.
    for group,prefix in [('enemy','enemy_'),('sites','site_'),('survivor','survivor_')]:
        entries=[e for e in new_layout['sprites'] if e['name'].startswith(prefix)]
        native=Image.new('RGB',(len(entries)*36,76),'#e3ddc8')
        for j,e in enumerate(entries):
            frame=im.crop((e['x'],e['y'],e['x']+32,e['y']+32));native.paste(frame,(j*36+2,2),frame)
            native.paste(Image.new('RGB',(36,36),'#263b35'),(j*36,40));native.paste(frame,(j*36+2,42),frame)
        native.save(review/(group+'_native_light_dark.png'))
    result={'status':'pass','revision':1,'renderId':REV,'enemyCatalog':catalog_check,
            'siteCatalog':site_check,'survivor':survivor_qa,
            'changedFrames':changed,'preservedWorldFrames':len(preserved),
            'iconsPreserved':{'frames':90,'pngSHA256':icons_before,'jsonSHA256':icons_json_before},
            'worldLayoutIdentical':True,'pixelsChangedOutsideApproved37Frames':0,
            'runtimeFilesDifferentFromOriginalBaseline':['Assets/DeadCircuit/Resources/Art/Expansion/world.png'],
            'totalSprites':241,'nameDuplicates':0,'requiredKeysMissing':[],
            'duplicateFramesAcrossBoth':0,'pythonSyntax':{'status':'pass','files':syntax},
            'selfVisualReview':{'views':['native light/dark','4x enemy/sites contacts','4x survivor gait'],
                'observations':['enemy_7 open jaw and cry marks; enemy_10 biped infected with strapped bundle',
                    'site_0..11 are differentiated facades/entrances in Core order, separate from machine_*',
                    'All 16 survivor frames show empty hands; no baked held or slung weapons'],
                'mainApproval':'pending; no game or GUI preview performed'},
            'worldQA':world_qa,'issues':issues,'renderer':metadata['renderer'],'published':args.publish,
            'actualWorldPNG':str(png),'newWorldSHA256':sha(png),'previousWorldSHA256':sha(original)}
    if args.publish:
        # Narrow replacement explicitly authorized by the main agent's review request.
        if sha(published)!=sha(png):shutil.copyfile(png,published)
        assert sha(published)==sha(png)
        assert json.loads(runtime_json.read_text(encoding='utf-8'))==new_layout
        assert sha(RUNTIME/'icons.png')==icons_before and sha(RUNTIME/'icons.json')==icons_json_before
        atlas_qa=json.loads((reports/'atlas_qa.json').read_text(encoding='utf-8'))
        atlas_qa['world']=world_qa;atlas_qa['enemyCatalog']=catalog_check
        atlas_qa['siteCatalog']=site_check;atlas_qa['survivor']=survivor_qa
        atlas_qa['activeRenderIds']={'icons':'final','world':REV}
        atlas_qa['runtime']['world'].update({'sha256':sha(published),'jsonSha256':sha(runtime_json)})
        atlas_qa['visualRevision']={'round':1,'changedFrames':changed,'preservedWorldFrames':len(preserved)}
        atlas_qa['visualRevision']['report']='visual_revision1_qa.json'
        write_json(reports/'atlas_qa.json',atlas_qa)
        handoff=json.loads((reports/'handoff_check.json').read_text(encoding='utf-8'))
        for atlas in handoff['atlases']:
            if atlas['name']=='world':atlas['pngSHA256']=sha(published)
        handoff['enemyCatalog']=catalog_check
        handoff['siteCatalog']=site_check;handoff['survivor']=survivor_qa
        handoff['duplicateFramesAcrossBoth']=0
        handoff['visualRevision']={'round':1,'status':'pass','report':'visual_revision1_qa.json',
            'changedFrames':changed,'preservedWorldFrames':len(preserved),'preservedIconFrames':90,
            'worldLayoutIdentical':True,'runtimeJSONRevalidated':True,'issues':[]}
        write_json(reports/'handoff_check.json',handoff)
    after_files={p.relative_to(PROJECT).as_posix():sha(p) for p in RUNTIME.iterdir() if p.is_file()}
    result['runtimeFilesChanged']=[p for p,v in after_files.items() if before_files.get(p)!=v]
    assert set(result['runtimeFilesChanged']).issubset({'Assets/DeadCircuit/Resources/Art/Expansion/world.png'})
    write_json(reports/'visual_revision1_qa.json',result)
    print(json.dumps({k:v for k,v in result.items() if k not in ('worldQA','enemyCatalog','siteCatalog','survivor')},ensure_ascii=False))

if __name__=='__main__':main()
