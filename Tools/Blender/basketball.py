"""
Модели для баскетбола: мяч, кольцо (обод) и сетка. Размеры по правилам FIBA (метры).

    blender -b --factory-startup -P basketball.py -- <папка Assets/_Project/Models/Basketball> [--preview файл.png]
"""
import math
import os
import sys

import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402

OUT = common.output_dir(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Models", "Basketball"))

BALL_R = 0.12        # мяч №7: диаметр ~24 см
RIM_R = 0.2286       # внутренний радиус обода (диаметр 45.7 см)
RIM_TUBE = 0.009     # толщина прутка обода ~1.8 см
NET_LEN = 0.42       # длина сетки

common.clear_scene()
orange = common.make_material("BallOrange", (0.9, 0.42, 0.12))
black = common.make_material("BallSeams", (0.05, 0.04, 0.04))
rim_mat = common.make_material("Rim", (0.9, 0.3, 0.1))
net_mat = common.make_material("Net", (0.95, 0.95, 0.95))


def join(objects, target):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    return target


def flat(obj):
    for p in obj.data.polygons:
        p.use_smooth = False


# ---------------------------------------------------------------- мяч
# Икосфера + три тонких тора-шва по большим кругам (упрощённый рисунок швов).
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=BALL_R)
ball = bpy.context.active_object
ball.name = ball.data.name = "Basketball"
ball.data.materials.append(orange)
ball.data.materials.append(black)

seams = []
for rot in [(0, 0, 0), (math.pi / 2, 0, 0), (0, math.pi / 2, 0)]:
    bpy.ops.mesh.primitive_torus_add(major_radius=BALL_R * 1.004, minor_radius=0.0035,
                                     major_segments=24, minor_segments=3, rotation=rot)
    t = bpy.context.active_object
    t.data.materials.append(black)
    seams.append(t)
join(seams, ball)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
flat(ball)
common.export_fbx(ball, os.path.join(OUT, "Basketball.fbx"))

# ---------------------------------------------------------------- обод
bpy.ops.mesh.primitive_torus_add(major_radius=RIM_R + RIM_TUBE, minor_radius=RIM_TUBE,
                                 major_segments=24, minor_segments=6)
rim = bpy.context.active_object
rim.name = rim.data.name = "Rim"
rim.data.materials.append(rim_mat)
flat(rim)
common.export_fbx(rim, os.path.join(OUT, "Rim.fbx"))

# ---------------------------------------------------------------- сетка
# Усечённый конус без крышек → модификатор Wireframe превращает рёбра в «нити».
# Точка отсчёта — верх сетки (крепится к ободу), сетка свисает вниз по −Z.
seg, rings = 12, 4
verts, faces = [], []
for r in range(rings + 1):
    k = r / rings
    radius = RIM_R * (1 - 0.4 * k)          # сужается книзу
    z = -NET_LEN * k
    twist = (math.pi / seg) * (r % 2)        # сдвиг через ряд — ромбовидные ячейки
    for s in range(seg):
        a = 2 * math.pi * s / seg + twist
        verts.append((radius * math.cos(a), radius * math.sin(a), z))
for r in range(rings):
    for s in range(seg):
        a0 = r * seg + s
        a1 = r * seg + (s + 1) % seg
        b0 = (r + 1) * seg + s
        b1 = (r + 1) * seg + (s + 1) % seg
        faces.append((a0, a1, b1, b0))
mesh = bpy.data.meshes.new("Net")
mesh.from_pydata(verts, [], faces)
net = bpy.data.objects.new("Net", mesh)
bpy.context.scene.collection.objects.link(net)
net.data.materials.append(net_mat)
bpy.context.view_layer.objects.active = net
wire = net.modifiers.new("Wire", "WIREFRAME")
wire.thickness = 0.006
wire.use_even_offset = True
bpy.ops.object.select_all(action="DESELECT")
net.select_set(True)
bpy.ops.object.modifier_apply(modifier="Wire")
flat(net)
common.export_fbx(net, os.path.join(OUT, "Net.fbx"))

# ---------------------------------------------------------------- превью
if "--preview" in sys.argv:
    ball.location = (0.45, 0, -0.3)
    rim.location = (0, 0, 0)
    net.location = (0, 0, 0)
    cam = common.render_preview(sys.argv[sys.argv.index("--preview") + 1])
    cam.location = (0.2, -1.6, 0.35)
    cam.rotation_euler = (math.radians(85), 0, math.radians(5))
    cam.data.lens = 40
    bpy.ops.render.render(write_still=True)
    print("[preview] готово")
