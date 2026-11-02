"""Audition file: three 'premium' lid-open sounds, the UI tick set, and a 16 s sample of the new music bed.
python sound_palette.py <out_dir>
"""
import sys
from pathlib import Path

import numpy as np
from scipy.signal import fftconvolve

from make_audio import SR, env, lp, hp, bp, midi, write, rng


def reverb(x, size=2.4, wet=0.28, tone=5000):
    n = int(size * SR)
    t = np.arange(n) / SR
    ir = lp(rng.standard_normal(n), tone) * np.exp(-t / (size / 4.5))
    ir /= np.sqrt(np.sum(ir ** 2))
    y = fftconvolve(x, ir)[: len(x) + n]
    dry = np.concatenate([x, np.zeros(len(y) - len(x))])
    return dry * (1 - wet) + y * wet


def bell(freq, dur=2.2, bright=1.0):
    """Glassy bell: slightly inharmonic partials, fast attack, long soft decay."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    parts = [(1.0, 1.0, 1.6), (2.01, 0.35 * bright, 0.9), (3.98, 0.12 * bright, 0.5), (5.43, 0.05 * bright, 0.3)]
    x = sum(a * np.sin(2 * np.pi * freq * r * t) * np.exp(-t / d) for r, a, d in parts)
    return x * np.minimum(1, t / 0.003)


def swell(dur=0.7, lo=500, hi=6000):
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = hp(rng.standard_normal(n), lo)
    x = lp(x, hi)
    return x * (np.sin(np.pi * t / t[-1]) ** 3) * 0.25


def place(buf, sig, at, gain=1.0):
    i = int(at * SR)
    j = min(len(buf), i + len(sig))
    if i < len(buf):
        buf[i:j] += sig[: j - i] * gain


def lid_a():   # two-note glass chime, rising fifth
    b = np.zeros(int(3 * SR))
    place(b, bell(midi(88)), 0.0, 0.5)
    place(b, bell(midi(95)), 0.11, 0.42)
    return reverb(b)


def lid_b():   # airy swell resolving into one soft bell
    b = np.zeros(int(3 * SR))
    place(b, swell(0.55), 0.0, 0.8)
    place(b, bell(midi(84), 2.6, 0.7), 0.42, 0.55)
    return reverb(b, wet=0.35)


def lid_c():   # crystalline three-note arpeggio
    b = np.zeros(int(3 * SR))
    for i, m in enumerate((84, 88, 91)):
        place(b, bell(midi(m), 1.8, 1.2), i * 0.07, 0.38)
    return reverb(b, wet=0.3)


def ui_tick():  # glassy micro click
    n = int(0.05 * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * 3200 * t) * np.exp(-t / 0.006) + 0.4 * bp(rng.standard_normal(n), 3000, 9000) * np.exp(-t / 0.002)
    return x * 0.5


def ui_toggle():
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    f = 900 + 500 * (1 - np.exp(-t * 60))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.02) * 0.4


def music_v2(length=16.0):
    """Slow, warm bed: soft piano-ish chords with reverb over a felt pad. No drums."""
    out = np.zeros(int((length + 3) * SR))
    beat = 60 / 70
    bar = 4 * beat
    chords = [[57, 64, 67, 71], [53, 60, 64, 69], [50, 57, 62, 65], [55, 62, 66, 71]]  # Am9-ish, Fmaj7, Dm, Gmaj7
    bass = [45, 41, 38, 43]
    t = 0.0
    k = 0
    while t < length:
        ch = chords[k % 4]
        n = int((bar + 1.0) * SR)
        tt = np.arange(n) / SR
        pad = sum(np.sin(2 * np.pi * midi(m) * (1 + d) * tt) for m in ch for d in (-0.002, 0.003)) / (2 * len(ch))
        pad = lp(pad, 1800) * np.minimum(1, tt / 0.9) * np.minimum(1, (bar + 1.0 - tt) / 0.9)
        place(out, pad, t, 0.10)
        bn = np.sin(2 * np.pi * midi(bass[k % 4]) * tt) * np.exp(-tt / 1.6)
        place(out, bn, t, 0.16)
        for s, m in enumerate([ch[1] + 12, ch[2] + 12, ch[3] + 12, ch[2] + 12]):
            place(out, bell(midi(m), 2.0, 0.5), t + s * beat, 0.07)
        t += bar
        k += 1
    out = reverb(out[: int(length * SR)], size=3.0, wet=0.3, tone=3500)[: int(length * SR)]
    f = int(1.2 * SR)
    out[:f] *= np.linspace(0, 1, f)
    out[-f:] *= np.linspace(1, 0, f)
    return out


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    # Palette: A, B, C lid sounds, then ticks/toggles, separated by silence.
    seq = np.zeros(int(14 * SR))
    place(seq, lid_a(), 0.3)
    place(seq, lid_b(), 3.6)
    place(seq, lid_c(), 6.9)
    for i in range(4):
        place(seq, ui_tick(), 10.3 + i * 0.25)
    for i in range(3):
        place(seq, ui_toggle(), 11.6 + i * 0.4)
    write(out / 'palette_lid_A_B_C_then_ticks.wav', seq / np.max(np.abs(seq)) * 0.8)
    m = music_v2(16)
    write(out / 'music_v2_sample.wav', m / np.max(np.abs(m)) * 0.6)
    print('ok')


if __name__ == '__main__':
    main()
