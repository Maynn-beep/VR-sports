using System.Collections;
using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Мишень диаметром 80 см, 10 колец по 4 см.
    /// Очки считаются по точке попадания: расстояние до центра d в плоскости мишени
    ///   кольцо = 10 − ⌊d / R · 10⌋   (d = 0…4 см → 10, 4…8 см → 9, … , 36…40 см → 1).
    /// Может ездить влево-вправо (кинематическое тело, синусоида).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcheryTarget : MonoBehaviour
    {
        [SerializeField] private Transform faceCenter;  // центр лицевой стороны, ось Z — нормаль мишени
        [SerializeField] private float radius = 0.4f;

        private Rigidbody _rb;
        private Vector3 _home;
        private float _amplitude, _speed, _phase;
        private Coroutine _flash;

        public void Setup(Transform face) => faceCenter = face;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _home = transform.position;
        }

        public int ScoreFor(Vector3 point)
        {
            // Проекция на плоскость мишени убирает глубину (стрела входит под углом).
            float d = Vector3.ProjectOnPlane(point - faceCenter.position, faceCenter.forward).magnitude;
            if (d > radius) return 0;
            return Mathf.Clamp(10 - Mathf.FloorToInt(d / radius * 10f), 1, 10);
        }

        /// <summary>Поставить мишень на позицию. amplitude/speed &gt; 0 — мишень ездит.</summary>
        public void Place(Vector3 position, float amplitude = 0f, float speed = 0f)
        {
            gameObject.SetActive(true);
            _home = position;
            _amplitude = amplitude;
            _speed = speed;
            _phase = Random.Range(0f, Mathf.PI * 2f); // разные мишени — в разной фазе
            Vector3 p = CurrentPos();
            _rb.position = p;
            transform.position = p;
        }

        private Vector3 CurrentPos() => _home + Vector3.right * (_amplitude * Mathf.Sin(_phase));

        private void FixedUpdate()
        {
            if (_speed <= 0f) return;
            _phase += _speed * Time.fixedDeltaTime;
            _rb.MovePosition(CurrentPos());
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Мишень «вздрагивает» от попадания — визуальная обратная связь.</summary>
        public void Flash()
        {
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var face = faceCenter.parent;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                face.localPosition = new Vector3(0f, face.localPosition.y, Mathf.Sin(t * 60f) * 0.02f * (1f - t / 0.25f));
                yield return null;
            }
            face.localPosition = new Vector3(0f, face.localPosition.y, 0f);
        }
    }
}
