"""Read-only comparison of the retained historical 151/90-frame review round.

This is not the current exporter. It never writes Sources, runtime files or
Reports, and --publish is rejected before any comparison or resource access.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review-only', action='store_true', help='compatibility alias; all operations are read-only')
    parser.add_argument('--publish', action='store_true', help='retired; publication is always rejected')
    args = parser.parse_args()
    if args.publish:
        parser.error('Historical publication is disabled. Use export_and_qc.py --publish for current sources.')
    report = json.loads((ROOT / 'Reports/visual_revision1_qa.json').read_text(encoding='utf-8'))
    old_path = ROOT / 'Reports/Renders/final/world/base/world.png'
    corrected_path = ROOT / 'Reports/Renders/visualreview1/world/base/world.png'
    icons_path = ROOT / 'Reports/Renders/final/icons/base/icons.png'
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest(old_path) == report['previousWorldSHA256']
    assert digest(corrected_path) == report['newWorldSHA256']
    assert digest(icons_path) == report['iconsPreserved']['pngSHA256']
    old = Image.open(old_path).convert('RGBA')
    corrected = Image.open(corrected_path).convert('RGBA')
    assert old.size == corrected.size == (576, 360)
    rows = report['worldQA']['frames']
    assert len(rows) == 151
    changed = []
    for index, row in enumerate(rows):
        x, y = index % 16 * 36 + 2, index // 16 * 36 + 2
        box = (x, y, x + 32, y + 32)
        actual = corrected.crop(box).tobytes()
        assert hashlib.sha256(actual).hexdigest() == row['sha256'], row['name']
        if actual != old.crop(box).tobytes():
            changed.append(row['name'])
    assert set(changed) == set(report['changedFrames']) and len(changed) == 37
    print(json.dumps({'status': 'pass', 'scope': 'historical render comparison only',
                      'historicalWorldFrames': 151, 'historicalIconFrames': 90,
                      'historicalChangedFrames': len(changed), 'filesWritten': [],
                      'currentRuntimeValidated': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
