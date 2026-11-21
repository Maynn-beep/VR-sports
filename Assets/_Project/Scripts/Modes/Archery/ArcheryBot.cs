using System;
using System.Collections;
using UnityEngine;
using VRSports.Core;
using Random = UnityEngine.Random;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Робот-лучник: стреляет по тем же мишеням и тарелочкам теми же стрелами (Arrow),
    /// с тем же запасом стрел на волну, что и у игрока.
    ///
    /// Прицеливание — обратная задача баллистики «методом стрельбы» в двух углах:
    ///  1. Берём направление прямо на цель.
    ///  2. Численно моделируем полёт стрелы с теми же силами, что в Arrow.cs
    ///     (гравитация + анизотропное сопротивление воздуха относительно ветра) до плоскости цели.
    ///  3. Смотрим, на сколько стрела промахнулась по горизонтали и вертикали, и поворачиваем
    ///     направление на углы atan(промах / дальность). 8 повторений — и точность до миллиметров.
    ///  4. Цель движется (мишень на рельсе, тарелочка) — подставляем, где она будет к моменту прилёта.
    /// Затем — «человеческие» ошибки:
    ///  • ветер учитывается не полностью (лёгкий бот поправку почти не берёт);
    ///  • направление сбивается на N(0, σ) градусов по каждой оси.
    /// </summary>
    public class ArcheryBot : MonoBehaviour
    {
        [SerializeField] private RivalAvatar avatar;
        [SerializeField] private Bow playerBow;           // откуда берём образец стрелы и силу лука
        [SerializeField] private Transform botBow;        // модель лука в руке робота
        [SerializeField] private ArcheryTarget[] targets;
        [SerializeField] private Color fletchColor = new Color(0.2f, 0.5f, 1f);

        public event Action<Arrow> ArrowShot;
        public int ArrowsLeft { get; private set; }

        private RivalLevel _level;
        private Coroutine _loop;
        private Vector3 _aim = Vector3.forward;
        private float _draw;            // 0..1
        private Arrow _nocked;
        private const float StringZ = 0.22f;

        public void Setup(RivalAvatar a, Bow bow, Transform bowModel, ArcheryTarget[] t)
        {
            avatar = a; playerBow = bow; botBow = bowModel; targets = t;
        }

        public void Show(bool on)
        {
            gameObject.SetActive(on);
            botBow.gameObject.SetActive(on);
        }

        private void Start() => Stop();

        /// <summary>Новая волна: столько же стрел, сколько у игрока.</summary>
        public void BeginWave(RivalLevel level, int arrows)
        {
            Stop();
            _level = level;
            ArrowsLeft = arrows;
            _loop = StartCoroutine(Loop());
            UpdateLabel();
        }

        public void Stop()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            ArrowsLeft = 0;
            _draw = 0f;
            if (_nocked != null) Destroy(_nocked.gameObject);
            _nocked = null;
            _aim = Vector3.forward;
            avatar.LookAt(avatar.transform.position + Vector3.forward);
            UpdateLabel();
        }

        private void UpdateLabel() => avatar.Say(ArrowsLeft > 0 ? $"Стрел: {ArrowsLeft}" : "");

        // ---------------------------------------------------------------- цикл стрельбы

        private IEnumerator Loop()
        {
            yield return new WaitForSeconds(Random.Range(0.8f, 1.6f));
            while (ArrowsLeft > 0)
            {
                // 1. Выбор цели: тарелочка, если она уже достаточно долго летит (время реакции), иначе мишень.
                if (!PickTarget(out Func<float, Vector3> target, out bool isDisc))
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // 2. Натяжение тетивы (по цели — заранее, чтобы робот «вёл» её).
                _nocked = Instantiate(playerBow.ArrowTemplate);
                _nocked.gameObject.SetActive(true);
                _nocked.name = "Arrow_Bot";
                _nocked.IsRival = true;
                Tint(_nocked);
                float drawTime = Rival.Pick(_level, 1.1f, 0.9f, 0.7f);
                for (float t = 0f; t < drawTime; t += Time.deltaTime)
                {
                    _draw = Mathf.SmoothStep(0f, 1f, t / drawTime);
                    if (Solve(target, out Vector3 dir, out _)) _aim = dir;
                    yield return null;
                }
                _draw = 1f;

                // 3. Окончательный прицел — с упреждением и ошибкой, выстрел.
                if (Solve(target, out Vector3 aim, out _)) _aim = aim;
                float sigma = Rival.Pick(_level, 0.7f, 0.4f, 0.2f) * (isDisc ? 1.3f : 1f);
                _aim = Rival.Scatter(_aim, sigma).normalized;
                yield return null; // кадр, чтобы стрела встала по новому направлению
                Release();

                yield return new WaitForSeconds(Rival.Pick(_level, 2.6f, 2.0f, 1.5f));
            }
            _loop = null;
        }

        private bool PickTarget(out Func<float, Vector3> target, out bool isDisc)
        {
            float reaction = Rival.Pick(_level, 1.2f, 0.8f, 0.5f);
            foreach (var disc in ClayDisc.Active)
            {
                // Тарелочка должна прожить ещё ~1.2 с, иначе стрела не успеет.
                if (disc.Airtime < reaction || disc.Predict(1.2f).y < 1f) continue;
                var d = disc;
                target = d.Predict;
                isDisc = true;
                return true;
            }

            isDisc = false;
            int count = 0;
            foreach (var t in targets) if (t.gameObject.activeSelf) count++;
            if (count == 0) { target = null; return false; }
            int pick = Random.Range(0, count);
            foreach (var t in targets)
            {
                if (!t.gameObject.activeSelf) continue;
                if (pick-- == 0) { var tt = t; target = tt.PredictFace; return true; }
            }
            target = null;
            return false;
        }

        private void Release()
        {
            var arrow = _nocked;
            _nocked = null;
            _draw = 0f;
            ArrowsLeft--;
            UpdateLabel();
            arrow.transform.SetPositionAndRotation(NockPosition(1f), Quaternion.LookRotation(_aim));
            arrow.Launch(_aim * playerBow.MaxArrowSpeed);
            ArrowShot?.Invoke(arrow);
        }

        // ---------------------------------------------------------------- поза: лук и руки

        private Vector3 BowPosition => avatar.transform.position + Vector3.up * 1.27f
                                       - avatar.transform.right * 0.25f * 0.4f + _aim * 0.55f;

        private Vector3 NockPosition(float draw) =>
            BowPosition - _aim * (StringZ + draw * playerBow.MaxDraw);

        private void LateUpdate()
        {
            // Робот стоит лицом по направлению выстрела; лук в левой руке, правая тянет тетиву.
            avatar.LookAt(avatar.transform.position + new Vector3(_aim.x, 0f, _aim.z));
            botBow.SetPositionAndRotation(BowPosition, Quaternion.LookRotation(_aim, Vector3.up));
            avatar.SetHandWorld(AvatarSide.Left, BowPosition, 0f);
            avatar.SetHandWorld(AvatarSide.Right, NockPosition(_draw), 0f);
            if (_nocked != null)
                _nocked.transform.SetPositionAndRotation(NockPosition(_draw), Quaternion.LookRotation(_aim));
        }

        private void Tint(Arrow arrow)
        {
            var r = arrow.GetComponent<Renderer>();
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb, 2);
            mpb.SetColor("_BaseColor", fletchColor);
            r.SetPropertyBlock(mpb, 2);
        }

        // ---------------------------------------------------------------- баллистика

        /// <summary>Направление выстрела, чтобы попасть в цель target(t) (t — время полёта).</summary>
        private bool Solve(Func<float, Vector3> target, out Vector3 dir, out float flight)
        {
            Vector3 tail = NockPosition(1f);
            Vector3 tip0;
            Vector3 aimPoint = target(0f);
            dir = (aimPoint - tail).normalized;
            flight = 0f;
            float speed = playerBow.MaxArrowSpeed;
            Vector3 wind = ArcheryWind.Current * Rival.Pick(_level, 0.4f, 0.75f, 0.95f);

            for (int i = 0; i < 8; i++)
            {
                tip0 = tail + dir * playerBow.ArrowTemplate.TipOffset;
                if (!Simulate(tail, dir * speed, aimPoint, wind, out Vector3 hit, out flight)) return false;

                Vector3 los = aimPoint - tip0;
                float dist = los.magnitude;
                Vector3 err = aimPoint - hit;
                // Раскладываем промах на горизонтальную и вертикальную составляющие относительно луча зрения.
                Vector3 right = Vector3.Cross(Vector3.up, los).normalized;
                float dx = Vector3.Dot(err, right), dy = Vector3.Dot(err, Vector3.up);
                dir = Quaternion.AngleAxis(Mathf.Atan2(dx, dist) * Mathf.Rad2Deg, Vector3.up) * dir;
                Vector3 pitchAxis = Vector3.Cross(dir, Vector3.up).normalized;
                dir = Quaternion.AngleAxis(Mathf.Atan2(dy, dist) * Mathf.Rad2Deg, pitchAxis) * dir; // ось dir×up: «+» — вверх

                aimPoint = target(flight); // упреждение для движущейся цели
            }
            return true;
        }

        /// <summary>
        /// Полёт стрелы — та же модель, что в Arrow.FixedUpdate. Интегрируем хвостовик (tail), а наконечник
        /// считаем как tail + L·(направление скорости): стрела на излёте «клюёт» носом, и наконечник
        /// опускается относительно хвоста на ~15–20 см (тесты в Unity показали: без этого бот бьёт ниже центра).
        /// Останавливаемся, когда наконечник пересёк плоскость через цель, перпендикулярную лучу зрения.
        /// </summary>
        private bool Simulate(Vector3 tail, Vector3 v, Vector3 aimPoint, Vector3 wind, out Vector3 hit, out float time)
        {
            var tmpl = playerBow.ArrowTemplate;
            float len = tmpl.TipOffset;
            Vector3 axis = v.normalized;
            Vector3 tip = tail + axis * len;
            Vector3 n = (aimPoint - tip).normalized;
            float dt = Time.fixedDeltaTime; // тот же шаг, что у настоящей стрелы
            for (time = 0f; time < 6f; time += dt)
            {
                Vector3 rel = v - wind;
                Vector3 relAx = Vector3.Dot(rel, axis) * axis;
                Vector3 relSide = rel - relAx;
                Vector3 a = Physics.gravity - tmpl.DragAxial * rel.magnitude * relAx - tmpl.DragSide * relSide.magnitude * relSide;
                v += a * dt;
                tail += v * dt;
                axis = v.normalized;
                Vector3 next = tail + axis * len;
                if (Vector3.Dot(next - aimPoint, n) >= 0f)
                {
                    // Точка пересечения плоскости — линейная интерполяция внутри шага.
                    float d0 = Vector3.Dot(tip - aimPoint, n), d1 = Vector3.Dot(next - aimPoint, n);
                    float k = d0 / Mathf.Min(d0 - d1, -1e-6f); // d0 < 0 ≤ d1 → k ∈ [0, 1]
                    hit = Vector3.Lerp(tip, next, Mathf.Clamp01(k));
                    time += dt * k;
                    return true;
                }
                tip = next;
            }
            hit = tip;
            return false;
        }
    }
}
