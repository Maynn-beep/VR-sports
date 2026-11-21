using System.Collections;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Basketball
{
    /// <summary>
    /// Робот-соперник в баскетболе: бросает свой (серый) мяч в то же кольцо с разных точек площадки.
    ///
    /// Как бот целится — обратная задача баллистики «методом стрельбы» (shooting method):
    ///  1. Угол вылета фиксирован (58° — крутая дуга, мяч входит в кольцо сверху), неизвестна только скорость v.
    ///  2. Для пробной v численно моделируем полёт с теми же силами, что действуют на мяч в игре:
    ///       a = g + (−k_d·|v|·v + k_m·(ω × v)) / m   (сопротивление воздуха + эффект Магнуса);
    ///     находим, где мяч на спуске пересечёт высоту кольца.
    ///  3. Перелёт — уменьшаем v, недолёт — увеличиваем (метод деления отрезка пополам, 20 итераций).
    ///  4. Если кольцо ездит — подставляем, ГДЕ оно будет к моменту прилёта мяча (упреждение), и решаем заново.
    /// Затем «портим» решение: v·(1 + N(0, σ_v)), направление ± N(0, σ_угла). Чем выше уровень, тем меньше σ.
    /// </summary>
    public class BasketballBot : MonoBehaviour
    {
        [SerializeField] private RivalAvatar avatar;
        [SerializeField] private BasketballBall ball;
        [SerializeField] private Hoop hoop;
        [Tooltip("Угол вылета. Крутая дуга даёт крутой вход в кольцо (~48°): при пологом входе мяч задевает передний обод.")]
        [SerializeField] private float launchAngle = 58f;
        [Tooltip("Прицел чуть дальше центра кольца: передняя дужка обода опаснее задней (проверено тестами в Unity).")]
        [SerializeField] private float aimBeyond = 0.07f;

        // Точки бросков: угол от линии «кольцо — игрок» (вправо, чтобы не мешать игроку) и расстояние.
        private static readonly Vector2[] Spots =
        {
            new Vector2(40f, 3.8f), new Vector2(55f, 4.6f), new Vector2(70f, 4.0f),
            new Vector2(45f, 6.0f), new Vector2(65f, 5.8f), new Vector2(30f, 5.0f),
        };

        private RivalLevel _level;
        private Coroutine _loop;

        public void Setup(RivalAvatar a, BasketballBall b, Hoop h)
        {
            avatar = a; ball = b; hoop = h;
        }

        public void Show(bool on)
        {
            gameObject.SetActive(on);
            ball.gameObject.SetActive(on);
            if (on) Idle();
        }

        private void Start() => Idle();

        /// <summary>Стоять с мячом у края площадки (до старта и после финиша).</summary>
        public void Idle()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            var spot = SpotPosition(Spots[0]);
            avatar.Teleport(spot);
            avatar.LookAt(hoop.HomeRimCenter);
            HoldBall();
            avatar.Say("");
        }

        public void Begin(RivalLevel level)
        {
            _level = level;
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(Loop());
        }

        public void Stop() => Idle();

        private IEnumerator Loop()
        {
            int last = -1;
            while (true)
            {
                // 1. Выбрать точку (не ту же, что в прошлый раз) и перейти туда с мячом.
                int i;
                do i = Random.Range(0, Spots.Length); while (i == last);
                last = i;
                var spot = SpotPosition(Spots[i]);
                avatar.LookAt(spot);
                avatar.MoveTo(spot, Rival.Pick(_level, 1.1f, 1.5f, 1.9f));
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.13f, 1.05f, 0.3f), 2f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.13f, 1.05f, 0.3f), 2f);
                while (!avatar.Arrived) { FollowHands(); yield return null; }

                // 2. Развернуться к кольцу и прицелиться (время «на подумать» зависит от уровня).
                avatar.LookAt(hoop.RimCenter);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.13f, 1.72f, 0.22f), 1.6f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.13f, 1.72f, 0.22f), 1.6f);
                float aim = Rival.Pick(_level, 1.6f, 1.1f, 0.6f);
                for (float t = 0f; t < aim; t += Time.deltaTime) { FollowHands(); yield return null; }

                // 3. Бросок: кисти выпрямляются вверх-вперёд.
                Shoot();
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.1f, 1.95f, 0.4f), 3f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.1f, 1.95f, 0.4f), 3f);
                avatar.Say("");
                yield return new WaitForSeconds(2.3f);

                // 4. «Возврат мяча» (как у аркадного автомата) — мяч снова в руках.
                HoldBall();
                yield return new WaitForSeconds(Rival.Pick(_level, 1.5f, 0.8f, 0.2f));
            }
        }

        private Vector3 SpotPosition(Vector2 spot)
        {
            var home = hoop.HomeRimCenter;
            float a = spot.x * Mathf.Deg2Rad;
            return new Vector3(home.x + Mathf.Sin(a) * spot.y, 0f, home.z - Mathf.Cos(a) * spot.y);
        }

        private Vector3 BallInHands()
        {
            Vector3 l = avatar.Hand(AvatarSide.Left).position, r = avatar.Hand(AvatarSide.Right).position;
            return (l + r) / 2f + avatar.Forward * 0.02f;
        }

        private void HoldBall()
        {
            avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.13f, 1.05f, 0.3f), 0f);
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.13f, 1.05f, 0.3f), 0f);
            ball.BotHold(avatar.transform.TransformPoint(new Vector3(0f, 1.05f, 0.32f)));
        }

        private void FollowHands() => ball.BotHold(BallInHands());

        // ---------------------------------------------------------------- баллистика

        private void Shoot()
        {
            Vector3 p = BallInHands();
            Vector3 target = Beyond(p, hoop.RimCenter);
            Vector3 v = Vector3.zero;
            // Итерации упреждения: решаем → время полёта → где будет кольцо → решаем снова.
            for (int k = 0; k < 3; k++)
            {
                if (!Solve(p, target, out v, out float flight)) break;
                target = Beyond(p, hoop.PredictRim(flight));
            }
            if (v == Vector3.zero) v = (target - p).normalized * 8f;

            float sigmaV = Rival.Pick(_level, 0.045f, 0.022f, 0.009f);
            float sigmaYaw = Rival.Pick(_level, 2.8f, 1.5f, 0.6f);
            v = Quaternion.AngleAxis(Rival.Gaussian(sigmaYaw), Vector3.up) * v * (1f + Rival.Gaussian(sigmaV));
            ball.BotLaunch(p, v, Backspin(v));
        }

        private Vector3 Beyond(Vector3 from, Vector3 rim)
        {
            Vector3 d = rim - from;
            d.y = 0f;
            return rim + d.normalized * aimBeyond;
        }

        /// <summary>Обратное вращение, как у помощника закрутки у игрока: ось = d × up.</summary>
        private Vector3 Backspin(Vector3 v)
        {
            Vector3 d = new Vector3(v.x, 0f, v.z);
            return d.sqrMagnitude < 1e-4f ? Vector3.zero : Vector3.Cross(d.normalized, Vector3.up) * ball.AssistBackspin;
        }

        private bool Solve(Vector3 p, Vector3 target, out Vector3 velocity, out float flight)
        {
            Vector3 flat = target - p;
            flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 dir = flat / Mathf.Max(dist, 1e-4f);
            float a = launchAngle * Mathf.Deg2Rad;
            Vector3 unit = dir * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);

            float lo = 3f, hi = 16f;
            velocity = Vector3.zero;
            flight = 0f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) / 2f;
                float reach = Simulate(p, unit * mid, target.y, out float t);
                if (reach < dist) lo = mid; else hi = mid;
                flight = t;
            }
            velocity = unit * ((lo + hi) / 2f);
            return Simulate(p, velocity, target.y, out flight) > 0f;
        }

        /// <summary>
        /// Численное моделирование полёта (явный метод Эйлера, шаг 5 мс).
        /// Возвращает горизонтальную дальность в момент, когда мяч на спуске опустился до высоты y,
        /// или −1, если мяч до этой высоты не долетел.
        /// </summary>
        private float Simulate(Vector3 p0, Vector3 v0, float y, out float time)
        {
            const float dt = 0.005f;
            Vector3 p = p0, v = v0, w = Backspin(v0);
            float m = ball.Mass;
            for (time = 0f; time < 4f; time += dt)
            {
                Vector3 force = -ball.DragK * v.magnitude * v + ball.MagnusK * Vector3.Cross(w, v);
                v += (Physics.gravity + force / m) * dt;
                p += v * dt;
                if (v.y < 0f && p.y <= y)
                {
                    Vector3 d = p - p0;
                    d.y = 0f;
                    return d.magnitude;
                }
            }
            return -1f;
        }
    }
}
