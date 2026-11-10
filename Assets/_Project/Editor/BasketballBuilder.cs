using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSports.Modes.Basketball;
using static VRSports.EditorTools.SceneBuilder;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сцена баскетбола. Размеры по правилам FIBA:
    ///   • высота кольца — 3.05 м, внутренний диаметр обода — 45.7 см;
    ///   • щит 1.80 × 1.05 м, обод выступает от щита на 15 см;
    ///   • мяч №7 — 24 см, 0.62 кг.
    /// Расстояния аркадные: старт в 4 м от кольца, 3-очковая дуга — 5.5 м.
    /// </summary>
    internal static class BasketballBuilder
    {
        private const float RimZ = 4f;
        private const float RimY = 3.05f;
        private const float RimR = 0.2286f;
        private const float RimTube = 0.009f;
        private const float BallR = 0.12f;
        private const int BallCount = 4;

        public static readonly ModeLayout Layout = new ModeLayout
        {
            BoardPosition = new Vector3(-2.6f, 0f, 3.0f),
        };

        public static void Build(BasketballMode mode)
        {
            // ---------- материалы
            var boardMat = Mat("Backboard", new Color(0.95f, 0.95f, 0.97f));
            var redMat = Mat("Foul_Red", new Color(0.85f, 0.15f, 0.15f));
            var steelMat = Mat("Steel", new Color(0.35f, 0.37f, 0.42f));
            var rimMat = Mat("Rim", new Color(0.92f, 0.32f, 0.1f));
            var netMat = Mat("Net", new Color(0.95f, 0.95f, 0.95f));
            var lineMat = Mat("Court_Line", new Color(0.97f, 0.97f, 0.97f));
            var trayMat = Mat("Lane_Wall", new Color(0.18f, 0.2f, 0.3f));
            var ballMat = Mat("Basketball", new Color(0.9f, 0.42f, 0.12f));
            var seamMat = Mat("Basketball_Seams", new Color(0.05f, 0.04f, 0.04f));
            var fireMat = Mat("Basketball_Fire", new Color(1f, 0.45f, 0.05f));
            fireMat.EnableKeyword("_EMISSION");
            fireMat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0f) * 2.5f);

            // Упругость: у мяча 0.75, режим Average — отскок зависит и от поверхности.
            var ballPhys = PhysMat("Basketball", 0.6f, 0.75f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);
            var floorPhys = PhysMat("Court_Floor", 0.6f, 0.85f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);
            var rimPhys = PhysMat("Rim", 0.5f, 0.4f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);
            // Щит «мягче» и шершавее: упругость 0.35 (итог с мячом 0.55), трение 0.6 —
            // чтобы обратное вращение успевало «сцепиться» со щитом и увести мяч вниз.
            var boardPhys = PhysMat("Backboard", 0.6f, 0.35f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);

            // Пол из общего сборщика: отмечаем его как «пол» и даём упругий материал.
            var floor = GameObject.Find("Floor");
            floor.AddComponent<BasketballFloor>();
            floor.GetComponent<Collider>().sharedMaterial = floorPhys;

            // ---------- стойка (вся двигается вместе: кинематическое тело)
            var hoopRoot = new GameObject("Hoop");
            hoopRoot.transform.position = new Vector3(0, 0, RimZ);
            var hoopRb = hoopRoot.AddComponent<Rigidbody>();
            hoopRb.isKinematic = true;
            hoopRb.interpolation = RigidbodyInterpolation.Interpolate;
            var h = hoopRoot.transform;

            float boardZ = RimR + RimTube * 2 + 0.15f;    // передняя плоскость щита
            var board = Prim(PrimitiveType.Cube, "Backboard", h, new Vector3(0, 2.9f + 0.525f, boardZ + 0.02f),
                new Vector3(1.8f, 1.05f, 0.04f), boardMat);
            board.GetComponent<Collider>().sharedMaterial = boardPhys;
            Unstatic(board);

            // Красный прямоугольник-прицел над кольцом (0.59 × 0.45 м) из четырёх полос.
            float fz = boardZ - 0.003f, cx = 0.59f / 2f, top = RimY + 0.45f;
            Bar(h, new Vector3(0, top, fz), new Vector3(0.59f, 0.05f, 0.004f), redMat);
            Bar(h, new Vector3(0, RimY + 0.025f, fz), new Vector3(0.59f, 0.05f, 0.004f), redMat);
            Bar(h, new Vector3(-cx, RimY + 0.225f, fz), new Vector3(0.05f, 0.45f, 0.004f), redMat);
            Bar(h, new Vector3(cx, RimY + 0.225f, fz), new Vector3(0.05f, 0.45f, 0.004f), redMat);

            // Опора: столб за щитом, вынос и основание.
            Unstatic(Prim(PrimitiveType.Cylinder, "Pole", h, new Vector3(0, 1.75f, boardZ + 1.1f), new Vector3(0.2f, 1.75f, 0.2f), steelMat));
            Unstatic(Prim(PrimitiveType.Cube, "Arm", h, new Vector3(0, 3.3f, boardZ + 0.58f), new Vector3(0.12f, 0.12f, 1.05f), steelMat));
            Unstatic(Prim(PrimitiveType.Cube, "Base", h, new Vector3(0, 0.15f, boardZ + 1.1f), new Vector3(0.9f, 0.3f, 0.9f), steelMat));
            Unstatic(Prim(PrimitiveType.Cube, "RimMount", h, new Vector3(0, RimY, RimR + RimTube * 2 + 0.07f),
                new Vector3(0.08f, 0.02f, 0.16f), rimMat));

            // Обод: модель из Blender + 16 капсул-коллайдеров по окружности.
            // Один MeshCollider-тор был бы невыпуклым; капсулы — точная и дешёвая замена.
            var rimCenter = new GameObject("RimCenter").transform;
            rimCenter.SetParent(h, false);
            rimCenter.localPosition = new Vector3(0, RimY, 0);
            var rimVis = MeshObject("Rim", rimCenter, Vector3.zero, LoadMesh("Assets/_Project/Models/Basketball/Rim.fbx"), rimMat);
            rimVis.transform.localRotation = Quaternion.identity;

            const int segments = 16;
            float ringR = RimR + RimTube;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var seg = new GameObject($"RimCollider_{i}");
                seg.transform.SetParent(rimCenter, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a) * ringR, 0, Mathf.Sin(a) * ringR);
                seg.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
                var cap = seg.AddComponent<CapsuleCollider>();
                cap.direction = 2; // ось капсулы — локальная Z (касательная к окружности)
                cap.radius = RimTube;
                cap.height = 2f * Mathf.PI * ringR / segments + RimTube * 2f;
                cap.sharedMaterial = rimPhys;
            }

            // Сетка — только визуал (без коллайдера); тормозит мяч нижний датчик.
            var net = MeshObject("Net", rimCenter, Vector3.zero, LoadMesh("Assets/_Project/Models/Basketball/Net.fbx"), netMat);

            // Датчики попадания.
            Sensor(h, "SensorTop", new Vector3(0, RimY + 0.05f, 0), new Vector3(0.30f, 0.05f, 0.30f), true);
            Sensor(h, "SensorBottom", new Vector3(0, RimY - 0.32f, 0), new Vector3(0.26f, 0.06f, 0.26f), false);

            var hoop = hoopRoot.AddComponent<Hoop>();
            hoop.Setup(rimCenter, net.transform);
            foreach (var s in hoopRoot.GetComponentsInChildren<HoopSensor>()) s.Setup(hoop, s.name == "SensorTop");

            // ---------- разметка: 3-очковая дуга и подпись
            var lines = new GameObject("CourtLines").transform;
            float r3 = mode.ThreePointRadius;
            for (int i = 0; i <= 36; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 36f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * r3, 0.002f, RimZ - Mathf.Cos(a) * r3);
                var dash = Prim(PrimitiveType.Cube, "Arc", lines, p, new Vector3(0.05f, 0.002f, 0.22f), lineMat);
                dash.transform.rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
                Object.DestroyImmediate(dash.GetComponent<Collider>());
            }
            var label = Text("ThreeLabel", lines, new Vector3(0, 0.004f, RimZ - r3 - 0.6f), "3 ОЧКА", 4f,
                new Vector2(2f, 0.5f), new Color(1f, 1f, 1f, 0.85f));
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // лежит на полу, читается от игрока

            // Счётчик серии над щитом.
            var combo = Text("Combo", null, new Vector3(0, 4.25f, RimZ + boardZ - 0.05f), "", 3f,
                new Vector2(3f, 0.5f), new Color(1f, 0.85f, 0.3f));
            combo.transform.SetParent(h, true);

            // ---------- лоток с мячами
            var tray = new GameObject("BallTray").transform;
            tray.position = new Vector3(0.65f, 0, 0.45f);
            Prim(PrimitiveType.Cube, "Table", tray, new Vector3(0, 0.4f, 0), new Vector3(0.62f, 0.8f, 0.62f), trayMat);
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(0, 0.85f, 0.3f), new Vector3(0.62f, 0.1f, 0.02f)),
                         (new Vector3(0, 0.85f, -0.3f), new Vector3(0.62f, 0.1f, 0.02f)),
                         (new Vector3(0.3f, 0.85f, 0), new Vector3(0.02f, 0.1f, 0.62f)),
                         (new Vector3(-0.3f, 0.85f, 0), new Vector3(0.02f, 0.1f, 0.62f)),
                     })
                Prim(PrimitiveType.Cube, "TrayWall", tray, pos, size, trayMat);

            const string trailPath = "Assets/_Project/Materials/Fire_Trail.mat";
            var trailMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(trailPath);
            if (trailMat == null)
            {
                trailMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                UnityEditor.AssetDatabase.CreateAsset(trailMat, trailPath);
            }
            trailMat.SetColor("_BaseColor", new Color(1f, 0.5f, 0.1f, 1f));

            var ballMesh = LoadMesh("Assets/_Project/Models/Basketball/Basketball.fbx");
            var balls = new BasketballBall[BallCount];
            for (int i = 0; i < BallCount; i++)
            {
                var spawn = new GameObject($"Spawn_{i}").transform;
                spawn.SetParent(tray, false);
                spawn.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * 0.14f, 0.8f + BallR + 0.03f, (i < 2 ? -1 : 1) * 0.14f);

                var go = MeshObject($"Basketball_{i + 1}", null, spawn.position, ballMesh, ballMat, seamMat);
                var col = go.AddComponent<SphereCollider>();
                col.radius = BallR;
                col.sharedMaterial = ballPhys;

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.62f;
                rb.linearDamping = 0f;      // сопротивление воздуха считаем сами (квадратичное)
                rb.angularDamping = 0.1f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var grab = go.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.useDynamicAttach = true;
                grab.throwOnDetach = true;
                grab.throwSmoothingDuration = 0.1f;
                grab.throwVelocityScale = 1.1f;
                grab.throwAngularVelocityScale = 1f;

                var trail = go.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.widthMultiplier = 0.2f;
                trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trail.sharedMaterial = trailMat;
                trail.emitting = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                balls[i] = go.AddComponent<BasketballBall>();
                balls[i].Setup(spawn, fireMat, trail);
            }

            mode.Setup(balls, hoop, combo);
        }

        private static void Sensor(Transform parent, string name, Vector3 pos, Vector3 size, bool top)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            go.AddComponent<HoopSensor>().Setup(null, top);
        }

        private static void Bar(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var b = Prim(PrimitiveType.Cube, "TargetLine", parent, pos, size, mat);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            Unstatic(b);
        }

        /// <summary>Части движущейся стойки не должны быть static (их нельзя «запечь» в один меш).</summary>
        private static void Unstatic(GameObject go) => UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, 0);
    }
}
