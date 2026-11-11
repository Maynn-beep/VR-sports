"""
Модели для бокса: перчатка, лапа (тренировочная мишень), боксёрская груша.

Оси: Blender Z → Unity Y (вверх), Blender −Y → Unity +Z (вперёд, от игрока).

    blender -b --factory-startup -P boxing.py -- <папка Assets/_Project/Models/Boxing> [--preview файл.png]
"""
import math
import os
import sys

import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402

OUT = common.output_dir(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Models", "Boxing"))
common.clear_scene()


def flat(obj):
    for p in obj.data.polygons:
        p.use_smooth = False


def join(parts, target):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return target


# ---------------------------------------------------------------- перчатка (правая; левая — зеркалом в Unity)
# Кулак — сплюснутая икосфера, вытянутая вперёд (−Y); сбоку большой палец; сзади манжета.
glove_main = common.make_material("GloveMain", (0.85, 0.1, 0.1))
glove_cuff = common.make_material("GloveCuff", (0.95, 0.95, 0.95))

bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(0, -0.01, 0))
fist = bpy.context.active_object
fist.scale = (0.062, 0.085, 0.068)
fist.name = fist.data.name = "Glove"
fist.data.materials.append(glove_main)
fist.data.materials.append(glove_cuff)

bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=(0.05, 0.0, -0.01))
thumb = bpy.context.active_object
thumb.scale = (0.025, 0.045, 0.028)
thumb.rotation_euler = (0, math.radians(-20), math.radians(-25))
thumb.data.materials.append(glove_main)

bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.052, depth=0.075, location=(0, 0.1, -0.005),
                                    rotation=(math.radians(90), 0, 0))
cuff = bpy.context.active_object
cuff.data.materials.append(glove_cuff)

glove = join([thumb, cuff], fist)
flat(glove)
common.export_fbx(glove, os.path.join(OUT, "Glove.fbx"))

# ---------------------------------------------------------------- лапа
# Тело вращения: толстая «подушка» с выпуклой лицевой стороной. Лицевая сторона — отдельный
# материал (слот 1), его цвет меняется в игре (подсветка: синий — левой, красный — правой).
pad_body = common.make_material("PadBody", (0.1, 0.1, 0.12))
pad_face = common.make_material("PadFace", (0.9, 0.9, 0.9))
PAD = [(0.0, 0.0), (0.0, 0.10), (0.015, 0.115), (0.04, 0.118), (0.055, 0.105), (0.065, 0.08), (0.07, 0.04),
       (0.072, 0.0)]
pad = common.lathe("Pad", PAD, segments=16, band_material=lambda z: 1 if z > 0.05 else 0)
pad.data.materials.append(pad_body)
pad.data.materials.append(pad_face)
# Поворачиваем ось вращения (Z) на +Y Blender = −Z Unity: лицевая сторона смотрит на игрока.
pad.rotation_euler = (math.radians(-90), 0, 0)
bpy.context.view_layer.objects.active = pad
pad.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
common.export_fbx(pad, os.path.join(OUT, "Pad.fbx"))

# ---------------------------------------------------------------- груша
# Цилиндр 1 м с закруглёнными торцами. Точка отсчёта — ВЕРХ (там шарнир подвеса).
bag_mat = common.make_material("BagLeather", (0.7, 0.12, 0.1))
bag_band = common.make_material("BagBand", (0.1, 0.1, 0.1))
BAG = [(-1.0, 0.0), (-1.0, 0.14), (-0.97, 0.175), (-0.92, 0.18), (-0.86, 0.18), (-0.84, 0.18),
       (-0.16, 0.18), (-0.14, 0.18), (-0.06, 0.175), (-0.02, 0.15), (0.0, 0.11), (0.0, 0.0)]
bag = common.lathe("Bag", BAG, segments=14,
                   band_material=lambda z: 1 if (-0.86 < z < -0.84 or -0.16 < z < -0.14) else 0)
bag.data.materials.append(bag_mat)
bag.data.materials.append(bag_band)
common.export_fbx(bag, os.path.join(OUT, "Bag.fbx"))

# ---------------------------------------------------------------- превью
if "--preview" in sys.argv:
    glove.location = (-0.35, 0, 0)
    pad.location = (-0.05, 0, 0.0)
    bag.location = (0.35, 0, 0.6)
    cam = common.render_preview(sys.argv[sys.argv.index("--preview") + 1])
    cam.location = (0.15, 1.9, 0.15)              # со стороны игрока (+Y)
    cam.rotation_euler = (math.radians(88), 0, math.radians(180))
    cam.data.lens = 35
    bpy.ops.render.render(write_still=True)
    print("[preview] готово")
