"""Dead Circuit batch 01: original, deterministic synthesis; no recorded samples.

Run with Python + NumPy. Outputs only this tool directory and Audio/Expansion.
The twenty event categories intentionally use different synthesis architectures.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import wave

import numpy as np

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
OUT = PROJECT / 'Assets/DeadCircuit/Resources/Audio/Expansion'
RATE = 44100
SEED = 2026111901


class Synth:
    def __init__(self, duration, seed):
        self.t = np.arange(round(duration * RATE), dtype=np.float64) / RATE
        self.rng = np.random.default_rng(seed)
        self.x = np.zeros_like(self.t)

    def noise(self, low, high, slope=0):
        n = len(self.t)
        f = np.fft.rfftfreq(n, 1 / RATE)
        # Smooth spectral shoulders avoid the ringing of a rectangular band cut.
        hp = 1 - np.exp(-((f / max(low, 1)) ** 4)) if low else np.ones_like(f)
        lp = np.exp(-((f / high) ** 4))
        curve = hp * lp * (np.maximum(f, 30) / 300) ** slope
        z = self.rng.normal(size=len(f)) + 1j * self.rng.normal(size=len(f))
        z[0] = 0
        z[-1] = z[-1].real
        y = np.fft.irfft(z * curve, n=n)
        return y / max(np.std(y), 1e-8)

    def env(self, start, decay, attack=.0015, power=1):
        t = self.t - start
        return np.where(t >= 0, (1 - np.exp(-np.maximum(t, 0) / attack))
                        * np.exp(-np.maximum(t, 0) / decay), 0) ** power

    def tone(self, hz, amplitude, start=0, decay=.08, end_hz=None, attack=.0015):
        q = np.maximum(self.t - start, 0)
        if end_hz is None:
            phase = hz * q
        else:
            tau = max(decay * .65, .005)
            phase = end_hz * q + (hz - end_hz) * tau * (1 - np.exp(-q / tau))
        self.x += amplitude * np.sin(2 * np.pi * phase) * self.env(start, decay, attack)

    def tap(self, start, amp, low=400, high=6500, decay=.018):
        self.x += amp * self.noise(low, high) * self.env(start, decay, attack=.0005)

    def resonator(self, start, fundamental, amp, decay, harmonics=(1, 2.47, 4.13)):
        for i, ratio in enumerate(harmonics):
            self.tone(fundamental * ratio, amp / (i + 1) ** 1.7, start, decay / (1 + .7 * i))


def ui_click(s, variant):
    s.tap(0, .65, 1300, 8500, .006)
    s.tone(1460 + variant * 90, .35, .003, .015, 980)
    s.resonator(.001, 580, .08, .018)


def ui_open(s, _):
    s.tap(.005, .18, 500, 5000, .022)
    for start, f in ((.01, 410), (.055, 615), (.10, 820)):
        s.tone(f, .27, start, .06, attack=.005)
    s.x += .09 * s.noise(1800, 6500) * s.env(.02, .075, .024)


def ui_close(s, _):
    for start, f in ((.002, 740), (.04, 490), (.08, 330)):
        s.tone(f, .25, start, .045, attack=.003)
    s.tap(.09, .35, 300, 2200, .024)


def equip(s, _):
    s.tap(.005, .36, 700, 5200, .025)
    s.resonator(.025, 340, .32, .075)
    s.tap(.135, .75, 2200, 9500, .012)
    s.tone(1360, .20, .14, .035)


def unequip(s, _):
    s.tap(.008, .60, 1800, 7500, .012)
    s.x += .30 * s.noise(250, 2100) * s.env(.055, .075, .015)
    s.resonator(.16, 230, .24, .06, (1, 1.83, 3.4))


def reload_start(s, _):
    s.tap(.005, .75, 1300, 8500, .009)
    s.resonator(.015, 320, .30, .055)
    s.x += .32 * s.noise(450, 3300) * s.env(.115, .11, .025)
    s.tap(.28, .45, 170, 1900, .045)
    s.resonator(.30, 170, .18, .065)


def reload_complete(s, _):
    s.tap(.005, .75, 180, 3700, .025)
    s.resonator(.01, 250, .40, .055)
    s.tap(.16, .60, 1700, 9500, .010)
    s.tone(720, .20, .164, .040, 490)
    s.tap(.205, .25, 3000, 10000, .005)


def gun(s, kind):
    # A pressure chirp, filtered turbulent blast, metal action and sparse reflections.
    data = {0: (175, 62, .028, 7800, .047, .40),
            1: (100, 38, .075, 5800, .105, .78),
            2: (220, 74, .022, 9500, .044, .36),
            3: (82, 31, .110, 6900, .125, .92)}
    f0, f1, tail, cutoff, reflection_decay, body = data[kind]
    blast = s.noise(70, cutoff, -.08)
    s.x += .75 * blast * s.env(0, tail, .00045)
    s.tone(f0, body, 0, tail * 1.2, f1, .0006)
    s.tap(.001, .30, 6500, 15000, .003)
    s.resonator(.012, 660 + kind * 110, .13, .019)
    for start, level in ((.041, .12), (.087, .07), (.141, .035)):
        s.x += level * s.noise(100, 3400) * s.env(start, reflection_decay, .002)
    if kind == 1:
        s.tap(.035, .22, 90, 900, .06)
        s.tap(.24, .12, 1200, 6500, .035)
    if kind == 3:
        s.x += .09 * s.noise(45, 700, -.3) * s.env(.13, .23, .02)


def knife(s, _):
    q = s.t
    env = np.exp(-((q - .12) / .044) ** 2)
    s.x += .65 * s.noise(700, 9000) * env
    s.x += .24 * s.noise(120, 1300) * np.exp(-((q - .15) / .036) ** 2)
    s.tone(810, .08, .085, .06, 210, .018)


def hit(s, variant):
    if variant == 0:
        s.tap(.001, .80, 100, 1800, .026)
        s.tone(160, .45, .003, .058, 55)
        s.tap(.025, .16, 1200, 5700, .015)
    elif variant == 1:
        s.tap(0, .70, 1700, 12000, .009)
        s.resonator(.002, 920, .50, .15, (1, 1.49, 2.73, 4.16))
    else:
        s.tap(0, .6, 300, 5800, .026)
        s.resonator(.004, 290, .38, .085, (1, 2.8, 4.9))
        s.tap(.035, .15, 2500, 8500, .035)


def gather(s, variant):
    if variant == 0:
        for t, a in ((0, .7), (.035, .22), (.10, .14)):
            s.tap(t, a, 220, 4300, .018)
        s.resonator(.003, 245, .4, .10, (1, 2.15, 3.56))
    else:
        s.tap(.002, .8, 1200, 11000, .015)
        s.resonator(.004, 1360, .4, .15, (1, 1.414, 2.31, 3.74))
        s.tap(.072, .13, 3200, 13000, .03)


def water(s, _):
    gate = s.env(.035, .34, .07)
    s.x += .35 * s.noise(500, 7400) * gate * (1 + .23 * np.sin(2 * np.pi * 17 * s.t))
    for t, f in ((.04, 1500), (.13, 1100), (.24, 1850), (.39, 1300), (.57, 2100)):
        s.tone(f, .10, t, .027, f * .58)
        s.tap(t, .10, 2800, 10000, .009)


def sow(s, _):
    s.x += .2 * s.noise(950, 6300) * s.env(.008, .09, .012)
    for i, t in enumerate((.05, .095, .14, .205, .255)):
        s.tap(t, .32 - i * .035, 1700, 9200, .005)
        s.tone(660 + i * 83, .05, t, .011)
    s.tap(.285, .2, 100, 950, .022)


def harvest(s, _):
    s.x += .36 * s.noise(1300, 9900) * s.env(.005, .085, .012)
    s.tap(.075, .4, 220, 3100, .04)
    s.resonator(.11, 420, .16, .06, (1, 1.25, 1.5))
    s.tone(630, .13, .16, .055)
    s.tap(.20, .18, 3300, 11000, .03)


def thunder(s, _):
    s.tap(.02, .75, 120, 10000, .035)
    for start, amp, decay in ((.025, .65, .30), (.18, .43, .70), (.60, .28, 1.05), (1.20, .13, .80)):
        s.x += amp * s.noise(22, 420, -.3) * s.env(start, decay, .015 + start * .08)
    s.x += .18 * s.noise(300, 2800, -.6) * s.env(.07, .28, .008)


def wind(s, _):
    # All Fourier and modulation frequencies span integer periods across the loop.
    dur = len(s.t) / RATE
    mod = .58 + .23 * np.sin(2 * np.pi * s.t / dur - .8) + .12 * np.sin(6 * np.pi * s.t / dur)
    s.x += .20 * s.noise(40, 1200, -.5) * mod
    s.x += .045 * s.noise(600, 4200) * (.6 + .3 * np.cos(4 * np.pi * s.t / dur))


def rain(s, _):
    dur = len(s.t) / RATE
    s.x += .15 * s.noise(900, 11000) * (.80 + .12 * np.cos(2 * np.pi * s.t / dur))
    s.x += .06 * s.noise(150, 2100, -.2)
    # Circularly wrapped individual drops have no hidden tail truncation at the seam.
    for _ in range(110):
        start = s.rng.uniform(0, dur)
        q = (s.t - start) % dur
        f = s.rng.uniform(1100, 4200)
        s.x += s.rng.uniform(.025, .065) * np.sin(2 * np.pi * f * q) * (1 - np.exp(-q / .0006)) * np.exp(-q / .009)


def machine(s, _):
    dur = len(s.t) / RATE
    for f, amp in ((50, .19), (100, .08), (150, .025), (300, .018)):
        s.x += amp * np.sin(2 * np.pi * f * s.t) * (1 + .08 * np.sin(2 * np.pi * 3 * s.t))
    s.x += .025 * s.noise(250, 2300) * (.8 + .2 * np.cos(2 * np.pi * s.t / dur))


def extra(s, variant):
    if variant == 0:  # electrical short
        for i, start in enumerate((0, .022, .056, .13, .19)):
            s.tap(start, .4 / (1 + i * .2), 3200, 17000, .006)
            s.tone(2100 - i * 210, .13, start, .012, 700)
    elif variant == 1:  # explosion: crack, pressure body, debris
        s.tap(0, .8, 1400, 14000, .012)
        s.tone(115, .8, .002, .14, 28)
        s.x += .60 * s.noise(35, 3400, -.3) * s.env(.003, .18, .002)
        for t in (.11, .23, .37, .52):
            s.tap(t, .12, 500, 6000, .035)
        s.x += .16 * s.noise(25, 480, -.4) * s.env(.13, .40, .04)
    elif variant == 2:  # cloth step
        s.tap(.005, .4, 200, 2100, .03)
        s.x += .2 * s.noise(1400, 7200) * s.env(.018, .055, .012)
    elif variant == 3:  # failure
        s.tone(240, .4, 0, .07)
        s.tone(185, .4, .09, .08)
    else:  # pickup
        for t, f in ((.00, 520), (.05, 780), (.10, 1040)):
            s.tone(f, .3, t, .08, attack=.004)


# id, category, seconds, function, variant, loop, target peak, design
JOBS = [
 ('ui_click_01', 'click', .085, ui_click, 0, False, .55, 'dry high click + falling electronic detent'),
 ('ui_click_02', 'click', .085, ui_click, 1, False, .52, 'higher alternate click with independent noise'),
 ('ui_open', 'open', .36, ui_open, 0, False, .56, 'three rising tones and sliding air'),
 ('ui_close', 'close', .30, ui_close, 0, False, .54, 'descending tones and terminal latch'),
 ('gear_equip', 'equip', .38, equip, 0, False, .67, 'metal seating plus sharp buckle lock'),
 ('gear_unequip', 'unequip', .42, unequip, 0, False, .62, 'release latch then fabric and low rattle'),
 ('reload_start', 'reload_start', .62, reload_start, 0, False, .70, 'catch release / magazine scrape / removal'),
 ('reload_complete', 'reload_complete', .48, reload_complete, 0, False, .72, 'magazine seat / bolt slap / lock'),
 ('weapon_pistol', 'pistol', .50, gun, 0, False, .84, 'short bright report with 175-to-62Hz pressure chirp'),
 ('weapon_shotgun', 'shotgun', .85, gun, 1, False, .86, 'broad low blast with longer turbulent tail'),
 ('weapon_rifle', 'rifle', .46, gun, 2, False, .82, 'tight sharp report and quick metallic action'),
 ('weapon_sniper', 'sniper', 1.18, gun, 3, False, .87, 'deep pressure transient with long sparse reflections'),
 ('weapon_knife_swing', 'knife', .36, knife, 0, False, .65, 'two spectral bands sweeping through a Gaussian envelope'),
 ('hit_flesh', 'hit', .32, hit, 0, False, .73, 'damped low thump with short grit'),
 ('hit_metal', 'hit', .65, hit, 1, False, .74, 'four inharmonic ringing partials and bright attack'),
 ('hit_wood', 'hit', .42, hit, 2, False, .70, 'hollow inharmonic wood body and splinter tail'),
 ('gather_wood', 'gather', .48, gather, 0, False, .69, 'woody chop with small chips'),
 ('gather_ore', 'gather', .65, gather, 1, False, .71, 'mineral ping and high fragment ticks'),
 ('farm_water', 'water', 1.00, water, 0, False, .63, 'modulated filtered pour with pitched droplets'),
 ('farm_sow', 'sow', .46, sow, 0, False, .56, 'seed packet rustle and five tiny seed taps'),
 ('farm_harvest', 'harvest', .58, harvest, 0, False, .64, 'stem snap / leaf rustle / short reward chime'),
 ('weather_thunder', 'thunder', 4.5, thunder, 0, False, .85, 'crack then overlapping low-frequency rumble swells'),
 ('amb_wind_loop', 'wind', 6.0, wind, 0, True, .50, 'periodic low wind and cyclic gust modulation'),
 ('amb_rain_loop', 'rain', 6.0, rain, 0, True, .48, 'periodic rain bed and circular individual drops'),
 ('amb_machine_loop', 'machine', 4.0, machine, 0, True, .44, '50Hz motor with harmonic and filtered bearing texture'),
 ('fx_electric_spark', 'spark', .34, extra, 0, False, .67, 'five irregular high crackles and falling buzz'),
 ('fx_explosion', 'explosion', 1.50, extra, 1, False, .87, 'shock crack / pressure chirp / rumble / debris'),
 ('footstep_grit', 'footstep', .28, extra, 2, False, .52, 'low contact and delayed grit drag'),
 ('ui_denied', 'denied', .40, extra, 3, False, .55, 'descending two-tone warning'),
 ('item_pickup', 'pickup', .46, extra, 4, False, .60, 'three clean ascending pickup partials'),
]


def pcm_finish(s, peak, loop):
    x = s.x.copy()
    x -= np.mean(x)
    # Gentle saturator catches coincident transient peaks before final peak limiting.
    x = np.tanh(x * .85)
    if loop:
        # Make both endpoints and their local slopes meet over 4ms, no silent gap.
        width = round(.004 * RATE)
        target = (x[0] + x[-1]) / 2
        ramp = np.linspace(0, 1, width)
        smooth = ramp * ramp * (3 - 2 * ramp)
        x[:width] = target + (x[:width] - target) * smooth
        x[-width:] = target + (x[-width:] - target) * smooth[::-1]
    else:
        width = round(.003 * RATE)
        x[:width] *= np.linspace(0, 1, width) ** 1.5
        fade = min(round(.035 * RATE), len(x) // 4)
        x[-fade:] *= np.linspace(1, 0, fade) ** 2
    x -= np.mean(x)
    x *= peak / max(np.max(np.abs(x)), 1e-12)
    if not loop:
        x[0] = x[-1] = 0
    return np.rint(x * 32767).astype('<i2')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    entries = []
    for index, (name, category, duration, fn, variant, loop, peak, design) in enumerate(JOBS):
        s = Synth(duration, SEED + index * 997)
        fn(s, variant)
        data = pcm_finish(s, peak, loop)
        file = OUT / (name + '.wav')
        with wave.open(str(file), 'wb') as wav:
            wav.setnchannels(1)
            wav.setsampwidth(2)
            wav.setframerate(RATE)
            wav.writeframes(data.tobytes())
        entries.append({'name': name, 'file': file.name,
                        'resourcePath': 'Audio/Expansion/' + name,
                        'category': category, 'loop': loop,
                        'duration': len(data) / RATE, 'sampleRate': RATE,
                        'channels': 1, 'encoding': 'PCM_S16LE',
                        'seed': SEED + index * 997, 'targetPeak': peak,
                        'recommendedVolume': .35 if loop else (.65 if category in ('pistol', 'shotgun', 'rifle', 'sniper', 'explosion') else .7),
                        'design': design, 'sha256': hashlib.sha256(file.read_bytes()).hexdigest()})
    manifest = {'schema': 'deadcircuit.expansion.audio.v1', 'batch': 1,
                'artSource': 'original_python_synthesis', 'recordedSamples': False,
                'note': 'Original synthesized sound effects, not field recordings. No third-party samples or pretrained audio model.',
                'generator': 'Tools/ExpansionArt/synthesize_audio.py',
                'sampleRate': RATE, 'channels': 1, 'encoding': 'PCM_S16LE',
                'unityImport': {'forceToMono': True, 'normalize': False,
                                'sampleRateSetting': 'PreserveSampleRate',
                                'loadType': 'DecompressOnLoad', 'compressionFormat': 'PCM',
                                'loopPlayback': 'AudioSource.loop from entry.loop; do not crossfade or append silence'},
                'cues': {'reload_start': 'trigger at reload initiation; tail is not the full reload timer',
                         'reload_complete': 'trigger only on game reload completion event',
                         'weapon_rifle': 'single shot; trigger at existing fire cadence, no built-in burst',
                         'amb_wind_loop': 'loop ambience; use existing mixer / weather strength',
                         'amb_rain_loop': 'loop ambience; layer with wind at low volume'},
                'clips': entries}
    (OUT / 'audio_manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'wav_count': len(entries), 'categories': len({e['category'] for e in entries}),
                      'loops': sum(e['loop'] for e in entries), 'out': str(OUT)}))


if __name__ == '__main__':
    main()
