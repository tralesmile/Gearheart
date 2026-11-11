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


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("engine", engine(), 0.7)
    save("drill", drill(), 0.6)
    save("pick", burst(0.09, 45, 3, tone=950), 0.7)
    save("break", burst(0.22, 18, 14), 0.8)
    save("place", burst(0.12, 30, 30, tone=140), 0.8)
    save("pickup", pickup(), 0.6)
    save("boost", boost(), 0.75)   # last, so the shared random stream of the older sounds is unchanged
    print("sounds written to", os.path.abspath(OUT))
