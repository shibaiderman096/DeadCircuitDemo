"""Package factual handoff evidence; never invokes a media route or Unity."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
LOG = ROOT / 'Logs/ExpansionArt'
OUT = PROJECT / 'Assets/DeadCircuit/Resources/Audio/Expansion'


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(name, data):
    (LOG / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*.wav')}
    commands = []
    for script in ('synthesize_audio.py', 'verify_audio.py'):
        run = subprocess.run([sys.executable, '-B', str(ROOT / script)],
                             capture_output=True, text=True, encoding='utf-8',
                             cwd=PROJECT, timeout=120)
        commands.append({'script': script, 'exit_code': run.returncode,
                         'stdout': run.stdout.strip(), 'stderr': run.stderr.strip()})
        if run.returncode:
            write('reproducibility.json', {'status': 'FAIL', 'commands': commands})
            raise SystemExit(run.returncode)
    after = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*.wav')}
    stable = bool(before) and before == after
    write('reproducibility.json', {'status': 'PASS' if stable else 'FAIL',
          'file_count': len(after), 'all_wav_sha256_equal': stable,
          'commands': commands, 'sha256': after})
    if not stable:
        raise SystemExit('Generated PCM changed unexpectedly')

    route = read(LOG / 'route_attempt.json')
    assert route['exit_code'] == 1 and 'RATE_LIMIT' in route['stderr']
    assert route['fake_route_enabled'] is False and route['codeart_allowed'] is False
    receipts = list((ROOT / '.forge/cli-runs').glob('*.json'))
    assert len(receipts) == 1, 'Exactly one image attempt is permitted for this batch'
    shutil.copyfile(receipts[0], LOG / 'route_receipt.json')
    receipt = read(receipts[0])
    doctor = read(LOG / 'doctor.json')
    diagnostics = [c for c in doctor['checks'] if c['status'] in ('WARN', 'FAIL', 'MISSING', 'UNKNOWN')]
    audio = read(LOG / 'audio_qa.json')
    manifest = read(OUT / 'audio_manifest.json')
    catalog = PROJECT / 'Assets/DeadCircuit/Scripts/Core/EquipmentCatalog.cs'
    content = catalog.read_text(encoding='utf-8-sig')
    # Source-order evidence only, explicitly not a runtime sprite manifest.
    names = {}
    for field in ('GearNames', 'PartNames'):
        match = re.search(r'\b' + field + r'\s*=\s*\{([^}]+)\}', content)
        names[field] = re.findall(r'"([^"]+)"', match.group(1))
    assert len(names['GearNames']) == 19 and len(names['PartNames']) == 7
    write('catalog_order_readback.json', {'purpose': 'Read-only order snapshot, not delivered image assets',
          'source': str(catalog.relative_to(PROJECT)).replace('\\', '/'),
          'source_sha256': hashlib.sha256(catalog.read_bytes()).hexdigest(), **names})
    files = sorted(OUT.glob('*'))
    report = {
        'schema': 'deadcircuit.expansion.batch.v1', 'status': 'PARTIAL_IMAGE_ROUTE_BLOCKED',
        'created_at_utc': datetime.now(timezone.utc).isoformat(),
        'modified_asset_scope': 'Assets/DeadCircuit/Resources/Audio/Expansion/',
        'tool_scope': 'Tools/ExpansionArt/',
        'existing_art_characters_and_source_modified_by_this_batch': False,
        'counts': {'wav': len(manifest['clips']), 'audio_categories': audio['categories'],
                   'required_audio_categories': audio['required_categories'],
                   'loop_audio': audio['loop_count'], 'png': 0, 'sprite_rectangles': 0},
        'audio': {'source': 'original procedural Python synthesis, not recordings',
                  'numeric_qa': audio['status'], 'reproducibility': 'PASS',
                  'sample_rate': 44100, 'channels': 1, 'format': 'PCM16',
                  'max_peak': audio['max_peak'], 'clipped_files': 0,
                  'listening_test': 'NOT_PERFORMED', 'game_integration': 'NOT_PERFORMED'},
        'image_route': {'attempts': 1, 'route': 'local:codex-cli',
                        'cli_version': receipt['cliRun']['version'],
                        'model_returned': None, 'result': 'RATE_LIMIT',
                        'exit_code': route['exit_code'], 'artifacts': [],
                        'codeart_fallback_used': False,
                        'reason': 'Fallback requires no-route exit 3; actual exit was 1.'},
        'image_qa': {k: 'NOT_RUN_NO_IMAGES' for k in
                     ('rgba_true_alpha', 'integral_32_64_grid', 'gutter', 'sheet_qc_spill', 'rectangles_pivots')},
        'doctor_diagnostics_verbatim': diagnostics,
        'prohibited_operations_performed': {'build': False, 'windows_game_launch': False,
                                          'unity_play': False, 'gui': False,
                                          'auth_model_or_key_change': False, 'remote_push': False},
        'pending': ['All 19 gear and 7 parts images', 'UI panel/button/slot/windowframe textures',
                    '6 biome surfaces', 'wild shrubs and crops', '12 building rooftop symbols',
                    'weather, sparks, muzzle, impact, explosion visual FX',
                    'real image sources / atlas.json / alpha-grid-spill QA', 'Main reviewer listening and integration'],
        'resource_files': [{'path': str(p.relative_to(PROJECT)).replace('\\', '/'),
                            'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
                           for p in files if p.is_file()]
    }
    write('batch_report.json', report)
    print(json.dumps({'status': report['status'], 'counts': report['counts'],
                      'audio_qa': audio['status'], 'reproducibility': stable,
                      'image_route_exit': route['exit_code'],
                      'resource_bytes': sum(p.stat().st_size for p in files if p.is_file())}))


if __name__ == '__main__':
    main()
