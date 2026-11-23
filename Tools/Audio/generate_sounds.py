"""
Генератор звуков игры — процедурный синтез на чистом Python (без сторонних библиотек).

Как и 3D-модели (Tools/Blender), звуки не скачиваются, а строятся кодом. Поэтому:
  • нет вопросов с лицензиями — все звуки авторские;
  • любой звук можно пересоздать и поменять (высоту, длительность, тембр).

Запуск:
    python Tools/Audio/generate_sounds.py [папка_вывода]
По умолчанию — Assets/_Project/Resources/Sounds (Unity загружает их по имени: Sfx.Play("pin_hit")).

Приёмы синтеза (как в звуковом дизайне игр):
  • удар по дереву/металлу — «модальный синтез»: сумма затухающих синусоид на частотах
    собственных колебаний предмета (у металла их много и они негармоничны — звенит);
  • шорох, свист, разбитие — отфильтрованный шум (полосовой фильтр = «окраска» шума);
  • струна — алгоритм Карплуса–Стронга (линия задержки + усреднение = затухающая струна);
  • глухой удар — низкая синусоида с падающей частотой + щелчок шума в начале.
"""
import math
import os
import random
import struct
import sys
import wave

RATE = 22050  # Гц — для коротких эффектов достаточно, файлы в 2 раза меньше
random.seed(7)  # одинаковый результат при каждом запуске


# ---------------------------------------------------------------- основа

def silence(sec):
    return [0.0] * int(sec * RATE)


def mix(*tracks):
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i, v in enumerate(t):
            out[i] += v
    return out


def place(track, at_sec, total_sec):
    """Поставить звук в момент at_sec внутри дорожки длиной total_sec."""
    out = silence(total_sec)
    off = int(at_sec * RATE)
    for i, v in enumerate(track):
        if off + i < len(out):
            out[off + i] += v
    return out


def gain(track, g):
    return [v * g for v in track]


def env_exp(n, decay_sec, attack_sec=0.002):
    """Огибающая: быстрая атака и экспоненциальное затухание (как у удара)."""
    a = max(1, int(attack_sec * RATE))
    return [(i / a if i < a else 1.0) * math.exp(-(i / RATE) / decay_sec) for i in range(n)]


def env_ar(n, attack_sec, release_sec):
    """Огибающая «нарастание — спад» (для шорохов и свиста)."""
    a = max(1, int(attack_sec * RATE))
    r = max(1, int(release_sec * RATE))
    out = []
    for i in range(n):
        if i < a:
            out.append(i / a)
        elif i > n - r:
            out.append(max(0.0, (n - i) / r))
        else:
            out.append(1.0)
    return out


def apply(track, env):
    return [v * e for v, e in zip(track, env)]


def noise(sec):
    return [random.uniform(-1, 1) for _ in range(int(sec * RATE))]


def sine(freq, sec, sweep_to=None, phase=0.0):
    """Синусоида; sweep_to — частота плавно меняется (падающий тон удара)."""
    n = int(sec * RATE)
    out = []
    ph = phase
    for i in range(n):
        f = freq if sweep_to is None else freq + (sweep_to - freq) * (i / n)
        ph += 2 * math.pi * f / RATE
        out.append(math.sin(ph))
    return out


def modal(partials, sec):
    """Модальный синтез: partials = [(частота, громкость, время затухания), ...]."""
    n = int(sec * RATE)
    out = [0.0] * n
    for f, amp, dec in partials:
        ph = random.uniform(0, 2 * math.pi)
        w = 2 * math.pi * f / RATE
        for i in range(n):
            out[i] += amp * math.sin(ph + w * i) * math.exp(-(i / RATE) / dec)
    return out


def biquad_bandpass(track, freq, q):
    """Полосовой фильтр (рецепт RBJ): пропускает полосу около freq, ширина ~ freq/q."""
    w0 = 2 * math.pi * freq / RATE
    alpha = math.sin(w0) / (2 * q)
    b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * math.cos(w0), 1 - alpha
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    x1 = x2 = y1 = y2 = 0.0
    out = []
    for x in track:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def sweep_bandpass(track, f0, f1, q):
    """Полосовой фильтр с плавно меняющейся частотой — «вжух»."""
    out = []
    x1 = x2 = y1 = y2 = 0.0
    n = len(track)
    for i, x in enumerate(track):
        f = f0 + (f1 - f0) * i / n
        w0 = 2 * math.pi * f / RATE
        alpha = math.sin(w0) / (2 * q)
        a0 = 1 + alpha
        b0, b2 = alpha / a0, -alpha / a0
        a1, a2 = -2 * math.cos(w0) / a0, (1 - alpha) / a0
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def lowpass(track, cutoff):
    """Однополюсный фильтр нижних частот: y += k·(x − y)."""
    k = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    y = 0.0
    out = []
    for x in track:
        y += k * (x - y)
        out.append(y)
    return out


def highpass(track, cutoff):
    lp = lowpass(track, cutoff)
    return [x - l for x, l in zip(track, lp)]


def karplus_strong(freq, sec, damping=0.996):
    """Струна: шум в линии задержки длиной один период; усреднение соседних отсчётов гасит высокие частоты."""
    period = int(RATE / freq)
    buf = [random.uniform(-1, 1) for _ in range(period)]
    out = []
    for i in range(int(sec * RATE)):
        v = buf[i % period]
        nxt = buf[(i + 1) % period]
        buf[i % period] = damping * 0.5 * (v + nxt)
        out.append(v)
    return out


def normalize(track, peak=0.9):
    m = max(1e-9, max(abs(v) for v in track))
    return [v * peak / m for v in track]


def fade_out(track, sec=0.01):
    n = int(sec * RATE)
    out = list(track)
    for i in range(min(n, len(out))):
        out[-1 - i] *= i / n
    return out


def save(name, track, out_dir, peak=0.9):
    track = fade_out(normalize(track, peak))
    path = os.path.join(out_dir, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in track))
    print(f"[sound] {name}.wav  {len(track) / RATE:.2f} с")


# ---------------------------------------------------------------- звуки

def thud(freq, sec, click=0.3, decay=0.08):
    """Глухой удар: низкий тон с падающей частотой + щелчок шума."""
    body = apply(sine(freq * 1.6, sec, sweep_to=freq * 0.7), env_exp(int(sec * RATE), decay))
    tick = apply(lowpass(noise(sec), 2500), env_exp(int(sec * RATE), 0.012))
    return mix(body, gain(tick, click))


def wood(freqs, sec=0.25, dec=0.05):
    """Деревянный «тук»: несколько мод с быстрым затуханием + короткий шум."""
    body = modal([(f, 1.0 / (k + 1), dec * (1.0 - 0.15 * k)) for k, f in enumerate(freqs)], sec)
    tick = apply(highpass(noise(sec), 1500), env_exp(int(sec * RATE), 0.006))
    return mix(body, gain(tick, 0.4))


def beep(freq, sec, harmonics=(1.0, 0.3, 0.1)):
    out = silence(sec)
    for k, a in enumerate(harmonics):
        out = mix(out, gain(sine(freq * (k + 1), sec), a))
    return apply(out, env_ar(len(out), 0.005, 0.05))


def build(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    # --- интерфейс и ход партии
    save("ui_click", mix(apply(sine(1800, 0.05), env_exp(int(0.05 * RATE), 0.012)),
                         gain(apply(highpass(noise(0.05), 3000), env_exp(int(0.05 * RATE), 0.004)), 0.3)), out_dir, 0.6)
    save("ui_hover", apply(sine(1200, 0.04), env_exp(int(0.04 * RATE), 0.01)), out_dir, 0.3)
    save("countdown", beep(880, 0.15), out_dir, 0.6)
    save("go", beep(1320, 0.4), out_dir, 0.7)
    notes = [523.25, 659.25, 783.99]                       # до-ми-соль: «партия окончена»
    save("finish", mix(*[place(beep(f, 0.3), i * 0.14, 0.8) for i, f in enumerate(notes)]), out_dir, 0.7)
    notes = [523.25, 659.25, 783.99, 1046.5]               # фанфара: «новый рекорд / победа»
    save("record", mix(*[place(beep(f, 0.5 if i == 3 else 0.22), i * 0.13, 1.1) for i, f in enumerate(notes)]),
         out_dir, 0.75)
    save("fail", mix(place(beep(392, 0.3), 0, 0.8), place(beep(311, 0.45), 0.25, 0.8)), out_dir, 0.6)

    # --- боулинг
    save("pin_hit", wood([920, 1480, 2310, 3150], 0.3, 0.06), out_dir)
    crash = silence(0.9)                                    # страйк: много кеглей с разбросом по времени
    for _ in range(14):
        hit = gain(wood([random.uniform(800, 1100), random.uniform(1350, 1650), random.uniform(2100, 2500)], 0.3, 0.05),
                   random.uniform(0.3, 1.0))
        crash = mix(crash, place(hit, random.uniform(0, 0.5), 0.9))
    save("pin_crash", crash, out_dir)
    roll = lowpass(lowpass(noise(2.0), 180), 220)          # гул шара по дорожке — зацикленный
    roll = [v * (0.8 + 0.2 * math.sin(2 * math.pi * 3 * i / RATE)) for i, v in enumerate(roll)]
    n = len(roll)                                           # бесшовная петля: конец плавно переходит в начало
    xf = int(0.2 * RATE)
    for i in range(xf):
        t = i / xf
        roll[i] = roll[i] * t + roll[n - xf + i] * (1 - t)
    save("ball_roll", roll[: n - xf], out_dir, 0.8)
    save("ball_drop", thud(70, 0.4, 0.2, 0.12), out_dir)

    # --- баскетбол
    save("ball_bounce", mix(thud(110, 0.25, 0.25, 0.05), gain(apply(sine(240, 0.25), env_exp(int(0.25 * RATE), 0.04)), 0.4)),
         out_dir)
    save("rim_hit", mix(modal([(520, 1.0, 0.35), (1180, 0.6, 0.25), (1960, 0.4, 0.18), (2870, 0.3, 0.12)], 0.8),
                        gain(apply(highpass(noise(0.8), 2000), env_exp(int(0.8 * RATE), 0.005)), 0.3)), out_dir)
    save("board_hit", mix(thud(150, 0.3, 0.4, 0.06), gain(wood([420, 780], 0.3, 0.04), 0.6)), out_dir)
    sw = noise(0.4)
    save("net_swish", apply(sweep_bandpass(sw, 2500, 5500, 1.2), env_ar(len(sw), 0.08, 0.25)), out_dir, 0.7)

    # --- стрельба из лука
    twang = mix(apply(karplus_strong(98, 0.5, 0.993), env_exp(int(0.5 * RATE), 0.15)),
                gain(thud(140, 0.5, 0.2, 0.03), 0.5))
    save("bow_release", twang, out_dir)
    wh = noise(0.35)
    save("arrow_whoosh", apply(sweep_bandpass(wh, 1800, 700, 2.0), env_ar(len(wh), 0.03, 0.25)), out_dir, 0.6)
    save("arrow_hit", mix(thud(180, 0.25, 0.5, 0.04), gain(wood([260, 610], 0.25, 0.03), 0.5)), out_dir)
    save("arrow_ground", thud(120, 0.2, 0.6, 0.03), out_dir, 0.6)
    shatter = silence(0.5)
    for _ in range(18):                                     # осколки: короткие звонкие «тики»
        f = random.uniform(2800, 6500)
        piece = modal([(f, 1.0, 0.03), (f * 1.53, 0.5, 0.02)], 0.15)
        shatter = mix(shatter, place(gain(piece, random.uniform(0.3, 1.0)), random.uniform(0, 0.25), 0.5))
    burst = apply(highpass(noise(0.5), 2500), env_exp(int(0.5 * RATE), 0.04))
    save("clay_shatter", mix(shatter, gain(burst, 0.6)), out_dir)
    lw = noise(0.5)
    save("clay_launch", mix(thud(90, 0.5, 0.3, 0.05),
                            gain(apply(sweep_bandpass(lw, 600, 2200, 1.5), env_ar(len(lw), 0.05, 0.3)), 0.6)), out_dir)

    # --- бокс
    slap = mix(apply(highpass(noise(0.12), 1200), env_exp(int(0.12 * RATE), 0.018)),
               gain(thud(260, 0.12, 0.0, 0.03), 0.8))
    save("punch_pad", slap, out_dir)
    save("punch_bag", mix(thud(85, 0.35, 0.35, 0.09), gain(apply(lowpass(noise(0.35), 900), env_exp(int(0.35 * RATE), 0.04)), 0.5)),
         out_dir)
    save("punch_block", mix(thud(220, 0.15, 0.3, 0.03), gain(wood([520, 900], 0.15, 0.02), 0.4)), out_dir, 0.7)
    save("player_hit", mix(thud(60, 0.5, 0.5, 0.12), gain(apply(lowpass(noise(0.5), 600), env_exp(int(0.5 * RATE), 0.06)), 0.7)),
         out_dir)
    bell = modal([(700, 1.0, 1.2), (700 * 2.76, 0.5, 0.8), (700 * 5.4, 0.3, 0.5), (700 * 8.93, 0.15, 0.3)], 2.0)
    save("bell", mix(place(bell, 0, 2.4), place(gain(bell, 0.8), 0.35, 2.4)), out_dir)   # «динь-динь» гонга
    dw = noise(0.45)
    save("dodge_whoosh", apply(sweep_bandpass(dw, 400, 1600, 1.8), env_ar(len(dw), 0.15, 0.2)), out_dir, 0.6)
    # робот: короткий «серво»-жужжащий звук при движении/замахе
    servo = [math.copysign(1, s) * 0.3 + s * 0.7 for s in sine(160, 0.3, sweep_to=240)]
    save("robot_servo", apply(lowpass(servo, 1500), env_ar(len(servo), 0.03, 0.1)), out_dir, 0.4)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    default = os.path.join(here, "..", "..", "Assets", "_Project", "Resources", "Sounds")
    build(sys.argv[1] if len(sys.argv) > 1 else default)
