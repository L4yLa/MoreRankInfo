using System;
using BeatSaberMarkupLanguage.MenuButtons;
using Zenject;

namespace MoreRankInfo.UI
{
    /// <summary>Adds the MoreRankInfo button to the mod button list on the left of the main menu.</summary>
    internal class MenuButtonRegistrar : IInitializable, IDisposable
    {
        private readonly MenuButtons _menuButtons;
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private readonly RankInfoSettingsFlowCoordinator _settingsFlowCoordinator;
        private MenuButton _button;

        public MenuButtonRegistrar(MenuButtons menuButtons, MainFlowCoordinator mainFlowCoordinator, RankInfoSettingsFlowCoordinator settingsFlowCoordinator)
        {
            _menuButtons = menuButtons;
            _mainFlowCoordinator = mainFlowCoordinator;
            _settingsFlowCoordinator = settingsFlowCoordinator;
        }

        public void Initialize()
        {
            _button = new MenuButton("MoreRankInfo", "Settings for the rank info shown above the leaderboard", Present);
            _menuButtons.RegisterButton(_button);
        }

        public void Dispose()
        {
            if (_button != null)
                _menuButtons.UnregisterButton(_button);
            _button = null;
        }

        private void Present()
        {
            _mainFlowCoordinator.PresentFlowCoordinator(_settingsFlowCoordinator);
        }
    }
}
