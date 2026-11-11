using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Метатель тарелочек: каждые interval секунд выпускает диск с одной из сторон поля
    /// по дуге поперёк линии стрельбы. Скорость и угол слегка случайны.
    /// </summary>
    public class ClayThrower : MonoBehaviour
    {
        [SerializeField] private ClayDisc[] pool;
        [SerializeField] private float interval = 2.5f;
        [SerializeField] private Vector2 distanceRange = new Vector2(14f, 24f);

        public bool Running { get; set; }
        public float Interval { get => interval; set => interval = value; }

        private float _timer;

        public void Setup(ClayDisc[] discs) => pool = discs;

        private void Update()
        {
            if (!Running) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = interval;
            Throw();
        }

        private void Throw()
        {
            ClayDisc disc = null;
            foreach (var d in pool) if (!d.gameObject.activeSelf) { disc = d; break; }
            if (disc == null) return;

            float side = Random.value < 0.5f ? -1f : 1f;
            float z = Random.Range(distanceRange.x, distanceRange.y);
            var start = new Vector3(side * 11f, 0.8f, z);
            var velocity = new Vector3(-side * Random.Range(8f, 11f), Random.Range(6.5f, 8.5f), Random.Range(-1f, 1f));
            disc.Launch(start, velocity);
        }

        public void StopAll()
        {
            Running = false;
            foreach (var d in pool) if (d.gameObject.activeSelf) d.Remove();
        }
    }
}
