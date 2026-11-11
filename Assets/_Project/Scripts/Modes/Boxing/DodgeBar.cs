using System;
using System.Collections;
using UnityEngine;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// «Ответный удар» тренажёра: мягкая перекладина на уровне головы выдвигается к игроку.
    /// От неё нужно уйти нырком — присесть. Это заставляет двигаться всем телом, а не только руками.
    ///
    /// Проверка: в момент, когда перекладина максимально выдвинута, голова (камера) должна быть
    /// ниже линии duckLine = рост_головы − duckDepth. Рост головы калибруется в начале партии.
    /// Перед ударом перекладина 0.9 с «предупреждает»: светится и дрожит.
    /// </summary>
    public class DodgeBar : MonoBehaviour
    {
        [SerializeField] private Transform bar;
        [SerializeField] private Renderer barRenderer;
        [SerializeField] private float restZ = 0.95f;     // локальная Z в покое (у стойки)
        [SerializeField] private float strikeZ = -0.15f;  // насквозь через место головы
        [SerializeField] private float warnTime = 0.9f;
        [SerializeField] private float strikeTime = 0.3f;
        [SerializeField] private float duckDepth = 0.22f;

        /// <summary>true — игрок увернулся, false — пропустил.</summary>
        public event Action<bool> Resolved;
        public bool Busy { get; private set; }

        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Setup(Transform b, Renderer r)
        {
            bar = b; barRenderer = r;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            SetGlow(0f);
        }

        public void Attack(Transform head, float calibratedHeadY)
        {
            if (!Busy) StartCoroutine(AttackRoutine(head, calibratedHeadY));
        }

        public void Cancel()
        {
            StopAllCoroutines(); // здесь можно: это собственный компонент, не режим
            Busy = false;
            bar.localPosition = new Vector3(0f, 0f, restZ);
            SetGlow(0f);
        }

        private IEnumerator AttackRoutine(Transform head, float headY)
        {
            Busy = true;
            // 1. Предупреждение.
            for (float t = 0f; t < warnTime; t += Time.deltaTime)
            {
                SetGlow(Mathf.PingPong(t * 6f, 1f) * 3f);
                bar.localPosition = new Vector3(Mathf.Sin(t * 60f) * 0.01f, 0f, restZ);
                yield return null;
            }
            // 2. Выпад к игроку.
            for (float t = 0f; t < strikeTime; t += Time.deltaTime)
            {
                bar.localPosition = new Vector3(0f, 0f, Mathf.Lerp(restZ, strikeZ, t / strikeTime));
                yield return null;
            }
            bar.localPosition = new Vector3(0f, 0f, strikeZ);

            // 3. Проверка: голова ниже линии нырка?
            bool dodged = head.position.y < headY - duckDepth;
            Resolved?.Invoke(dodged);

            // 4. Возврат.
            SetGlow(0f);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                bar.localPosition = new Vector3(0f, 0f, Mathf.Lerp(strikeZ, restZ, t / 0.4f));
                yield return null;
            }
            bar.localPosition = new Vector3(0f, 0f, restZ);
            Busy = false;
        }

        private void SetGlow(float k)
        {
            if (barRenderer == null) return;
            barRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, Color.Lerp(new Color(0.95f, 0.75f, 0.2f), new Color(1f, 0.3f, 0.1f), Mathf.Clamp01(k / 3f)));
            _mpb.SetColor(EmissionId, new Color(1f, 0.4f, 0.1f) * k);
            barRenderer.SetPropertyBlock(_mpb);
        }
    }
}
