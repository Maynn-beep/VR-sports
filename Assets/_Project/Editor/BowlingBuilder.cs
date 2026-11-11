using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSports.Modes.Bowling;
using static VRSports.EditorTools.SceneBuilder;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сцена боулинга. Размеры дорожки — по стандарту (в метрах):
    ///   • от линии заступа до первой кегли — 18.29 м (60 футов);
    ///   • ширина дорожки — 1.054 м, по бокам жёлоба по 0.235 м;
    ///   • кегли стоят треугольником с шагом 0.3048 м (12 дюймов).
    ///
    /// Дорожка из трёх участков с разным трением (как у настоящей, смазанной маслом):
    ///   смазанный (12.2 м) → сухой → площадка кеглей.
    /// </summary>
    internal static class BowlingBuilder
    {
        private const float FoulZ = 1f;                 // линия заступа
        private const float HeadPinZ = FoulZ + 18.29f;  // первая кегля
        private const float OilEndZ = FoulZ + 12.2f;    // конец смазанной зоны
        private const float DeckStartZ = HeadPinZ - 0.5f;
        private const float DeckEndZ = HeadPinZ + 0.9f;
        private const float PitEndZ = DeckEndZ + 1.0f;

        private const float LaneHalfW = 0.527f;
        private const float GutterW = 0.235f;
        private const float GutterTopY = -0.09f;
        private const float PinSpacing = 0.3048f;

        private static readonly Vector3 RackPos = new Vector3(0.85f, 0f, 0.2f);

        public static readonly ModeLayout Layout = new ModeLayout
        {
            // Пол только в зоне разбега — на дорожку телепортироваться нельзя.
            FloorCenter = new Vector3(0, -0.05f, -1.5f),
            FloorSize = new Vector3(8f, 0.1f, 5f),
            BoardPosition = new Vector3(-2.0f, 0f, 3.0f),
        };

        public static void Build(BowlingMode mode)
        {
            var root = new GameObject("Lane").transform;

            // ---------- материалы
            var oilMat = Mat("Lane_Oil", new Color(0.88f, 0.74f, 0.52f));
            var dryMat = Mat("Lane_Dry", new Color(0.8f, 0.64f, 0.43f));
            var gutterMat = Mat("Gutter", new Color(0.25f, 0.25f, 0.28f));
            var wallMat = Mat("Lane_Wall", new Color(0.18f, 0.2f, 0.3f));
            var carpetMat = Mat("Carpet", new Color(0.3f, 0.22f, 0.4f));
            var darkMat = Mat("Panel_Dark", new Color(0.12f, 0.14f, 0.2f));
            var redMat = Mat("Foul_Red", new Color(0.85f, 0.15f, 0.15f));

            // Трение. У дорожки 1.0 и режим Minimum: действует трение ШАРА, а шар меняет
            // свой материал по зонам (BowlingBall.ZoneMaterial). Кегли на дорожке — 0.35.
            var lanePhys = PhysMat("Lane_Surface", 1f, 0f, PhysicsMaterialCombine.Minimum);
            var ballApproach = PhysMat("Ball_Approach", 0.5f, 0.05f, PhysicsMaterialCombine.Minimum);
            var ballOil = PhysMat("Ball_Oil", 0.04f, 0.05f, PhysicsMaterialCombine.Minimum);
            var ballDry = PhysMat("Ball_Dry", 0.22f, 0.05f, PhysicsMaterialCombine.Minimum);
            var ballDeck = PhysMat("Ball_Deck", 0.35f, 0.05f, PhysicsMaterialCombine.Minimum);
            var wallPhys = PhysMat("Lane_Wall", 0.3f, 0.2f);
            var pinPhys = PhysMat("Pin", 0.35f, 0.3f);

            // ---------- дорожка: три участка — только визуал (разный цвет)
            LaneSegment(root, "Lane_Oil", FoulZ, OilEndZ, oilMat);
            LaneSegment(root, "Lane_Dry", OilEndZ, DeckStartZ, dryMat);
            LaneSegment(root, "Pin_Deck", DeckStartZ, DeckEndZ, dryMat);

            // ...а физически дорожка — ОДИН сплошной коллайдер. Он начинается ещё в зоне разбега,
            // чтобы на пути шара не было ни одного стыка (на стыках PhysX даёт подскок).
            float surfStart = Layout.FloorCenter.z - Layout.FloorSize.z / 2f;
            var surface = new GameObject("Lane_Collider");
            surface.transform.SetParent(root, false);
            surface.transform.localPosition = new Vector3(0, -0.05f, (surfStart + DeckEndZ) / 2f);
            var surfCol = surface.AddComponent<BoxCollider>();
            surfCol.size = new Vector3(LaneHalfW * 2, 0.1f, DeckEndZ - surfStart);
            surfCol.sharedMaterial = lanePhys;

            // Жёлоба — ниже дорожки; шар, попавший туда, доезжает до ямы мимо кеглей.
            float gutterX = LaneHalfW + GutterW / 2f;
            float laneMidZ = (FoulZ + DeckEndZ) / 2f, laneLen = DeckEndZ - FoulZ;
            foreach (float side in new[] { -1f, 1f })
            {
                var g = Prim(PrimitiveType.Cube, "Gutter", root, new Vector3(side * gutterX, GutterTopY - 0.05f, laneMidZ),
                    new Vector3(GutterW, 0.1f, laneLen), gutterMat);
                g.GetComponent<Collider>().sharedMaterial = wallPhys;

                // Бортики (kickbacks) вдоль всей дорожки и ямы.
                float wallX = LaneHalfW + GutterW + 0.04f;
                var w = Prim(PrimitiveType.Cube, "Kickback", root,
                    new Vector3(side * wallX, 0.105f, (FoulZ + PitEndZ) / 2f),
                    new Vector3(0.08f, 0.49f, PitEndZ - FoulZ), wallMat);
                w.GetComponent<Collider>().sharedMaterial = wallPhys;

                // Ковровое покрытие по бокам, на одном уровне с полом разбега.
                Prim(PrimitiveType.Cube, "Carpet", root, new Vector3(side * 2.42f, -0.05f, (FoulZ + 23f) / 2f),
                    new Vector3(3.16f, 0.1f, 23f - FoulZ), carpetMat);
            }

            // Линия заступа и прицельные точки (без коллайдеров — только визуал).
            Decal(root, "FoulLine", new Vector3(0, 0.001f, FoulZ), new Vector3(LaneHalfW * 2, 0.002f, 0.025f), redMat, PrimitiveType.Cube);
            for (int k = -3; k <= 3; k++)
                Decal(root, "AimDot", new Vector3(k * 0.135f, 0.001f, FoulZ + 4.57f), new Vector3(0.03f, 0.001f, 0.03f),
                    darkMat, PrimitiveType.Cylinder);

            // ---------- яма и задняя стенка
            Prim(PrimitiveType.Cube, "PitFloor", root, new Vector3(0, -0.55f, (DeckEndZ + PitEndZ) / 2f),
                new Vector3(1.7f, 0.1f, PitEndZ - DeckEndZ), wallMat);
            Prim(PrimitiveType.Cube, "BackCushion", root, new Vector3(0, 0.2f, PitEndZ + 0.05f),
                new Vector3(1.75f, 1.5f, 0.1f), darkMat);

            var pitGo = new GameObject("PitTrigger");
            pitGo.transform.SetParent(root, false);
            pitGo.transform.localPosition = new Vector3(0, -0.27f, (DeckEndZ + PitEndZ) / 2f);
            var pitCol = pitGo.AddComponent<BoxCollider>();
            pitCol.isTrigger = true;
            pitCol.size = new Vector3(1.7f, 0.45f, PitEndZ - DeckEndZ);
            pitGo.AddComponent<BowlingPit>().Setup(mode);

            // Козырёк над кеглями с надписью (как в боулинг-клубе).
            Prim(PrimitiveType.Cube, "Masking", root, new Vector3(0, 1.3f, HeadPinZ + 0.4f), new Vector3(1.75f, 0.6f, 0.08f), darkMat);
            Text("MaskingText", root, new Vector3(0, 1.3f, HeadPinZ + 0.35f), "VR SPORTS  •  BOWLING", 3f,
                new Vector2(1.7f, 0.5f), new Color(1f, 0.85f, 0.4f));

            // ---------- кегли
            var pinMesh = LoadMesh("Assets/_Project/Models/Bowling/Pin.fbx");
            var ballMesh = LoadMesh("Assets/_Project/Models/Bowling/Ball.fbx");
            if (pinMesh != null && pinMesh.bounds.size.y < 0.3f)
                Debug.LogWarning($"[BowlingBuilder] Высота кегли {pinMesh.bounds.size.y:F3} м — проверьте оси экспорта FBX.");

            var pinWhite = Mat("Pin_White", new Color(0.95f, 0.95f, 0.92f));
            var pinRed = Mat("Pin_Red", new Color(0.8f, 0.1f, 0.1f));
            var pinsRoot = new GameObject("Pins").transform;
            var pins = new BowlingPin[10];
            int n = 0;
            for (int row = 0; row < 4; row++)          // треугольник 1-2-3-4
            {
                for (int i = 0; i <= row; i++)
                {
                    var pos = new Vector3((i - row / 2f) * PinSpacing, 0f, HeadPinZ + row * PinSpacing * 0.866f);
                    var pin = MeshObject($"Pin_{n + 1}", pinsRoot, pos, pinMesh, pinWhite, pinRed);

                    var col = pin.AddComponent<MeshCollider>();
                    col.sharedMesh = pinMesh;
                    col.convex = true; // динамическим телам нужен выпуклый коллайдер
                    col.sharedMaterial = pinPhys;

                    var rb = pin.AddComponent<Rigidbody>();
                    rb.mass = 1.5f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                    pins[n++] = pin.AddComponent<BowlingPin>();
                }
            }

            // ---------- подставка возврата шара
            var rack = new GameObject("BallRack").transform;
            rack.position = RackPos;
            Prim(PrimitiveType.Cube, "Stand", rack, new Vector3(0, 0.35f, 0), new Vector3(0.35f, 0.7f, 0.6f), wallMat);
            foreach (float side in new[] { -1f, 1f })
            {
                var rail = Prim(PrimitiveType.Cylinder, "Rail", rack, new Vector3(side * 0.05f, 0.72f, 0),
                    new Vector3(0.03f, 0.28f, 0.03f), gutterMat);
                rail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, "Stop", rack, new Vector3(0, 0.75f, side * 0.29f),
                    new Vector3(0.16f, 0.07f, 0.02f), gutterMat);
            }
            var rackPoint = new GameObject("RackPoint").transform;
            rackPoint.SetParent(rack, false);
            rackPoint.localPosition = new Vector3(0, 0.86f, 0);

            // ---------- шар
            var ballGo = MeshObject("Ball", null, rackPoint.position, ballMesh,
                Mat("Ball_Body", new Color(0.25f, 0.3f, 0.85f)), Mat("Ball_Holes", new Color(0.03f, 0.03f, 0.05f)));
            var sphere = ballGo.AddComponent<SphereCollider>();
            sphere.radius = 0.109f;
            sphere.sharedMaterial = ballApproach;

            var ballRb = ballGo.AddComponent<Rigidbody>();
            ballRb.mass = 6.5f;                 // ~14 фунтов
            ballRb.linearDamping = 0f;
            ballRb.angularDamping = 0.05f;
            ballRb.interpolation = RigidbodyInterpolation.Interpolate;
            // Непрерывное обнаружение столкновений: быстрый шар не «пролетит» сквозь кеглю за один шаг.
            ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = ballGo.AddComponent<XRGrabInteractable>();
            ballGo.AddComponent<VRSports.Core.NearOnlyGrab>(); // без захвата лучом издалека
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = true;          // шар берётся там, где его коснулась рука
            grab.throwOnDetach = true;
            grab.throwSmoothingDuration = 0.12f;   // усреднение скорости руки перед отпусканием
            grab.throwVelocityScale = 1.15f;
            grab.throwAngularVelocityScale = 1f;

            var ball = ballGo.AddComponent<BowlingBall>();
            ball.Setup(rackPoint, FoulZ);
            ball.SetupZones(OilEndZ, DeckStartZ, ballApproach, ballOil, ballDry, ballDeck);

            // ---------- табло фреймов над дорожкой
            var sheetRoot = new GameObject("FrameSheet").transform;
            sheetRoot.position = new Vector3(0, 0, 7f);
            Prim(PrimitiveType.Cube, "Panel", sheetRoot, new Vector3(0, 2.9f, 0), new Vector3(3.7f, 0.8f, 0.05f), darkMat);
            var sheetText = Text("Frames", sheetRoot, new Vector3(0, 2.9f, -0.04f), "", 1.2f, new Vector2(3.6f, 0.75f), Color.white);
            var sheet = sheetRoot.gameObject.AddComponent<BowlingScoreSheet>();
            sheet.Setup(sheetText);

            mode.Setup(ball, pins, sheet);
        }

        private static void LaneSegment(Transform root, string name, float z0, float z1, Material mat)
        {
            var seg = Prim(PrimitiveType.Cube, name, root, new Vector3(0, -0.05f, (z0 + z1) / 2f),
                new Vector3(LaneHalfW * 2, 0.1f, z1 - z0), mat);
            Object.DestroyImmediate(seg.GetComponent<Collider>()); // физику даёт общий Lane_Collider
        }

        private static void Decal(Transform root, string name, Vector3 pos, Vector3 scale, Material mat, PrimitiveType type)
        {
            var d = Prim(type, name, root, pos, scale, mat);
            Object.DestroyImmediate(d.GetComponent<Collider>());
        }
    }
}
