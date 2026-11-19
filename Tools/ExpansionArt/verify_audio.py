"""Independent PCM/header, silence, clipping, spectrum and loop-seam audit."""
import hashlib
import json
from pathlib import Path
import wave

import numpy as np

ROOT = Path(__file__).resolve().parent
OUT = ROOT.parents[1] / 'Assets/DeadCircuit/Resources/Audio/Expansion'
LOG = ROOT / 'Logs/ExpansionArt'
REQUIRED = set('click open close equip unequip reload_start reload_complete pistol shotgun rifle sniper knife hit gather water sow harvest thunder wind rain'.split())


def main():
    manifest = json.loads((OUT / 'audio_manifest.json').read_text(encoding='utf-8'))
    rows, errors, hashes = [], [], {}
    for entry in manifest['clips']:
        path = OUT / entry['file']
        checks = {}
        with wave.open(str(path), 'rb') as wav:
            meta = {'sample_rate': wav.getframerate(), 'channels': wav.getnchannels(),
                    'sample_width': wav.getsampwidth(), 'frames': wav.getnframes(),
                    'compression': wav.getcomptype()}
            raw = wav.readframes(wav.getnframes())
        pcm = np.frombuffer(raw, dtype='<i2').astype(np.int32)
        x = pcm / 32768.0
        peak = float(np.max(np.abs(x)))
        rms = float(np.sqrt(np.mean(x * x)))
        duration = len(x) / meta['sample_rate']
        checks['format'] = meta['sample_rate'] == 44100 and meta['channels'] == 1 and meta['sample_width'] == 2 and meta['compression'] == 'NONE'
        checks['duration'] = abs(duration - entry['duration']) < 1 / 44100 and .04 <= duration <= 10
        checks['no_clipping'] = bool(np.max(np.abs(pcm)) < 32767 and peak <= .88)
        checks['audible_energy'] = rms > .008 and peak > .3
        checks['dc_offset'] = abs(float(np.mean(x))) < .0001
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        checks['manifest_hash'] = digest == entry['sha256']
        pcm_hash = hashlib.sha256(raw).hexdigest()
        checks['unique_pcm'] = pcm_hash not in hashes
        hashes[pcm_hash] = entry['name']
        spectrum = np.abs(np.fft.rfft(x)) ** 2
        f = np.fft.rfftfreq(len(x), 1 / meta['sample_rate'])
        centroid = float(np.sum(f * spectrum) / max(np.sum(spectrum), 1e-12))
        bands = [float(np.sum(spectrum[(f >= low) & (f < high)]) / np.sum(spectrum))
                 for low, high in ((0, 200), (200, 1500), (1500, 6000), (6000, 22051))]
        seam = int(abs(pcm[0] - pcm[-1]))
        if entry['loop']:
            boundary_slopes = [int(abs(pcm[1] - pcm[0])), int(abs(pcm[-1] - pcm[-2]))]
            checks['seam_equal'] = seam <= 1
            checks['seam_slopes'] = max(boundary_slopes) <= 6
            width = round(.010 * meta['sample_rate'])
            seam_rms = float(np.sqrt(np.mean(np.concatenate([x[-width:], x[:width]]) ** 2)))
            checks['no_silent_loop_gap'] = seam_rms > rms * .10
        else:
            boundary_slopes = None
            seam_rms = None
            checks['one_shot_zero_endpoints'] = pcm[0] == pcm[-1] == 0
        checks = {k: bool(v) for k, v in checks.items()}
        failed = [k for k, v in checks.items() if not v]
        errors.extend([entry['name'] + ':' + k for k in failed])
        rows.append({'name': entry['name'], 'category': entry['category'], **meta,
                     'duration_seconds': duration, 'peak': peak,
                     'peak_dbfs': 20 * float(np.log10(max(peak, 1e-12))),
                     'rms': rms, 'dc_offset': float(np.mean(x)),
                     'spectral_centroid_hz': centroid, 'band_energy_fractions': bands,
                     'loop': entry['loop'], 'seam_delta_pcm_lsb': seam,
                     'seam_slope_pcm_lsb': boundary_slopes, 'seam_10ms_rms': seam_rms,
                     'checks': checks})
    missing = REQUIRED - {e['category'] for e in manifest['clips']}
    errors.extend('missing_category:' + k for k in sorted(missing))
    unexpected = {p.name for p in OUT.glob('*.wav')} - {e['file'] for e in manifest['clips']}
    errors.extend('unlisted_wav:' + name for name in sorted(unexpected))
    pairs = [('ui_open', 'ui_close'), ('gear_equip', 'gear_unequip'),
             ('reload_start', 'reload_complete'), ('weapon_pistol', 'weapon_shotgun'),
             ('weapon_rifle', 'weapon_sniper'), ('gather_wood', 'gather_ore')]
    lookup = {r['name']: r for r in rows}
    comparisons = []
    for a, b in pairs:
        va, vb = np.array(lookup[a]['band_energy_fractions']), np.array(lookup[b]['band_energy_fractions'])
        distance = float(np.linalg.norm(va - vb))
        comparisons.append({'a': a, 'b': b, 'spectral_band_distance': distance,
                            'a_centroid': lookup[a]['spectral_centroid_hz'],
                            'b_centroid': lookup[b]['spectral_centroid_hz'],
                            'a_duration': lookup[a]['duration_seconds'],
                            'b_duration': lookup[b]['duration_seconds']})
    report = {'schema': 'deadcircuit.expansion.audio_qa.v1',
              'status': 'PASS' if not errors else 'FAIL', 'wav_count': len(rows),
              'categories': len({e['category'] for e in manifest['clips']}),
              'required_categories': len(REQUIRED), 'missing_categories': sorted(missing),
              'loop_count': sum(r['loop'] for r in rows),
              'min_duration': min(r['duration_seconds'] for r in rows),
              'max_duration': max(r['duration_seconds'] for r in rows),
              'max_peak': max(r['peak'] for r in rows), 'errors': errors,
              'limitations': ['No listening test performed; numeric seam/spectrum checks do not establish perceptual quality.',
                              'No Unity import, build, game launch, Windows playback or UnityPlay performed.'],
              'design_comparisons': comparisons, 'clips': rows}
    LOG.mkdir(parents=True, exist_ok=True)
    (LOG / 'audio_qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: report[k] for k in ('status', 'wav_count', 'categories', 'required_categories', 'loop_count', 'min_duration', 'max_duration', 'max_peak', 'errors')}))
    raise SystemExit(0 if not errors else 1)


if __name__ == '__main__':
    main()
