"""Procedural audio generator for Bal Hanuman.
Run:  python Tools/AudioGen/generate_audio.py
Writes WAVs to Assets/Resources/Audio (loaded at runtime by Music.cs / Sfx.cs).
Music is in raga Bhoop (D E F# A B) over a tanpura drone, with bansuri flute, frame drum, temple bell.
"""
import os, wave
import numpy as np

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def midi(m): return 440.0 * 2 ** ((m - 69) / 12.0)


def save(name, data, sr):
    d = np.asarray(data, dtype=np.float64)
    if d.ndim == 1: d = d[:, None]
    peak = np.max(np.abs(d)) or 1.0
    d = d / peak * 0.89
    pcm = (d * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(d.shape[1]); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    print(f"{name}.wav  {d.shape[0]/sr:.1f}s  {d.shape[1]}ch")


def lp(x, a):  # one-pole low-pass
    from scipy.signal import lfilter
    return lfilter([a], [1, a - 1], x)


def noise(n): return rng.standard_normal(n)


def reverb_ir(sr, secs, damp=0.5):
    n = int(sr * secs)
    t = np.arange(n) / sr
    ir = noise(n) * np.exp(-t * (6.9 / secs))
    ir = lp(ir, 1 - damp * 0.85)
    ir[0] = 0
    return ir / np.sqrt(np.sum(ir ** 2))


def fft_conv(x, ir, circular=False):
    n = len(x) if circular else len(x) + len(ir)
    N = int(2 ** np.ceil(np.log2(n if not circular else n)))
    if circular:
        # wrap the IR into x's length so the tail loops seamlessly
        irw = np.zeros(len(x))
        for i in range(0, len(ir), len(x)):
            seg = ir[i:i + len(x)]
            irw[:len(seg)] += seg
        return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(irw), len(x))
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:n]


# =========================================================== MUSIC
# "Bal Hanuman bhajan": raga Bhoop (D E F# A B), keherwa taal (8 matras, Dha Ge Na Ti Na Ke Dhi Na).
# 15 cycles x 4 s = 60 s loop.   0-1 alap | 2-5 sitar theme + soft tabla | 6-9 bansuri theme, full tabla
# | 10-13 climax: choir, dholak, conch | 14 resolve
from scipy.signal import iirpeak, lfilter
SR_M = 32000
MATRA = 0.5
CYCLES = 15
LOOP = int(CYCLES * 8 * MATRA * SR_M)
mt = MATRA * SR_M
SA = 62  # D4


def add(buf, start, sig):
    n = len(sig); s = int(round(start)) % len(buf)
    first = min(n, len(buf) - s)
    buf[s:s + first] += sig[:first]
    if first < n:
        rest = min(n - first, len(buf)); buf[:rest] += sig[first:first + rest]


def tanpura_pluck(f, dur=3.4):
    n = int(SR_M * dur); t = np.arange(n) / SR_M
    s = np.zeros(n)
    for h in range(1, 24):
        amp = (1.0 / h ** 0.7) * (1 + 1.8 * np.exp(-((h - 7) ** 2) / 10))
        s += amp * np.sin(2 * np.pi * f * h * t + h) * np.exp(-t * (0.8 + h * 0.2))
    return s * np.minimum(1, t / 0.004) / 6


def temple_bell(f, dur=6.0, vol=1.0, sr=SR_M):
    n = int(sr * dur); t = np.arange(n) / sr
    parts = [(0.5, .35, 0.5), (1.0, 1, 0.7), (1.19, .45, 0.9), (1.56, .3, 1.1), (2.0, .55, 1.0),
             (2.74, .4, 1.5), (3.0, .22, 1.7), (4.1, .18, 2.2), (5.4, .12, 3.0)]
    s = np.zeros(n)
    for ratio, a_, d in parts:
        s += a_ * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t * d * 0.55)
        s += a_ * 0.6 * np.sin(2 * np.pi * f * ratio * 1.002 * t) * np.exp(-t * d * 0.55)
    s *= np.minimum(1, t / 0.002)
    strike = lp(noise(n), 0.4) * np.exp(-t * 90) * 0.5
    return (s * 0.22 + strike * 0.2) * vol


def gliss(f0, from_f, t, tau):
    """frequency contour that starts at from_f and settles on f0 (meend)"""
    if from_f is None: return f0 * np.ones_like(t)
    return f0 * np.exp(np.log(from_f / f0) * np.exp(-t / tau))


def sitar(f, dur, from_f=None, vol=1.0):
    n = int(SR_M * (dur + 0.3)); t = np.arange(n) / SR_M
    ph = 2 * np.pi * np.cumsum(gliss(f, from_f, t, 0.09)) / SR_M
    s = np.zeros(n)
    for h in range(1, 28):
        amp = h ** -0.5 * (1 + 2.4 * np.exp(-((h - 10) ** 2) / 30))       # jawari bridge buzz
        s += amp * np.sin(h * ph + 0.4 * h) * np.exp(-t * (1.1 + 0.2 * h))
    s *= np.minimum(1, t / 0.002)
    s += lp(noise(n), 0.5) * np.exp(-t * 120) * 0.25                      # pick
    return s * vol * 0.07


def flute(f0, dur, from_f=None, vol=1.0):
    n = int(SR_M * (dur + 0.45)); t = np.arange(n) / SR_M
    g = gliss(f0, from_f, t, 0.07) if from_f else f0 * (1 - 0.05 * np.exp(-t / 0.05))
    vib = 0.006 * np.clip((t - 0.25) / 0.5, 0, 1) * np.sin(2 * np.pi * 5.4 * t)
    gam = (0.022 * np.clip((t - (dur - 0.6)) / 0.2, 0, 1) * np.sin(2 * np.pi * 6.5 * t)) if dur >= 1.25 else 0
    ph = 2 * np.pi * np.cumsum(g * (1 + vib + gam)) / SR_M
    s = np.sin(ph) + 0.30 * np.sin(2 * ph) + 0.10 * np.sin(3 * ph) + 0.04 * np.sin(4 * ph)
    br = lp(noise(n), 0.35) - lp(noise(n), 0.03)
    s = s + br * 0.45 * (0.6 + 0.4 * np.sin(2 * np.pi * 5.4 * t))
    att = np.minimum(1, t / 0.1); rel = np.clip((dur + 0.35 - t) / 0.35, 0, 1)
    return s * att * rel * vol * 0.2


def harmonium(f, dur, vol=1.0):
    n = int(SR_M * dur); t = np.arange(n) / SR_M
    s = np.zeros(n)
    for det in (-0.0025, 0.0025):
        for h in range(1, 14):
            s += h ** -0.75 * np.sin(2 * np.pi * f * (1 + det) * h * t + h)
    s = lp(s, 0.4)
    env = np.minimum(1, t / 0.9) * np.minimum(1, (dur - t) / 1.1)
    return s * env * vol * 0.03


def formant(src, spec):
    out = np.zeros_like(src)
    for fc, q, g in spec:
        b, a = iirpeak(fc / (SR_M / 2), q)
        out += g * lfilter(b, a, src)
    return out


def choir(freqs, dur, mode, vol=1.0):
    """choral voices: mode 'om' (oh -> mmm each 8 s phrase) or 'aa' (open ahh)"""
    n = int(SR_M * dur); t = np.arange(n) / SR_M
    src = np.zeros(n)
    for f in freqs:
        for det in (-0.006, 0.0, 0.006):
            vib = 1 + 0.004 * np.sin(2 * np.pi * (5.2 + rng.uniform(-.4, .4)) * t + rng.uniform(0, 6))
            ph = 2 * np.pi * np.cumsum(f * (1 + det) * vib) / SR_M
            src += sum(np.sin(h * ph + rng.uniform(0, 6)) / h for h in range(1, 31))
    if mode == 'aa':
        out = formant(src, [(700, 6, 1.0), (1150, 8, 0.6), (2800, 10, 0.25)])
        env = np.minimum(1, t / 2.0) * np.minimum(1, (dur - t) / 2.0)
    else:
        o = formant(src, [(450, 6, 1.0), (800, 8, 0.5), (2830, 10, 0.12)])
        m = formant(src, [(250, 5, 1.0), (1000, 12, 0.08)])
        ph8 = (t % 8.0) / 8.0
        wo = np.sin(np.pi * np.clip(ph8 / 0.66, 0, 1)) * (ph8 < 0.66)
        wm = np.where(ph8 >= 0.5, np.sin(np.pi * np.clip((ph8 - 0.5) / 0.5, 0, 1)), 0)
        out = o * wo + m * wm * 1.2
        env = np.minimum(1, t / 0.5) * np.minimum(1, (dur - t) / 1.0)
    return out * env * vol * 0.012


def conch(sr=SR_M, dur=3.0):
    t = np.arange(int(sr * dur)) / sr
    f = 233 * (0.97 + 0.03 * np.minimum(1, t / 0.4))
    ph = 2 * np.pi * np.cumsum(f * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) / sr
    s = sum(np.sin(h * ph) / h ** 0.8 for h in range(1, 14))
    return lp(s, 0.35) * np.minimum(1, t / 0.35) * np.clip((dur - t) / 1.2, 0, 1) ** 1.2 * 0.3


# ---- tabla
TABLA_F = midi(SA)  # dayan tuned to Sa


def dayan(kind):
    dur = {'na': .4, 'tin': .5, 'tun': 1.1, 'ti': .15}[kind]
    n = int(SR_M * dur); t = np.arange(n) / SR_M
    dec = {'na': 11, 'tin': 8, 'tun': 3.2, 'ti': 30}[kind]
    amps = {'na': (.5, 1, .6, .3), 'tin': (.6, 1, .45, .25), 'tun': (1, .55, .25, .1), 'ti': (.3, 1, .5, .3)}[kind]
    s = np.zeros(n)
    for a_, r in zip(amps, (1, 2.0, 3.0, 4.1)):
        s += a_ * np.sin(2 * np.pi * TABLA_F * r * t * (1 + 0.02 * np.exp(-t * 50))) * np.exp(-t * dec * (0.6 + 0.5 * r / 2))
    s += lp(noise(n), 0.7) * np.exp(-t * 300) * 0.6
    return s * 0.5


def bayan(kind):
    n = int(SR_M * .6); t = np.arange(n) / SR_M
    if kind == 'ge':
        f = 88 + 62 * (1 - np.exp(-t * 9))
        s = np.sin(2 * np.pi * np.cumsum(f) / SR_M) * np.exp(-t * 7) + 0.3 * np.sin(2 * np.pi * np.cumsum(f * 2) / SR_M) * np.exp(-t * 12)
        s += lp(noise(n), 0.3) * np.exp(-t * 150) * 0.4
        return s * 0.9
    s = lp(noise(n), 0.45) * np.exp(-t * 70) * 0.9 + np.sin(2 * np.pi * 190 * t) * np.exp(-t * 45) * 0.5   # ke
    return s * 0.6


def boom(vol=1.0):
    n = int(SR_M * 0.9); t = np.arange(n) / SR_M
    f = 48 + 40 * np.exp(-t * 16)
    return (np.sin(2 * np.pi * np.cumsum(f) / SR_M) * np.exp(-t * 4.5) + lp(noise(n), 0.2) * np.exp(-t * 45) * 0.35) * vol


_tc = {}


def mix2(x, y):
    n = max(len(x), len(y)); o = np.zeros(n); o[:len(x)] += x; o[:len(y)] += y; return o


def stroke(name):
    if name in _tc: return _tc[name]
    m = {'na': lambda: dayan('na'), 'ti': lambda: dayan('ti'), 'tin': lambda: dayan('tin'), 'tun': lambda: dayan('tun'),
         'ge': lambda: bayan('ge'), 'ke': lambda: bayan('ke'),
         'dha': lambda: mix2(dayan('na'), bayan('ge')), 'dhi': lambda: mix2(dayan('tin') * 0.8, bayan('ge') * 0.7)}
    _tc[name] = m[name]()
    return _tc[name]


def manjira(vol=1.0):
    n = int(SR_M * .5); t = np.arange(n) / SR_M
    s = sum(np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, d in ((2650, 20), (4130, 26), (5710, 32), (7390, 40), (9100, 50)))
    return (s * 0.2 + lp(noise(n), 0.9) * np.exp(-t * 200) * 0.3) * vol


def chain(notes):
    out = []; prev = None
    for b, d, m in notes:
        adj = prev is not None and abs(prev[0] + prev[1] - b) < 1e-6 and abs(prev[2] - m) >= 2
        out.append((b, d, m, prev[2] if adj else None)); prev = (b, d, m)
    return out


def make_music():
    S = {k: np.zeros(LOOP) for k in ('tanpura', 'pad', 'choir', 'sitar', 'flute', 'tabla', 'boom', 'manjira', 'bells')}
    cyc = 8 * mt

    # tanpura: Pa Sa Sa low-Sa, a pluck every 1.5 matras
    strings = [midi(57), midi(62), midi(62), midi(50)]
    step = 1.5 * mt
    for i in range(int(LOOP / step)):
        add(S['tanpura'], i * step + rng.uniform(-200, 200), tanpura_pluck(strings[i % 4]) * 0.9)

    # harmonium drone chords
    chords = {0: (38, 50, 57, 62), 4: (35, 47, 54, 59), 6: (38, 50, 57, 62), 8: (33, 45, 52, 57),
              10: (38, 50, 57, 66, 62), 12: (35, 47, 54, 59, 62), 14: (38, 50, 57, 62)}
    keys = sorted(chords)
    for i, c in enumerate(keys):
        end = keys[i + 1] if i + 1 < len(keys) else CYCLES
        for m in chords[c]:
            add(S['pad'], c * cyc, harmonium(midi(m), (end - c) * 8 * MATRA + 1.6, 1.3 if c >= 10 else 1.0))

    # Om chant in the alap + final cycle; open "ahh" choir in the climax
    add(S['choir'], 0, choir([midi(38), midi(50)], 16.0, 'om'))
    add(S['choir'], 10 * cyc, choir([midi(50), midi(57), midi(62), midi(66)], 5 * 8 * MATRA + 3.0, 'aa'))
    add(S['choir'], 14 * cyc, choir([midi(38), midi(50)], 4.5, 'om', 0.8))

    # ---- melody lines (start matra, duration in matras, midi)
    alap = [(1, 4, 69), (5, 3, 71), (8, 3, 69), (11, 2, 66), (13, 3, 62)]
    A = [(16, 3, 69), (19, 1, 71), (20, 2, 74), (22, 1, 71), (23, 1, 69),
         (24, 2, 66), (26, 2, 69), (28, 1, 66), (29, 1, 64), (30, 2, 62),
         (32, 3, 69), (35, 1, 71), (36, 2, 74), (38, 1, 76), (39, 1, 74),
         (40, 2, 71), (42, 1, 69), (43, 1, 66), (44, 2, 69), (46, 2, 62)]
    B = [(48, 3, 74), (51, 1, 76), (52, 2, 78), (54, 1, 76), (55, 1, 74),
         (56, 2, 71), (58, 2, 74), (60, 2, 76), (62, 2, 74),
         (64, 3, 78), (67, 1, 81), (68, 2, 78), (70, 1, 76), (71, 1, 74),
         (72, 2, 76), (74, 2, 74), (76, 2, 71), (78, 2, 69)]
    C = [(80, 2, 74), (82, 1, 76), (83, 1, 78), (84, 3, 81), (87, 1, 78),
         (88, 2, 76), (90, 2, 74), (92, 2, 78), (94, 2, 76),
         (96, 2, 81), (98, 1, 83), (99, 1, 86), (100, 3, 83), (103, 1, 81),
         (104, 2, 78), (106, 1, 76), (107, 1, 74), (108, 2, 71), (110, 2, 69)]
    D = [(112, 3, 69), (115, 1, 66), (116, 2, 64), (118, 2, 62)]

    def play_sitar(notes, shift=0, vol=1.0):
        for b, d, m, prev in chain(notes):
            m += shift; pf = midi(prev + shift) if prev is not None else None
            add(S['sitar'], b * mt, sitar(midi(m), d * MATRA * 0.9, pf, vol))
            k = 1
            while d * MATRA >= 1.0 and k < d:                         # re-pick sustained notes like a real sitar
                add(S['sitar'], (b + k) * mt, sitar(midi(m), 0.8, None, vol * 0.7 ** k)); k += 1

    def play_flute(notes, shift=0, vol=1.0):
        for b, d, m, prev in chain(notes):
            add(S['flute'], b * mt, flute(midi(m + shift), d * MATRA, midi(prev + shift) if prev is not None else None, vol))

    play_flute(alap, 0, 0.9)
    play_sitar(A, 0, 1.0)
    play_flute(A, 12, 0.45)                 # bansuri answers an octave up, softly
    play_flute(B, 0, 1.0); play_sitar(B, -12, 0.8)
    play_flute(C, 0, 1.0); play_sitar(C, -12, 1.0); play_sitar(C, 0, 0.55)
    play_flute(D, 0, 0.9); play_sitar(D, -12, 0.8)

    # sitar chikari (sympathetic high strings) in the climax
    for k in range(80, 112, 4):
        add(S['sitar'], (k + 2) * mt, sitar(midi(74), 1.0, None, 0.4))
        add(S['sitar'], (k + 3) * mt, sitar(midi(81), 1.0, None, 0.3))

    # ---- tabla: keherwa  Dha Ge Na Ti | Na Ke Dhi Na
    kher = ['dha', 'ge', 'na', 'ti', 'na', 'ke', 'dhi', 'na']
    rv = lambda: rng.uniform(0.88, 1.08)
    jit = lambda: rng.uniform(-0.006, 0.006) * SR_M

    def hit(name, pos, vol):
        add(S['tabla'], pos + jit(), stroke(name) * vol * rv())

    for c in range(2, 15):
        base = c * cyc
        if c <= 5:
            pat = {0: 'dha', 2: 'na', 4: 'ke', 6: 'na'}; v = 0.55
        elif c <= 9:
            pat = {i: n for i, n in enumerate(kher)}; v = 0.85
        elif c <= 13:
            pat = {i: n for i, n in enumerate(kher)}; v = 1.0
        else:
            pat = {0: 'dha', 2: 'na', 4: 'tun'}; v = 0.7
        for i, name in pat.items():
            hit(name, base + i * mt, v * (1.2 if i == 0 else 1.0))
        if 6 <= c <= 13:                                              # ghost notes on the offbeats
            for i in (1, 3, 5, 7):
                hit('ti' if c < 10 else 'na', base + (i + 0.5) * mt, 0.28 * v)
        if c in (5, 9, 13):                                           # fill into the next section
            for pos, nm, vv in ((6, 'dha', 1), (6.5, 'ti', .6), (7, 'dha', 1), (7.25, 'ti', .6), (7.5, 'na', .9), (7.75, 'ke', .7)):
                hit(nm, base + pos * mt, vv * (0.9 if c < 13 else 1.1))
        if 10 <= c <= 13:                                             # dholak boom
            for i, vv in ((0, 1.0), (4, 0.8), (6, 0.5)):
                add(S['boom'], base + i * mt, boom(vv))

    # ---- manjira from the tabla's full entry onward
    for m in range(48, 120):
        hard = m >= 80
        add(S['manjira'], m * mt, manjira((0.9 if m % 2 == 0 else 0.6) * (1.0 if hard else 0.7)))
        if hard: add(S['manjira'], (m + 0.5) * mt, manjira(0.45))

    # ---- temple bells & conch
    for c, v in ((0, 0.8), (6, 0.5), (10, 0.9), (14, 0.7)):
        add(S['bells'], c * cyc, temple_bell(midi(74), 7.0, v))
    add(S['bells'], 10 * cyc, conch() * 0.9)
    for m in range(92, 112, 8):
        add(S['bells'], m * mt, temple_bell(midi(86), 3.0, 0.14))

    # ---- mix: (target loudness, pan, wet send, dry send)
    cfg = {'tanpura': (0.045, -0.25, 0.8, 0.5), 'pad': (0.035, 0.0, 0.7, 0.45), 'choir': (0.045, 0.0, 1.0, 0.3),
           'sitar': (0.075, -0.3, 0.55, 0.85), 'flute': (0.06, 0.3, 0.8, 0.6), 'tabla': (0.075, -0.1, 0.15, 1.0),
           'boom': (0.09, 0.0, 0.2, 1.0), 'manjira': (0.02, 0.55, 0.2, 0.7), 'bells': (0.05, 0.0, 1.0, 0.6)}
    dL = np.zeros(LOOP); dR = np.zeros(LOOP); wet = np.zeros(LOOP)
    for k, x in S.items():
        act = np.abs(x) > 1e-5
        rms = np.sqrt(np.mean(x[act] ** 2)) if act.any() else 1
        tgt, pan, w, d = cfg[k]
        x = x * (tgt / rms)
        gl, gr = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
        dL += x * d * gl; dR += x * d * gr; wet += x * w
    ir_a = reverb_ir(SR_M, 3.0, 0.5); ir_b = reverb_ir(SR_M, 3.0, 0.5)
    L = dL + fft_conv(wet, ir_a, circular=True) * 1.1
    R = dR + fft_conv(wet, ir_b, circular=True) * 1.1
    # master dynamics: hushed alap -> steady build -> climax -> settle (ends close to where it starts so it loops)
    cx = np.arange(LOOP) / (8 * mt)
    arc = np.interp(cx, [0, 2, 4, 6, 8, 10, 11, 13, 14, 15], [0.5, 0.68, 0.76, 0.86, 0.92, 1.0, 1.0, 1.0, 0.78, 0.52])
    st = np.stack([L * arc, R * arc], 1)
    st = st / np.percentile(np.abs(st), 99.7)
    st = np.tanh(st * 1.15)                                           # soft glue limiter
    save("music_devotional", st, SR_M)


# =========================================================== SFX
SR = 44100


def T(d): return np.arange(int(SR * d)) / SR


def env(t, a=0.003, d=3.0): return np.minimum(1, t / a) * np.exp(-t * d)


def chime(f, dur=1.4, vol=1.0, decay=3.2):
    t = T(dur)
    s = (np.sin(2 * np.pi * f * t) + 0.4 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 4)
         + 0.25 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 8)) * env(t, 0.002, decay)
    return s * vol * 0.3


def place(total, parts):
    out = np.zeros(int(SR * total))
    for start, sig in parts:
        i = int(start * SR); n = min(len(sig), len(out) - i)
        if n > 0: out[i:i + n] += sig[:n]
    return out


def wet_sfx(x, secs=1.2, mix=0.28):
    ir = reverb_ir(SR, secs, 0.5)
    y = fft_conv(x, ir)
    out = np.zeros(len(y)); out[:len(x)] += x * (1 - mix * 0.5)
    out += y * mix * 1.5
    return out


def make_sfx():
    D5, FS5, A5, B5, D6, E6, FS6, A6 = [midi(m) for m in (74, 78, 81, 83, 86, 88, 90, 93)]

    # collect a mango: soft rising sparkle
    save("pickup", wet_sfx(place(1.6, [(0, chime(A5, 1.2, 0.8)), (0.07, chime(D6, 1.2, 0.8)), (0.14, chime(FS6, 1.3, 0.7))]), 1.0, 0.25), SR)
    # basket / quest item: warm rising pentatonic with a bell underneath
    save("pickup_big", wet_sfx(place(2.6, [(0, temple_bell(midi(62), 2.4, 0.5, SR)), (0.0, chime(D5, 1.6, 0.8)), (0.1, chime(FS5, 1.6, 0.8)),
         (0.2, chime(A5, 1.6, 0.8)), (0.3, chime(B5, 1.8, 0.8)), (0.42, chime(D6, 2.0, 1.0))]), 1.6, 0.3), SR)
    # checkpoint
    save("checkpoint", wet_sfx(place(1.8, [(0, chime(FS5, 1.4, 0.8)), (0.12, chime(B5, 1.5, 0.9))]), 1.2, 0.3), SR)
    # baby monkey rescued: happy trill
    save("rescue", wet_sfx(place(1.8, [(i * 0.09, chime(f, 0.9, 0.6)) for i, f in enumerate([A5, B5, D6, E6, FS6])] + [(0.5, chime(A6, 1.2, 0.5))]), 1.0, 0.25), SR)

    # jump: airy whoosh with a gentle upward sine
    t = T(0.22)
    save("jump", (np.sin(2 * np.pi * np.cumsum(300 + 700 * t / 0.22) / SR) * 0.35 + lp(noise(len(t)), 0.3) * 0.15) * np.sin(np.pi * t / 0.22) ** 0.7, SR)
    # land: dull soft thud + dust
    t = T(0.16)
    save("land", (np.sin(2 * np.pi * np.cumsum(110 * np.exp(-t * 18) + 45) / SR) * 0.9 + lp(noise(len(t)), 0.15) * 0.5) * env(t, 0.002, 22), SR)
    # footstep on dirt
    t = T(0.09)
    save("step", lp(noise(len(t)), 0.12) * env(t, 0.002, 45) + 0.4 * np.sin(2 * np.pi * 85 * t) * env(t, 0.001, 50), SR)
    # click / UI
    t = T(0.09)
    save("click", (np.sin(2 * np.pi * 1000 * t) * 0.5 + np.sin(2 * np.pi * 1500 * t) * 0.25) * env(t, 0.001, 45), SR)
    # dialogue blip: soft wooden tick
    t = T(0.07)
    save("dialogue", np.sin(2 * np.pi * 520 * t) * env(t, 0.002, 55) * 0.5, SR)
    # lever: heavy wooden clunk + chain rattle
    t = T(0.7)
    clunk = np.sin(2 * np.pi * np.cumsum(140 * np.exp(-t * 10) + 60) / SR) * env(t, 0.002, 9)
    rattle = lp(noise(len(t)), 0.5) * np.where((t > 0.2) & (t < 0.5), np.abs(np.sin(t * 150)), 0) * env(t - 0.2 + 0.2, 0.01, 4) * 0.4
    save("lever", clunk + rattle, SR)
    # splash
    t = T(0.8)
    sp = lp(noise(len(t)), 0.5) * env(t, 0.01, 6) + lp(noise(len(t)), 0.08) * env(t, 0.05, 3) * 0.6
    save("splash", sp, SR)
    # bird chirp: two rising tweets
    t = T(0.3)
    def tweet(t0, f0, f1):
        tt = t - t0; m = (tt > 0) & (tt < 0.09)
        ph = 2 * np.pi * np.cumsum(np.where(m, f0 + (f1 - f0) * np.clip(tt / 0.09, 0, 1), 0)) / SR
        return np.sin(ph) * np.where(m, np.sin(np.pi * np.clip(tt / 0.09, 0, 1)), 0)
    save("chirp", (tweet(0.0, 2600, 3600) + tweet(0.12, 3000, 4200) * 0.8) * 0.5, SR)
    # portal: mystical rising shimmer with a bell tone
    t = T(1.6)
    sh = sum(np.sin(2 * np.pi * np.cumsum(f * (0.6 + 0.9 * t / 1.6)) / SR) for f in (220, 330, 440)) * 0.12
    sh = sh * np.sin(np.pi * np.clip(t / 1.6, 0, 1)) ** 0.8
    sh += chime(D6, 1.6, 0.8) * np.clip(t / 0.6, 0, 1)
    save("portal", wet_sfx(sh, 1.6, 0.35), SR)

    # big temple bell (ending: "the temple bell rings across the forest")
    bell = temple_bell(midi(55), 8.0, 1.0, SR)
    save("temple_bell", fft_conv(bell, reverb_ir(SR, 2.5, 0.5))[:len(bell)] * 0.6 + bell, SR)

    # conch (shankh): rich buzzing swell
    t = T(3.0)
    f = 233 * (0.97 + 0.03 * np.minimum(1, t / 0.4))
    ph = 2 * np.pi * np.cumsum(f * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) / SR
    s = sum(np.sin(h * ph) / h ** 0.8 for h in range(1, 14))
    s = lp(s, 0.35) * np.minimum(1, t / 0.35) * np.clip((3.0 - t) / 1.2, 0, 1) ** 1.2
    save("conch", fft_conv(s, reverb_ir(SR, 1.8, 0.5))[:len(s)] * 0.35 + s, SR)

    # LEVEL COMPLETE (the temple bell is played separately just before): conch -> ascending pentatonic chimes -> warm pad chord
    conch = np.asarray(wave_read("conch"))
    bell = temple_bell(midi(55), 7.0, 1.0, SR)
    ch = [(2.2 + i * 0.16, chime(m, 3.0, 0.9, 1.6)) for i, m in enumerate([midi(62), midi(66), midi(69), midi(71), midi(74), midi(78), midi(81), midi(86)])]
    tt = T(7.5)
    pad = sum(np.sin(2 * np.pi * midi(m) * tt) + 0.3 * np.sin(2 * np.pi * midi(m) * 2 * tt) for m in (50, 57, 62, 66, 69)) * 0.05
    pad *= np.minimum(1, tt / 1.5) * np.clip((7.5 - tt) / 2.5, 0, 1)
    comp = place(9.5, [(0, conch * 0.8)] + ch + [(2.2, pad)])
    save("complete", wet_sfx(comp, 2.2, 0.3), SR)


def wave_read(name):
    with wave.open(os.path.join(OUT, name + ".wav")) as w:
        d = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float64) / 32767
    return d


if __name__ == "__main__":
    make_sfx()
    make_music()
