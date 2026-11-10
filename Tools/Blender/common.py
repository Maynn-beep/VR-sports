"""
Общие функции для генерации low-poly моделей в Blender.

Запуск (без интерфейса Blender):
    blender -b --factory-startup -P <скрипт>.py -- <папка_вывода>

Модели строятся из кода: вершины и грани задаются явно, поэтому любую модель
можно воспроизвести и поменять её параметры (размеры, число сегментов).
"""
import math
import os
import sys

import bpy


def output_dir(default):
    """Папка вывода — аргумент после '--' или значение по умолчанию."""
    argv = sys.argv
    if "--" in argv and len(argv) > argv.index("--") + 1:
        return argv[argv.index("--") + 1]
    return default


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_material(name, rgb):
    """Материал-заглушка: в Unity назначаются свои материалы по номеру слота,
    а здесь цвет нужен для превью."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1.0)
    return mat


def lathe(name, profile, segments, band_material=None):
    """
    Тело вращения («токарный станок»): профиль (высота z, радиус r) вращается вокруг оси Z.
    Так в реальности вытачивают кегли, и так же их удобно моделировать.

    profile: список (z, r) снизу вверх; r = 0 у первой/последней точки — полюс.
    band_material(z_center) -> индекс материала для каждого пояса граней.
    """
    verts, faces, mat_idx = [], [], []
    rings = []  # индексы вершин каждого кольца (у полюса — одна вершина)

    for z, r in profile:
        if r < 1e-6:
            rings.append([len(verts)])
            verts.append((0.0, 0.0, z))
        else:
            ring = []
            for s in range(segments):
                a = 2 * math.pi * s / segments
                ring.append(len(verts))
                verts.append((r * math.cos(a), r * math.sin(a), z))
            rings.append(ring)

    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        zc = (profile[i][0] + profile[i + 1][0]) / 2
        m = band_material(zc) if band_material else 0
        if len(a) == 1:      # нижний полюс — веер треугольников
            for s in range(segments):
                faces.append((a[0], b[(s + 1) % segments], b[s]))
                mat_idx.append(m)
        elif len(b) == 1:    # верхний полюс
            for s in range(segments):
                faces.append((a[s], a[(s + 1) % segments], b[0]))
                mat_idx.append(m)
        else:                # обычный пояс из четырёхугольников
            for s in range(segments):
                s2 = (s + 1) % segments
                faces.append((a[s], a[s2], b[s2], b[s]))
                mat_idx.append(m)

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for poly, m in zip(mesh.polygons, mat_idx):
        poly.material_index = m
        poly.use_smooth = False   # плоское затенение — low-poly стиль
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export_fbx(obj, path):
    """
    Экспорт в FBX с настройками под Unity:
      • FBX_SCALE_ALL + bake_space_transform — 1 м в Blender = 1 единица в Unity
        и без лишнего поворота −90° по X (в Blender ось вверх Z, в Unity — Y).
    """
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"[export] {os.path.basename(path)}: {tris} треугольников")


def render_preview(path, objects_offset=None):
    """Быстрый рендер-превью (Workbench), чтобы проверить модель без Unity."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.filepath = path

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam
