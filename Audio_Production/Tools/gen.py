import sys; sys.path.insert(0, '.')
from wav import *
exec(open('cut.py').read().split('# One-shots')[0])
rng = np.random.default_rng(20261123)
S = ROOT + "Audio_Production/Source_SFX/"
RD = ROOT + "Audio_Production/Renders/"

# ---------- DSP helpers ----------
def stft_shape(x, gain_fn, n=2048, hop=512):
    """Filter a signal with a gain that may change over time: gain_fn(freqs, t_seconds) -> gains."""
    x = np.asarray(x, float); w = np.hanning(n); pad = np.concatenate([np.zeros(n), x, np.zeros(n)])
    out = np.zeros_like(pad); norm = np.zeros_like(pad); f = np.fft.rfftfreq(n, 1 / SR)
    for s in range(0, len(pad) - n, hop):
        X = np.fft.rfft(pad[s:s + n] * w) * gain_fn(f, (s - n + n / 2) / SR)
        out[s:s + n] += np.fft.irfft(X, n) * w; norm[s:s + n] += w * w
    return (out / np.maximum(norm, 1e-6))[n:n + len(x)]

def static_filter(x, gain):  # gain(freqs) -> gains, whole-signal FFT (fine for loops)
    X = np.fft.rfft(x, axis=0); f = np.fft.rfftfreq(len(x), 1 / SR)
    g = gain(f); return np.fft.irfft(X * (g[:, None] if X.ndim > 1 else g), len(x), axis=0)

def lp(f, fc, order=2): return 1 / np.sqrt(1 + (f / fc) ** (2 * order))
def hp(f, fc, order=2): return 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order))
def bump(f, fc, q, gdb): return 1 + (10 ** (gdb / 20) - 1) * np.exp(-0.5 * (np.log2(np.maximum(f, 1) / fc) * q) ** 2)
def pink(n): return static_filter(rng.standard_normal(n), lambda f: 1 / np.sqrt(np.maximum(f, 20)))
def norm_rms(x, d): return x * (10 ** (d / 20) / max(rms(x), 1e-9))
def smooth_rand(n, rate_hz, lo, hi):
    """A smooth random curve between lo and hi (cubic through random points every 1/rate seconds)."""
    k = max(4, int(n / SR * rate_hz) + 4); pts = rng.uniform(lo, hi, k)
    xs = np.linspace(0, n, k); t = np.arange(n)
    y = np.interp(t, xs, pts); w = max(1, int(SR / rate_hz / 2))
    c = np.cumsum(np.concatenate([np.full(w, y[0]), y, np.full(w, y[-1])]))
    return ((c[w:] - c[:-w]) / w)[w // 2: w // 2 + n]

def conv(x, ir):
    n = len(x) + len(ir) - 1; N = 1 << (n - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:n]

def room_ir(rt60, length=None, damp=6000, pre=0.012, stereo=False):
    length = length or rt60 * 1.2; m = int(length * SR); t = np.arange(m) / SR
    def one():
        e = rng.standard_normal(m) * np.exp(-6.91 * t / rt60)
        e = stft_shape(e, lambda f, tt: lp(f, damp * np.exp(-tt / rt60 * 1.2) + 800, 1))
        e[: int(pre * SR)] = 0; return e / np.sqrt(np.sum(e ** 2))
    return np.stack([one(), one()], 1) if stereo else one()

def reverb(x, rt60, wet_db, damp=6000):
    ir = room_ir(rt60, damp=damp); y = conv(x, ir)[: len(x) + len(ir) - 1]
    out = np.concatenate([x, np.zeros(len(y) - len(x))]) + y * 10 ** (wet_db / 20)
    return out

def resample(x, ratio):  # play at `ratio` speed (pitch up > 1)
    n = int(len(x) / ratio); return np.interp(np.arange(n) * ratio, np.arange(len(x)), x)

def src(path, t0=0, t1=None, mono=True):
    x = read(path); x = x[int(t0 * SR): int(t1 * SR) if t1 else None]
    return x.mean(1) if mono else x

def put(buf, x, at):
    i = int(at * SR); j = min(len(buf), i + len(x)); buf[i:j] += x[: j - i]

def save(x, path, peak=-3.0, rms_db=None):
    x = np.asarray(x, float)
    if rms_db is not None: x = norm_rms(x, rms_db)
    p = np.abs(x).max()
    if p > 10 ** (peak / 20): x *= 10 ** (peak / 20) / p
    write(A + path, x); print(f"{path}: {len(x)/SR:.2f}s rms {db(rms(x)):.1f} peak {db(np.abs(x).max()):.1f}")

def loop_stereo(make, seconds, xf=3.0):
    """make(n) -> (n,2); the tail is crossfaded into the head, so the file loops without a seam."""
    return loopxf(make(int((seconds + xf) * SR)), xf)

def loop_circ(make, seconds, extra=3.0):
    """Events with reverb tails: rendered longer, and what rings past the loop point is folded onto its start."""
    L = int(seconds * SR); y = make(L + int(extra * SR)); out = y[:L].copy(); out[: len(y) - L] += y[L:]; return out

def loop_mono(make, seconds, xf=1.0):
    return loopxf(make(int((seconds + xf) * SR))[:, None], xf)[:, 0]

# ---------- the wind (two bands of filtered noise with slow gusts) ----------
def wind(n, base=380, spread=0.6, gust=(0.25, 1.0), rate=0.18, howl=0.0, hiss=0.15):
    g = smooth_rand(n, rate, *gust)                       # gust strength
    fc = base * 2 ** (smooth_rand(n, rate * 0.8, -spread, spread))
    gs, fs = g[::512], fc[::512]
    def body(f, t):
        i = min(len(gs) - 1, max(0, int(t * SR / 512)))
        c = fs[i] * (0.7 + 0.6 * gs[i])
        bandg = np.exp(-0.5 * (np.log2(np.maximum(f, 1) / c) * 1.1) ** 2)
        res = howl * (np.exp(-0.5 * (np.log2(np.maximum(f, 1) / (c * 1.6)) * 14) ** 2) + 0.4 * np.exp(-0.5 * (np.log2(np.maximum(f, 1) / (c * 2.4)) * 14) ** 2))
        return (bandg + res + hiss * hp(f, 2500, 1) * gs[i] + 0.5 * lp(f, 110, 2)) * gs[i] * lp(f, 3500, 2)
    return stft_shape(pink(n) * 30, body)

def hum(n, f0=100, amp=1.0, top=3000, rough=1.0):
    t = np.arange(n) / SR; y = np.zeros(n)
    for k in range(1, int(top / f0) + 1):
        y += (1 / k ** (1.3 / rough)) * np.sin(2 * np.pi * f0 * k * t + rng.uniform(0, 6.28))
    return y * amp

E = A + "Ambience/AMB_Exterior_Dusk.wav"
def exterior(n, offset):
    x = read(E); x = np.concatenate([x, x])[int(offset * SR): int(offset * SR) + n]; return x

# ---------- ambience beds ----------
def l1(n):  # ventilation and fluorescent buzz, the outside faintly through the entrance
    air = np.stack([stft_shape(pink(n), lambda f, t: lp(f, 900, 2) * bump(f, 140, 2, 6)) for _ in range(2)], 1)
    air = norm_rms(air, -36) * smooth_rand(n, 0.08, 0.85, 1.1)[:, None]
    buzz = hum(n, 100, 1, 2500) * (1 + 0.04 * np.sin(2 * np.pi * 0.37 * np.arange(n) / SR))
    buzz = norm_rms(static_filter(buzz, lambda f: hp(f, 150, 2) * lp(f, 2000, 2)), -52)
    out = static_filter(exterior(n, 20), lambda f: lp(f, 900, 4))
    return air + buzz[:, None] * [0.9, 1.0] + norm_rms(out, -64)

def l2(n):  # big hall: darker air, the odd metal tick, a radio far off
    air = np.stack([stft_shape(pink(n), lambda f, t: lp(f, 600, 2) * bump(f, 90, 1.5, 6)) for _ in range(2)], 1)
    air = norm_rms(air, -35) * smooth_rand(n, 0.06, 0.8, 1.1)[:, None]
    dry = np.zeros((n, 2)); click = src(S + "SFX_EmptyGun.wav"); step = src(A + "SFX/FS_Metal_02.wav")
    t = 2.0
    while t < n / SR - 3:
        c = resample(click if rng.random() < 0.6 else step, rng.uniform(0.45, 0.8))
        c = static_filter(c, lambda f: lp(f, 2500, 2)) * 10 ** (rng.uniform(-30, -22) / 20) / max(np.abs(c).max(), 1e-6)
        pan = rng.uniform(0.15, 0.85); put(dry[:, 0], c * np.sqrt(1 - pan), t); put(dry[:, 1], c * np.sqrt(pan), t)
        t += rng.uniform(3.0, 8.0)
    radio = src(S + "SFX_RadioCommunication.wav", 0, 37.4)
    radio = static_filter(radio, lambda f: hp(f, 400, 2) * lp(f, 2200, 2))
    radio = np.tile(radio, n // len(radio) + 1)[:n] * 10 ** (-38 / 20) / max(rms(radio), 1e-9)
    dry[:, 0] += radio * 0.8; dry[:, 1] += radio * 0.45
    ir = room_ir(2.4, damp=4000, stereo=True)
    wet = np.stack([conv(dry[:, 0], ir[:, 0])[:n], conv(dry[:, 1], ir[:, 1])[:n]], 1)
    return air + dry * 0.35 + wet * 0.9

def l3(n):  # wind through the breach, the structure groaning, the evening outside
    w = np.stack([wind(n, 330, 0.5, (0.15, 0.9), 0.15, howl=0.05) for _ in range(2)], 1)
    w = norm_rms(w, -31)
    air = norm_rms(np.stack([stft_shape(pink(n), lambda f, t: lp(f, 700, 2)) for _ in range(2)], 1), -40)
    gate = src(RD + "GATE_Opening.wav", 81.9, 82.9)
    groan = static_filter(resample(gate, 0.42), lambda f: lp(f, 900, 2) * hp(f, 60, 2))
    groan = fade(groan[:, None], 0.3, 0.8)[:, 0]
    dry = np.zeros((n, 2))
    for at, pan in [(9.0, 0.3), (31.0, 0.75)]:
        g = groan * 10 ** (-30 / 20) / max(np.abs(groan).max(), 1e-6)
        put(dry[:, 0], g * np.sqrt(1 - pan), at); put(dry[:, 1], g * np.sqrt(pan), at)
    ir = room_ir(1.8, damp=3000, stereo=True)
    dry = dry + np.stack([conv(dry[:, 0], ir[:, 0])[:n], conv(dry[:, 1], ir[:, 1])[:n]], 1) * 0.7
    return w + air + dry + exterior(n, 60) * 10 ** (8 / 20)

def helipad(n):  # strong open wind on top of the tower
    w = np.stack([wind(n, 420, 0.7, (0.2, 1.0), 0.22, howl=0.08, hiss=0.3) for _ in range(2)], 1)
    w = norm_rms(w, -27)
    return w + exterior(n, 100) * 10 ** (10 / 20)

save(loop_stereo(l1, 45), "Ambience/AMB_L1_Interior.wav", peak=-6)
save(loop_stereo(l2, 50), "Ambience/AMB_L2_Armory.wav", peak=-6)
save(loop_stereo(l3, 50), "Ambience/AMB_L3_Destroyed.wav", peak=-6)
save(loop_stereo(helipad, 50), "Ambience/AMB_Helipad.wav", peak=-4)

# ---------- level 3 debris (one-shots, mono, 3D) ----------
grains = [src(A + f"SFX/FS_Dirt_0{i}.wav") for i in range(1, 5)]
def trickle(count, start, spread, ratio=(1.6, 2.6)):
    y = np.zeros(int((start + spread + 0.6) * SR)); t = start
    for k in range(count):
        g = grains[rng.integers(4)]; i = int(np.abs(g).argmax()); g = g[max(0, i - 40): i + int(0.05 * SR)]
        g = resample(g, rng.uniform(*ratio)); g = fade(g[:, None], 0.001, 0.02)[:, 0]
        put(y, g * 10 ** (-k * 1.6 / 20 + rng.uniform(-4, 0) / 20) / max(np.abs(g).max(), 1e-6), t)
        t += spread / count * rng.uniform(0.3, 1.7) * (1 + k / count)
    return y
heavy = src(S + "SFX_HeavyObjectFalling.wav", 0, 1.4); fall = src(S + "SFX_FallingDown.wav", 1.95, 2.8)
def debris(core, ratio, count, spread):
    c = resample(core, ratio) if core is not None else np.zeros(1)
    c = static_filter(c, lambda f: lp(f, 5000, 2) * hp(f, 80, 2))
    tr = trickle(count, 0.12 if core is not None else 0.0, spread)
    y = np.zeros(max(len(c), len(tr))); y[: len(c)] += c / max(np.abs(c).max(), 1e-6); y[: len(tr)] += tr * 0.55
    dust = stft_shape(pink(len(y)), lambda f, t: lp(f, 1800, 1) * np.exp(-t * 2.5)); y += norm_rms(dust, db(rms(y)) - 16)
    y = reverb(y, 1.2, -14, 3500); return fade(y[:, None], 0.002, 0.25)[:, 0]
save(debris(heavy, 1.7, 9, 0.9), "Ambience/AMB_Debris_01.wav", peak=-4)
save(debris(None, 1, 14, 1.3), "Ambience/AMB_Debris_02.wav", peak=-6)
save(debris(fall, 1.35, 7, 0.7), "Ambience/AMB_Debris_03.wav", peak=-4)

# ---------- guide loops (mono, 3D, 0.8 volume, heard from ~30 m) ----------
def speaker(x, lo=350, hi=3500, drive=2.2, res=1600):
    y = static_filter(x, lambda f: hp(f, lo, 3) * lp(f, hi, 3) * bump(f, res, 1.5, 5))
    y = np.tanh(drive * y / max(np.abs(y).max(), 1e-6)) / np.tanh(drive)
    return static_filter(y, lambda f: hp(f, lo, 2) * lp(f, hi * 1.2, 2))

def chime(freqs=(659.3, 523.3), each=0.55):
    y = np.zeros(int((each * len(freqs) + 1.2) * SR)); t = np.arange(int(1.6 * SR)) / SR
    for i, f0 in enumerate(freqs):
        tone = sum(a * np.sin(2 * np.pi * f0 * m * t) * np.exp(-t * d) for m, a, d in [(1, 1, 2.2), (2, 0.35, 3.5), (3, 0.12, 5), (4.2, 0.05, 7)])
        put(y, tone * np.minimum(1, t / 0.004), i * each)
    return y

def pa(n):  # chime, the lockdown call through a tannoy, a slap off the hallway walls
    y = np.zeros(n); c = chime(); put(y, c / np.abs(c).max() * 0.6, 0.2)
    v = read("pa_voice.wav").mean(1)
    idx = np.where(np.abs(v) > 0.01)[0]; v = v[idx[0]: idx[-1] + 2000]
    put(y, v / np.abs(v).max(), 1.55)
    y = speaker(y)
    crackle = np.zeros(n); k = rng.integers(0, n, 40); crackle[k] = rng.uniform(-1, 1, 40)
    crackle = static_filter(crackle, lambda f: hp(f, 1500, 2) * lp(f, 6000, 2))
    y += crackle / max(np.abs(crackle).max(), 1e-6) * 0.08 * (np.abs(y) > 0.02)
    slap = np.zeros(n); put(slap, y, 0.11); y = y + 0.32 * slap
    return reverb(y, 1.4, -10, 4000)[:n]
vlen = len(read("pa_voice.wav")) / SR
save(loop_circ(pa, round(vlen + 3.6, 1)), "SFX/GUIDE_PA.wav", peak=-3)

def stairs(n):  # a draft rising and falling up the stairwell, a vent flap ticking at each swell, a low motor hum
    t = np.arange(n) / SR; swell = 0.45 + 0.55 * np.sin(np.pi * t / 4.0) ** 2   # two swells in 8 s
    air = stft_shape(pink(n), lambda f, tt: (lp(f, 700 + 900 * (0.45 + 0.55 * np.sin(np.pi * tt / 4.0) ** 2), 2) * hp(f, 120, 2)))
    air = norm_rms(air, -24) * swell
    motor = norm_rms(static_filter(hum(n, 50, 1, 600, 0.8), lambda f: hp(f, 45, 2)), -36)
    flap = src(A + "SFX/FS_Metal_03.wav"); flap = static_filter(resample(flap, 1.9), lambda f: hp(f, 500, 2))
    flap = flap / np.abs(flap).max() * 10 ** (-17 / 20)
    y = air + motor
    for at in [1.7, 2.15, 5.7, 6.15]: put(y, flap * rng.uniform(0.7, 1.0), at)
    return reverb(y, 1.6, -9, 3500)[:n]
save(loop_mono(stairs, 8.0, 1.0), "SFX/GUIDE_Stairs.wav", peak=-4)

def phone(n):  # an office desk phone: double ring (0.4 on, 0.2 off, 0.4 on), 2 s rest, twice
    t = np.arange(n) / SR; tone = np.zeros(n)
    trill = np.sign(np.sin(2 * np.pi * 20 * t))           # 20 Hz warble between the two tones
    f = np.where(trill > 0, 1150.0, 1450.0); ph = 2 * np.pi * np.cumsum(f) / SR
    wave = np.sin(ph) + 0.3 * np.sin(3 * ph) + 0.12 * np.sin(5 * ph)
    gate = np.zeros(n)
    for c in range(2):
        for on in (0.0, 0.6):
            a = int((c * 3.0 + on) * SR); b = a + int(0.4 * SR); gate[a:b] = 1
    gate = np.convolve(gate, np.ones(int(0.006 * SR)) / int(0.006 * SR), 'same')
    y = speaker(wave * gate, 500, 4200, 1.6, 2200)
    return reverb(y, 0.5, -9, 5000)[:n]
save(loop_circ(phone, 6.0), "SFX/GUIDE_Phone.wav", peak=-3)

def keyslot(n):  # the slot waiting for its key: a rising double chirp every 2 s over the reader's faint hum
    y = np.zeros(n)
    def blip(f0, f1, dur=0.06):
        tt = np.arange(int(dur * SR)) / SR; ph = 2 * np.pi * np.cumsum(np.linspace(f0, f1, len(tt))) / SR
        w = np.sin(ph) + 0.25 * np.sin(3 * ph); env = np.minimum(1, tt / 0.003) * np.exp(-tt * 18)
        return w * env
    for k in range(int(n / SR / 2)):
        put(y, blip(1800, 2050) * 0.5, k * 2.0 + 0.1); put(y, blip(2400, 2700) * 0.5, k * 2.0 + 0.2)
    y = speaker(y, 300, 6000, 1.3, 2400)
    return reverb(y, 0.7, -12, 6000)[:n]
L4 = 4 * SR; ks = loop_circ(keyslot, 4.0)
ks += speaker(norm_rms(static_filter(hum(L4, 50, 1, 1200), lambda f: hp(f, 90, 2)), -42) * 20, 300, 6000, 1.0, 2400) * 0.05
save(ks, "SFX/KEYSLOT_Beep.wav", peak=-4)

def breach(n):  # the wind howling through the breach: a resonant whistle over the gusts
    w = wind(n, 470, 0.35, (0.35, 1.0), 0.3, howl=4.0, hiss=0.05)
    return norm_rms(static_filter(w, lambda f: hp(f, 120, 2)), -20)
save(loop_mono(breach, 12.0, 1.5), "SFX/GUIDE_Wind.wav", peak=-3)

def heliradio(n):  # the pilot on the radio, the rotor behind his mic, squelch on each key-up
    y = np.zeros(n); radio = SFX = S + "SFX_RadioCommunication.wav"
    rotor = src(RD + "HELICOPTER_SFX.wav", 86, 100)
    def transmission(t0, t1):
        v = src(radio, t0, t1); r = rotor[: len(v)]
        mix = v / max(rms(v), 1e-9) + 0.35 * r / max(rms(r), 1e-9)
        sq = stft_shape(rng.standard_normal(int(0.09 * SR)), lambda f, t: hp(f, 800, 1) * np.exp(-t * 30))
        body = np.concatenate([sq * 0.9, mix * 0.12, sq[: int(0.05 * SR)] * 0.7])
        return speaker(body, 400, 2800, 3.0, 1300)
    a = transmission(0.0, 2.6); b = transmission(28.0, 32.4)
    put(y, a, 0.15); put(y, b, 0.15 + len(a) / SR + 1.1)
    return y
la = 2.6 + 0.14; lb = 4.4 + 0.14
save(loop_circ(heliradio, round(0.15 + la + 1.1 + lb + 1.4, 2)), "SFX/GUIDE_HeliRadio.wav", peak=-3)
