using UnityEngine;

namespace VRSports.Modes.Basketball
{
    /// <summary>
    /// Обратная задача баллистики для баскетбольного мяча — «какую скорость дать мячу, чтобы он попал в кольцо».
    /// Используется в двух местах:
    ///  • робот-соперник (BasketballBot) — считает свой бросок;
    ///  • казуальная сложность — доводка броска игрока (BasketballBall.ApplyShotAssist).
    ///
    /// Метод стрельбы (shooting method): угол вылета задан, неизвестна скорость v.
    /// Для пробной v численно моделируем полёт с теми же силами, что действуют на мяч в игре:
    ///   a = g + (−k_d·|v|·v + k_m·(ω × v)) / m   (сопротивление воздуха + эффект Магнуса),
    /// находим, где мяч на спуске пересечёт высоту кольца, и делением отрезка пополам подбираем v.
    /// </summary>
    public static class BasketballBallistics
    {
        /// <summary>Обратное вращение (backspin), как у помощника закрутки: ось = d × up.</summary>
        public static Vector3 Backspin(BasketballBall ball, Vector3 v)
        {
            Vector3 d = new Vector3(v.x, 0f, v.z);
            return d.sqrMagnitude < 1e-4f ? Vector3.zero : Vector3.Cross(d.normalized, Vector3.up) * ball.AssistBackspin;
        }

        /// <summary>
        /// Скорость для попадания из точки p в движущееся кольцо: решаем → время полёта →
        /// где будет кольцо (Hoop.PredictRim) → решаем снова (3 итерации упреждения).
        /// aimBeyond — прицел чуть дальше центра: передняя дужка обода опаснее задней.
        /// </summary>
        public static bool SolveToHoop(BasketballBall ball, Hoop hoop, Vector3 p, float angleDeg, float aimBeyond,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;
            Vector3 target = Beyond(p, hoop.RimCenter, aimBeyond);
            bool ok = false;
            for (int k = 0; k < 3; k++)
            {
                if (!Solve(ball, p, target, angleDeg, out velocity, out float flight)) break;
                ok = true;
                target = Beyond(p, hoop.PredictRim(flight), aimBeyond);
            }
            return ok;
        }

        private static Vector3 Beyond(Vector3 from, Vector3 rim, float aimBeyond)
        {
            Vector3 d = rim - from;
            d.y = 0f;
            return rim + d.normalized * aimBeyond;
        }

        public static bool Solve(BasketballBall ball, Vector3 p, Vector3 target, float angleDeg,
            out Vector3 velocity, out float flight)
        {
            Vector3 flat = target - p;
            flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 dir = flat / Mathf.Max(dist, 1e-4f);
            float a = angleDeg * Mathf.Deg2Rad;
            Vector3 unit = dir * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);

            float lo = 3f, hi = 16f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) / 2f;
                float reach = Simulate(ball, p, unit * mid, target.y, out _);
                if (reach < dist) lo = mid; else hi = mid;
            }
            velocity = unit * ((lo + hi) / 2f);
            return Simulate(ball, p, velocity, target.y, out flight) > 0f;
        }

        /// <summary>
        /// Численное моделирование полёта (явный метод Эйлера, шаг 5 мс).
        /// Возвращает горизонтальную дальность в момент, когда мяч на спуске опустился до высоты y,
        /// или −1, если мяч до этой высоты не долетел.
        /// </summary>
        public static float Simulate(BasketballBall ball, Vector3 p0, Vector3 v0, float y, out float time)
        {
            const float dt = 0.005f;
            Vector3 p = p0, v = v0, w = Backspin(ball, v0);
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
