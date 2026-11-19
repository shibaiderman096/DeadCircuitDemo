"""Deterministically author SVG, lint, render, verify and publish landmarks.

Writes only LandmarkArt/** and the four explicitly owned runtime asset paths.
All PNGs, including review graphics, come from SVG via codeart2d/resvg-py;
4x images are integer nearest-neighbour enlargements produced by that renderer.
No Unity, Git, browser, image model, old generator or dependency installation.
"""
import argparse
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid

sys.dont_write_bytecode = True
from landmark_svg import ROOT, PROJECT, RUNTIME, SOURCE, write_json, write_sources
from verify import verify, protected_check, sha

DEFAULT_RENDERER = Path('C:/Users/man/.codex/skills/codeart2d/scripts/svg_render.py')


def run(argv, timeout=120):
    env = dict(os.environ, PYTHONUTF8='1', PYTHONDONTWRITEBYTECODE='1')
    p = subprocess.run([str(a) for a in argv], cwd=PROJECT, env=env,
                       text=True, capture_output=True, encoding='utf-8', errors='replace', timeout=timeout)
    record = dict(argv=[str(a) for a in argv], exitCode=p.returncode, stdout=p.stdout, stderr=p.stderr)
    if p.returncode:
        raise RuntimeError(json.dumps(record, ensure_ascii=False))
    return record


def source_hashes():
    return {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted(SOURCE.iterdir()) if p.is_file()}


def asset_guid(filename):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deadcircuit/svg-landmarks/v1/'+filename).hex


def write_metas():
    # Reference the texture import conventions, never run or modify ModularArt.
    texture = (RUNTIME / 'modules.png.meta').read_text(encoding='utf-8')
    textmeta = (RUNTIME / 'modules.json.meta').read_text(encoding='utf-8')
    for filename, template in (('landmarks.png', texture), ('landmarks.json', textmeta)):
        out = RUNTIME / (filename+'.meta')
        guid = asset_guid(filename)
        if out.exists():
            match = re.search(r'^guid: ([0-9a-f]{32})$', out.read_text(encoding='utf-8'), re.M)
            assert match, ('Invalid existing landmark meta', out)
            guid = match.group(1)  # Preserve references if re-exporting after import.
        template = re.sub(r'^guid: [0-9a-f]{32}$', 'guid: '+guid, template, flags=re.M)
        template = re.sub(r'textureCompression: [0-9]+', 'textureCompression: 0', template)
        out.write_text(template, encoding='utf-8', newline='\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--renderer', type=Path, default=DEFAULT_RENDERER)
    parser.add_argument('--doctor', action='store_true', help='Run local renderer conformance before export')
    args = parser.parse_args()
    assert args.renderer.is_file(), args.renderer
    protected_check()
    authored = write_sources()
    first = source_hashes()
    write_sources()
    assert source_hashes() == first, 'Source generation is not deterministic'
    run_index = 1
    while (ROOT / 'Reports' / f'run_{run_index:03d}').exists():
        run_index += 1
    run_id = f'run_{run_index:03d}'
    reports = ROOT / 'Reports' / run_id
    reports.mkdir(parents=True)
    renders = ROOT / 'Renders' / run_id
    if args.doctor:
        doctor = run([sys.executable, '-B', args.renderer, 'doctor', '--backend', 'resvg_py',
                      '--report', reports / 'renderer_doctor.json'])
        write_json(reports / 'doctor_command.json', doctor)
    lint_records = []
    for path in sorted(SOURCE.glob('*.svg')):
        lint_records.append(run([sys.executable, '-B', args.renderer, 'lint', '--svg', path, '--profile', 'pixel']))
    write_json(reports / 'lint_commands.json', dict(status='pass', count=len(lint_records), commands=lint_records))
    render_records, metadata_paths = [], []
    for stem in ('landmarks', 'landmarks_review', 'floor_tiling_review'):
        directory = renders / stem
        render_records.append(run([sys.executable, '-B', args.renderer, 'render', '--svg', SOURCE/(stem+'.svg'),
                                   '--output-dir', directory, '--backend', 'resvg_py', '--crisp', '--zoom', '4', '--strict-qc']))
        meta_path = directory / 'codeart-meta.json'
        metadata = json.loads(meta_path.read_text(encoding='utf-8'))
        assert metadata['qa']['status'] == 'pass', metadata['qa']
        metadata_paths.append(meta_path.relative_to(ROOT).as_posix())
    write_json(reports / 'render_commands.json', render_records)
    atlas = renders / 'landmarks/base/landmarks.png'
    staged = verify(atlas, published=False)
    write_json(reports / 'staged_verify.json', staged)
    protected_check()
    shutil.copyfile(atlas, RUNTIME / 'landmarks.png')
    shutil.copyfile(SOURCE / 'landmarks.layout.json', RUNTIME / 'landmarks.json')
    write_metas()
    review = ROOT / 'Review'
    review.mkdir(exist_ok=True)
    for stem in ('landmarks_review', 'floor_tiling_review'):
        shutil.copyfile(renders/stem/'base'/(stem+'.png'), review/(stem+'_1x.png'))
        shutil.copyfile(renders/stem/'base'/(stem+'@4x.png'), review/(stem+'_4x.png'))
    # Atlas review retains the transparent gutters at both exact integer scales.
    shutil.copyfile(atlas, review / 'landmarks_atlas_1x.png')
    shutil.copyfile(renders/'landmarks/base/landmarks@4x.png', review/'landmarks_atlas_4x.png')
    result = verify()
    result.update(lintedSvgCount=len(lint_records), sourceGenerationRepeatIdentical=True,
                  rendererMetadata=metadata_paths, python=sys.executable,
                  dependencies={name: importlib.metadata.version(name) for name in ('Pillow', 'numpy', 'resvg-py')},
                  run=run_id)
    write_json(reports/'published_verify.json', result)
    write_json(ROOT/'Reports/latest_verify.json', result)
    write_json(ROOT/'codeart-meta.json', dict(art_source='code', placeholder=False,
               disclosure='code-drawn, no image model', source='Sources/landmarks.svg',
               sourceSHA256=result['svgSHA256'], manifest='Sources/manifest.json',
               rendererMetadata=metadata_paths, verification='Reports/latest_verify.json',
               limits=result['limits']))
    digest_paths = list(SOURCE.iterdir()) + [RUNTIME/(n+suffix) for n in ('landmarks.png', 'landmarks.json') for suffix in ('', '.meta')]
    digest_paths += sorted(review.glob('*.png'))
    digests = {p.relative_to(PROJECT).as_posix(): sha(p) for p in sorted(digest_paths) if p.is_file()}
    previous = ROOT / 'Reports/reproducibility_baseline.json'
    if previous.exists():
        before = json.loads(previous.read_text(encoding='utf-8'))
        identical = before == digests
        changes = [p for p in set(before) | set(digests) if before.get(p) != digests.get(p)]
        write_json(reports/'repeat_export.json', dict(status='pass' if identical else 'changed',
                   identical=identical, comparedFiles=len(digests), changed=sorted(changes)))
    else:
        write_json(previous, digests)
    write_json(reports/'output_hashes.json', digests)
    protected_check()
    print(json.dumps(dict(status='pass', **authored, lintedSvgCount=len(lint_records),
                          sourcePixelMismatches=0, gutterSpillPixels=0, protectedHashChanges=0,
                          report=(reports/'published_verify.json').as_posix()), ensure_ascii=False))


if __name__ == '__main__':
    main()
