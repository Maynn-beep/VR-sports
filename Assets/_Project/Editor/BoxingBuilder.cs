using UnityEditor;
using UnityEngine;
using VRSports.Core;
using VRSports.Modes.Boxing;
using static VRSports.EditorTools.SceneBuilder;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сцена бокса: ринг 5×5 м с канатами, тренажёр с пятью лапами и выдвижной перекладиной,
    /// боксёрская груша на шарнире, перчатки на контроллерах игрока.
    /// </summary>
    internal static class BoxingBuilder
    {
        private const string Models = "Assets/_Project/Models/Boxing/";
        private const float ColumnZ = 1.05f;

        public static readonly ModeLayout Layout = new ModeLayout
        {
            BoardPosition = new Vector3(-3.0f, 0f, 2.6f),
            ControlsPosition = new Vector3(-1.4f, 0f, 0.1f),   // в стороне от зоны ударов
        };

        /// <summary>Лапа: позиция (y = 0 — на 10 см ниже глаз), направление удара, вид, «своя» рука.</summary>
        private readonly struct PadDef
        {
            public readonly Vector3 Pos, Dir;
            public readonly PadKind Kind;
            public readonly Hand Hand;
            public readonly bool Any;
            public PadDef(Vector3 pos, Vector3 dir, PadKind kind, Hand hand, bool any = false)
            {
                Pos = pos; Dir = dir.normalized; Kind = kind; Hand = hand; Any = any;
            }
        }

        // Ориентация лапы задаёт вид удара: ось Z лапы = направление, в котором должен лететь кулак.
        private static readonly PadDef[] Pads =
        {
            // Прямые: лицом к игроку, кулак летит вперёд. Шаг между центрами лап ≥ 30 см.
            new PadDef(new Vector3(-0.25f, -0.03f, 0.58f), Vector3.forward, PadKind.Straight, Hand.Left),
            new PadDef(new Vector3(0.25f, -0.03f, 0.58f), Vector3.forward, PadKind.Straight, Hand.Right),
            new PadDef(new Vector3(-0.28f, -0.45f, 0.5f), Vector3.forward, PadKind.Straight, Hand.Left),
            new PadDef(new Vector3(0.28f, -0.45f, 0.5f), Vector3.forward, PadKind.Straight, Hand.Right),
            new PadDef(new Vector3(0f, -0.24f, 0.64f), Vector3.forward, PadKind.Straight, Hand.Left, any: true),
            // Хуки: по бокам на уровне головы, лицом ВНУТРЬ (к центру) и чуть к игроку.
            // По правой лапе бьёт ЛЕВАЯ рука (кулак идёт слева направо, n = +X), по левой — правая.
            new PadDef(new Vector3(0.52f, -0.05f, 0.42f), new Vector3(1f, 0f, 0.35f), PadKind.Hook, Hand.Left),
            new PadDef(new Vector3(-0.52f, -0.05f, 0.42f), new Vector3(-1f, 0f, 0.35f), PadKind.Hook, Hand.Right),
            // Апперкоты: НАД верхними прямыми лапами, лицом вниз — кулак идёт снизу вверх.
            new PadDef(new Vector3(-0.25f, 0.33f, 0.5f), new Vector3(0f, 1f, 0.2f), PadKind.Uppercut, Hand.Left),
            new PadDef(new Vector3(0.25f, 0.33f, 0.5f), new Vector3(0f, 1f, 0.2f), PadKind.Uppercut, Hand.Right),
        };

        public static void Build(BoxingMode mode)
        {
            var dark = Mat("Steel", new Color(0.35f, 0.37f, 0.42f));
            var post = Mat("Ring_Post", new Color(0.85f, 0.85f, 0.88f));
            var rope = Mat("Ring_Rope", new Color(0.9f, 0.15f, 0.15f));
            var canvas = Mat("Ring_Canvas", new Color(0.82f, 0.82f, 0.78f));
            var padBody = Mat("Pad_Body", new Color(0.1f, 0.1f, 0.12f));
            var padFace = Mat("Pad_Face", Color.white);
            padFace.EnableKeyword("_EMISSION");     // подсветка лап через _EmissionColor
            var barMat = Mat("Dodge_Bar", new Color(0.95f, 0.75f, 0.2f));
            barMat.EnableKeyword("_EMISSION");

            // ---------- ринг: настил, угловые стойки, три ряда канатов
            var ring = new GameObject("Ring").transform;
            Vector3 c = new Vector3(0, 0, 0.8f);
            const float half = 2.5f;
            Prim(PrimitiveType.Cube, "Canvas", ring, c + new Vector3(0, 0.002f, 0), new Vector3(half * 2, 0.004f, half * 2), canvas);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1 : 1, sz = i < 2 ? -1 : 1;
                Prim(PrimitiveType.Cylinder, "Post", ring, c + new Vector3(sx * half, 0.75f, sz * half), new Vector3(0.1f, 0.75f, 0.1f), post);
            }
            foreach (float h in new[] { 0.5f, 0.9f, 1.3f })
            {
                RopeSide(ring, c + new Vector3(0, h, -half), true, half, rope);
                RopeSide(ring, c + new Vector3(0, h, half), true, half, rope);
                RopeSide(ring, c + new Vector3(-half, h, 0), false, half, rope);
                RopeSide(ring, c + new Vector3(half, h, 0), false, half, rope);
            }

            // ---------- тренажёр: колонна + поднимаемая под рост «голова» с лапами
            Prim(PrimitiveType.Cylinder, "Column", null, new Vector3(0, 1.15f, ColumnZ), new Vector3(0.16f, 1.15f, 0.16f), dark);
            var padRig = new GameObject("PadRig").transform;
            padRig.position = new Vector3(0, 1.5f, 0);

            var padMesh = LoadMesh(Models + "Pad.fbx");
            var pads = new BoxingPad[Pads.Length];
            for (int i = 0; i < pads.Length; i++)
            {
                var def = Pads[i];
                var padGo = new GameObject($"Pad_{i}_{def.Kind}_{def.Hand}");
                padGo.transform.SetParent(padRig, false);
                padGo.transform.localPosition = def.Pos;
                // Ось Z лапы — направление удара; «вверх» для апперкота берём «назад», чтобы не было вырождения.
                padGo.transform.localRotation = Quaternion.LookRotation(def.Dir,
                    def.Kind == PadKind.Uppercut ? Vector3.back : Vector3.up);
                var col = padGo.AddComponent<BoxCollider>();
                col.isTrigger = true;                    // перчатка (кинематическое тело) ловит вход в триггер
                col.size = new Vector3(0.2f, 0.2f, 0.12f);   // чуть меньше лапы — реже задевается соседняя
                col.center = new Vector3(0, 0, -0.03f);

                var vis = MeshObject("Visual", padGo.transform, Vector3.zero, padMesh, padBody, padFace);
                pads[i] = padGo.AddComponent<BoxingPad>();
                pads[i].Setup(vis.GetComponent<Renderer>(), vis.transform, def.Kind, def.Hand, def.Any);

                // Кронштейн от тыльной стороны лапы к колонне.
                Vector3 back = def.Pos + def.Dir * 0.08f;
                Arm(padRig, back, new Vector3(0, def.Pos.y, ColumnZ), dark);
            }

            // Перекладина «ответного удара» на уровне головы.
            var dodgeRoot = new GameObject("DodgeBar").transform;
            dodgeRoot.SetParent(padRig, false);
            dodgeRoot.localPosition = new Vector3(0, 0.17f, 0); // между прямыми (верх 0.09) и апперкотами (низ 0.26)
            var holder = new GameObject("BarHolder").transform;
            holder.SetParent(dodgeRoot, false);
            holder.localPosition = new Vector3(0, 0, 0.95f);
            var bar = Prim(PrimitiveType.Capsule, "Bar", holder, Vector3.zero, new Vector3(0.12f, 0.45f, 0.12f), barMat);
            bar.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(bar, 0);
            var dodge = dodgeRoot.gameObject.AddComponent<DodgeBar>();
            dodge.Setup(holder, bar.GetComponent<Renderer>());

            var combo = Text("Combo", null, new Vector3(0, 2.45f, ColumnZ - 0.12f), "", 2.2f, new Vector2(2f, 0.4f),
                new Color(1f, 0.85f, 0.3f));
            Text("Legend", null, new Vector3(0, 2.15f, ColumnZ - 0.12f),
                "<color=#3D8CFF>синий — левой</color>    <color=#FF4033>красный — правой</color>", 1.1f,
                new Vector2(2.4f, 0.25f), Color.white);

            // ---------- груша на шарнире
            var anchor = new Vector3(1.45f, 2.65f, 1.0f);
            Prim(PrimitiveType.Cylinder, "BagPost", null, new Vector3(2.05f, 1.35f, 1.0f), new Vector3(0.12f, 1.35f, 0.12f), dark);
            Prim(PrimitiveType.Cube, "BagBeam", null, new Vector3(1.75f, 2.7f, 1.0f), new Vector3(0.7f, 0.1f, 0.1f), dark);

            var bagGo = MeshObject("PunchingBag", null, anchor - new Vector3(0, 0.85f, 0), LoadMesh(Models + "Bag.fbx"),
                Mat("Bag_Leather", new Color(0.7f, 0.12f, 0.1f)), Mat("Bag_Band", new Color(0.1f, 0.1f, 0.1f)));
            var chain = Prim(PrimitiveType.Cylinder, "Chain", bagGo.transform, new Vector3(0, 0.425f, 0), new Vector3(0.02f, 0.425f, 0.02f), dark);
            Object.DestroyImmediate(chain.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(chain, 0);
            var bagCol = bagGo.AddComponent<CapsuleCollider>();
            bagCol.center = new Vector3(0, -0.5f, 0);
            bagCol.height = 1.0f;
            bagCol.radius = 0.18f;
            var bagRb = bagGo.AddComponent<Rigidbody>();
            bagRb.mass = 30f;
            bagRb.angularDamping = 0.8f;            // кожа и наполнитель гасят качание
            bagRb.interpolation = RigidbodyInterpolation.Interpolate;
            var joint = bagGo.AddComponent<ConfigurableJoint>();
            joint.anchor = new Vector3(0, 0.85f, 0); // точка подвеса — верх цепи
            joint.autoConfigureConnectedAnchor = true;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;   // не смещается
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free; // качается
            var readoutPos = anchor + new Vector3(0, 0.35f, -0.1f);
            var readout = Text("BagReadout", null, readoutPos, "Ударьте грушу", 1.2f, new Vector2(1.4f, 0.4f), Color.white);
            var flat = new Vector3(readoutPos.x, 0, readoutPos.z);
            readout.transform.rotation = Quaternion.LookRotation(flat.normalized);
            bagGo.AddComponent<PunchingBag>().Setup(readout);

            // ---------- перчатки на контроллерах
            var rig = GameObject.Find("XR Origin (XR Rig)").transform;
            var gloveMesh = LoadMesh(Models + "Glove.fbx");
            AttachGlove(rig, "Left Controller", Hand.Left, gloveMesh, Mat("Glove_Blue", new Color(0.2f, 0.45f, 0.95f)));
            AttachGlove(rig, "Right Controller", Hand.Right, gloveMesh, Mat("Glove_Red", new Color(0.9f, 0.15f, 0.12f)));

            mode.Setup(pads, padRig, dodge, combo);
        }

        private static void AttachGlove(Transform rig, string controllerName, Hand hand, Mesh mesh, Material color)
        {
            var controller = FindDeep(rig, controllerName);
            // Модель контроллера прячем — вместо неё перчатка.
            var visual = FindDeep(controller, controllerName + " Visual");
            if (visual != null) visual.gameObject.SetActive(false);

            var glove = MeshObject(hand == Hand.Left ? "Glove_L" : "Glove_R", controller, new Vector3(0, -0.01f, 0.0f), mesh,
                color, Mat("Glove_Cuff", new Color(0.95f, 0.95f, 0.95f)));
            if (hand == Hand.Left) glove.transform.localScale = new Vector3(-1, 1, 1); // левая — зеркальная копия правой

            var sphere = glove.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.075f;
            var rb = glove.AddComponent<Rigidbody>();
            rb.isKinematic = true;   // движется за рукой, физикой не толкается
            rb.useGravity = false;
            glove.AddComponent<VelocityTracker>();
            glove.AddComponent<BoxingGlove>().Setup(hand);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform ch in root)
            {
                var f = FindDeep(ch, name);
                if (f != null) return f;
            }
            return null;
        }

        private static void RopeSide(Transform parent, Vector3 center, bool alongX, float half, Material mat)
        {
            var r = Prim(PrimitiveType.Cylinder, "Rope", parent, center, new Vector3(0.035f, half, 0.035f), mat);
            r.transform.localRotation = alongX ? Quaternion.Euler(0, 0, 90f) : Quaternion.Euler(90f, 0, 0);
            Object.DestroyImmediate(r.GetComponent<Collider>());
        }

        private static void Arm(Transform parent, Vector3 from, Vector3 to, Material mat)
        {
            Vector3 d = to - from;
            var a = Prim(PrimitiveType.Cylinder, "Arm", parent, (from + to) / 2f, new Vector3(0.035f, d.magnitude / 2f, 0.035f), mat);
            a.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            Object.DestroyImmediate(a.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(a, 0);
        }
    }
}
