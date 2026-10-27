using HMUI;
using Zenject;

namespace MoreRankInfo.UI
{
    /// <summary>Settings screen opened from the MoreRankInfo button in the main menu's mod button list.</summary>
    internal class RankInfoSettingsFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator;
        private SettingsViewController _settingsViewController;

        [Inject]
        public void Construct(MainFlowCoordinator mainFlowCoordinator, SettingsViewController settingsViewController)
        {
            _mainFlowCoordinator = mainFlowCoordinator;
            _settingsViewController = settingsViewController;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("MoreRankInfo");
                showBackButton = true;
            }
            if (addedToHierarchy)
                ProvideInitialViewControllers(_settingsViewController);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
