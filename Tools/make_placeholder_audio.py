"""Placeholder audio for Wulang, synthesised so the game has music, ambience and effects
until real recordings replace them. Every file lands in Assets/Resources/Audio, where
GameAudio loads it by name: drop a real clip over a file with the same name to replace it.

Music loops are rendered with wrap-around note tails and circular reverb, so they loop
without a seam. The rain song (Nan's song in the story) is the shared melody.

Run:  python Tools/make_placeholder_audio.py   (needs numpy and scipy)
"""
import os, wave
import numpy as np
from scipy import signal

SR = 22050
ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio")
rng = np.random.default_rng(7)


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def write(name, x, peak=0.8):
    path = os.path.join(ROOT, name + ".wav")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print(f"{name}: {len(x) / SR:.1f}s")


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], "band")
    return signal.lfilter(b, a, x)


def reverb(x, seconds=2.5, wet=0.35, circular=True, bright=3000):
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-6.9 * np.arange(n) / n)
    ir = lowpass(ir, bright)
    ir /= np.sqrt(np.sum(ir ** 2))
    if circular:  # wrap the tail onto the start: the loop stays seamless
        L = len(x)
        tail = np.zeros(L)
        tail[: min(n, L)] += ir[: min(n, L)]
        y = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(tail)))
    else:
        y = signal.fftconvolve(x, ir)[: len(x) + n]
        x = np.pad(x, (0, len(y) - len(x)))
    return (1 - wet) * x + wet * y


def env(n, attack, release):
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a) if a < n else np.linspace(0, 1, n)[:n]
    if r < n:
        e[-r:] *= np.linspace(1, 0, r)
    return e


def add(buf, start, sound):
    """Mix sound into buf at a sample offset, wrapping past the end (loopable)."""
    L = len(buf)
    start %= L
    end = start + len(sound)
    if end <= L:
        buf[start:end] += sound
    else:
        k = L - start
        buf[start:] += sound[:k]
        rest = sound[k:]
        while len(rest):
            m = min(L, len(rest))
            buf[:m] += rest[:m]
            rest = rest[m:]


# ---------- instruments ----------

def pad(midis, dur, bright=1400, detune=0.12, attack=1.2, release=1.5):
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for m in midis:
        for d in (-detune, 0, detune):
            f = hz(m + d)
            for k in range(1, 7):  # a soft sawtooth
                x += np.sin(2 * np.pi * f * k * t + rng.uniform(0, 6.28)) / k
    x = lowpass(x, bright)
    return x * env(n, attack, release) / (len(midis) * 3)


def bell(m, dur=2.5, gain=1.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = hz(m)
    x = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.2)
         + 0.45 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 4.5)
         + 0.2 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 8))
    return x * env(n, 0.004, 0.2) * gain


def pluck(m, dur=1.2, gain=1.0, bright=3500):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = hz(m)
    x = sum(np.sin(2 * np.pi * f * k * t) / k ** 1.3 * np.exp(-t * (2.5 + k)) for k in range(1, 8))
    return lowpass(x, bright) * env(n, 0.003, 0.15) * gain


def bass(m, dur, gain=1.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = hz(m)
    x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t)
    return x * env(n, 0.01, 0.1) * gain


def drum(low=55, dur=0.6, gain=1.0, noise=0.3):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = low * (1 + 1.5 * np.exp(-t * 30))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
    x += noise * lowpass(rng.standard_normal(n), 1800) * np.exp(-t * 25)
    return x * gain


def hat(dur=0.08, gain=0.3):
    n = int(dur * SR)
    return highpass(rng.standard_normal(n), 6000) * np.exp(-np.arange(n) / SR * 60) * gain


# ---------- the rain song (Nan's song): scale degrees in D major, beats ----------
# "Rain on the roof, rain on the hill, the kettle's warm and the world is still"
RAIN_SONG = [(7, 1), (7, .5), (9, .5), (7, 2), (4, 1), (7, .5), (4, .5), (2, 2),
             (0, 1), (2, .5), (4, .5), (7, 2), (4, .5), (2, .5), (0, .5), (2, .5), (0, 3)]


def song(buf, root, bpm, start_beat, instrument, gain=1.0, wobble=0.0):
    beat = 60 / bpm
    b = start_beat
    for degree, length in RAIN_SONG:
        drift = wobble * np.sin(b * 1.7)  # fake memories go slightly out of tune
        add(buf, int(b * beat * SR), instrument(root + degree + drift, length * beat + 1.2) * gain)
        b += length
    return b


# ---------- music ----------

def track(bars, bpm, beats_per_bar=4):
    return np.zeros(int(bars * beats_per_bar * 60 / bpm * SR)), 60 / bpm


def chords(buf, beat, progression, beats_each, octave_shift=0, bright=1400, gain=1.0):
    for i, ch in enumerate(progression):
        add(buf, int(i * beats_each * beat * SR), pad([m + octave_shift for m in ch], beats_each * beat + 1.5, bright) * gain)


def music_menu():
    buf, beat = track(16, 70)
    D, G, A, Bm = [50, 57, 62, 66], [43, 55, 59, 62], [45, 57, 61, 64], [47, 54, 59, 62]
    chords(buf, beat, [D, G, Bm, A] * 4, 4, gain=0.9)
    song(buf, 74, 70, 0, bell, 0.5)
    song(buf, 74, 70, 32, bell, 0.45)
    write("Music/Menu", reverb(buf, 3.5, 0.45), 0.7)


def music_forest():
    buf, beat = track(16, 84)
    G, Em, C, D = [43, 55, 59, 62], [40, 52, 55, 59], [36, 52, 55, 60], [38, 54, 57, 62]
    prog = [G, Em, C, D] * 4
    chords(buf, beat, prog, 4, bright=1800, gain=0.7)
    for i, ch in enumerate(prog):  # a walking plucked arpeggio
        for k in range(8):
            add(buf, int((i * 4 + k * 0.5) * beat * SR), pluck(ch[1 + k % 3] + 12, 1.0, 0.25))
    write("Music/Forest", reverb(buf, 2.2, 0.3), 0.7)


def music_ice():
    buf, beat = track(12, 60)
    Em, C, Am, B = [40, 52, 59, 64], [36, 52, 55, 64], [33, 52, 57, 60], [35, 54, 59, 63]
    chords(buf, beat, [Em, C, Am, B] * 3, 4, bright=2600, gain=0.6)
    notes = [76, 79, 83, 81, 78, 76, 74, 71]
    for i in range(24):
        add(buf, int(i * 2 * beat * SR + rng.integers(0, int(0.3 * SR))), bell(notes[(i * 3) % len(notes)], 3.5, 0.35))
    write("Music/Ice", reverb(buf, 4.5, 0.5, bright=6000), 0.65)


def music_volcano():
    buf, beat = track(16, 76)
    for b in range(64):
        add(buf, int(b * beat * SR), drum(45, 0.8, 0.9 if b % 4 == 0 else 0.45))
        if b % 8 == 6:
            add(buf, int((b + 0.5) * beat * SR), drum(60, 0.5, 0.5))
    for i, root in enumerate([38, 38, 39, 38] * 4):  # phrygian drone
        add(buf, int(i * 4 * beat * SR), pad([root, root + 7, root + 13 if i % 4 == 2 else root + 12], 4 * beat + 1.5, 700) * 0.9)
    write("Music/Volcano", reverb(buf, 2.5, 0.25), 0.75)


def music_sanctum():
    buf, beat = track(12, 58)
    Cm, Ab, Fm, G = [36, 51, 55, 60, 67], [32, 51, 56, 60, 68], [29, 48, 56, 60, 65], [31, 50, 55, 59, 67]
    chords(buf, beat, [Cm, Ab, Fm, G] * 3, 4, bright=2200, gain=0.8)
    song(buf, 62, 58, 8, lambda m, d: bell(m, d, 0.6), 0.35, wobble=0.35)  # the song, wrong
    write("Music/FinalArena", reverb(buf, 4, 0.45), 0.7)


def music_boss():
    buf, beat = track(16, 128)
    riff = [45, 45, 48, 45, 50, 45, 52, 51]
    for b in range(64):
        add(buf, int(b * beat * SR), drum(50, 0.45, 1.0 if b % 2 == 0 else 0.6))
        add(buf, int((b + 0.5) * beat * SR), hat())
        add(buf, int(b * beat * SR), bass(riff[b % 8] - 12, beat * 0.9, 0.6))
    Am, F, Dm, E = [45, 57, 60, 64], [41, 57, 60, 65], [38, 57, 62, 65], [40, 56, 59, 64]
    chords(buf, beat, [Am, F, Dm, E] * 4, 4, bright=2600, gain=0.55)
    write("Music/Boss", reverb(buf, 1.6, 0.2), 0.8)


def music_memory():
    buf, beat = track(10, 64)
    D, Bm, G, A = [50, 57, 62, 66], [47, 54, 59, 62], [43, 55, 59, 62], [45, 57, 61, 64]
    chords(buf, beat, [D, Bm, G, A] * 2 + [D, A], 4, bright=1100, gain=0.6)
    song(buf, 74, 64, 4, lambda m, d: bell(m, d, 0.7), 0.6, wobble=0.25)
    write("Music/Memory", reverb(buf, 4, 0.55), 0.6)


def music_truth():
    buf, beat = track(16, 66)
    D, G, A, Bm, Em = [50, 57, 62, 66], [43, 55, 59, 62], [45, 57, 61, 64], [47, 54, 59, 62], [40, 55, 59, 64]
    chords(buf, beat, [D, G, Em, A, D, Bm, G, A] * 2, 4, bright=2000, gain=0.85)
    song(buf, 74, 66, 0, lambda m, d: pluck(m, d, 0.9, 5000), 0.6)
    song(buf, 74, 66, 32, lambda m, d: bell(m, d), 0.5)
    write("Music/Truth", reverb(buf, 3, 0.35), 0.7)


def music_gilded():
    buf, beat = track(12, 60)
    Dm, Bb, Gm, A = [50, 57, 62, 65], [46, 58, 62, 65], [43, 55, 58, 62], [45, 57, 61, 64]
    chords(buf, beat, [Dm, Bb, Gm, A] * 3, 4, bright=900, gain=0.8)
    song(buf, 74, 60, 4, lambda m, d: bell(m - 0.3, d, 0.6), 0.4, wobble=0.5)
    write("Music/Gilded", reverb(buf, 5, 0.6), 0.6)


# ---------- ambience (loops) ----------

def loop_noise(seconds):
    n = int(seconds * SR)
    x = rng.standard_normal(n + SR)
    fade = np.linspace(0, 1, SR)
    x[:SR] = x[:SR] * fade + x[n:] * (1 - fade)  # crossfade the end into the start
    return x[:n]


def lfo(n, rate, depth, phase=0.0):
    return 1 - depth + depth * (0.5 + 0.5 * np.sin(2 * np.pi * rate * np.arange(n) / SR + phase))


# The beds sit quietly under the music: soft, low-passed air with slow swells and nothing sharp in it.
# (The first versions had a resonant wind howl in the Frozen Reach and a hiss of lava crackle that grated.)

def ambience_forest():
    n = int(40 * SR)
    wind = lowpass(loop_noise(40), 350, 3) * lfo(n, 0.05, 0.5) * 0.6
    leaves = bandpass(loop_noise(40), 1500, 3500) * lfo(n, 0.1, 0.8, 1.0) * 0.03
    buf = wind + leaves
    for _ in range(14):  # distant birdsong: a few soft chirps
        f0 = rng.uniform(2200, 3400)
        start = int(rng.uniform(0, 39) * SR)
        for c in range(rng.integers(2, 4)):
            m = int(0.11 * SR)
            t = np.arange(m) / SR
            f = f0 * (1 + 0.18 * np.sin(np.pi * t / 0.11))
            add(buf, start + c * int(0.16 * SR), np.sin(2 * np.pi * np.cumsum(f) / SR) * np.hanning(m) * 0.07)
    write("Ambience/Forest", lowpass(buf, 5000), 0.3)


def ambience_ice():
    n = int(40 * SR)
    # A hush of snow wind: broad, low and slowly breathing, no whistle.
    breath = lowpass(loop_noise(40), 420, 3) * lfo(n, 0.025, 0.6) * 0.7
    air = bandpass(loop_noise(40), 600, 1800) * lfo(n, 0.05, 0.8, 2.0) * 0.08
    write("Ambience/Ice", lowpass(breath + air, 2500), 0.3)


def ambience_volcano():
    n = int(40 * SR)
    # A deep, slow rumble with an occasional soft pop: heat felt more than heard.
    rumble = lowpass(loop_noise(40), 80, 4) * 6 * lfo(n, 0.025, 0.4)
    buf = rumble + lowpass(loop_noise(40), 250) * 0.12
    for _ in range(30):
        m = int(rng.uniform(0.02, 0.06) * SR)
        add(buf, int(rng.uniform(0, 39.5) * SR), lowpass(rng.standard_normal(m), 900) * np.exp(-np.arange(m) / m * 4) * rng.uniform(0.05, 0.15))
    write("Ambience/Volcano", buf, 0.32)


def ambience_sanctum():
    n = int(40 * SR)
    air = bandpass(loop_noise(40), 700, 2200) * lfo(n, 0.05, 0.7) * 0.12
    t = np.arange(n) / SR
    hum = sum(np.sin(2 * np.pi * f * t) * lfo(n, 0.1 + 0.02 * i, 0.5, i) for i, f in enumerate([55, 82.5, 110.25]))  # loops: whole cycles in 40 s
    write("Ambience/FinalArena", air + hum * 0.06, 0.28)


# ---------- effects ----------

def sfx():
    t = lambda s: np.arange(int(s * SR)) / SR
    # Thunder: a crack, then a long rolling rumble.
    n = int(5 * SR)
    x = lowpass(rng.standard_normal(n), 2500) * np.exp(-t(5) * 9) * 1.5
    x += lowpass(rng.standard_normal(n), 220, 3) * (np.exp(-t(5) * 0.9) * (0.6 + 0.4 * np.sin(t(5) * 7))) * 5
    write("Sfx/Thunder", reverb(x, 2, 0.3, circular=False), 0.95)
    # Roar: a growling saw with vibrato through vowel formants, plus breath.
    n = int(2.2 * SR)
    tt = t(2.2)
    f = 85 + 25 * np.sin(np.pi * tt / 2.2) + 6 * np.sin(2 * np.pi * 7 * tt)
    saw = signal.sawtooth(2 * np.pi * np.cumsum(f) / SR)
    voice = bandpass(saw, 300, 900) * 1.2 + bandpass(saw, 1000, 1800) * 0.6 + lowpass(saw, 250)
    voice += bandpass(rng.standard_normal(n), 400, 3000) * 0.5
    voice *= env(n, 0.15, 0.6) * (1 + 0.3 * np.sin(2 * np.pi * 13 * tt))
    write("Sfx/Roar", reverb(np.tanh(voice * 2), 1.5, 0.3, circular=False), 0.95)
    # Whooshes: a swept band of noise (casts, swings, projectiles).
    for name, dur, lo, hi, gain in [("Whoosh", 0.45, 400, 3000, 1), ("Swing", 0.35, 200, 1500, 1), ("Cast", 0.5, 800, 5000, 1)]:
        n = int(dur * SR)
        noise = rng.standard_normal(n)
        out = np.zeros(n)
        steps = 12
        for i in range(steps):  # piecewise bandpass sweep
            a, b = i * n // steps, (i + 1) * n // steps
            c = lo + (hi - lo) * (i / steps)
            out[a:b] = bandpass(noise, c * 0.7, c * 1.3)[a:b]
        out *= np.sin(np.pi * np.arange(n) / n) ** 2 * gain
        if name == "Cast":
            out += sum(bell(88 + k * 3, dur, 0.15)[:n] for k in range(3))
        write("Sfx/" + name, out, 0.9)
    # Hurt: a dull body hit.
    n = int(0.3 * SR)
    x = np.sin(2 * np.pi * 70 * t(0.3)) * np.exp(-t(0.3) * 18) + lowpass(rng.standard_normal(n), 900) * np.exp(-t(0.3) * 30) * 0.6
    write("Sfx/Hurt", x, 0.9)
    # Chest: a hinge creak, then a little chime.
    n = int(1.6 * SR)
    creak = np.zeros(n)
    m = int(0.7 * SR)
    pulses = signal.square(2 * np.pi * np.cumsum(28 + 10 * np.sin(np.arange(m) / SR * 9)) / SR) * 0.5 + 0.5
    creak[:m] = bandpass(rng.standard_normal(m) * pulses, 600, 1400) * np.hanning(m)
    for k, note in enumerate([74, 78, 81, 86]):
        add(creak, int((0.65 + k * 0.09) * SR), bell(note, 0.9, 0.5)[: n - int((0.65 + k * 0.09) * SR)])
    write("Sfx/Chest", reverb(creak, 1.2, 0.25, circular=False), 0.9)
    # Coin and potion.
    coin = np.zeros(int(0.6 * SR))
    add(coin, 0, bell(93, 0.5, 0.7)[: len(coin)])
    add(coin, int(0.07 * SR), bell(98, 0.5, 0.6)[: len(coin) - int(0.07 * SR)])
    write("Sfx/Coin", coin, 0.7)
    n = int(0.7 * SR)
    gulps = sum(np.roll(np.sin(2 * np.pi * np.cumsum(np.linspace(300, 120, n)) / SR) * np.exp(-t(0.7) * 14), int(k * 0.18 * SR)) for k in range(3))
    write("Sfx/Potion", lowpass(gulps, 1200), 0.8)
    # Claim: the element joins her. A rising shimmer of bells.
    n = int(3 * SR)
    claim = np.zeros(n)
    for k, note in enumerate([62, 66, 69, 74, 78, 81, 86]):
        add(claim, int(k * 0.11 * SR), bell(note, 2.2, 0.6)[: n - int(k * 0.11 * SR)])
    claim += pad([50, 57, 62], 3, 2500)[:n] * 0.6
    write("Sfx/Claim", reverb(claim, 2.5, 0.4, circular=False), 0.85)
    # Reveal: a reversed swell into a deep boom.
    n = int(3 * SR)
    swell = (bandpass(rng.standard_normal(n), 200, 4000) * np.linspace(0, 1, n) ** 3)
    boom = np.zeros(n)
    boom[int(2.2 * SR):] = drum(35, 0.8, 2, 0.6)[: n - int(2.2 * SR)]
    write("Sfx/Reveal", reverb(swell + boom, 2.5, 0.35, circular=False), 0.95)
    # Seal: the elemental barrier pushing back.
    n = int(0.6 * SR)
    zap = signal.sawtooth(2 * np.pi * np.cumsum(np.linspace(900, 200, n) + 80 * rng.standard_normal(n)) / SR) * np.exp(-t(0.6) * 6)
    write("Sfx/Seal", bandpass(zap, 300, 4000), 0.8)
    # Impact: a heavy magical thump for spell and boss hits.
    n = int(0.9 * SR)
    hit = drum(48, 0.9, 1.0, 0.8)[:n] + bandpass(rng.standard_normal(n), 500, 5000) * np.exp(-t(0.9) * 12) * 0.6
    write("Sfx/Impact", reverb(hit, 1, 0.25, circular=False), 0.95)


if __name__ == "__main__":
    import sys
    makers = (music_menu, music_forest, music_ice, music_volcano, music_sanctum, music_boss, music_memory, music_truth, music_gilded,
              ambience_forest, ambience_ice, ambience_volcano, ambience_sanctum, sfx)
    # Optional names pick a subset, e.g.  python Tools/make_placeholder_audio.py ambience
    for make in makers:
        if len(sys.argv) < 2 or any(arg in make.__name__ for arg in sys.argv[1:]):
            make()

