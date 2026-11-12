#!/usr/bin/env python3
"""Generates GEARHEART's placeholder sound effects into assets/audio/ (22.05 kHz, mono, 16-bit).

Run:  python3 tools/make_sounds.py
The engine and drill are exactly one second long with whole cycles, so they loop without a click.
"""
import os, wave
import numpy as np

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "audio")
SR = 22050
rng = np.random.default_rng(7)


def save(name, signal, gain=0.8):
    signal = signal / (np.max(np.abs(signal)) + 1e-9) * gain
    data = (signal * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR); f.writeframes(data.tobytes())


def lowpass(x, k):
    kernel = np.ones(k) / k
    return np.convolve(x, kernel, mode="same")


def loop_noise(n, k):
    """Noise that wraps around seamlessly (filtered in the frequency domain)."""
    spectrum = np.fft.rfft(rng.standard_normal(n))
    spectrum[k:] = 0
    return np.fft.irfft(spectrum, n)


def engine():
    t = np.arange(SR) / SR
    f0 = 55.0
    s = sum(np.sin(2 * np.pi * f0 * k * t + k) / k for k in range(1, 14))
    s *= 1.0 + 0.35 * np.sign(np.sin(2 * np.pi * f0 * 2 * t))        # firing pulses
    s += 0.25 * loop_noise(SR, 900) / 0.02
    return s


def drill():
    t = np.arange(SR) / SR
    s = np.sign(np.sin(2 * np.pi * 180 * t)) * 0.5 + np.sin(2 * np.pi * 360 * t) * 0.3
    s += loop_noise(SR, 4000) / 0.03 * (0.6 + 0.4 * np.sin(2 * np.pi * 30 * t))
    return s


def boost():
    """Rocket roar: rumbling low noise with a crackle on top, one second, loops cleanly."""
    t = np.arange(SR) / SR
    s = loop_noise(SR, 500) / 0.02 * 1.4                        # deep rumble
    s += loop_noise(SR, 5000) / 0.05 * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 14 * t)))  # crackle
    s += 0.35 * np.sin(2 * np.pi * 70 * t)
    return s


def burst(seconds, decay, smooth, tone=None):
    n = int(SR * seconds); t = np.arange(n) / SR
    s = lowpass(rng.standard_normal(n), smooth) * np.exp(-t * decay)
    if tone: s += 0.5 * np.sin(2 * np.pi * tone * t) * np.exp(-t * decay * 1.5)
    return s


def pickup():
    n = int(SR * 0.18); t = np.arange(n) / SR
    f = np.where(t < 0.07, 880.0, 1320.0)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14)


def tail_fade(s, ms=40):
    """Fades the last few milliseconds to silence so a one-shot never ends on a click."""
    k = int(SR * ms / 1000)
    s[-k:] *= np.linspace(1, 0, k)
    return s


def thud():
    """A heavy landing: a low pitch-dropping boom with a short dirty knock on top."""
    n = int(SR * 0.45); t = np.arange(n) / SR
    f = 95 * np.exp(-t * 5) + 38
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
    s += 0.6 * lowpass(crash_rng.standard_normal(n), 40) / 0.15 * np.exp(-t * 30)
    return s


def metal(n, partials, decay):
    """Inharmonic ringing, like struck sheet metal."""
    t = np.arange(n) / SR
    return sum(a * np.sin(2 * np.pi * f * t + crash_rng.uniform(0, 6)) * np.exp(-t * decay * (1 + f / 2000))
               for f, a in partials)


def crunch():
    """Body slam: grinding noise bursts plus a dull metal ring."""
    n = int(SR * 0.6); t = np.arange(n) / SR
    grit = crash_rng.standard_normal(n) * (crash_rng.random(n) < 0.25)       # crackle
    s = lowpass(grit, 6) / 0.12 * np.exp(-t * 9)
    s += lowpass(crash_rng.standard_normal(n), 25) / 0.2 * np.exp(-t * 14)  # body of the hit
    s += 0.8 * metal(n, [(173, 1), (419, 0.7), (787, 0.5), (1312, 0.35), (2210, 0.2)], 7)
    s += 0.7 * np.sin(2 * np.pi * 60 * t) * np.exp(-t * 12)
    return s


def snap():
    """A beam or bolt letting go: a sharp crack and a bright ringing ping."""
    n = int(SR * 0.5); t = np.arange(n) / SR
    s = crash_rng.standard_normal(n) * np.exp(-t * 90)
    s += 0.9 * metal(n, [(1180, 1), (1873, 0.8), (2950, 0.5), (4120, 0.3)], 9)
    return s


def treasure():
    """A buried stash opens: a quick bright rising arpeggio (C E G C) with a shimmering tail."""
    notes = [523.25, 659.25, 783.99, 1046.5]
    step = 0.075
    n = int(SR * (step * len(notes) + 0.7)); t = np.arange(n) / SR
    s = np.zeros(n)
    for i, f in enumerate(notes):
        start = int(SR * step * i)
        tt = t[: n - start]
        ring = 2.6 if i == len(notes) - 1 else 9.0
        tone = np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * f * 2 * tt) + 0.15 * np.sin(2 * np.pi * f * 3 * tt)
        s[start:] += tone * np.exp(-tt * ring) * (1 - np.exp(-tt * 400))
    shimmer = sum(np.sin(2 * np.pi * f * t) for f in (2093.0, 2637.0, 3136.0)) * (0.5 + 0.5 * np.sin(2 * np.pi * 11 * t))
    s += 0.18 * shimmer * np.exp(-np.maximum(t - 0.3, 0) * 4) * (t > 0.22)
    return s


def fuel():
    """A fuel can grabbed: three quick glugs rising in pitch, then a bright two-note ding."""
    n = int(SR * 0.55); t = np.arange(n) / SR
    s = np.zeros(n)
    for i, f in enumerate((180.0, 230.0, 300.0)):
        start = int(SR * 0.07 * i); tt = t[: n - start]
        sweep = f * (1 + 1.2 * tt)                                       # bubble: pitch rises as it pops
        s[start:] += np.sin(2 * np.pi * np.cumsum(sweep) / SR) * np.exp(-tt * 28) * (1 - np.exp(-tt * 600))
    for f, at in ((1046.5, 0.2), (1568.0, 0.27)):
        start = int(SR * at); tt = t[: n - start]
        s[start:] += 0.55 * (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2 * tt)) * np.exp(-tt * 9)
    return s


def wind():
    """Wind rushing past the car: band-limited noise with slow gusts and a soft whistle, one second, loops cleanly."""
    n = SR; t = np.arange(n) / SR
    spectrum = np.fft.rfft(wind_rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    spectrum *= np.exp(-((f - 700) / 650) ** 2) + 0.35 * np.exp(-((f - 2400) / 900) ** 2)   # airy hiss, no rumble
    s = np.fft.irfft(spectrum, n)
    s /= np.max(np.abs(s)) + 1e-9
    s *= 0.72 + 0.18 * np.sin(2 * np.pi * 2 * t) + 0.1 * np.sin(2 * np.pi * 5 * t + 1.3)     # gusts (whole cycles)
    s += 0.05 * np.sin(2 * np.pi * 1320 * t) * (0.5 + 0.5 * np.sin(2 * np.pi * 3 * t))     # faint whistle
    return s


def tally():
    """One tick of the trip-score counter: a short, dry wooden-and-bell click."""
    n = int(SR * 0.07); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * 1760 * t) * np.exp(-t * 70) + 0.5 * np.sin(2 * np.pi * 2640 * t) * np.exp(-t * 110)
    return s * (1 - np.exp(-t * 900))


def record():
    """A new best trip: a brassy da-da-DAAA (G, G, C an octave up) over a held chord, with a little sparkle."""
    hits = [(0.0, 392.0, 0.11), (0.13, 392.0, 0.11), (0.26, 523.25, 0.75)]
    n = int(SR * 1.25); t = np.arange(n) / SR
    s = np.zeros(n)
    for start_s, f, length in hits:
        start = int(SR * start_s)
        tt = t[: n - start]
        env = (1 - np.exp(-tt * 60)) * np.exp(-np.maximum(tt - length, 0) * 9)
        vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * tt) * (tt > 0.15)
        tone = sum(np.sin(2 * np.pi * f * k * tt * vib) / k ** 1.1 for k in range(1, 7))   # bright, brassy stack
        s[start:] += tone * env
    chord = sum(np.sin(2 * np.pi * f * t) for f in (261.63, 329.63, 392.0)) * 0.22
    s += chord * (t > 0.26) * np.exp(-np.maximum(t - 0.26, 0) * 2.2)
    sparkle = sum(np.sin(2 * np.pi * f * t) for f in (2093.0, 3136.0)) * (0.5 + 0.5 * np.sin(2 * np.pi * 13 * t))
    s += 0.1 * sparkle * (t > 0.3) * np.exp(-np.maximum(t - 0.3, 0) * 3.5)
    return s


def land():
    """A perfect landing: a soft low thump, a rising whoosh of air, and a bright two-note ta-DING."""
    n = int(SR * 0.75); t = np.arange(n) / SR
    thump = np.sin(2 * np.pi * (70 + 50 * np.exp(-t * 30)) * t) * np.exp(-t * 22)
    noise = land_rng.standard_normal(n)
    sweep = np.zeros(n)
    # Band of noise whose centre slides up (crude moving filter: two low-passes of different width).
    for i, k in enumerate((40, 24, 14, 8, 5)):
        u = np.clip((t - 0.03 * i) / 0.14, 0, 1)
        seg = np.sin(np.pi * u) ** 2          # smooth swell in and out, no clicks
        band = lowpass(noise, k) - lowpass(noise, k * 3)
        sweep += band * seg
    s = 0.8 * thump + 0.9 * sweep
    for start_s, f, length, amp in ((0.06, 1318.5, 0.08, 0.5), (0.15, 1975.5, 0.5, 0.6)):   # E6, then B6
        start = int(SR * start_s); tt = t[: n - start]
        env = (1 - np.exp(-tt * 400)) * np.exp(-tt * (4.5 if length > 0.2 else 30))
        s[start:] += amp * env * (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * 2 * f * tt) + 0.12 * np.sin(2 * np.pi * 3.01 * f * tt))
    return s


wind_rng = np.random.default_rng(57)    # own stream, so adding wind leaves every other sound byte-identical
crash_rng = np.random.default_rng(31)   # separate stream: adding sounds never changes the older ones
land_rng = np.random.default_rng(91)    # own stream for the perfect-landing whoosh


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("engine", engine(), 0.7)
    save("drill", drill(), 0.6)
    save("pick", burst(0.09, 45, 3, tone=950), 0.7)
    save("break", burst(0.22, 18, 14), 0.8)
    save("place", burst(0.12, 30, 30, tone=140), 0.8)
    save("pickup", pickup(), 0.6)
    save("boost", boost(), 0.75)   # last, so the shared random stream of the older sounds is unchanged
    save("thud", tail_fade(thud()), 0.85)
    save("crunch", tail_fade(crunch()), 0.8)
    save("snap", tail_fade(snap()), 0.6)
    save("treasure", tail_fade(treasure(), 80), 0.6)
    save("fuel", tail_fade(fuel(), 60), 0.6)
    save("wind", wind(), 0.6)
    save("tally", tail_fade(tally(), 20), 0.5)     # deterministic: uses no random stream
    save("record", tail_fade(record(), 120), 0.6)
    save("land", tail_fade(land(), 80), 0.6)
    print("sounds written to", os.path.abspath(OUT))
