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
        [SerializeField] private Vector2 distanceRange = new Vector2(8f, 14f);
        [Tooltip("Откуда вылетают диски: ±X от линии стрельбы.")]
        [SerializeField] private float sideX = 7f;

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
            // Медленная высокая дуга поперёк поля: ~3 с в воздухе, пик ~5 м — есть время прицелиться.
            var start = new Vector3(side * sideX, 0.8f, z);
            var velocity = new Vector3(-side * Random.Range(4.5f, 5.5f), Random.Range(6.2f, 7.2f), Random.Range(-0.5f, 0.5f));
            disc.Launch(start, velocity);
        }

        public void StopAll()
        {
            Running = false;
            foreach (var d in pool) if (d.gameObject.activeSelf) d.Remove();
        }
    }
}
