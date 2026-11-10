using System.Collections;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Плавное затемнение экрана при смене сцен.
    /// В VR резкая смена картинки неприятна и может укачивать, поэтому
    /// сначала гасим экран в чёрный, меняем сцену, затем плавно проявляем.
    ///
    /// Висит на камере игрока. Создаёт перед глазами квадрат с шейдером
    /// VRSports/ScreenFade и меняет его прозрачность.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Material fadeMaterial;
        [SerializeField] private float fadeDuration = 0.4f;

        private Material _material;   // копия, чтобы не менять общий ассет
        private Renderer _quad;

        public float FadeDuration => fadeDuration;

        private void Awake()
        {
            Instance = this;

            var cam = GetComponent<Camera>();
            var quad = new GameObject("FadeQuad");
            quad.transform.SetParent(transform, false);
            // Чуть дальше ближней плоскости отсечения камеры, иначе квадрат обрежется.
            quad.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.02f);
            quad.transform.localScale = Vector3.one * 2f;

            quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _quad = quad.AddComponent<MeshRenderer>();
            _material = new Material(fadeMaterial);
            _quad.sharedMaterial = _material;
            _quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quad.receiveShadows = false;

            SetAlpha(1f); // сцена появляется из темноты
        }

        private void Start() => StartCoroutine(FadeTo(0f));

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_material != null) Destroy(_material);
        }

        public IEnumerator FadeTo(float targetAlpha)
        {
            float start = _material.color.a;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, targetAlpha, t / fadeDuration));
                yield return null;
            }
            SetAlpha(targetAlpha);
        }

        private void SetAlpha(float a)
        {
            var c = _material.color;
            c.a = a;
            _material.color = c;
            _quad.enabled = a > 0.001f; // полностью прозрачный — не рисуем вовсе (экономим GPU)
        }
    }
}
