using System;
using System.Collections.Generic;
using IPA.Loader;
using MoreRankInfo.Models;
using MoreRankInfo.UI;
using UnityEngine;
using Zenject;

namespace MoreRankInfo.Services
{
    /// <summary>
    /// Tracks which leaderboard mod's view is currently shown. LeaderboardCore activates the selected custom
    /// leaderboard's view controller and deactivates its GameObject when hidden, and the whole leaderboard goes
    /// inactive with its parent, so the GameObject's active state is exactly "shown on screen".
    /// </summary>
    internal class LeaderboardVisibilityWatcher : IInitializable, IDisposable
    {
        // View controllers bound as singletons in the menu container.
        private static readonly Dictionary<RankService, string> ViewTypes = new Dictionary<RankService, string>
        {
            { RankService.ScoreSaber, "ScoreSaber.Features.Leaderboards.Adapters.LeaderboardCore.ScoreSaberLeaderboardCoreViewController" },
            { RankService.BeatLeader, "BeatLeader.ViewControllers.LeaderboardView" }
        };

        private readonly DiContainer _container;
        private readonly RankDataManager _dataManager;
        private readonly Dictionary<RankService, VisibilityProbe> _probes = new Dictionary<RankService, VisibilityProbe>();
        private readonly Dictionary<RankService, Action<bool>> _handlers = new Dictionary<RankService, Action<bool>>();

        /// <summary>The service whose leaderboard is shown, or null.</summary>
        internal RankService? Visible { get; private set; }

        internal event Action VisibleChanged;

        public LeaderboardVisibilityWatcher(DiContainer container, RankDataManager dataManager)
        {
            _container = container;
            _dataManager = dataManager;
        }

        public void Initialize()
        {
            foreach (var pair in ViewTypes)
            {
                if (_dataManager.IsInstalled(pair.Key))
                    Attach(pair.Key, pair.Value);
            }
            UpdateVisible(null);
        }

        public void Dispose()
        {
            foreach (var pair in _probes)
            {
                if (pair.Value == null)
                    continue;
                pair.Value.VisibilityChanged -= _handlers[pair.Key];
                UnityEngine.Object.Destroy(pair.Value);
            }
            _probes.Clear();
            _handlers.Clear();
        }

        private void Attach(RankService service, string typeName)
        {
            try
            {
                var type = PluginManager.GetPluginFromId(service.PluginId())?.Assembly.GetType(typeName);
                if (type == null)
                    throw new MissingMemberException($"type {typeName} not found");
                var view = _container.TryResolve(type) as Component;
                if (view == null)
                    throw new MissingMemberException($"{typeName} is not bound in the menu container");

                var probe = view.gameObject.GetComponent<VisibilityProbe>() ?? view.gameObject.AddComponent<VisibilityProbe>();
                Action<bool> handler = visible => UpdateVisible(visible ? service : (RankService?)null, service);
                probe.VisibilityChanged += handler;
                _probes[service] = probe;
                _handlers[service] = handler;
                Plugin.Log.Debug($"Watching {service} leaderboard view ({typeName}).");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"{service} leaderboard view cannot be watched ({ex.Message}); its info will not be shown.");
                Plugin.Log.Debug(ex);
            }
        }

        private void UpdateVisible(RankService? shown, RankService? changed = null)
        {
            RankService? next;
            if (shown != null)
                next = shown;
            else if (changed != null && Visible != changed)
                next = Visible; // another view's hide event; keep the current one
            else
                next = FindVisible();

            if (next == Visible)
                return;
            Visible = next;
            VisibleChanged?.Invoke();
        }

        private RankService? FindVisible()
        {
            foreach (var pair in _probes)
            {
                if (pair.Value != null && pair.Value.IsVisible)
                    return pair.Key;
            }
            return null;
        }
    }
}
