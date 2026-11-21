using System.Collections;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Робот-соперник в боулинге. Играет по очереди с игроком на той же дорожке тем же шаром.
    ///
    /// Как бот бросает:
    ///  1. Выбирает точку прицела на линии первой кегли:
    ///     • первый бросок фрейма — «карман» между кеглями 1 и 3 (x ≈ +6 см): отсюда чаще всего страйк;
    ///     • добивание — середина между ближней стоящей кеглей и центром оставшихся.
    ///  2. Портит прицел случайной ошибкой: x += N(0, σ), скорость += N(0, σ_v)  (σ зависит от уровня).
    ///  3. Отпускает шар у линии заступа: скорость v вдоль направления на прицел и вращение «качения
    ///     без проскальзывания» ω = (up × v) / r — как у шара, который уже катится, а не скользит.
    ///  Дальше всё решает физика: та же дорожка с маслом, те же кегли.
    /// </summary>
    public class BowlingBot : MonoBehaviour
    {
        [SerializeField] private RivalAvatar avatar;
        [SerializeField] private BowlingBall ball;
        [SerializeField] private BowlingPin[] pins;
        [SerializeField] private Transform rackPoint;
        [SerializeField] private float foulZ = 1f;
        [SerializeField] private float headPinZ = 19.29f;
        [SerializeField] private Vector3 waitSpot = new Vector3(1.7f, 0f, -0.9f);

        public bool Busy { get; private set; }

        public void Setup(RivalAvatar a, BowlingBall b, BowlingPin[] p, Transform rack, float foul, float headPin)
        {
            avatar = a; ball = b; pins = p; rackPoint = rack; foulZ = foul; headPinZ = headPin;
        }

        private void Start() => Park();

        public void Show(bool on) => gameObject.SetActive(on);

        /// <summary>Встать на место ожидания лицом к дорожке.</summary>
        public void Park()
        {
            StopAllCoroutines(); // свой компонент — можно
            Busy = false;
            avatar.Teleport(waitSpot);
            avatar.transform.rotation = Quaternion.identity;
            avatar.LookAt(waitSpot + Vector3.forward);
            avatar.RestHands(0f);
            avatar.Say("");
        }

        public void TakeTurn(RivalLevel level, bool firstRoll, int frame)
        {
            // Если робот ещё возвращается после прошлого броска — прерываем это и начинаем ход.
            StopAllCoroutines();
            StartCoroutine(TurnRoutine(level, firstRoll, frame));
        }

        private IEnumerator TurnRoutine(RivalLevel level, bool firstRoll, int frame)
        {
            Busy = true;
            avatar.Say(firstRoll ? $"Фрейм {frame + 1}" : "Добиваю");

            // 1. Подойти к подставке и взять шар.
            var rack = rackPoint.position;
            yield return Walk(new Vector3(rack.x + 0.33f, 0f, rack.z - 0.2f), rack);
            avatar.SetHandWorld(AvatarSide.Right, rack, 1.5f);
            while (!avatar.HandArrived(AvatarSide.Right)) yield return null;

            // 2. Выйти на разбег, шар — в руке (кинематический, следует за кистью).
            var hand = avatar.Hand(AvatarSide.Right);
            float releaseX = Mathf.Clamp(Random.Range(0.05f, 0.25f), -0.3f, 0.3f);
            var approach = new Vector3(releaseX - 0.22f, 0f, -0.9f);
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.25f, 1.0f, 0.2f), 1.2f);
            StartCoroutine(Walk(approach, approach + Vector3.forward * 10f));
            while (!avatar.Arrived) { ball.BotHold(hand.position); yield return null; }
            float t0 = Time.time;
            while (Time.time - t0 < 0.5f) { ball.BotHold(hand.position); yield return null; }

            // 3. Замах назад и разбег вперёд: рука идёт по дуге от бедра назад и вниз к полу.
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.24f, 0.85f, -0.35f), 2.2f);
            for (t0 = Time.time; Time.time - t0 < 0.45f;) { ball.BotHold(hand.position); yield return null; }

            avatar.MoveTo(new Vector3(approach.x, 0f, foulZ - 0.65f), 1.6f);
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.22f, 0.13f, 0.5f), 3.4f);
            while (!avatar.HandArrived(AvatarSide.Right)) { ball.BotHold(hand.position); yield return null; }

            // 4. Бросок.
            Launch(level, firstRoll, hand.position);
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.25f, 1.25f, 0.45f), 2.5f); // «проводка» руки
            yield return new WaitForSeconds(1.0f);

            // 5. Уйти на место ожидания.
            avatar.RestHands(1.5f);
            yield return Walk(waitSpot, waitSpot + Vector3.forward);
            avatar.Say("");
            Busy = false;
        }

        private void Launch(RivalLevel level, bool firstRoll, Vector3 handPos)
        {
            float r = ball.Radius;
            var release = new Vector3(Mathf.Clamp(handPos.x, -0.4f, 0.4f), r + 0.003f, foulZ - 0.25f);

            float aimX = firstRoll ? 0.06f : SpareAim();

            // «Неудачный бросок»: рука дрогнула — шар уходит далеко от прицела (часто в жёлоб).
            // У новичка такое бывает часто, у мастера — почти никогда. Это делает результат бота
            // разнообразным: то страйк, то 2–3 кегли, то «мимо».
            float badChance = Rival.Pick(level, 0.35f, 0.15f, 0.03f);
            if (Random.value < badChance)
                aimX += (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.3f, 0.75f);
            else
                aimX += Rival.Gaussian(Rival.Pick(level, 0.25f, 0.13f, 0.04f));
            aimX = Mathf.Clamp(aimX, -0.75f, 0.75f);

            var target = new Vector3(aimX, release.y, headPinZ);
            Vector3 dir = (target - release).normalized;
            float speed = Rival.Pick(level, 5.6f, 6.8f, 8.0f) + Rival.Gaussian(Rival.Pick(level, 1.0f, 0.6f, 0.15f));
            speed = Mathf.Max(3.5f, speed);
            Vector3 v = dir * speed;
            Vector3 w = Vector3.Cross(Vector3.up, v) / r;   // качение без проскальзывания
            // Случайная боковая закрутка: на сухом участке дорожки шар непредсказуемо уходит в дугу.
            w += dir * Rival.Gaussian(Rival.Pick(level, 5f, 3f, 0.5f));
            ball.BotLaunch(release, v, w);
        }

        /// <summary>Добивание: середина между ближней стоящей кеглей и центром всех оставшихся.</summary>
        private float SpareAim()
        {
            float sumX = 0f; int n = 0;
            float frontZ = float.MaxValue, frontX = 0f;
            foreach (var p in pins)
            {
                if (!p.IsStanding) continue;
                var pos = p.transform.position;
                sumX += pos.x; n++;
                if (pos.z < frontZ) { frontZ = pos.z; frontX = pos.x; }
            }
            return n == 0 ? 0f : (frontX + sumX / n) / 2f;
        }

        private IEnumerator Walk(Vector3 to, Vector3 lookAt)
        {
            avatar.LookAt(to);
            avatar.MoveTo(to, 1.4f);
            while (!avatar.Arrived) yield return null;
            avatar.LookAt(lookAt);
        }
    }
}
