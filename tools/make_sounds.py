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


# ---- Engines by part, gear shifts and the starter (own random stream: older sounds stay byte-identical) ----

def pulse_train(t, rate, gains, ring, decay, noise, body=0.0):
    """Exhaust pulses: each firing is a short damped ring (frequency `ring`) with a puff of noise.
    `rate` is pulses per second (a whole number, so a one-second loop wraps cleanly) and `gains`
    a repeating pattern of pulse strengths (its length must divide `rate`). Each pulse rings on
    under the next two, so there is no click where one pulse hands over to the next."""
    phase = t * rate
    s = np.zeros_like(t)
    for back in range(3):
        idx = np.floor(phase).astype(int) - back
        tau = (phase - np.floor(phase) + back) / rate          # seconds since that pulse fired
        g = np.asarray(gains)[idx % len(gains)]
        env = np.exp(-tau * decay) * (1 - np.exp(-tau * 3000))
        s += g * env * (np.sin(2 * np.pi * ring * tau) + body * np.sin(2 * np.pi * ring * 0.5 * tau) + noise)
    return s


def loop_noise_own(n, k):
    """Like loop_noise, from the engine stream: noise that wraps around seamlessly, band-limited to bin k."""
    spectrum = np.fft.rfft(engine_rng.standard_normal(n))
    spectrum[k:] = 0
    x = np.fft.irfft(spectrum, n)
    return x / (np.std(x) + 1e-9)


def engine_e4():
    """The Scrap 4-Cylinder: a thin, buzzy, rattly little motor, like a lawnmower that wants to be a car."""
    t = np.arange(SR) / SR
    rate = 64
    jitter = 0.8 + 0.4 * engine_rng.random(rate)                 # every firing is a bit different
    s = pulse_train(t, rate, jitter, 300.0, 70.0, 0.35 * loop_noise_own(SR, 2500))
    s += 0.25 * sum(np.sin(2 * np.pi * rate * k * t) / k for k in range(1, 12, 2))   # buzzy, square-ish
    rattle = loop_noise_own(SR, 5000) * (np.mod(t * 128, 1.0) < 0.1)                  # loose tappets
    s += 0.12 * rattle
    return s


def engine_v8():
    """The V8: a deep, lumpy burble with uneven pulses (the cross-plane lope) and a low rumble."""
    t = np.arange(SR) / SR
    lope = [1.0, 0.62, 0.92, 0.55, 1.0, 0.7, 0.85, 0.5]           # eight firings per two turns, 40 per second
    s = pulse_train(t, 40, lope, 92.0, 24.0, 0.5 * loop_noise_own(SR, 700), body=0.8)
    s += 0.35 * sum(np.sin(2 * np.pi * 40 * k * t + k * 0.7) / k for k in range(1, 6))
    s += 0.25 * loop_noise_own(SR, 250)                             # low rumble
    return s


def shift():
    """A gear change: a dull gearbox clunk, then an exhaust pop."""
    n = int(SR * 0.32); t = np.arange(n) / SR
    s = 0.9 * np.sin(2 * np.pi * 120 * t) * np.exp(-t * 45) + 0.5 * lowpass(engine_rng.standard_normal(n), 6) * np.exp(-t * 90)
    start = int(SR * 0.05); tt = t[: n - start]
    pop = lowpass(engine_rng.standard_normal(n - start), 9) / 0.3 * np.exp(-tt * 38) + 0.9 * np.sin(2 * np.pi * (85 + 60 * np.exp(-tt * 40)) * tt) * np.exp(-tt * 28)
    s[start:] += 0.8 * pop
    return s


def pop():
    """An exhaust pop when you lift off at high revs: a short dull bang with a crackle."""
    n = int(SR * 0.25); t = np.arange(n) / SR
    s = lowpass(engine_rng.standard_normal(n), 7) / 0.3 * np.exp(-t * 32)
    s += np.sin(2 * np.pi * (75 + 70 * np.exp(-t * 45)) * t) * np.exp(-t * 24)
    crackle = engine_rng.standard_normal(n) * (engine_rng.random(n) < 0.03)
    s += 0.8 * lowpass(crackle, 2) * np.exp(-t * 14) * (t > 0.03)
    return s


def start():
    """Getting in: the starter motor cranks (rr-rr-rr), the engine catches, revs once and settles."""
    n = int(SR * 1.35); t = np.arange(n) / SR
    crank_end = 0.55
    crank = (t < crank_end)
    motor = np.sign(np.sin(2 * np.pi * (170 + 30 * t) * t)) * 0.3 + lowpass(engine_rng.standard_normal(n), 4) * 0.4
    strokes = 0.35 + 0.65 * np.abs(np.sin(2 * np.pi * 3.6 * t)) ** 2       # compression strokes slow it down
    s = lowpass(motor, 5) * strokes * crank * (1 - np.exp(-t * 60))
    # The catch: pulses whose rate jumps to a rev and falls back to idle.
    tt = np.maximum(t - crank_end, 0)
    rate = 26 + 46 * np.exp(-((tt - 0.18) / 0.12) ** 2) + 6 * np.exp(-tt * 3)
    phase = np.cumsum(rate) / SR
    tau = (phase - np.floor(phase)) / rate
    pulses = np.exp(-tau * 30) * np.sin(2 * np.pi * 140 * tau) + 0.6 * lowpass(engine_rng.standard_normal(n), 8) * np.exp(-tau * 40)
    swell = (t >= crank_end) * (1 - np.exp(-tt * 40)) * (0.55 + 0.45 * np.exp(-((tt - 0.18) / 0.16) ** 2))
    s += 1.1 * pulses * swell * np.exp(-np.maximum(tt - 0.5, 0) * 2.5)
    return s


engine_rng = np.random.default_rng(113)   # own stream for the engine sounds, shift and starter


wind_rng = np.random.default_rng(57)    # own stream, so adding wind leaves every other sound byte-identical
crash_rng = np.random.default_rng(31)   # separate stream: adding sounds never changes the older ones
land_rng = np.random.default_rng(91)    # own stream for the perfect-landing whoosh


def creak():
    """Rocking a stuck car: a leaf-spring creak (stick-slip clicks through a wooden-metal resonance)
    over a soft suspension thump."""
    n = int(SR * 0.42); t = np.arange(n) / SR
    # Stick-slip: clicks whose rate sweeps up then down, like a spring compressing and coming back.
    rate = 55 + 90 * np.sin(np.pi * np.clip(t / 0.34, 0, 1))
    phase = np.cumsum(rate) / SR
    tau = (phase - np.floor(phase)) / rate
    jitter = 1 + 0.25 * lowpass(creak_rng.standard_normal(n), 200) / 0.07
    clicks = np.exp(-tau * 900) * jitter
    ring = np.zeros(n)
    for f, g, d in ((640, 1.0, 38), (1130, 0.55, 55), (1720, 0.3, 70)):   # resonances of the spring and body
        kernel_t = np.arange(int(SR * 0.05)) / SR
        ring += g * np.convolve(clicks, np.sin(2 * np.pi * f * kernel_t) * np.exp(-kernel_t * d), mode="full")[:n]
    env = np.sin(np.pi * np.clip(t / 0.36, 0, 1)) ** 0.6
    s = ring / (np.max(np.abs(ring)) + 1e-9) * env * 0.8
    # The body settling on the springs: a soft low thump at the start.
    s += 0.7 * np.sin(2 * np.pi * (70 + 40 * np.exp(-t * 30)) * t) * np.exp(-t * 16)
    return s


creak_rng = np.random.default_rng(141)   # own stream for the rock-it-free creak


turbo_rng = np.random.default_rng(273)   # own stream for the turbo whistle and blow-off


def band_noise_loop(n, lo, hi):
    """Seamlessly looping noise with only the frequencies lo..hi Hz (n = one second, so bin = Hz)."""
    spectrum = np.fft.rfft(turbo_rng.standard_normal(n))
    spectrum[:lo] = 0
    spectrum[hi:] = 0
    x = np.fft.irfft(spectrum, n)
    return x / (np.std(x) + 1e-9)


def turbo():
    """Turbo whistle: a thin high whine (whole cycles in one second so it loops) with a breathy hiss.
    The game raises its pitch and volume as the boost builds."""
    t = np.arange(SR) / SR
    s = np.sin(2 * np.pi * 2300 * t) + 0.35 * np.sin(2 * np.pi * 4600 * t + 1.0) + 0.18 * np.sin(2 * np.pi * 3450 * t + 2.0)
    s *= 1.0 + 0.08 * np.sin(2 * np.pi * 7 * t)                  # a slow waver, like blades and air
    s += 0.55 * band_noise_loop(SR, 1800, 3200)                   # rushing air through the compressor
    return s


def blowoff():
    """Blow-off valve: a sharp 'pssh' of high hiss that fades, with a fluttering 'tu-tu-tu' tail."""
    n = int(SR * 0.5); t = np.arange(n) / SR
    noise = turbo_rng.standard_normal(n)
    hiss = noise - lowpass(noise, 6)                               # crude high-pass: keep the fizz
    env = np.minimum(1.0, t / 0.006) * np.exp(-t * 7.5)
    flutter = 1.0 - 0.55 * (t > 0.12) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 26 * t)))
    s = hiss * env * flutter
    s += 0.25 * lowpass(noise, 40) / 0.15 * env                    # a little body under the hiss
    return s


night_rng = np.random.default_rng(389)   # own stream for the night sounds


def crickets():
    """Night ambience, a 3 s loop: two crickets chirping out of step (a few 30 Hz pulses of a
    4–5 kHz tone per chirp) over a faint far-off chorus. Chirps wrap round the loop end, so it is seamless."""
    n = SR * 3; t = np.arange(n) / SR
    s = np.zeros(n)
    for freq, period, offset, gain in ((4400, 0.62, 0.05, 1.0), (4950, 0.81, 0.33, 0.6)):
        start = offset
        while start < 3.0:
            pulses = 3 + int(night_rng.integers(0, 2))
            for k in range(pulses):
                t0 = start + k / 31.0
                local = np.mod(t - t0, 3.0)                      # wraps round the loop end
                env = np.where(local < 0.022, np.sin(np.pi * local / 0.022) ** 2, 0.0)
                s += gain * env * np.sin(2 * np.pi * freq * local)
            start += period * (0.93 + 0.14 * night_rng.random())
    # A far-off chorus: a soft band of noise around 4.5 kHz that swells slowly (whole cycles in 3 s).
    spectrum = np.fft.rfft(night_rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    spectrum[(f < 3800) | (f > 5300)] = 0
    chorus = np.fft.irfft(spectrum, n)
    chorus /= np.std(chorus) + 1e-9
    s += 0.06 * chorus * (0.7 + 0.3 * np.sin(2 * np.pi * t / 3.0))
    return s


bird_rng = np.random.default_rng(521)   # own stream for the birds and the horn


def flutter():
    """A flock taking off: a burst of wing beats (soft clicks of filtered noise at about 16 a second,
    several birds out of step) that thins out over 0.7 s."""
    n = int(SR * 0.75); t = np.arange(n) / SR
    s = np.zeros(n)
    for bird in range(4):
        rate = 14 + 5 * bird_rng.random()
        start = 0.03 * bird + 0.04 * bird_rng.random()
        k = 0
        while True:
            t0 = start + k / rate
            if t0 > 0.7: break
            beat = np.where((t >= t0) & (t < t0 + 0.035), 1.0, 0.0)
            env = beat * np.exp(-(t - t0) * 70) * np.exp(-t * 2.2)
            s += env * lowpass(bird_rng.standard_normal(n), 5) * (0.7 + 0.3 * bird_rng.random())
            k += 1
    return s


def chirp():
    """Two quick sparrow chirps: short whistles sweeping down from about 5.5 to 3.5 kHz."""
    n = int(SR * 0.32); t = np.arange(n) / SR
    s = np.zeros(n)
    for t0, f0, f1, ln in ((0.0, 5600, 3600, 0.07), (0.13, 6000, 4000, 0.09)):
        local = t - t0
        on = (local >= 0) & (local < ln)
        f = f0 + (f1 - f0) * np.clip(local / ln, 0, 1)
        phase = 2 * np.pi * np.cumsum(f) / SR
        s += np.where(on, np.sin(np.pi * np.clip(local / ln, 0, 1)) ** 1.5, 0.0) * np.sin(phase)
    return s


def caw():
    """A crow's caw, twice: a hoarse buzz around 600 Hz that sags in pitch, with breath noise."""
    n = int(SR * 0.62); t = np.arange(n) / SR
    s = np.zeros(n)
    for t0, ln in ((0.0, 0.24), (0.33, 0.26)):
        local = t - t0
        on = (local >= 0) & (local < ln)
        x = np.clip(local / ln, 0, 1)
        f = 640 - 140 * x
        phase = 2 * np.pi * np.cumsum(f) / SR
        saw = 2 * (phase / (2 * np.pi) % 1.0) - 1
        env = np.where(on, np.minimum(1, x * 12) * (1 - x) ** 0.6, 0.0)
        s += env * (lowpass(saw, 4) + 0.35 * lowpass(bird_rng.standard_normal(n), 3))
    return s


def horn():
    """A car horn: two reedy tones a third apart (about 410 and 515 Hz), square-ish and a bit nasal, 0.5 s."""
    n = int(SR * 0.5); t = np.arange(n) / SR
    s = np.zeros(n)
    for f, g in ((410, 1.0), (515, 0.85)):
        wob = 1 + 0.004 * np.sin(2 * np.pi * 6 * t)
        phase = 2 * np.pi * f * np.cumsum(wob) / SR
        s += g * np.tanh(3.0 * np.sin(phase))
    s = lowpass(s, 3)
    env = np.minimum(1, t / 0.015) * np.minimum(1, (0.5 - t) / 0.06)
    return s * env


devil_rng = np.random.default_rng(1907)   # own stream for the dust devil sounds


def devil():
    """A dust devil: a swirling roar, two seconds that loop. Sand hiss whose band sweeps up and down
    as the column turns, over a low rumble; built in the frequency domain so it wraps without a click."""
    n = 2 * SR; t = np.arange(n) / SR
    f = np.fft.rfftfreq(n, 1 / SR)

    def band(lo_hz, hi_hz):
        spectrum = np.fft.rfft(devil_rng.standard_normal(n))
        spectrum *= np.exp(-((f - (lo_hz + hi_hz) / 2) / ((hi_hz - lo_hz) / 2)) ** 2)
        x = np.fft.irfft(spectrum, n)
        return x / (np.std(x) + 1e-9)

    low, mid, high = band(60, 260), band(500, 1400), band(1800, 4200)
    swirl = 0.5 + 0.5 * np.sin(2 * np.pi * 1.5 * t)          # whole cycles in 2 s: the column turning
    s = 0.55 * low * (0.8 + 0.2 * np.sin(2 * np.pi * 0.5 * t))
    s += 0.6 * mid * (0.45 + 0.55 * swirl)
    s += 0.35 * high * (0.3 + 0.7 * (1 - swirl))
    s *= 0.85 + 0.15 * np.sin(2 * np.pi * 3 * t + 0.7)         # gusts
    return s


def gust():
    """A whoosh: the devil grabbing the car, or blowing itself out. Noise that swells and fades with
    its band rising, then falling."""
    n = int(SR * 0.9); t = np.arange(n) / SR
    s = np.zeros(n)
    noise = devil_rng.standard_normal(n)
    env = np.sin(np.pi * np.minimum(t / 0.9, 1)) ** 1.5
    # A crude sweep: blend lowpassed copies, sharper in the middle of the whoosh.
    soft, sharp = lowpass(noise, 24), lowpass(noise, 5) - lowpass(noise, 40)
    mix = np.sin(np.pi * t / 0.9)
    s = env * ((1 - mix) * soft * 2.2 + mix * sharp)
    return s


hook_rng = np.random.default_rng(136)   # its own stream, so the older sounds stay the same


def hook():
    """The grappling hook thrown: a quick airy whizz that rises in pitch, with a rattle of rope
    and chain links (short metallic ticks, fast then slowing) running out behind it. 0.3 s."""
    n = int(SR * 0.3); t = np.arange(n) / SR
    noise = hook_rng.standard_normal(n)
    whizz = (lowpass(noise, 3) - lowpass(noise, 12)) * np.sin(np.pi * np.minimum(t / 0.22, 1)) ** 2
    s = 1.4 * whizz
    # A whistle sweeping up from ~900 Hz to ~1.8 kHz as the hook leaves the hand.
    f = 900 + 900 * np.minimum(t / 0.18, 1)
    s += 0.25 * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
    # Chain ticks: about 45 a second at first, slowing as the rope pays out.
    tick_t = 0.0; gap = 1 / 45
    while tick_t < 0.26:
        i = int(tick_t * SR)
        m = min(n - i, int(SR * 0.012))
        tt = np.arange(m) / SR
        ring = np.sin(2 * np.pi * hook_rng.uniform(3200, 4200) * tt) * np.exp(-tt * 600)
        s[i:i + m] += 0.45 * (1 - tick_t / 0.3) * ring
        tick_t += gap; gap *= 1.08
    return s


def clank():
    """The hook bites rock: a hard metallic 'tink' with a short bright ring, a gritty scrape of
    stone and a low knock. 0.35 s."""
    n = int(SR * 0.35); t = np.arange(n) / SR
    s = hook_rng.standard_normal(n) * np.exp(-t * 160)                       # the hit
    s += 0.9 * sum(a * np.sin(2 * np.pi * f * t + hook_rng.uniform(0, 6)) * np.exp(-t * (28 + f / 220))
                   for f, a in [(1460, 1), (2390, 0.7), (3720, 0.45), (5150, 0.25)])
    grit = hook_rng.standard_normal(n) * (hook_rng.random(n) < 0.3)
    s += 0.6 * lowpass(grit, 4) / 0.2 * np.exp(-t * 30)                    # stone chips
    s += 0.7 * np.sin(2 * np.pi * 110 * t) * np.exp(-t * 40)                 # knock
    return s


def vein():
    """One ore of a vein run: a bright glassy 'tink' (a struck bar: fundamental plus the inharmonic
    partials of a xylophone bar), short so the climbing notes of a run stay crisp. Deterministic:
    uses no random stream, so every older sound stays byte-identical."""
    n = int(SR * 0.32); t = np.arange(n) / SR
    f = 1318.5   # E6; the game pitches it up a major scale as the run grows
    s = np.sin(2 * np.pi * f * t) * np.exp(-t * 14)
    s += 0.45 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 32)
    s += 0.2 * np.sin(2 * np.pi * f * 5.40 * t) * np.exp(-t * 60)
    s += 0.35 * np.sin(2 * np.pi * f * 0.5 * t) * np.exp(-t * 22)     # a little body under it
    return s * (1 - np.exp(-t * 2500))


hop_rng = np.random.default_rng(889)   # own stream for the hydraulics, so every older sound stays the same


def hop():
    """Hydraulics firing: a hard pneumatic 'chk' of the valve, a deep spring 'thoomp' that drops in
    pitch as the car leaves the ground, and a short fizzy vent of air. 0.45 s."""
    n = int(SR * 0.45); t = np.arange(n) / SR
    noise = hop_rng.standard_normal(n)
    s = 0.9 * (noise - lowpass(noise, 4)) * np.exp(-t * 120)               # valve click
    f = 95 * np.exp(-t * 3.2) + 38                                         # thump sliding down
    s += 1.1 * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, t / 0.004) * np.exp(-t * 11)
    s += 0.35 * np.sin(2 * np.pi * np.cumsum(f * 2.02) / SR) * np.exp(-t * 18)
    hiss = noise - lowpass(noise, 7)
    s += 0.45 * hiss * np.minimum(1, t / 0.02) * np.exp(-np.maximum(0, t - 0.03) * 13)   # air vents
    return s


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
    save("engine_e4", engine_e4(), 0.6)            # Scrap 4-Cylinder ("engine" is the V6)
    save("engine_v8", engine_v8(), 0.75)
    save("shift", tail_fade(shift(), 40), 0.7)
    save("start", tail_fade(start(), 120), 0.6)
    save("pop", tail_fade(pop(), 30), 0.7)
    save("creak", tail_fade(creak(), 40), 0.6)
    save("turbo", turbo(), 0.5)
    save("blowoff", tail_fade(blowoff(), 60), 0.6)
    save("crickets", crickets(), 0.35)
    save("flutter", tail_fade(flutter(), 60), 0.6)
    save("chirp", tail_fade(chirp(), 20), 0.45)
    save("caw", tail_fade(caw(), 40), 0.55)
    save("horn", tail_fade(horn(), 20), 0.6)
    save("devil", devil(), 0.5)
    save("gust", tail_fade(gust(), 60), 0.6)
    save("hook", tail_fade(hook(), 30), 0.55)
    save("clank", tail_fade(clank(), 40), 0.6)
    save("vein", tail_fade(vein(), 30), 0.5)
    save("hop", tail_fade(hop(), 50), 0.65)
    print("sounds written to", os.path.abspath(OUT))
