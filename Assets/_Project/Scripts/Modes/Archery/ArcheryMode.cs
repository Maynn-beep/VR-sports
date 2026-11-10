using VRSports.Core;

namespace VRSports.Modes.Archery
{
    /// <summary>Стрельба из лука: волны мишеней. Логика — на этапе 4.</summary>
    public class ArcheryMode : SportModeBase
    {
        public override string ModeId => "archery";
        public override string DisplayName => "Стрельба из лука";

        protected override void OnGameStarted() => ShowMessage("Режим в разработке");
    }
}
