using VRSports.Core;

namespace VRSports.Modes.Bowling
{
    /// <summary>Боулинг: 10 фреймов, без таймера. Логика — на этапе 2.</summary>
    public class BowlingMode : SportModeBase
    {
        public override string ModeId => "bowling";
        public override string DisplayName => "Боулинг";

        protected override void OnGameStarted() => ShowMessage("Режим в разработке");
    }
}
