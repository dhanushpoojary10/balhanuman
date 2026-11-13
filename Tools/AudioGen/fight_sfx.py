"""Level 2 (Patala Yuddha) fight sound effects.
Run:  python Tools/AudioGen/fight_sfx.py
Writes WAVs to Assets/Resources/Audio/Level02 using the exact names FightSfx.cs loads
(plus _2 / _3 variants for the sounds you hear most, which FightSfx picks at random).
"""
import os
import numpy as np
from scipy.signal import butter, sosfilt, iirpeak, lfilter

import generate_audio as g
from generate_audio import SR, T, noise, midi, temple_bell, conch, chime, place

OUT2 = os.path.join(g.OUT, "Level02")
os.makedirs(OUT2, exist_ok=True)
NY = SR / 2


def save(name, x, peak=0.9):
    g.save(name, x, SR, peak=peak, out=OUT2)


# ---------------------------------------------------------------- building blocks
def bp(x, lo, hi):
    return sosfilt(butter(2, [lo / NY, min(hi, NY * 0.95) / NY], 'band', output='sos'), x)


def lpf(x, fc):
    return sosfilt(butter(2, min(fc, NY * 0.95) / NY, 'low', output='sos'), x)


def dec(t, k): return np.exp(-t * k)


def atk(t, a): return np.minimum(1, t / a)


def sat(x, d=1.5): return np.tanh(x * d) / np.tanh(d)


def swell(t, dur, pk, a=2.0, b=2.0):
    """0 -> 1 at pk (fraction of dur) -> 0"""
    p = pk * dur
    return np.where(t < p, (t / p) ** a, np.clip((dur - t) / (dur - p), 0, 1) ** b)


def chirp(t, f0, f1, k):
    """sine whose pitch glides from f0 to f1 (rate k)"""
    f = f1 + (f0 - f1) * np.exp(-t * k)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def sweep_noise(dur, f0, f1, width=1.0):
    """band-limited noise whose centre frequency glides f0 -> f1 (air, wind, cloth)"""
    t = T(dur); n = len(t)
    centers = np.geomspace(min(f0, f1) * 0.7, max(f0, f1) * 1.5, 9)
    w = noise(n)
    tc = np.exp(np.log(f0) + (np.log(f1) - np.log(f0)) * np.clip(t / dur, 0, 1))
    out = np.zeros(n)
    for c in centers:
        wgt = np.exp(-0.5 * ((np.log(tc) - np.log(c)) / (0.38 * width)) ** 2)
        out += bp(w, c / 1.4, c * 1.4) * wgt
    return out / (np.std(out) + 1e-9)


def formant(src, spec):
    out = np.zeros_like(src)
    for fc, q, gn in spec:
        b, a = iirpeak(fc / NY, q)
        out += gn * lfilter(b, a, src)
    return out


def echo(x, delay, fb, taps=3):
    d = int(delay * SR)
    y = np.zeros(len(x) + d * taps)
    y[:len(x)] += x
    for i in range(1, taps + 1):
        y[d * i:d * i + len(x)] += x * (fb ** i)
    return y


def partials(t, base, ratios, amps, decays, detune=0.0):
    s = np.zeros_like(t)
    for r, a, k in zip(ratios, amps, decays):
        s += a * np.sin(2 * np.pi * base * r * t) * dec(t, k)
        if detune: s += 0.6 * a * np.sin(2 * np.pi * base * r * (1 + detune) * t) * dec(t, k)
    return s


def wet(x, secs, mix):
    return g.wet_sfx(x, secs, mix)


def chord_swell(midis, dur, pk, vol=1.0, harm=6):
    t = T(dur)
    s = np.zeros_like(t)
    for m in midis:
        f = midi(m)
        for det in (-0.004, 0.004):
            for h in range(1, harm + 1):
                s += np.sin(2 * np.pi * f * (1 + det) * h * t + np.random.rand() * 6) / h ** 1.1
    s = lpf(s, 2600)
    return s / (np.std(s) + 1e-9) * swell(t, dur, pk, 1.6, 1.8) * vol * 0.5


# ---------------------------------------------------------------- the sounds
def whoosh(i=0):
    d = 0.4; t = T(d)
    x = sweep_noise(d, 250 * (1 + .18 * i), 1900 * (1 - .08 * i), 1.0) * swell(t, d, 0.55, 1.6, 2.2)
    body = chirp(t, 150, 90, 5) * swell(t, d, 0.5) * 0.35
    return x * 0.8 + body


def whoosh_heavy(i=0):
    d = 0.85; t = T(d)
    x = sweep_noise(d, 90 * (1 + .2 * i), 700, 1.3) * swell(t, d, 0.62, 1.8, 2.0)
    rumble = lpf(noise(len(t)), 160) * swell(t, d, 0.6) * 1.2
    sub = chirp(t, 60, 42, 3) * swell(t, d, 0.6) * 0.5
    return sat(x * 0.9 + rumble + sub, 1.3)


def hit(i=0):
    d = 0.4; t = T(d); k = 1 + .12 * i
    thump = chirp(t, 150 * k, 55, 40) * dec(t, 14)
    crack = bp(noise(len(t)), 1200 * k, 5200) * atk(t, .0006) * dec(t, 60) * 0.9
    body = lpf(noise(len(t)), 900) * dec(t, 28) * 0.8
    return sat(thump + crack + body, 1.4)


def hit_heavy(i=0):
    d = 1.1; t = T(d); k = 1 + .05 * i
    boom = chirp(t, 95, 38, 18) * dec(t, 4.2) * 1.2
    metal = partials(t, 640 * k, (1, 2.31, 3.87, 5.6, 7.9), (1, .7, .5, .35, .25), (5, 7, 9, 12, 15), 0.003) * 0.35
    crack = bp(noise(len(t)), 1000, 6000) * dec(t, 45) * atk(t, .0005)
    low = lpf(noise(len(t)), 500) * dec(t, 12)
    return wet(sat(boom + metal + crack + low, 1.6), 0.5, 0.15)


def slam():
    d = 1.5; t = T(d)
    boom = chirp(t, 70, 28, 10) * dec(t, 2.8) * 1.3
    dust = lpf(noise(len(t)), 350) * dec(t, 3) * 0.9
    crack = bp(noise(len(t)), 700, 6000) * dec(t, 30) * atk(t, .0006)
    out = boom + dust + crack
    # falling rubble: sparse grains that thin out
    r = np.random.default_rng(5)
    for _ in range(46):
        tt = 0.05 + (r.random() ** 1.8) * 1.2
        gd = bp(noise(int(SR * 0.05)), r.uniform(600, 3500), r.uniform(4000, 9000)) * dec(T(0.05), 90) * r.uniform(.1, .4)
        i0 = int(tt * SR); out[i0:i0 + len(gd)] += gd[:len(out) - i0]
    return wet(sat(out, 1.5), 1.2, 0.25)


def clang(i=0):
    d = 1.4; t = T(d); base = 900 * (1 + .07 * i)
    ring = partials(t, base, (1, 2.32, 4.25, 6.63, 9.38), (1, .6, .45, .3, .2), (3.5, 4.5, 6, 8, 10), 0.003)
    ring *= 1 + 0.25 * np.sin(2 * np.pi * 9 * t) * dec(t, 2)
    spark = bp(noise(len(t)), 2500, 11000) * dec(t, 120) * atk(t, .0004)
    return wet(sat(ring * 0.5 + spark, 1.3), 0.8, 0.2)


def block():
    d = 0.28; t = T(d)
    thud = chirp(t, 160, 85, 60) * dec(t, 22)
    nz = lpf(noise(len(t)), 1200) * dec(t, 45) * 0.6
    clank = np.sin(2 * np.pi * 520 * t) * dec(t, 40) * 0.2
    return sat(thud + nz + clank, 1.3)


def kick(i=0):
    d = 0.45
    air = sweep_noise(0.18, 500, 3500) * swell(T(0.18), 0.18, 0.7)
    imp = hit(i + 1)[:int(SR * 0.3)] * 0.9
    return place(d, [(0, air * 0.6), (0.11, imp)])


def dodge():
    d = 0.38; t = T(d)
    x = sweep_noise(d, 1200, 4500, 1.2) * swell(t, d, 0.4)
    x *= 1 + 0.5 * np.sin(2 * np.pi * 28 * t) * dec(t, 6)                  # cloth flutter
    return x * 0.7


def jump():
    d = 0.3; t = T(d)
    cloth = sweep_noise(d, 400, 1400) * swell(t, d, 0.3) * 0.4
    push = chirp(t, 110, 70, 30) * dec(t, 25) * 0.5
    return cloth + push


def land():
    d = 0.3; t = T(d)
    thump = chirp(t, 100, 55, 50) * dec(t, 20)
    dust = lpf(noise(len(t)), 700) * dec(t, 30) * 0.7 + bp(noise(len(t)), 1500, 5000) * dec(t, 40) * 0.2
    return sat(thump + dust, 1.3)


def wind():
    d = 1.8; t = T(d)
    x = sweep_noise(d, 250, 1100, 1.5) * swell(t, d, 0.5, 1.4, 1.4)
    x *= 1 + 0.2 * np.sin(2 * np.pi * 4 * t)
    return x * 0.9 + lpf(noise(len(t)), 300) * swell(t, d, 0.5) * 0.4


def fire():
    d = 1.4; t = T(d)
    whoomp = lpf(noise(len(t)), 400) * atk(t, .04) * dec(t, 2.5) * 1.2 + chirp(t, 90, 45, 6) * dec(t, 6) * 0.8
    roar = bp(noise(len(t)), 300, 2500) * atk(t, .1) * dec(t, 1.8) * 0.6
    r = np.random.default_rng(9); out = whoomp + roar
    for _ in range(90):                                                    # crackle
        tt = r.random() ** 1.5 * 1.2
        gd = bp(noise(int(SR * .01)), r.uniform(1500, 4000), 9000) * r.uniform(.1, .5)
        i0 = int(tt * SR); out[i0:i0 + len(gd)] += gd[:len(out) - i0]
    return wet(sat(out, 1.4), 0.6, 0.15)


def shrink():
    d = 1.0; parts = []
    f = 4200
    for i in range(7):
        t = T(0.4)
        tone = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t)) * dec(t, 8) * atk(t, .002)
        parts.append((i * 0.07, tone * 0.5)); f *= 0.78
    sp = bp(noise(int(SR * .5)), 5000, 12000) * dec(T(.5), 10) * 0.2
    return wet(place(d, parts + [(0, sp)]), 1.0, 0.3)


def grow():
    d = 2.2; t = T(d)
    f = 32 + 50 * np.clip(t / 1.6, 0, 1) ** 1.5
    sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * swell(t, d, 0.68, 1.5, 2.5)
    rumble = sweep_noise(d, 80, 700, 1.2) * swell(t, d, 0.7) * 0.9
    tone = np.sin(2 * np.pi * np.cumsum(110 + 180 * np.clip(t / 1.6, 0, 1)) / SR) * 0.2 * swell(t, d, 0.7)
    return wet(sat(sub + rumble + tone, 1.3), 1.2, 0.25)


def step_giant():
    d = 1.0; t = T(d)
    boom = chirp(t, 75, 34, 14) * dec(t, 5) * 1.3
    rum = lpf(noise(len(t)), 250) * dec(t, 6) * 0.9
    crumble = np.concatenate([np.zeros(int(.03 * SR)), bp(noise(len(t)), 400, 2500) * dec(t, 14) * 0.2])[:len(t)]
    return wet(sat(boom + rum + crumble, 1.5), 0.8, 0.2)


def thunder():
    d = 3.0; t = T(d)
    crack = bp(noise(len(t)), 800, 9000) * atk(t, .003) * dec(t, 30)
    roll = lpf(noise(len(t)), 180) * atk(t, .08) * dec(t, 1.3) * (1 + .5 * np.sin(2 * np.pi * 7 * t)) * 1.4
    holy = sum(np.sin(2 * np.pi * midi(m) * t) * 0.07 + np.sin(2 * np.pi * midi(m) * 2 * t) * 0.03 for m in (74, 81, 86)) * atk(t, .4) * dec(t, 1.2)
    return wet(sat(crack + roll + holy * 3, 1.3), 2.0, 0.35)


def power():
    d = 1.8
    chord = chord_swell((50, 57, 62, 66, 69), d, 0.35, 1.0)
    top = place(d, [(0.35, chime(midi(86), 1.4, 1.0, 2.5))])
    t = T(d); air = sweep_noise(d, 400, 3000) * swell(t, d, .35) * 0.12
    return wet(chord + top + air, 1.5, 0.3)


def ultimate():
    d = 3.6; t = T(d)
    chord = chord_swell((38, 50, 57, 62, 66, 69, 74), d, 0.4, 1.0)
    cn = conch(SR, 3.0) * 1.2
    bell = temple_bell(midi(62), 3.5, 0.9, SR)
    boom = chirp(t, 60, 30, 5) * dec(t, 2.5) * 0.9
    wnd = sweep_noise(d, 200, 1400, 1.3) * swell(t, d, .35) * 0.25
    return wet(place(d, [(0, chord + boom + wnd), (0.1, cn), (1.2, bell)]), 2.0, 0.3)


def lamp_out():
    d = 0.7; t = T(d)
    puff = bp(noise(len(t)), 600, 3500) * atk(t, .004) * dec(t, 9)
    hiss = np.concatenate([np.zeros(int(.05 * SR)), bp(noise(len(t)), 3000, 9000) * dec(t, 8) * 0.3])[:len(t)]
    flick = chirp(t, 900, 300, 8) * dec(t, 14) * 0.12
    return (puff + hiss + flick) * (1 + .3 * np.sin(2 * np.pi * 22 * t) * dec(t, 5))


def orb_cast():
    d = 1.2; t = T(d)
    f = 70 + 90 * (t / 1.0) ** 2 + 6 * np.sin(2 * np.pi * 9 * t)
    ph = 2 * np.pi * np.cumsum(f) / SR
    hum = lpf(sum(np.sin(h * ph) / h for h in range(1, 6)), 900) * atk(t, .15) * (0.3 + 0.7 * np.clip(t / 1.0, 0, 1))
    swl = sweep_noise(d, 200, 1500) * swell(t, d, .85, 2, 8) * 0.4
    return sat((hum * 1.3 + swl) * np.clip((d - t) / 0.08, 0, 1), 1.3)


def orb_blast():
    d = 1.4; t = T(d)
    boom = chirp(t, 110, 34, 12) * dec(t, 3.8) * 1.2
    nz = lpf(noise(len(t)), 1800) * dec(t, 7)
    dis = (np.sin(2 * np.pi * 85 * t) + np.sin(2 * np.pi * 91 * t) + 0.6 * np.sin(2 * np.pi * 140 * t)) * dec(t, 4) * 0.25
    siz = bp(noise(len(t)), 3000, 9000) * dec(t, 9) * 0.25
    return wet(sat(boom + nz + dis + siz, 1.6), 1.0, 0.3)


def teleport():
    d = 0.8
    t1 = T(0.5)
    rev = sweep_noise(0.5, 300, 5000) * swell(t1, 0.5, 0.92, 2.2, 6) * 0.8
    sh = np.sin(2 * np.pi * np.cumsum(500 + 2500 * (t1 / .5) ** 2) / SR) * swell(t1, .5, .92, 2, 6) * 0.2
    t2 = T(0.3)
    pop = lpf(noise(len(t2)), 1500) * dec(t2, 25) + chirp(t2, 500, 120, 40) * dec(t2, 20) * 0.6
    return place(d, [(0, rev + sh), (0.48, pop)])


def poof():
    d = 0.45; t = T(d)
    p = bp(noise(len(t)), 500, 3500) * atk(t, .004) * dec(t, 11)
    return p + chirp(t, 180, 80, 30) * dec(t, 25) * 0.4 + bp(noise(len(t)), 4000, 9000) * dec(t, 30) * 0.15


def magic():
    d = 1.4; parts = []
    for i, m in enumerate((74, 76, 78, 81, 83, 86, 88, 90, 93)):
        t = T(0.5); f = midi(m)
        tone = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t)) * dec(t, 7) * atk(t, .002)
        parts.append((i * 0.055, tone * 0.45))
    t = T(d); sp = bp(noise(len(t)), 6000, 14000) * swell(t, d, .4) * 0.12
    return wet(place(d, parts + [(0, sp)]), 1.2, 0.35)


def bell():
    b = temple_bell(311.0, 4.5, 1.0, SR)
    return wet(b, 1.5, 0.25)


def ko():
    d = 3.0; t = T(d)
    boom = chirp(t, 80, 24, 6) * dec(t, 1.3) * 1.4
    nz = lpf(noise(len(t)), 600) * dec(t, 3)
    crash = bp(noise(len(t)), 1500, 9000) * dec(t, 5) * 0.5
    met = partials(t, 220, (1, 2.7, 4.3, 6.2), (1, .6, .4, .25), (2.5, 3, 4, 5), 0.004) * 0.3
    return wet(sat(boom + nz + crash + met, 1.6), 2.2, 0.4)


def tick():
    t = T(0.12)
    return (np.sin(2 * np.pi * 1700 * t) + 0.3 * np.sin(2 * np.pi * 3400 * t)) * dec(t, 60) * atk(t, .0004)


def rank():
    d = 1.1; parts = []
    for i, f in enumerate((784, 988, 1175, 1568)):
        t = T(1.0 if i == 3 else 0.5)
        tone = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t) + 0.15 * np.sin(2 * np.pi * f * 3 * t)) * dec(t, 3 if i == 3 else 6) * atk(t, .002)
        parts.append((i * 0.09, tone * 0.5))
    return wet(place(d, parts), 0.8, 0.25)


def roar():
    d = 1.9; t = T(d)
    f = 75 - 12 * t + 10 * np.sin(2 * np.pi * 7 * t)
    ph = 2 * np.pi * np.cumsum(f) / SR
    src = sum(np.sin(h * ph) / h for h in range(1, 41))
    v = formant(src, [(600, 5, 1.0), (1000, 6, 0.8), (2400, 8, 0.4)])
    v *= 1 + 0.6 * np.sin(2 * np.pi * 30 * t)                              # growl
    nz = bp(noise(len(t)), 300, 3000) * 0.5
    e = atk(t, .08) * np.where(t < 1.0, 1, dec(t - 1.0, 4))
    return wet(sat((v / (np.std(v) + 1e-9) * 0.8 + nz) * e, 2.5), 0.8, 0.2)


def laugh():
    d = 2.2
    syl = [(0.0, 0.18, 108), (0.26, 0.18, 104), (0.52, 0.18, 100), (0.84, 0.7, 96)]
    parts = []
    for st, sd, f0 in syl:
        t = T(sd)
        f = f0 * (1 - 0.18 * t / sd) * (1 + 0.012 * np.sin(2 * np.pi * 6 * t))
        ph = 2 * np.pi * np.cumsum(f) / SR
        src = sum(np.sin(h * ph) / h for h in range(1, 31))
        v = formant(src, [(700, 6, 1.0), (1150, 8, 0.7), (2800, 10, 0.25)])
        e = atk(t, .015) * (dec(t, 6) if sd < .3 else swell(t, sd, .3, 1.5, 1.2))
        br = bp(noise(len(t)), 1500, 5000) * dec(t, 40) * 0.5
        parts.append((st, (v / (np.std(v) + 1e-9) * 0.7 + br) * e))
    dry = sat(place(d, parts), 1.8)
    return wet(echo(dry, 0.22, 0.4, 3), 1.0, 0.3)


# ---------------------------------------------------------------- render (name, builder, peak, variants)
PLAN = [
    ("whoosh", whoosh, .62, 3), ("whoosh_heavy", whoosh_heavy, .8, 2),
    ("hit", hit, .85, 3), ("hit_heavy", hit_heavy, .95, 2),
    ("slam", slam, .95, 1), ("clang", clang, .8, 2), ("block", block, .6, 1), ("kick", kick, .8, 2),
    ("dodge", dodge, .5, 1), ("jump", jump, .45, 1), ("land", land, .6, 1), ("wind", wind, .6, 1),
    ("fire", fire, .8, 1), ("shrink", shrink, .6, 1), ("grow", grow, .85, 1), ("step_giant", step_giant, .9, 1),
    ("thunder", thunder, .95, 1), ("power", power, .7, 1), ("ultimate", ultimate, .95, 1),
    ("lamp_out", lamp_out, .6, 1), ("orb_cast", orb_cast, .7, 1), ("orb_blast", orb_blast, .9, 1),
    ("teleport", teleport, .7, 1), ("poof", poof, .6, 1), ("magic", magic, .6, 1), ("bell", bell, .7, 1),
    ("ko", ko, .95, 1), ("tick", tick, .35, 1), ("rank", rank, .6, 1), ("roar", roar, .85, 1), ("laugh", laugh, .75, 1),
]


def build(fn, i):
    try:
        return fn(i)
    except TypeError:
        return fn()


if __name__ == "__main__":
    np.random.seed(11)
    for name, fn, peak, variants in PLAN:
        for v in range(variants):
            save(name if v == 0 else f"{name}_{v + 1}", build(fn, v), peak)
