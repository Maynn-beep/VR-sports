using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Хват лука без «перехвата».
    ///
    /// По умолчанию XRGrabInteractable в режиме Single отдаёт объект руке, которая взяла его последней.
    /// Для лука это ломает игру: вторая рука тянется к тетиве — и лук «перепрыгивает» в неё.
    /// Поэтому, пока лук в руке, выбрать его может только эта же рука (IsSelectableBy возвращает false
    /// для всех остальных), и ближайшим доступным объектом для второй руки становится тетива.
    /// </summary>
    public class BowGrabInteractable : XRGrabInteractable
    {
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (isSelected && !IsSelectedBy(interactor)) return false;
            return base.IsSelectableBy(interactor);
        }

        private bool IsSelectedBy(IXRSelectInteractor interactor)
        {
            foreach (var i in interactorsSelecting)
                if (i == interactor) return true;
            return false;
        }
    }
}
