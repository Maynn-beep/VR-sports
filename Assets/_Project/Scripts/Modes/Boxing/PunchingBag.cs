using TMPro;
using UnityEngine;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// Боксёрская груша 30 кг на шарнире (ConfigurableJoint: перемещения заблокированы,
    /// вращения свободны — груша качается вокруг точки подвеса, как маятник).
    ///
    /// Удар — это передача импульса: p = m_эфф · v_⊥,
    /// где m_эфф ≈ 2 кг — «эффективная масса» удара (кулак + часть предплечья),
    /// v_⊥ — составляющая скорости кулака в сторону груши. Импульс прикладывается в точке
    /// удара (AddForceAtPosition), поэтому удар сверху/сбоку ещё и закручивает грушу.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PunchingBag : MonoBehaviour
    {
        [SerializeField] private float effectiveMass = 2f;
        [SerializeField] private TMP_Text readout;

        public float LastSpeed { get; private set; }

        private Rigidbody _rb;

        public void Setup(TMP_Text text) => readout = text;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        public void ReceivePunch(BoxingGlove glove, Vector3 velocity, Vector3 point)
        {
            Vector3 toBag = _rb.worldCenterOfMass - point;
            toBag.y = 0f;
            if (toBag.sqrMagnitude < 1e-6f) return;
            toBag.Normalize();

            float speed = Vector3.Dot(velocity, toBag);   // скорость кулака В грушу
            if (speed < 0.5f) return;

            Vector3 impulse = toBag * (effectiveMass * speed);
            _rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);

            LastSpeed = speed;
            glove.Feedback(Mathf.Clamp01(speed / 7f));
            if (readout != null)
                readout.text = $"Удар: {speed:F1} м/с\nИмпульс: {impulse.magnitude:F1} кг·м/с";
        }
    }
}
