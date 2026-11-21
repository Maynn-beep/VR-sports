"""
Робот-соперник (общий для всех режимов): голова, торс с «воздушной подушкой», кисть.
Руки (плечо и предплечье) — цилиндры в Unity, их сгибает IK (RivalAvatar.cs).

Слоты материалов у всех частей одинаковые:
    0 — корпус (светлый пластик), 1 — акцент (цвет команды), 2 — свечение (глаза, огни; меняется в игре).

Оси: Blender Z → Unity Y (вверх), Blender −Y → Unity +Z (робот смотрит вперёд, на игрока).

    blender -b --factory-startup -P robot.py -- <папка Assets/_Project/Models/Robot> [--preview файл.png]
"""
import math
import os
import sys

import bpy

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402

OUT = common.output_dir(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Models", "Robot"))
common.clear_scene()

BODY = common.make_material("RobotBody", (0.88, 0.89, 0.92))
ACCENT = common.make_material("RobotAccent", (0.95, 0.45, 0.1))
GLOW = common.make_material("RobotGlow", (0.2, 0.9, 1.0))
MATS = [BODY, ACCENT, GLOW]


def part(obj, slot, bevel=0.0, segments=1):
    """Подготовить деталь: все три материала в одном порядке, грани — в нужный слот, фаска."""
    for m in MATS:
        obj.data.materials.append(m)
    for p in obj.data.polygons:
        p.material_index = slot
        p.use_smooth = False
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def box(size, loc, slot, bevel=0.0, segments=1):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return part(o, slot, bevel, segments)


def ico(radius, loc, slot, scale=(1, 1, 1), subdiv=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return part(o, slot)


def cyl(radius, depth, loc, slot, rot=(0, 0, 0), verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return part(o, slot)


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = o.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


# ---------------------------------------------------------------- голова (центр — в начале координат)
head_parts = [
    box((0.22, 0.21, 0.23), (0, 0, 0), 0, bevel=0.03, segments=2),
    # Тёмный визор спереди (−Y Blender = вперёд в Unity) и глаза-огни на нём.
    box((0.19, 0.03, 0.085), (0, -0.098, 0.02), 1, bevel=0.01),
    box((0.045, 0.02, 0.022), (-0.045, -0.116, 0.025), 2),
    box((0.045, 0.02, 0.022), (0.045, -0.116, 0.025), 2),
    # «Уши»-динамики и антенна.
    cyl(0.045, 0.03, (-0.12, 0, 0), 1, rot=(0, math.radians(90), 0), verts=10),
    cyl(0.045, 0.03, (0.12, 0, 0), 1, rot=(0, math.radians(90), 0), verts=10),
    cyl(0.008, 0.09, (0.05, 0.02, 0.16), 0, verts=6),
    ico(0.02, (0.05, 0.02, 0.21), 2),
]
head = join(head_parts, "RobotHead")
common.export_fbx(head, os.path.join(OUT, "Head.fbx"))

# ---------------------------------------------------------------- торс (0 — основание шеи)
torso_parts = [
    cyl(0.04, 0.08, (0, 0, -0.02), 0, verts=8),                                 # шея
    box((0.42, 0.24, 0.3), (0, 0, -0.2), 0, bevel=0.04, segments=2),            # грудь
    box((0.2, 0.03, 0.1), (0, -0.12, -0.17), 1, bevel=0.01),                    # нагрудная панель
    cyl(0.035, 0.02, (0, -0.137, -0.17), 2, rot=(math.radians(90), 0, 0), verts=12),  # огонь «реактора»
    cyl(0.13, 0.12, (0, 0, -0.41), 1, verts=12),                                 # пояс
    ico(0.075, (-0.25, 0, -0.1), 1),                                             # плечевые шарниры
    ico(0.075, (0.25, 0, -0.1), 1),
]
# «Юбка» подушки: тело вращения, сужается книзу к соплу.
SKIRT = [(-0.47, 0.15), (-0.55, 0.16), (-0.68, 0.12), (-0.78, 0.07), (-0.8, 0.0)]
skirt = common.lathe("Skirt", SKIRT, segments=12)
torso_parts.append(part(skirt, 0))
ring = cyl(0.075, 0.02, (0, 0, -0.785), 2, verts=12)                            # светящееся сопло
torso_parts.append(ring)
torso = join(torso_parts, "RobotTorso")
common.export_fbx(torso, os.path.join(OUT, "Torso.fbx"))

# ---------------------------------------------------------------- кисть (для бросков; в боксе — перчатка)
hand_parts = [
    ico(0.055, (0, -0.015, 0), 0, scale=(0.9, 1.25, 0.75), subdiv=2),
    box((0.03, 0.05, 0.03), (0.045, -0.01, 0.01), 1, bevel=0.008),           # большой палец
    cyl(0.045, 0.04, (0, 0.05, 0), 1, rot=(math.radians(90), 0, 0), verts=10),  # запястье
]
hand = join(hand_parts, "RobotHand")
common.export_fbx(hand, os.path.join(OUT, "Hand.fbx"))

# ---------------------------------------------------------------- превью
if "--preview" in sys.argv:
    head.location = (0, 0, 1.52)
    torso.location = (0, 0, 1.37)
    hand.location = (0.32, -0.1, 1.05)
    cam = common.render_preview(sys.argv[sys.argv.index("--preview") + 1])
    cam.location = (1.0, -2.4, 1.4)
    cam.rotation_euler = (math.radians(88), 0, math.radians(22))
    cam.data.lens = 50
    bpy.ops.render.render(write_still=True)
    print("[preview] готово")
