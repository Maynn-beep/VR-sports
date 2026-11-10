using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRSports.Core
{
    /// <summary>
    /// Объёмная кнопка в мире. Нажимается лучом (курок/grip) или рукой вблизи.
    ///
    /// Используем XRSimpleInteractable из XR Interaction Toolkit: он сообщает,
    /// когда на объект навели (hover) и когда его выбрали (select).
    /// Кнопка при наведении подсвечивается, при нажатии «проседает» и даёт вибрацию.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class VRButton : MonoBehaviour
    {
        [SerializeField] private Transform cap;           // подвижная «шляпка» кнопки
        [SerializeField] private Renderer capRenderer;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField] private float pressDepth = 0.015f;
        [SerializeField] private float cooldown = 0.4f;   // защита от двойного нажатия

        public UnityEvent onPressed = new UnityEvent();

        /// <summary>То же, что onPressed, но удобнее для подписки из кода.</summary>
        public event Action Pressed;

        private XRSimpleInteractable _interactable;
        private Vector3 _capRestPos;
        private float _lastPressTime = -10f;
        private float _pressAnim;  // 1 → кнопка нажата, затухает до 0
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _mpb = new MaterialPropertyBlock();
            if (cap != null) _capRestPos = cap.localPosition;
            SetColor(normalColor);
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelect);
            _interactable.hoverEntered.AddListener(OnHoverEnter);
            _interactable.hoverExited.AddListener(OnHoverExit);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelect);
            _interactable.hoverEntered.RemoveListener(OnHoverEnter);
            _interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            SetColor(hoverColor);
            Haptics.Pulse(args.interactorObject.handedness, 0.1f, 0.03f);
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            if (!_interactable.isHovered) SetColor(normalColor);
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            if (Time.time - _lastPressTime < cooldown) return;
            _lastPressTime = Time.time;
            _pressAnim = 1f;
            Haptics.Pulse(args.interactorObject.handedness, 0.5f, 0.08f);
            onPressed.Invoke();
            Pressed?.Invoke();
        }

        private void Update()
        {
            if (cap == null || _pressAnim <= 0f) return;
            _pressAnim = Mathf.MoveTowards(_pressAnim, 0f, Time.deltaTime * 4f);
            cap.localPosition = _capRestPos + Vector3.down * (pressDepth * _pressAnim);
        }

        private void SetColor(Color c)
        {
            if (capRenderer == null) return;
            // MaterialPropertyBlock меняет цвет только этому объекту,
            // не создавая копию материала (меньше нагрузка, батчинг сохраняется).
            capRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            capRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Настройка из редактора-сборщика сцен.</summary>
        public void Setup(Transform capTransform, Renderer capRend, Color normal)
        {
            cap = capTransform;
            capRenderer = capRend;
            normalColor = normal;
            hoverColor = Color.Lerp(normal, Color.white, 0.45f);
        }
    }
}
