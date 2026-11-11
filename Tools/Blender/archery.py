"""
Модели для стрельбы из лука: классический лук (recurve), стрела, мишень, тарелочка.

Оси (проверено импортом в Unity): Blender Z → Unity Y (вверх), Blender −Y → Unity +Z (вперёд).
Поэтому «вперёд, к мишени» в этом скрипте — это −Y.

    blender -b --factory-startup -P archery.py -- <папка Assets/_Project/Models/Archery> [--preview файл.png]
"""
import math
import os
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402

OUT = common.output_dir(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Models", "Archery"))
common.clear_scene()


def mesh_object(name, verts, faces, mats, mat_idx, recalc=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in mats:
        obj.data.materials.append(m)
    for p, i in zip(mesh.polygons, mat_idx):
        p.material_index = i
        p.use_smooth = False
    if recalc:  # развернуть нормали наружу (для замкнутых тел)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
    return obj


# ---------------------------------------------------------------- лук
# Лук «выметается» (sweep) прямоугольным сечением вдоль кривой плеча.
# Натянутый лук — буква «D»: рукоять дальше всего от лучника, плечи загибаются к нему,
# а у классического лука (recurve) кончики плеч снова отгибаются вперёд.
BOW_HALF = 0.68      # половина длины лука (лук ~1.4 м)
BRACE = 0.22         # «высота тетивы» — расстояние от рукояти до тетивы
wood = common.make_material("BowWood", (0.45, 0.25, 0.1))
grip_mat = common.make_material("BowGrip", (0.1, 0.08, 0.07))


def bow_curve(s):
    """Точка оси лука: s ∈ [−1, 1] вдоль лука. Возвращает (y, z) в Blender."""
    t = abs(s)
    curl = max(0.0, (t - 0.8) / 0.2) ** 2           # отгиб кончика (recurve)
    y = (BRACE + 0.04) * t * t - 0.04 * curl          # +Y — к лучнику
    return y, s * BOW_HALF


def bow_section(s):
    """Ширина (X) и толщина сечения: толстая жёсткая рукоять, тонкие гибкие плечи."""
    t = abs(s)
    if t < 0.3:
        return 0.032, 0.036
    k = (t - 0.3) / 0.7
    return 0.034 - 0.018 * k, 0.02 - 0.011 * k


samples = [i / 20 * 2 - 1 for i in range(21)]
bv, bf, bmi = [], [], []
for i, s in enumerate(samples):
    y, z = bow_curve(s)
    s2 = samples[min(i + 1, 20)] if i < 20 else s
    s1 = samples[max(i - 1, 0)]
    (y1, z1), (y2, z2) = bow_curve(s1), bow_curve(s2)
    ty, tz = y2 - y1, z2 - z1
    ln = math.hypot(ty, tz)
    ny, nz = -tz / ln, ty / ln                      # нормаль к оси в плоскости YZ
    w, th = bow_section(s)
    for sx, sn in [(-1, -1), (1, -1), (1, 1), (-1, 1)]:
        bv.append((sx * w / 2, y + sn * th / 2 * ny, z + sn * th / 2 * nz))
for i in range(20):
    s_mid = (samples[i] + samples[i + 1]) / 2
    m = 1 if abs(s_mid) < 0.1 else 0                # обмотка рукояти — тёмный материал
    for k in range(4):
        a, b = i * 4 + k, i * 4 + (k + 1) % 4
        bf.append((a, b, b + 4, a + 4))
        bmi.append(m)
bf.append((0, 1, 2, 3)); bmi.append(0)
bf.append((80, 81, 82, 83)); bmi.append(0)
bow = mesh_object("Bow", bv, bf, [wood, grip_mat], bmi)
common.export_fbx(bow, os.path.join(OUT, "Bow.fbx"))

# ---------------------------------------------------------------- стрела
# Стрела 85 см направлена вперёд (−Y), точка отсчёта — хвостовик (место на тетиве).
ARROW_LEN = 0.85        # длиннее максимального натяжения (0.55 м) + запас за рукоятью
shaft_mat = common.make_material("ArrowShaft", (0.85, 0.75, 0.55))
head_mat = common.make_material("ArrowHead", (0.6, 0.62, 0.66))
fletch_mat = common.make_material("ArrowFletch", (0.9, 0.15, 0.15))

seg = 6
av, af, ami = [], [], []


def ring(y, r):
    base = len(av)
    for k in range(seg):
        a = 2 * math.pi * k / seg
        av.append((r * math.cos(a), y, r * math.sin(a)))
    return base


r0 = ring(0.0, 0.004)
r1 = ring(-(ARROW_LEN - 0.05), 0.004)
r2 = ring(-(ARROW_LEN - 0.05), 0.008)
tip = len(av); av.append((0.0, -ARROW_LEN, 0.0))
back = len(av); av.append((0.0, 0.0, 0.0))
for k in range(seg):
    k2 = (k + 1) % seg
    af.append((r0 + k, r0 + k2, r1 + k2, r1 + k)); ami.append(0)   # древко
    af.append((r1 + k, r1 + k2, r2 + k2, r2 + k)); ami.append(1)   # основание наконечника
    af.append((r2 + k, r2 + k2, tip)); ami.append(1)               # наконечник-конус
    af.append((r0 + k2, r0 + k, back)); ami.append(0)              # торец
# Оперение: три пера под 120°.
for j in range(3):
    a = 2 * math.pi * j / 3 + math.pi / 2
    dx, dz = math.cos(a), math.sin(a)
    p = len(av)
    av += [(0.004 * dx, -0.03, 0.004 * dz), (0.004 * dx, -0.13, 0.004 * dz), (0.022 * dx, -0.05, 0.022 * dz)]
    af.append((p, p + 1, p + 2)); ami.append(2)
    af.append((p + 2, p + 1, p)); ami.append(2)                    # обратная сторона пера
arrow = mesh_object("Arrow", av, af, [shaft_mat, head_mat, fletch_mat], ami, recalc=False)
common.export_fbx(arrow, os.path.join(OUT, "Arrow.fbx"))

# ---------------------------------------------------------------- мишень
# Диск радиусом 0.4 м, 10 колец по 4 см. Лицевая сторона смотрит на лучника (+Y в Blender).
# Цвета по правилам World Archery: 10–9 жёлтые, 8–7 красные, 6–5 синие, 4–3 чёрные, 2–1 белые.
R, RINGS, SEG, THICK = 0.4, 10, 32, 0.08
colors = [("TGold", (0.98, 0.82, 0.1)), ("TRed", (0.9, 0.15, 0.12)), ("TBlue", (0.15, 0.5, 0.9)),
          ("TBlack", (0.08, 0.08, 0.08)), ("TWhite", (0.95, 0.95, 0.95)), ("TStraw", (0.8, 0.68, 0.38))]
tmats = [common.make_material(n, c) for n, c in colors]
tv, tf, tmi = [], [], []
front = THICK / 2
center_f = len(tv); tv.append((0.0, front, 0.0))
ring_idx = []
for r in range(1, RINGS + 1):
    base = len(tv)
    for k in range(SEG):
        a = 2 * math.pi * k / SEG
        tv.append((R * r / RINGS * math.cos(a), front, R * r / RINGS * math.sin(a)))
    ring_idx.append(base)
back_base = len(tv)
for k in range(SEG):
    a = 2 * math.pi * k / SEG
    tv.append((R * math.cos(a), -front, R * math.sin(a)))
center_b = len(tv); tv.append((0.0, -front, 0.0))
for k in range(SEG):
    k2 = (k + 1) % SEG
    tf.append((center_f, ring_idx[0] + k, ring_idx[0] + k2)); tmi.append(0)     # «десятка»
    for r in range(1, RINGS):
        a, b = ring_idx[r - 1], ring_idx[r]
        tf.append((a + k, b + k, b + k2, a + k2)); tmi.append(r // 2)
    o = ring_idx[-1]
    tf.append((o + k, back_base + k, back_base + k2, o + k2)); tmi.append(5)     # боковина
    tf.append((center_b, back_base + k2, back_base + k)); tmi.append(5)          # тыльная сторона
target = mesh_object("TargetFace", tv, tf, tmats, tmi)
common.export_fbx(target, os.path.join(OUT, "TargetFace.fbx"))

# ---------------------------------------------------------------- тарелочка
disc_mat = common.make_material("Clay", (1.0, 0.45, 0.1))
disc = common.lathe("ClayDisc", [(0.0, 0.0), (0.0, 0.052), (0.008, 0.055), (0.02, 0.04), (0.026, 0.02), (0.027, 0.0)],
                    segments=12)
disc.data.materials.append(disc_mat)
common.export_fbx(disc, os.path.join(OUT, "ClayDisc.fbx"))

# ---------------------------------------------------------------- превью
if "--preview" in sys.argv:
    bow.location = (-0.5, 0, 0)
    arrow.location = (-0.1, 0.2, 0.0)
    target.location = (0.6, 0, 0)
    disc.location = (0.15, 0, -0.4)
    cam = common.render_preview(sys.argv[sys.argv.index("--preview") + 1])
    cam.location = (1.6, 2.4, 0.4)          # смотрим со стороны лучника и сбоку
    cam.rotation_euler = (math.radians(85), 0, math.radians(146))
    cam.data.lens = 35
    bpy.ops.render.render(write_still=True)
    print("[preview] готово")
