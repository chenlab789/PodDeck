"""Synthesize a soft background loop and UI sound effects for the PodDeck promo.

Everything is generated here (no samples), so the track is free to publish.
  music.wav           soft lo-fi pad + keys + light beat, LENGTH seconds
  sfx_<part>.wav      clicks/pops at the cue times exported by motion.html (sfx_<part>.json)
Usage: python make_audio.py <sfx_json_dir> <out_dir> [length_seconds]
"""
import json
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 48000
rng = np.random.default_rng(7)


def env(n, a=0.005, r=0.3):
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(a, 1e-4)) * np.exp(-t / max(r, 1e-4))
    return e


def lp(x, f):
    return sosfilt(butter(2, f, 'low', fs=SR, output='sos'), x)


def hp(x, f):
    return sosfilt(butter(2, f, 'high', fs=SR, output='sos'), x)


def bp(x, lo, hi):
    return sosfilt(butter(2, [lo, hi], 'band', fs=SR, output='sos'), x)


def note(freq, dur, kind='keys'):
    n = int(dur * SR)
    t = np.arange(n) / SR
    if kind == 'keys':  # soft electric-piano-ish
        x = np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(2 * np.pi * 2 * freq * t) * np.exp(-t * 6)
        return x * env(n, 0.004, 0.9)
    if kind == 'pad':
        x = sum(np.sin(2 * np.pi * freq * (1 + d) * t) for d in (-0.003, 0, 0.004)) / 3
        a = np.minimum(1, t / 0.6) * np.minimum(1, (dur - t) / 0.6)
        return lp(x, 2400) * a
    if kind == 'bass':
        x = np.sin(2 * np.pi * freq * t)
        return x * env(n, 0.01, 0.5)
    raise ValueError(kind)


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def music(length):
    bpm = 92
    beat = 60 / bpm
    bar = 4 * beat
    out = np.zeros(int((length + 2) * SR))

    def add(sig, at, gain):
        i = int(at * SR)
        if i >= len(out):
            return
        j = min(len(out), i + len(sig))
        out[i:j] += sig[: j - i] * gain

    # Fmaj7 - Am7 - Dm9 - Bbmaj7 (calm, open)
    chords = [[53, 57, 60, 64], [57, 60, 64, 67], [50, 57, 60, 64, 65], [46, 53, 57, 62]]
    bass = [41, 45, 38, 34]
    t = 0.0
    k = 0
    while t < length:
        ch = chords[k % 4]
        add(sum(note(midi(m + 12), bar + 0.6, 'pad') for m in ch[1:]), t, 0.05)
        add(note(midi(bass[k % 4]), bar, 'bass'), t, 0.20)
        # gentle broken-chord keys on 8ths
        for s in range(8):
            m = ch[[1, 2, 3, 2, 1, 3, 2, 3][s] % len(ch)] + 12
            add(note(midi(m), 1.2, 'keys'), t + s * beat / 2, 0.06 if s % 2 == 0 else 0.035)
        # soft beat: kick on 1 and 3, closed hat on off-beats
        for b in (0, 2):
            n = int(0.25 * SR)
            tt = np.arange(n) / SR
            kick = np.sin(2 * np.pi * (50 + 70 * np.exp(-tt * 30)) * tt) * env(n, 0.002, 0.12)
            add(kick, t + b * beat, 0.22)
        for s in range(8):
            if s % 2 == 1:
                n = int(0.05 * SR)
                hat = hp(rng.standard_normal(n), 7000) * env(n, 0.001, 0.015)
                add(hat, t + s * beat / 2, 0.035)
        t += bar
        k += 1
    out = out[: int(length * SR)]
    # fade in/out
    f = int(1.0 * SR)
    out[:f] *= np.linspace(0, 1, f)
    out[-int(1.5 * SR):] *= np.linspace(1, 0, int(1.5 * SR))
    return out


def sfx(kind):
    if kind == 'click':
        n = int(0.03 * SR)
        x = bp(rng.standard_normal(n), 2000, 6000) * env(n, 0.0005, 0.006)
        x += np.sin(2 * np.pi * 1800 * np.arange(n) / SR) * env(n, 0.0005, 0.01) * 0.5
        return x * 0.9
    if kind == 'tick':
        n = int(0.04 * SR)
        return np.sin(2 * np.pi * 2600 * np.arange(n) / SR) * env(n, 0.0005, 0.012) * 0.5
    if kind == 'key':
        n = int(0.06 * SR)
        x = lp(rng.standard_normal(n), 3500) * env(n, 0.0005, 0.012)
        x += np.sin(2 * np.pi * 220 * np.arange(n) / SR) * env(n, 0.001, 0.02) * 0.6
        return x * 0.9
    if kind == 'pop':
        n = int(0.12 * SR)
        t = np.arange(n) / SR
        f = 380 + 600 * (1 - np.exp(-t * 40))
        return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(n, 0.002, 0.045) * 0.6
    if kind == 'soft':
        n = int(0.35 * SR)
        t = np.arange(n) / SR
        x = lp(rng.standard_normal(n), 900) * np.sin(np.pi * t / t[-1]) ** 2
        return x * 0.35
    if kind == 'whoosh':
        n = int(0.6 * SR)
        t = np.arange(n) / SR
        x = rng.standard_normal(n)
        out = np.zeros(n)
        seg = n // 12
        for i in range(12):  # rising band
            lo = 300 + i * 250
            out[i * seg:(i + 1) * seg] = bp(x[i * seg:(i + 1) * seg], lo, lo * 2.2)
        return out * np.sin(np.pi * t / t[-1]) ** 2 * 0.35
    raise ValueError(kind)


def write(path, x, peak=0.89):
    m = np.max(np.abs(x)) or 1
    if m > peak:
        x = x / m * peak
    wavfile.write(path, SR, (np.stack([x, x], 1) * 32767).astype(np.int16))


def main():
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    length = float(sys.argv[3]) if len(sys.argv) > 3 else 32
    out.mkdir(parents=True, exist_ok=True)
    m = music(length)
    write(out / 'music.wav', m / (np.max(np.abs(m)) or 1) * 0.5)
    for part in ('intro', 'outro'):
        cue = json.loads((src / f'sfx_{part}.json').read_text())
        buf = np.zeros(int((cue['total'] + 1) * SR))
        for c in cue['sfx']:
            s = sfx(c['kind'])
            i = int(c['t'] * SR)
            buf[i:i + len(s)] += s[: max(0, len(buf) - i)]
        write(out / f'sfx_{part}.wav', buf[: int(cue['total'] * SR)])
    print('ok', length)


if __name__ == '__main__':
    main()
