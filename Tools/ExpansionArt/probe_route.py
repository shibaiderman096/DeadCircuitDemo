"""One real Forge image attempt; keeps Forge/temp/CLI diagnostic paths local."""
import json
import os
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent
LOG = ROOT / 'Logs' / 'ExpansionArt'
LOG.mkdir(parents=True, exist_ok=True)
if '--inner' not in sys.argv and (LOG / 'route_attempt.json').exists():
    raise SystemExit('A real image attempt is already recorded. No automatic retry is permitted.')
if os.environ.get('FORGE_ROUTE_MEDIA_FAKE'):
    raise SystemExit('Refusing fake-route environment: this probe requires a real route.')
TMP = LOG / 'temp'
TMP.mkdir(exist_ok=True)
os.environ['TMP'] = os.environ['TEMP'] = str(TMP)
os.environ['PYTHONDONTWRITEBYTECODE'] = '1'
os.environ['PYTHONUTF8'] = '1'
SKILL = Path('C:/Users/man/.codex/skills/generate2dmedia/scripts')

if '--inner' in sys.argv:
    sys.path.insert(0, str(SKILL))
    import route_media
    import cli_media
    original = cli_media.codex_argv
    def local_logs(*args, **kwargs):
        argv = original(*args, **kwargs)
        return argv[:-1] + ['-c', 'log_dir=' + json.dumps(str(LOG / 'codex')),
                           '-c', 'sqlite_home=' + json.dumps(str(LOG / 'state'))] + argv[-1:]
    cli_media.codex_argv = local_logs
    raise SystemExit(route_media.main([
        'image', '--prompt-file', str(ROOT / 'equipment_prompt.txt'),
        '--size', '1024x1024', '--out-dir', str(ROOT / 'RouteImage'),
        '--project-dir', str(ROOT), '--timeout', '110',
        '--purpose', 'DeadCircuit independent expansion equipment first batch']))

try:
    result = subprocess.run([sys.executable, '-B', __file__, '--inner'],
                            cwd=ROOT, capture_output=True, timeout=120)
    stdout = result.stdout.decode('utf-8', 'replace')
    stderr = result.stderr.decode('utf-8', 'replace')
    record = {'entrypoint': str(SKILL / 'route_media.py'), 'real_request': True,
              'fake_route_enabled': bool(os.environ.get('FORGE_ROUTE_MEDIA_FAKE')),
              'timeout_seconds': 120, 'exit_code': result.returncode,
              'stdout': stdout, 'stderr': stderr,
              'overrides': 'diagnostic/temp paths only; auth, API keys and model untouched',
              'codeart_allowed': result.returncode == 3}
except subprocess.TimeoutExpired as exc:
    record = {'real_request': True, 'timeout_seconds': 120, 'exit_code': 'timeout',
              'stdout': (exc.stdout or b'').decode('utf-8', 'replace'),
              'stderr': (exc.stderr or b'').decode('utf-8', 'replace'),
              'codeart_allowed': False}
(LOG / 'route_attempt.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(record, ensure_ascii=True))
