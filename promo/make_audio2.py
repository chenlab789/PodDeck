"""Promo v2 audio: premium music bed (70 bpm, piano + pad, reverb) with ducking/muffle cues,
plus glassy UI sounds placed at the cues exported by motion2.html.

python make_audio2.py <cue_dir> <out_dir> <total_seconds> <outro_start_seconds>
  cue_dir has sfx_intro.json and sfx_outro.json (from render.js).
Writes music.wav (whole video length) and sfx.wav (whole video length).
"""
import json
import sys
from pathlib import Path

import numpy as np

from make_audio import SR, lp, write
from sound_palette import lid_a, ui_tick, ui_toggle, swell, bell, reverb, music_v2, place, midi


def fine_tick():
    return ui_tick() * 0.35


def key_click():
    x = ui_tick()
    n = len(x)
    t = np.arange(n) / SR
    return (x * 0.6 + np.sin(2 * np.pi * 420 * t) * np.exp(-t / 0.012) * 0.35)


def arrive():
    b = np.zeros(int(2.5 * SR))
    place(b, bell(midi(91), 2.0, 0.8), 0, 0.45)
    return reverb(b, wet=0.3)


def whoosh():
    return reverb(swell(0.55, 700, 9000), wet=0.2)


KINDS = {
    'chime': lambda: lid_a() * 0.9,
    'tick': lambda: ui_tick() * 0.8,
    'fine': fine_tick,
    'toggle': lambda: ui_toggle() * 1.1,
    'swell': lambda: reverb(swell(0.7), wet=0.25) * 0.8,
    'whoosh': whoosh,
    'key': key_click,
    'arrive': arrive,
}


def envelope(n, cues, kind_down, kind_up, floor, ramp=0.25):
    """Gain curve that drops to `floor` at kind_down cues and returns at kind_up cues."""
    gcurve = np.ones(n)
    downs = sorted(c['t'] for c in cues if c['kind'] == kind_down)
    ups = sorted(c['t'] for c in cues if c['kind'] == kind_up)
    for d, u in zip(downs, ups):
        i, j, r = int(d * SR), int(u * SR), int(ramp * SR)
        gcurve[i:i + r] = np.minimum(gcurve[i:i + r], np.linspace(1, floor, len(gcurve[i:i + r])))
        gcurve[i + r:j] = floor
        gcurve[j:j + r] = np.linspace(floor, 1, len(gcurve[j:j + r]))
    return gcurve


def main():
    cue_dir, out = Path(sys.argv[1]), Path(sys.argv[2])
    total, outro_at = float(sys.argv[3]), float(sys.argv[4])
    out.mkdir(parents=True, exist_ok=True)
    intro = json.loads((cue_dir / 'sfx_intro.json').read_text())['sfx']
    outro = [dict(c, t=c['t'] + outro_at) for c in json.loads((cue_dir / 'sfx_outro.json').read_text())['sfx']]
    cues = intro + outro
    n = int(total * SR)

    m = music_v2(total + 1.5)[:n]
    m = m / (np.max(np.abs(m)) or 1)
    # pause scene: music ducks out and comes back
    m = m * envelope(n, cues, 'duck_out', 'duck_in', 0.04, 0.18)
    # keep-stereo scene: start muffled + mono (hands-free feel), clear on the toggle
    muff = lp(lp(m, 900), 900) * 0.8
    mix = envelope(n, cues, 'muffle_on', 'muffle_off', 0.0, 0.2)   # 1 = clear, 0 = muffled
    on = [c['t'] for c in cues if c['kind'] == 'muffle_on']
    if on:  # the muffled section starts abruptly at the scene cut, not with a ramp
        i = int(on[0] * SR)
        j = int([c['t'] for c in cues if c['kind'] == 'muffle_off'][0] * SR)
        mix[i:j] = 0
    m = m * mix + muff * (1 - mix)
    # final fade
    f = int(1.8 * SR)
    m[-f:] *= np.linspace(1, 0, f)
    write(out / 'music.wav', m * 0.5)

    s = np.zeros(n + SR)
    for c in cues:
        k = KINDS.get(c['kind'])
        if k:
            place(s, k(), c['t'])
    write(out / 'sfx.wav', s[:n] * 0.8)
    print('ok', total)


if __name__ == '__main__':
    main()
