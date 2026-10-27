using System;
using Zenject;

namespace MoreRankInfo.Services
{
    /// <summary>
    /// Tells whether the menu (including the results screen) is the active scene.
    /// When a level starts, GameScenesManager deactivates the menu scene's root objects
    /// (or reparents them under a disabled object), so the menu hierarchy is inactive while playing.
    /// </summary>
    internal class MenuState : IInitializable, IDisposable
    {
        private readonly GameScenesManager _gameScenesManager;
        private readonly PlatformLeaderboardViewController _leaderboardViewController;

        /// <summary>Raised on the main thread when a scene transition into the menu has finished.</summary>
        internal event Action EnteredMenu;

        public MenuState(GameScenesManager gameScenesManager, PlatformLeaderboardViewController leaderboardViewController)
        {
            _gameScenesManager = gameScenesManager;
            _leaderboardViewController = leaderboardViewController;
        }

        internal bool IsInMenu =>
            _leaderboardViewController != null
            && !_gameScenesManager.isInTransition
            && _leaderboardViewController.transform.root.gameObject.activeInHierarchy;

        public void Initialize()
        {
            _gameScenesManager.transitionDidFinishEvent += OnTransitionDidFinish;
        }

        public void Dispose()
        {
            _gameScenesManager.transitionDidFinishEvent -= OnTransitionDidFinish;
        }

        private void OnTransitionDidFinish(GameScenesManager.SceneTransitionType transitionType, ScenesTransitionSetupDataSO setupData, DiContainer container)
        {
            if (IsInMenu)
                EnteredMenu?.Invoke();
        }
    }
}
