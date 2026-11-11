using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSports.Core
{
    /// <summary>
    /// Запрет «телекинеза»: предмет можно взять только рукой вблизи.
    ///
    /// По умолчанию NearFarInteractor из XR Interaction Toolkit умеет хватать лучом издалека:
    /// предмет прилетает в руку, и его можно бросить с силой, которую рука сама не развила бы.
    /// Для спортивного симулятора это ломает физику броска.
    ///
    /// Решение — фильтр выбора XRI (IXRSelectFilter): перед каждым захватом XRI спрашивает фильтры
    /// «можно ли?», а мы отвечаем «да» только если контроллер ближе maxDistance к коллайдеру предмета.
    /// Луч при этом продолжает работать для кнопок и телепорта — фильтр стоит только на предметах.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public class NearOnlyGrab : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("Макс. расстояние от контроллера до поверхности предмета, м.")]
        [SerializeField] private float maxDistance = 0.2f;

        private XRBaseInteractable _interactable;

        public bool canProcess => isActiveAndEnabled;

        private void Awake()
        {
            _interactable = GetComponent<XRBaseInteractable>();
            _interactable.selectFilters.Add(this);
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.selectFilters.Remove(this);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            Vector3 hand = interactor.transform.position;
            float maxSqr = maxDistance * maxDistance;
            foreach (var col in _interactable.colliders)
            {
                if (col == null || !col.enabled) continue;
                if ((col.ClosestPoint(hand) - hand).sqrMagnitude <= maxSqr) return true;
            }
            return false;
        }
    }
}
