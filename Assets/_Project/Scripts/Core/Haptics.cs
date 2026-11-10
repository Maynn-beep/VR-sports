using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSports.Core
{
    /// <summary>
    /// Вибрация контроллеров (тактильная отдача).
    /// Используем универсальный API UnityEngine.XR.InputDevices — он работает
    /// через OpenXR на любом шлеме, в том числе на Quest 2.
    /// </summary>
    public static class Haptics
    {
        /// <param name="amplitude">Сила 0..1</param>
        /// <param name="duration">Длительность в секундах</param>
        public static void Pulse(XRNode hand, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }

        public static void Pulse(InteractorHandedness handedness, float amplitude, float duration)
        {
            switch (handedness)
            {
                case InteractorHandedness.Left: Pulse(XRNode.LeftHand, amplitude, duration); break;
                case InteractorHandedness.Right: Pulse(XRNode.RightHand, amplitude, duration); break;
            }
        }

        public static void PulseBoth(float amplitude, float duration)
        {
            Pulse(XRNode.LeftHand, amplitude, duration);
            Pulse(XRNode.RightHand, amplitude, duration);
        }
    }
}
