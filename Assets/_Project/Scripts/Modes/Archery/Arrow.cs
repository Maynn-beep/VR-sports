using System;
using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Стрела летит по СОБСТВЕННОМУ интегратору, а не через Rigidbody.
    ///
    /// Почему не PhysX: стрела летит до 50 м/с, за один физический шаг (1/72 с) это ~0.7 м.
    /// Тонкая стрела «проскочит» сквозь мишень, а нам ещё нужна точная точка попадания
    /// для подсчёта колец. Поэтому каждый шаг:
    ///   1) считаем ускорение: a = g − c·|v − w|·(v − w)  (гравитация + сопротивление воздуха с учётом ветра w);
    ///   2) обновляем скорость и положение (полунеявный метод Эйлера: сначала v, потом x);
    ///   3) пускаем луч (Raycast) от старого положения наконечника к новому — если он что-то
    ///      пересёк, стрела втыкается точно в точку пересечения;
    ///   4) поворачиваем стрелу по направлению скорости — так работает оперение:
    ///      перья сзади стабилизируют стрелу, и она летит «носом вперёд», опуская его на излёте.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        [SerializeField] private float tipOffset = 0.85f;   // наконечник — на 0.85 м впереди хвостовика
        [Tooltip("Сопротивление ВДОЛЬ стрелы (на единицу массы), 1/м: стрела теряет ~10% скорости на 40 м.")]
        [SerializeField] private float dragAxial = 0.0025f;
        [Tooltip("Сопротивление ПОПЕРЁК стрелы, 1/м: ρ·C_d·A_бок/(2m) = 1.225·1.2·0.0068/(2·0.025) ≈ 0.2. " +
                 "Боковая площадь древка в ~135 раз больше лобовой — поэтому ветер сносит стрелу.")]
        [SerializeField] private float dragSide = 0.2f;
        [SerializeField] private float penetration = 0.06f; // насколько входит в цель
        [SerializeField] private float maxLifetime = 8f;

        /// <summary>Стрела остановилась: target — мишень (или null, если промах), point — точка попадания.</summary>
        public event Action<Arrow, ArcheryTarget, Vector3> Landed;
        /// <summary>Стрела разбила тарелочку и летит дальше.</summary>
        public event Action<Arrow, ClayDisc> DiscHit;

        public bool Flying { get; private set; }
        public Vector3 LaunchPosition { get; private set; }

        private Vector3 _velocity;
        private float _life;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private Vector3 Tip => transform.position + transform.forward * tipOffset;

        public void Launch(Vector3 velocity)
        {
            _velocity = velocity;
            LaunchPosition = transform.position;
            Flying = true;
            _life = 0f;
        }

        private void FixedUpdate()
        {
            if (!Flying) return;
            float dt = Time.fixedDeltaTime;

            // 1–2. Ускорение и интегрирование. Сопротивление считается относительно ВОЗДУХА (rel = v − w)
            // и АНИЗОТРОПНО: поток раскладывается на составляющую вдоль оси стрелы и поперёк неё.
            //   a = g − c_ax·|rel|·rel_ax − c_side·|rel_⊥|·rel_⊥
            // Вдоль стрелы она обтекаемая (маленький коэффициент), поперёк — «парус» из древка и перьев.
            // Упрощение: стрела ориентирована по скорости относительно земли и не успевает
            // развернуться против ветра — поэтому боковой ветер даёт заметный снос.
            Vector3 rel = _velocity - ArcheryWind.Current;
            Vector3 axis = transform.forward;
            Vector3 relAxial = Vector3.Dot(rel, axis) * axis;
            Vector3 relSide = rel - relAxial;
            Vector3 accel = Physics.gravity
                            - dragAxial * rel.magnitude * relAxial
                            - dragSide * relSide.magnitude * relSide;
            _velocity += accel * dt;

            Vector3 from = Tip;
            Vector3 step = _velocity * dt;

            // Тарелочки: проверка «отрезок — сфера» (точная, без задержки физики).
            for (int i = ClayDisc.Active.Count - 1; i >= 0; i--) // с конца: Shatter удаляет из списка
            {
                var disc = ClayDisc.Active[i];
                if (!disc.IntersectsSegment(from, from + step)) continue;
                disc.Shatter();
                DiscHit?.Invoke(this, disc);
            }

            // 3. Луч вдоль пути наконечника за этот шаг.
            if (CastPath(from, step, out RaycastHit hit))
            {
                Stick(hit);
                return;
            }

            transform.position += step;
            // 4. Стабилизация оперением: стрела смотрит туда, куда летит.
            if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);

            _life += dt;
            if (_life > maxLifetime || transform.position.y < -5f) Finish(null, transform.position);
        }

        private static bool CastPath(Vector3 from, Vector3 step, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, step.normalized, Hits, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                // Игнорируем сам лук и «тело» игрока.
                if (c.GetComponentInParent<Bow>() != null || c is CharacterController) continue;
                if (Hits[i].distance < bestDist) { bestDist = Hits[i].distance; best = Hits[i]; }
            }
            return bestDist < float.MaxValue;
        }

        private void Stick(RaycastHit hit)
        {
            // Ставим стрелу так, чтобы наконечник вошёл в точку попадания на глубину penetration.
            Vector3 dir = _velocity.normalized;
            transform.rotation = Quaternion.LookRotation(dir);
            transform.position = hit.point + dir * penetration - transform.forward * tipOffset;

            var target = hit.collider.GetComponentInParent<ArcheryTarget>();
            // Втыкаемся в движущуюся мишень — становимся её дочерним объектом и едем вместе с ней.
            if (target != null) transform.SetParent(target.transform, true);
            Finish(target, hit.point);
        }

        private void Finish(ArcheryTarget target, Vector3 point)
        {
            if (!Flying) return;
            Flying = false;
            Landed?.Invoke(this, target, point);
            if (target == null && transform.position.y < -1f) Destroy(gameObject);
        }
    }
}
