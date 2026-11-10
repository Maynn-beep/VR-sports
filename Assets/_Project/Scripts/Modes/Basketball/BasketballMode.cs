using VRSports.Core;

namespace VRSports.Modes.Basketball
{
    /// <summary>Баскетбол: аркада на 60 секунд. Логика — на этапе 3.</summary>
    public class BasketballMode : SportModeBase
    {
        public override string ModeId => "basketball";
        public override string DisplayName => "Баскетбол";

        // Reset вызывается редактором при добавлении компонента — задаём таймер по умолчанию.
        private void Reset() => RoundDuration = 60f;

        protected override void OnGameStarted() => ShowMessage("Режим в разработке");
    }
}
