using TMPro;
using UnityEngine;
using VRSports.Core;
using static VRSports.EditorTools.SceneBuilder;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Сборка робота-соперника из частей (Tools/Blender/robot.py → Models/Robot).
    /// У робота нет коллайдеров: шары, стрелы и мячи сквозь него не застревают.
    /// (В боксе зоны попадания добавляет BoxingBuilder.)
    /// </summary>
    internal static class RobotBuilder
    {
        private const string Models = "Assets/_Project/Models/Robot/";
        public const float HeadY = 1.52f;       // высота центра головы над «полом» робота
        public const float ShoulderY = 1.27f;
        public const float ShoulderX = 0.25f;

        /// <param name="handMesh">Своя модель кисти (в боксе — перчатка); null — стандартная.</param>
        public static RivalAvatar Build(string name, Vector3 pos, Quaternion rot, Color accent,
            Mesh handMesh = null, Material[] leftHandMats = null, Material[] rightHandMats = null)
        {
            var body = Mat("Robot_Body", new Color(0.86f, 0.87f, 0.9f));
            var acc = Mat("Robot_Accent_" + ColorUtility.ToHtmlStringRGB(accent), accent);
            var glow = Mat("Robot_Glow", new Color(0.2f, 0.9f, 1f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 1f) * 2f);
            var joint = Mat("Steel", new Color(0.35f, 0.37f, 0.42f));

            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(pos, rot);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);

            MeshObject("Torso", visual, new Vector3(0, HeadY - 0.15f, 0), LoadMesh(Models + "Torso.fbx"), body, acc, glow);
            var head = MeshObject("Head", visual, new Vector3(0, HeadY, 0), LoadMesh(Models + "Head.fbx"), body, acc, glow);

            var shoulders = new Transform[2];
            var hands = new Transform[2];
            var upper = new Transform[2];
            var fore = new Transform[2];
            var defaultHand = LoadMesh(Models + "Hand.fbx");
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                shoulders[i] = new GameObject(i == 0 ? "Shoulder_L" : "Shoulder_R").transform;
                shoulders[i].SetParent(visual, false);
                shoulders[i].localPosition = new Vector3(side * ShoulderX, ShoulderY, 0);

                var mats = handMesh == null ? new[] { body, acc } : (i == 0 ? leftHandMats : rightHandMats);
                var hand = MeshObject(i == 0 ? "Hand_L" : "Hand_R", root, new Vector3(side * 0.3f, 0.95f, 0.12f),
                    handMesh != null ? handMesh : defaultHand, mats);
                if (i == 0) hand.transform.localScale = new Vector3(-1, 1, 1); // левая — зеркальная
                hands[i] = hand.transform;

                upper[i] = Limb(i == 0 ? "UpperArm_L" : "UpperArm_R", root, joint, 0.075f);
                fore[i] = Limb(i == 0 ? "ForeArm_L" : "ForeArm_R", root, body, 0.065f);
            }

            var label = Text("Label", root, new Vector3(0, HeadY + 0.38f, 0), "", 1.4f, new Vector2(1.6f, 0.3f),
                new Color(1f, 0.85f, 0.4f));

            Unstatic(root);
            var avatar = root.gameObject.AddComponent<RivalAvatar>();
            avatar.Setup(visual, head.transform, shoulders, hands, upper, fore, head.GetComponent<Renderer>(), label);
            return avatar;
        }

        private static Transform Limb(string name, Transform parent, Material mat, float thickness)
        {
            var go = Prim(PrimitiveType.Cylinder, name, parent, Vector3.zero, new Vector3(thickness, 0.13f, thickness), mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go.transform;
        }

        /// <summary>Робот двигается — у всех его частей снимаем флаг Static.</summary>
        public static void Unstatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
    }
}
