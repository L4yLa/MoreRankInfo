using MoreRankInfo.Services;
using MoreRankInfo.UI;
using Zenject;

namespace MoreRankInfo.Installers
{
    /// <summary>Everything lives in the menu container; nothing is installed into gameplay scenes.</summary>
    internal class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<RankApiClient>().AsSingle();
            Container.Bind<StateStore>().AsSingle();
            Container.BindInterfacesAndSelfTo<MenuState>().AsSingle();
            Container.BindInterfacesAndSelfTo<RankDataManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<ScoreUploadWatcher>().AsSingle();
            Container.BindInterfacesAndSelfTo<LeaderboardVisibilityWatcher>().AsSingle();
            Container.BindInterfacesAndSelfTo<RankInfoView>().AsSingle();
            Container.Bind<SettingsViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<RankInfoSettingsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            Container.BindInterfacesTo<MenuButtonRegistrar>().AsSingle();
        }
    }
}
