"""
Модели для боулинга: кегля и шар. Размеры — по стандарту USBC (в метрах).

    blender -b --factory-startup -P bowling.py -- <папка Assets/_Project/Models/Bowling>
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402

OUT = common.output_dir(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Models", "Bowling"))

common.clear_scene()
white = common.make_material("PinWhite", (0.95, 0.95, 0.92))
red = common.make_material("PinRed", (0.8, 0.1, 0.1))

# ---------------------------------------------------------------- кегля
# Профиль кегли (высота, радиус). Стандарт: высота 38.1 см, макс. диаметр 12.1 см
# на высоте ~11.4 см, диаметр шейки 4.6 см, основание ~5.2 см.
PIN_PROFILE = [
    (0.000, 0.0),
    (0.000, 0.026),
    (0.020, 0.040),
    (0.060, 0.055),
    (0.114, 0.0605),   # «пузо» — самое широкое место
    (0.170, 0.055),
    (0.215, 0.040),
    (0.240, 0.028),
    (0.250, 0.024),    # начало красной полосы
    (0.262, 0.023),    # шейка
    (0.274, 0.024),    # конец красной полосы
    (0.300, 0.028),
    (0.335, 0.032),    # голова
    (0.360, 0.028),
    (0.375, 0.018),
    (0.381, 0.0),
]


def pin_band(z):
    """Красная полоса на шейке — второй материал (слот 1)."""
    return 1 if 0.250 <= z <= 0.274 else 0


pin = common.lathe("Pin", PIN_PROFILE, segments=12, band_material=pin_band)
pin.data.materials.append(white)
pin.data.materials.append(red)
common.export_fbx(pin, os.path.join(OUT, "Pin.fbx"))

# ---------------------------------------------------------------- шар
# Диаметр 21.8 см. Икосфера 3-го уровня — 320 граней, «гранёный» low-poly вид.
BALL_R = 0.109
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=BALL_R, location=(0, 0, 0))
ball = bpy.context.active_object
ball.name = "Ball"
ball.data.name = "Ball"

body = common.make_material("BallBody", (0.25, 0.3, 0.85))
holes = common.make_material("BallHoles", (0.03, 0.03, 0.05))
ball.data.materials.append(body)
ball.data.materials.append(holes)

# Три отверстия для пальцев — тёмные «таблетки», утопленные в поверхность.
hole_parts = []
for (ax, az, hr) in [(-0.028, 0.0, 0.011), (0.028, 0.0, 0.011), (0.0, -0.055, 0.013)]:
    # Точка на «верхушке» шара (+Y в Blender), смещённая по сфере.
    d = Vector((ax, BALL_R, az)).normalized()
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=hr, depth=0.01, location=d * (BALL_R - 0.003))
    c = bpy.context.active_object
    c.rotation_mode = "QUATERNION"
    c.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d)
    c.data.materials.append(holes)
    hole_parts.append(c)

bpy.ops.object.select_all(action="DESELECT")
for c in hole_parts:
    c.select_set(True)
ball.select_set(True)
bpy.context.view_layer.objects.active = ball
bpy.ops.object.join()   # материалы объединяются по имени: слот 0 — корпус, 1 — отверстия
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
for p in ball.data.polygons:
    p.use_smooth = False
common.export_fbx(ball, os.path.join(OUT, "Ball.fbx"))

# ---------------------------------------------------------------- превью
if "--preview" in sys.argv:
    pin.location = (-0.12, 0, 0)
    ball.location = (0.12, 0, BALL_R)
    cam = common.render_preview(sys.argv[sys.argv.index("--preview") + 1])
    cam.location = (0.0, -0.9, 0.35)
    cam.rotation_euler = (math.radians(82), 0, 0)
    cam.data.lens = 50
    bpy.ops.render.render(write_still=True)
    print("[preview] готово")
