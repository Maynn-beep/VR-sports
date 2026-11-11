using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using VRSports.Core;
using VRSports.Modes.Archery;
using VRSports.Modes.Basketball;
using VRSports.Modes.Bowling;
using VRSports.Modes.Boxing;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сборщик сцен: меню «VR Sports → Построить все сцены».
    ///
    /// Сцены не собираются руками, их строит код. Плюсы:
    ///  • сцену можно пересоздать в любой момент, она всегда одинаковая;
    ///  • расположение объектов, размеры и цвета видны в коде, и это легко объяснить;
    ///  • мы (человек и ИИ-ассистент) правим один и тот же источник — этот файл.
    /// </summary>
    public static class SceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenesDir = Root + "/Scenes";
        private const string MatDir = Root + "/Materials";
        private const string FontDir = Root + "/Fonts";

        private const string XrOriginPrefab =
            "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string InterTtf = "Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf";

        private static TMP_FontAsset _font;

        // ---------------------------------------------------------------- меню

        [MenuItem("VR Sports/Построить все сцены")]
        public static void BuildAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        /// <summary>Без диалогов — для вызова из скриптов/автоматизации.</summary>
        public static void BuildAll()
        {
            EnsureFolder(ScenesDir);
            EnsureFolder(MatDir);
            EnsureFolder(FontDir);
            _font = GetOrCreateFont();

            BuildHub();
            BuildMode<BowlingMode>("Bowling", new Color(0.55f, 0.38f, 0.24f), BowlingBuilder.Layout, BowlingBuilder.Build);
            BuildMode<BasketballMode>("Basketball", new Color(0.78f, 0.55f, 0.33f), BasketballBuilder.Layout, BasketballBuilder.Build);
            BuildMode<ArcheryMode>("Archery", new Color(0.55f, 0.42f, 0.28f), ArcheryBuilder.Layout, ArcheryBuilder.Build);
            BuildMode<BoxingMode>("Boxing", new Color(0.3f, 0.34f, 0.48f), BoxingBuilder.Layout, BoxingBuilder.Build);

            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesDir + "/Hub.unity");
            Debug.Log("[SceneBuilder] Все сцены построены.");
        }

        // ---------------------------------------------------------------- хаб

        private static void BuildHub()
        {
            var scene = NewScene();

            // Круглая площадка и трава вокруг.
            var platform = Prim(PrimitiveType.Cylinder, "Platform", null, new Vector3(0, -0.05f, 0),
                new Vector3(10, 0.05f, 10), Mat("Hub_Platform", new Color(0.85f, 0.85f, 0.9f)));
            ReplaceWithMeshCollider(platform);
            MakeTeleportArea(platform);
            Prim(PrimitiveType.Plane, "Ground", null, new Vector3(0, -0.11f, 0), new Vector3(10, 1, 10),
                Mat("Hub_Grass", new Color(0.45f, 0.7f, 0.4f)));

            // Заголовок.
            var title = new GameObject("Title").transform;
            title.position = new Vector3(0, 0, 5.5f);
            Text("Name", title, new Vector3(0, 3.2f, 0), "VR SPORTS", 8f, new Vector2(8, 1.5f), new Color(0.15f, 0.2f, 0.35f));
            Text("Subtitle", title, new Vector3(0, 2.5f, 0), "Сборник мини-спортивных симуляторов", 2f,
                new Vector2(8, 1), new Color(0.25f, 0.3f, 0.45f));

            // Станции режимов — дугой перед игроком.
            Color[] colors =
            {
                new Color(0.85f, 0.35f, 0.3f), new Color(0.95f, 0.6f, 0.2f),
                new Color(0.35f, 0.7f, 0.4f), new Color(0.35f, 0.5f, 0.9f)
            };
            int n = SportCatalog.All.Length;
            const float radius = 3f, arc = 120f;
            for (int i = 0; i < n; i++)
            {
                float angle = Mathf.Lerp(-arc / 2, arc / 2, n == 1 ? 0.5f : i / (float)(n - 1)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                BuildStation(SportCatalog.All[i], dir * radius, Quaternion.LookRotation(dir), colors[i % colors.Length]);
            }

            Save(scene, "Hub");
        }

        private static void BuildStation(SportCatalog.Entry entry, Vector3 pos, Quaternion rot, Color color)
        {
            var root = new GameObject("Station_" + entry.Id).transform;
            root.SetPositionAndRotation(pos, rot);

            Prim(PrimitiveType.Cube, "Pedestal", root, new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.9f, 0.5f),
                Mat("Station_" + entry.Id, color));
            Prim(PrimitiveType.Cube, "Sign", root, new Vector3(0, 1.65f, 0.1f), new Vector3(1.3f, 0.85f, 0.04f),
                Mat("Panel_Dark", new Color(0.12f, 0.14f, 0.2f)));

            // Текст стоит перед табличкой со стороны игрока (локальная −Z).
            var t = Text("Title", root, new Vector3(0, 1.9f, 0.07f), entry.Title, 1.1f, new Vector2(1.25f, 0.2f), Color.white);
            var d = Text("Description", root, new Vector3(0, 1.64f, 0.07f), entry.Description, 0.55f, new Vector2(1.25f, 0.25f),
                new Color(0.8f, 0.85f, 0.95f));
            var b = Text("Best", root, new Vector3(0, 1.38f, 0.07f), "", 0.55f, new Vector2(1.25f, 0.1f),
                new Color(1f, 0.85f, 0.4f));

            var play = Button("PlayButton", root, new Vector3(0, 0.9f, 0), "Играть", new Color(0.3f, 0.8f, 0.4f));

            root.gameObject.AddComponent<HubStation>().Setup(entry.Id, play, t, d, b);
        }

        // ---------------------------------------------------------------- сцена режима

        /// <summary>Где в сцене режима стоят пол и табло (у каждого вида спорта своё).</summary>
        internal class ModeLayout
        {
            public Vector3 FloorCenter = new Vector3(0, -0.05f, 0);
            public Vector3 FloorSize = new Vector3(20, 0.1f, 20);
            public Vector3 BoardPosition = new Vector3(0, 0, 4f);
            public Vector3 ControlsPosition = new Vector3(-0.8f, 0, 0.8f);
        }

        private static void BuildMode<T>(string sceneName, Color floorColor, ModeLayout layout = null,
            System.Action<T> buildContent = null) where T : SportModeBase
        {
            layout ??= new ModeLayout();
            var scene = NewScene();

            var floor = Prim(PrimitiveType.Cube, "Floor", null, layout.FloorCenter, layout.FloorSize,
                Mat("Floor_" + sceneName, floorColor));
            MakeTeleportArea(floor);

            var mode = new GameObject("Mode").AddComponent<T>();

            // Табло. Если оно сбоку — разворачиваем лицом к игроку.
            var board = new GameObject("ScoreBoard").transform;
            var flat = new Vector3(layout.BoardPosition.x, 0, layout.BoardPosition.z);
            board.SetPositionAndRotation(layout.BoardPosition,
                Mathf.Abs(flat.x) < 0.01f ? Quaternion.identity : Quaternion.LookRotation(flat.normalized));
            Prim(PrimitiveType.Cube, "Panel", board, new Vector3(0, 2.2f, 0), new Vector3(2.4f, 1.3f, 0.05f),
                Mat("Panel_Dark", new Color(0.12f, 0.14f, 0.2f)));
            const float z = -0.04f;
            var title = Text("Title", board, new Vector3(0, 2.65f, z), "", 1.6f, new Vector2(2.3f, 0.3f), Color.white);
            var score = Text("Score", board, new Vector3(-0.55f, 2.25f, z), "", 1.4f, new Vector2(1.2f, 0.3f),
                new Color(0.5f, 1f, 0.6f));
            var timer = Text("Timer", board, new Vector3(0.7f, 2.25f, z), "", 1.4f, new Vector2(0.8f, 0.3f), Color.white);
            var best = Text("Best", board, new Vector3(0, 1.95f, z), "", 0.9f, new Vector2(2.3f, 0.2f),
                new Color(1f, 0.85f, 0.4f));
            var msg = Text("Message", board, new Vector3(0, 1.72f, z), "", 1.1f, new Vector2(2.3f, 0.25f),
                new Color(0.75f, 0.85f, 1f));
            board.gameObject.AddComponent<ScoreBoard>().Setup(mode, title, score, timer, best, msg);

            // Пульт слева-спереди на уровне пояса.
            var standPos = layout.ControlsPosition;
            var stand = new GameObject("Controls").transform;
            stand.SetPositionAndRotation(standPos, Quaternion.LookRotation(standPos.normalized));
            Prim(PrimitiveType.Cube, "Stand", stand, new Vector3(0, 0.42f, 0), new Vector3(0.5f, 0.84f, 0.3f),
                Mat("Stand", new Color(0.25f, 0.27f, 0.32f)));
            var start = Button("StartButton", stand, new Vector3(-0.12f, 0.84f, 0), "Старт", new Color(0.3f, 0.8f, 0.4f));
            var hub = Button("HubButton", stand, new Vector3(0.12f, 0.84f, 0), "В хаб", new Color(0.95f, 0.6f, 0.25f));
            stand.gameObject.AddComponent<ModeControls>().Setup(mode, start, hub);

            // «Начинка» конкретного вида спорта (дорожка, кольцо, мишени...).
            buildContent?.Invoke(mode);

            Save(scene, sceneName);
        }

        // ---------------------------------------------------------------- общие части сцены

        private static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Один направленный свет — дёшево для Quest 2.
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Hard; // тени помогают оценивать расстояние при бросках

            // Окружение: процедурное небо и трёхцветный ambient (небо/горизонт/земля).
            RenderSettings.skybox = SkyboxMaterial();
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.75f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);

            // XR Interaction Manager связывает «руки» (interactors) и объекты (interactables).
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            // Игрок: готовый XR Origin из XR Interaction Toolkit (камера, контроллеры, телепорт, повороты).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefab);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var cam = rig.GetComponentInChildren<Camera>(true);
            var fader = cam.gameObject.AddComponent<ScreenFader>();
            var so = new SerializedObject(fader);
            so.FindProperty("fadeMaterial").objectReferenceValue = FadeMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();

            return scene;
        }

        private static void Save(Scene scene, string name)
        {
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{name}.unity");
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene($"{ScenesDir}/Hub.unity", true) };
            foreach (var e in SportCatalog.All)
                scenes.Add(new EditorBuildSettingsScene($"{ScenesDir}/{e.Scene}.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- строительные блоки

        internal static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>У цилиндра-примитива капсульный коллайдер — для плоского диска он неверен.</summary>
        private static void ReplaceWithMeshCollider(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<MeshCollider>();
        }

        private static void MakeTeleportArea(GameObject floor)
        {
            var area = floor.AddComponent<TeleportationArea>();
            // Телепорт-луч игрока работает только со слоем взаимодействия «Teleport».
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport");
        }

        internal static TextMeshPro Text(string name, Transform parent, Vector3 localPos, string content, float size,
            Vector2 rect, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = rect;
            return t;
        }

        internal static VRButton Button(string name, Transform parent, Vector3 localPos, string label, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            var baseGo = Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0, 0.01f, 0),
                new Vector3(0.16f, 0.01f, 0.16f), Mat("Button_Base", new Color(0.15f, 0.15f, 0.18f)));
            Object.DestroyImmediate(baseGo.GetComponent<Collider>());

            // Коллайдер только у «шляпки» — по нему XRSimpleInteractable ловит луч и руку.
            var cap = Prim(PrimitiveType.Cylinder, "Cap", root.transform, new Vector3(0, 0.03f, 0),
                new Vector3(0.12f, 0.015f, 0.12f), Mat("Button_Cap", Color.white));
            GameObjectUtility.SetStaticEditorFlags(cap, 0); // шляпка двигается — не статичная
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            var col = cap.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2.5f, 1f); // чуть выше самой шляпки — легче попасть рукой

            Text("Label", root.transform, new Vector3(0, 0.12f, 0), label, 0.6f, new Vector2(0.4f, 0.12f), Color.white);

            root.AddComponent<XRSimpleInteractable>();
            var button = root.AddComponent<VRButton>();
            button.Setup(cap.transform, cap.GetComponent<Renderer>(), color);

            // Цвет в редакторе: такой же, как в игре (там его задаёт VRButton).
            cap.GetComponent<Renderer>().sharedMaterial = Mat("Button_" + ColorUtility.ToHtmlStringRGB(color), color);
            return button;
        }

        // ---------------------------------------------------------------- ассеты

        /// <summary>
        /// Физический материал: трение и упругость поверхности.
        /// combine — как объединять с материалом второго тела (Minimum: берётся меньшее трение).
        /// </summary>
        internal static PhysicsMaterial PhysMat(string name, float friction, float bounciness,
            PhysicsMaterialCombine combine = PhysicsMaterialCombine.Average,
            PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Minimum)
        {
            string path = $"{MatDir}/Physics/{name}.asset";
            EnsureFolder($"{MatDir}/Physics");
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.dynamicFriction = friction;
            pm.staticFriction = friction;
            pm.bounciness = bounciness;
            pm.frictionCombine = combine;
            pm.bounceCombine = bounceCombine;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        /// <summary>Меш из FBX, сгенерированного Blender-скриптом (Tools/Blender).</summary>
        internal static Mesh LoadMesh(string fbxPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is Mesh m) return m;
            Debug.LogError($"[SceneBuilder] Нет меша в {fbxPath}. Запустите Blender-скрипт из Tools/Blender.");
            return null;
        }

        /// <summary>Объект с мешем из FBX и нашими материалами по слотам.</summary>
        internal static GameObject MeshObject(string name, Transform parent, Vector3 pos, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        internal static Material Mat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                // Simple Lit — упрощённое освещение URP, заметно дешевле Lit на мобильном GPU.
                mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { enableInstancing = true };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material FadeMaterial()
        {
            string path = $"{MatDir}/ScreenFade.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("VRSports/ScreenFade"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material SkyboxMaterial()
        {
            string path = $"{MatDir}/Sky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Skybox/Procedural"));
            mat.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.85f));
            mat.SetColor("_GroundColor", new Color(0.4f, 0.42f, 0.38f));
            mat.SetFloat("_AtmosphereThickness", 0.8f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Шрифт TextMeshPro с кириллицей. Стандартный шрифт TMP русских букв не содержит,
        /// поэтому делаем динамический SDF-шрифт из Inter (лицензия OFL): недостающие
        /// символы добавляются в атлас автоматически.
        /// </summary>
        private static TMP_FontAsset GetOrCreateFont()
        {
            string path = $"{FontDir}/Inter Dynamic SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa != null) return fa;

            var font = AssetDatabase.LoadAssetAtPath<Font>(InterTtf);
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fa.name = "Inter Dynamic SDF";
            AssetDatabase.CreateAsset(fa, path);

            fa.material.name = "Inter Dynamic SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = "Inter Dynamic SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);

            // Сразу кладём в атлас латиницу, кириллицу и цифры.
            const string chars =
                " !\"#%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz«»—–…№" +
                "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя×";
            fa.TryAddCharacters(chars);

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
