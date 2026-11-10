using VRSports.Core;

namespace VRSports.Modes.Boxing
{
    /// <summary>Бокс: 3 раунда по 60 секунд. Логика — на этапе 5.</summary>
    public class BoxingMode : SportModeBase
    {
        public override string ModeId => "boxing";
        public override string DisplayName => "Бокс";

        private void Reset() => RoundDuration = 60f;

        protected override void OnGameStarted() => ShowMessage("Режим в разработке");
    }
}
