using System;
using System.Collections;
using UnityEngine;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// Тренировочная лапа. Загорается цветом руки, которой нужно ударить
    /// (синий — левая, красный — правая), и гаснет через «окно» времени.
    ///
    /// Удар засчитывается, только если кулак летит В лапу:
    ///   (v · n) ≥ 0.5·|v|, n — нормаль лапы от игрока.
    /// Это отсекает «касания» вскользь и удары тыльной стороной при возврате руки.
    /// </summary>
    public class BoxingPad : MonoBehaviour
    {
        [SerializeField] private Renderer faceRenderer;
        [SerializeField] private int faceSlot = 1;
        [SerializeField] private Transform visual;

        public static readonly Color LeftColor = new Color(0.2f, 0.55f, 1f);
        public static readonly Color RightColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color IdleColor = new Color(0.55f, 0.55f, 0.58f);

        /// <summary>Удар пришёл: перчатка, скорость вдоль нормали, правильная ли рука, была ли лапа активна.</summary>
        public event Action<BoxingPad, BoxingGlove, float, bool, bool> Punched;
        public event Action<BoxingPad> Expired;

        public bool IsLit { get; private set; }
        public Hand RequiredHand { get; private set; }

        private float _deadline;
        private MaterialPropertyBlock _mpb;
        private Coroutine _recoil;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public void Setup(Renderer face, Transform vis)
        {
            faceRenderer = face; visual = vis;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            SetColor(IdleColor, 0f);
        }

        public void Light(Hand hand, float window)
        {
            IsLit = true;
            RequiredHand = hand;
            _deadline = Time.time + window;
            SetColor(hand == Hand.Left ? LeftColor : RightColor, 2f);
        }

        public void TurnOff()
        {
            IsLit = false;
            SetColor(IdleColor, 0f);
        }

        private void Update()
        {
            if (!IsLit) return;
            // Чем меньше осталось времени, тем сильнее «мигает» лапа — подсказка игроку.
            float left = _deadline - Time.time;
            if (left <= 0f)
            {
                TurnOff();
                Expired?.Invoke(this);
                return;
            }
            float blink = left < 0.4f ? (Mathf.Sin(Time.time * 40f) > 0 ? 2.5f : 0.6f) : 2f;
            SetColor(RequiredHand == Hand.Left ? LeftColor : RightColor, blink);
        }

        public void ReceivePunch(BoxingGlove glove, Vector3 velocity, Vector3 point)
        {
            Vector3 n = transform.forward; // от игрока
            float into = Vector3.Dot(velocity, n);
            if (into < 0.5f * velocity.magnitude) return; // удар вскользь / рука возвращается

            bool wasLit = IsLit;
            bool correct = wasLit && glove.Hand == RequiredHand;
            if (wasLit) TurnOff();

            if (_recoil != null) StopCoroutine(_recoil);
            _recoil = StartCoroutine(Recoil(Mathf.Clamp01(into / 7f)));
            Punched?.Invoke(this, glove, into, correct, wasLit);
        }

        /// <summary>Лапа «проседает» от удара и возвращается — визуальная отдача.</summary>
        private IEnumerator Recoil(float strength)
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / 0.25f * Mathf.PI) * strength;
                visual.localPosition = Vector3.forward * (0.06f * k);
                yield return null;
            }
            visual.localPosition = Vector3.zero;
        }

        private void SetColor(Color c, float emission)
        {
            if (faceRenderer == null) return;
            faceRenderer.GetPropertyBlock(_mpb, faceSlot);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(EmissionId, c * emission);
            faceRenderer.SetPropertyBlock(_mpb, faceSlot);
        }
    }
}
