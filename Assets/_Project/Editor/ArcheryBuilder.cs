using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSports.Modes.Archery;
using static VRSports.EditorTools.SceneBuilder;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сцена стрельбища: площадка для стрелка, поле до 35 м, три мишени на стойках,
    /// метатель тарелочек, флаг ветра и подставка с луком.
    /// </summary>
    internal static class ArcheryBuilder
    {
        private const string Models = "Assets/_Project/Models/Archery/";
        private const float StringZ = -0.22f;   // тетива в покое (из модели: кончики плеч на z = −0.22)
        private const float TipY = 0.68f;
        private static readonly Vector3 RackPos = new Vector3(0.6f, 1.15f, 0.5f);

        public static readonly ModeLayout Layout = new ModeLayout
        {
            // Телепорт — только по площадке стрелка; на поле выйти нельзя.
            FloorCenter = new Vector3(0, -0.05f, 0),
            FloorSize = new Vector3(6f, 0.1f, 4f),
            BoardPosition = new Vector3(-2.6f, 0f, 2.2f),
        };

        public static void Build(ArcheryMode mode)
        {
            var grass = Mat("Field_Grass", new Color(0.4f, 0.65f, 0.32f));
            var woodDark = Mat("Wood_Dark", new Color(0.35f, 0.22f, 0.12f));
            var steel = Mat("Steel", new Color(0.35f, 0.37f, 0.42f));
            var flagMat = Mat("Flag", new Color(1f, 0.5f, 0.1f));

            // ---------- поле (в него втыкаются промахи)
            // Поле до 60 м: максимальный выстрел под 45° (~64 м) улетает чуть дальше края.
            Prim(PrimitiveType.Cube, "Field", null, new Vector3(0, -0.06f, 27.5f), new Vector3(40f, 0.1f, 65f), grass);
            for (int d = 10; d <= 50; d += 10)
            {
                var t = Text($"Dist_{d}", null, new Vector3(-6f, 0.01f, d), $"{d} м", 6f, new Vector2(3f, 1f), Color.white);
                t.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // ---------- подставка и лук
            Prim(PrimitiveType.Cube, "BowStand", null, new Vector3(RackPos.x, 0.45f, RackPos.z), new Vector3(0.25f, 0.9f, 0.25f), woodDark);
            var rack = new GameObject("BowRackPoint").transform;
            rack.SetPositionAndRotation(RackPos, Quaternion.identity);

            var bowGo = MeshObject("Bow", null, RackPos, LoadMesh(Models + "Bow.fbx"),
                Mat("Bow_Wood", new Color(0.45f, 0.25f, 0.1f)), Mat("Bow_Grip", new Color(0.1f, 0.08f, 0.07f)));
            var bowRb = bowGo.AddComponent<Rigidbody>();
            bowRb.isKinematic = true; // лук не падает: отпущенный — возвращается на подставку
            bowRb.interpolation = RigidbodyInterpolation.Interpolate;

            var gripCol = new GameObject("GripCollider");
            gripCol.transform.SetParent(bowGo.transform, false);
            var gc = gripCol.AddComponent<BoxCollider>();
            gc.size = new Vector3(0.05f, 0.3f, 0.07f);

            var grab = bowGo.AddComponent<BowGrabInteractable>(); // без перехвата второй рукой
            bowGo.AddComponent<VRSports.Core.NearOnlyGrab>();      // брать только рукой вблизи
            grab.colliders.Clear();
            grab.colliders.Add(gc);                // только рукоять — тетива ловится отдельно
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous; // лук жёстко в руке
            grab.useDynamicAttach = false;         // точка хвата фиксирована: ось лука = направление контроллера
            grab.throwOnDetach = false;

            // Точки тетивы.
            var top = Child(bowGo.transform, "TopTip", new Vector3(0, TipY, StringZ));
            var bottom = Child(bowGo.transform, "BottomTip", new Vector3(0, -TipY, StringZ));
            var rest = Child(bowGo.transform, "StringRest", new Vector3(0, 0, StringZ));

            // Хвостовик (место, где берут тетиву).
            var nockGo = new GameObject("Nock");
            nockGo.transform.SetParent(bowGo.transform, false);
            nockGo.transform.localPosition = rest.localPosition;
            var nc = nockGo.AddComponent<SphereCollider>();
            nc.radius = 0.08f;
            // НЕ триггер: ближний захват руки (SphereInteractionCaster) в XR Origin игнорирует триггеры.
            nc.isTrigger = false;
            var nockInteractable = nockGo.AddComponent<XRSimpleInteractable>();
            nockGo.AddComponent<VRSports.Core.NearOnlyGrab>();

            // Тетива — LineRenderer на дочернем объекте (на одном объекте не может быть двух рендереров).
            var line = Child(bowGo.transform, "String", Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.widthMultiplier = 0.004f;
            line.sharedMaterial = UnlitMat("String", new Color(0.92f, 0.9f, 0.85f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.SetPositions(new[] { top.localPosition, rest.localPosition, bottom.localPosition });

            var ammo = Text("Ammo", bowGo.transform, new Vector3(0.09f, -0.12f, 0f), "", 0.35f, new Vector2(0.3f, 0.06f),
                new Color(1f, 0.9f, 0.5f));

            // ---------- образец стрелы (неактивный, лук клонирует его)
            var arrowGo = MeshObject("ArrowTemplate", null, new Vector3(0, -10, 0), LoadMesh(Models + "Arrow.fbx"),
                Mat("Arrow_Shaft", new Color(0.85f, 0.75f, 0.55f)), Mat("Arrow_Head", new Color(0.6f, 0.62f, 0.66f)),
                Mat("Arrow_Fletch", new Color(0.9f, 0.15f, 0.15f)));
            var arrowTemplate = arrowGo.AddComponent<Arrow>();
            arrowGo.SetActive(false);

            var bow = bowGo.AddComponent<Bow>();
            bow.Setup(nockInteractable, line, top, bottom, rest, arrowTemplate, rack, ammo);

            // ---------- мишени
            var faceMesh = LoadMesh(Models + "TargetFace.fbx");
            var faceMats = new[]
            {
                Mat("Target_Gold", new Color(0.98f, 0.82f, 0.1f)), Mat("Target_Red", new Color(0.9f, 0.15f, 0.12f)),
                Mat("Target_Blue", new Color(0.15f, 0.5f, 0.9f)), Mat("Target_Black", new Color(0.08f, 0.08f, 0.08f)),
                Mat("Target_White", new Color(0.95f, 0.95f, 0.95f)), Mat("Target_Straw", new Color(0.8f, 0.68f, 0.38f)),
            };
            var targets = new ArcheryTarget[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var root = new GameObject($"Target_{i + 1}");
                root.transform.position = new Vector3(i * 3f - 3f, 0, 12f + i * 6f);
                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var face = MeshObject("Face", root.transform, new Vector3(0, 1.3f, 0), faceMesh, faceMats);
                var mc = face.AddComponent<MeshCollider>();
                mc.sharedMesh = faceMesh; // невыпуклый допустим: тело кинематическое
                var center = Child(face.transform, "FaceCenter", new Vector3(0, 0, -0.04f));

                foreach (float side in new[] { -1f, 1f })
                {
                    var leg = Prim(PrimitiveType.Cube, "Leg", root.transform, new Vector3(side * 0.28f, 0.65f, 0.12f),
                        new Vector3(0.05f, 1.35f, 0.05f), woodDark);
                    leg.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                    GameObjectUtility.SetStaticEditorFlags(leg, 0);
                }
                var back = Prim(PrimitiveType.Cube, "BackLeg", root.transform, new Vector3(0, 0.6f, 0.45f),
                    new Vector3(0.05f, 1.3f, 0.05f), woodDark);
                back.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                GameObjectUtility.SetStaticEditorFlags(back, 0);

                targets[i] = root.AddComponent<ArcheryTarget>();
                targets[i].Setup(center);
            }

            // ---------- тарелочки
            var fx = new GameObject("ShatterFX").AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fx.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.45f, 0.1f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = fx.emission;
            emission.rateOverTime = 0f;
            var shape = fx.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = UnlitParticleMat("Shards", new Color(1f, 0.5f, 0.15f));

            var discMesh = LoadMesh(Models + "ClayDisc.fbx");
            var clayMat = Mat("Clay", new Color(1f, 0.45f, 0.1f));
            var discs = new ClayDisc[6];
            for (int i = 0; i < discs.Length; i++)
            {
                var d = MeshObject($"ClayDisc_{i + 1}", null, Vector3.zero, discMesh, clayMat);
                d.transform.localScale = Vector3.one * 2f; // 22 см вместо 11 — видно в шлеме
                discs[i] = d.AddComponent<ClayDisc>();
                discs[i].Setup(fx);
                d.SetActive(false);
            }
            var thrower = new GameObject("ClayThrower").AddComponent<ClayThrower>();
            thrower.Setup(discs);
            // Будки метателей по бокам поля.
            foreach (float side in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "ThrowerHouse", null, new Vector3(side * 7.6f, 0.5f, 11f), new Vector3(1f, 1f, 6.5f), steel);

            // ---------- флаг ветра
            var flagRoot = new GameObject("WindFlag").transform;
            flagRoot.position = new Vector3(2.2f, 0, 7f);
            Prim(PrimitiveType.Cylinder, "Pole", flagRoot, new Vector3(0, 1.5f, 0), new Vector3(0.05f, 1.5f, 0.05f), steel);
            var pivot = Child(flagRoot, "FlagPivot", new Vector3(0, 2.95f, 0));
            var cloth = Prim(PrimitiveType.Cube, "Cloth", pivot, new Vector3(0, -0.15f, 0.4f), new Vector3(0.02f, 0.3f, 0.8f), flagMat);
            Object.DestroyImmediate(cloth.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(cloth, 0);
            var windLabel = Text("WindLabel", flagRoot, new Vector3(0, 3.35f, 0), "", 2f, new Vector2(2.5f, 0.4f), Color.white);
            flagRoot.gameObject.AddComponent<WindFlag>().Setup(pivot, windLabel);

            mode.Setup(bow, targets, thrower);
        }

        private static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static Material UnlitMat(string name, Color c) => ShaderMat(name, "Universal Render Pipeline/Unlit", c);
        private static Material UnlitParticleMat(string name, Color c) => ShaderMat(name, "Universal Render Pipeline/Particles/Unlit", c);

        private static Material ShaderMat(string name, string shader, Color c)
        {
            string path = $"Assets/_Project/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
