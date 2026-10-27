using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IPA.Loader;
using MoreRankInfo.Configuration;
using MoreRankInfo.Models;
using OculusStudios.Platform.Core;
using Zenject;

namespace MoreRankInfo.Services
{
    internal enum FetchStatus
    {
        NotLoaded,
        Loading,
        Loaded,
        Failed
    }

    /// <summary>Per-service fetch state.</summary>
    internal class ServiceTracker
    {
        internal RankService Service { get; }
        internal FetchStatus Status { get; set; } = FetchStatus.NotLoaded;
        internal ServiceState State { get; set; }
        internal DateTime LastSuccess { get; set; } = DateTime.MinValue;

        internal bool Busy;
        internal bool Pending;
        internal bool PendingAfterUpload;
        internal DateTime NotBefore = DateTime.MinValue;

        internal ServiceTracker(RankService service)
        {
            Service = service;
        }
    }

    /// <summary>
    /// Fetches player data, keeps the daily baseline and the last pp-changing play.
    /// All network work starts only while the menu is active; requests made during gameplay
    /// are kept pending and run when the menu becomes active again.
    /// </summary>
    internal class RankDataManager : IInitializable, IDisposable
    {
        private static readonly TimeSpan RefreshOnShowInterval = TimeSpan.FromMinutes(5);

        private readonly IPlatform _platform;
        private readonly MenuState _menuState;
        private readonly RankApiClient _api;
        private readonly StateStore _store;
        private readonly Dictionary<RankService, ServiceTracker> _trackers = new Dictionary<RankService, ServiceTracker>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private string _userId;

        /// <summary>Raised on the main thread whenever a service's data or status changed.</summary>
        internal event Action<RankService> DataChanged;

        public RankDataManager(IPlatform platform, MenuState menuState, RankApiClient api, StateStore store)
        {
            _platform = platform;
            _menuState = menuState;
            _api = api;
            _store = store;

            // Done here (not in Initialize) so other IInitializables can query IsInstalled in any order.
            foreach (RankService service in Enum.GetValues(typeof(RankService)))
            {
                if (PluginManager.GetPluginFromId(service.PluginId()) != null)
                    _trackers[service] = new ServiceTracker(service);
            }
        }

        public void Initialize()
        {
            Plugin.Log.Info($"Leaderboard mods found: {(_trackers.Count == 0 ? "none" : string.Join(", ", _trackers.Keys))}");

            _menuState.EnteredMenu += RunPending;
            foreach (var service in _trackers.Keys)
                RequestRefresh(service, false, 0);
        }

        public void Dispose()
        {
            _menuState.EnteredMenu -= RunPending;
            _cts.Cancel();
            _cts.Dispose();
        }

        internal bool IsInstalled(RankService service) => _trackers.ContainsKey(service);

        internal ServiceTracker GetTracker(RankService service)
        {
            _trackers.TryGetValue(service, out var tracker);
            return tracker;
        }

        /// <summary>Refreshes when the data is stale (new game day or older than a few minutes).</summary>
        internal void RefreshIfStale(RankService service)
        {
            var tracker = GetTracker(service);
            if (tracker == null || tracker.Busy || tracker.Pending)
                return;
            var stale = tracker.Status != FetchStatus.Loaded
                || tracker.State?.BaselineDay != GameDay()
                || DateTime.Now - tracker.LastSuccess > RefreshOnShowInterval;
            if (stale)
                RequestRefresh(service, false, 0);
        }

        /// <param name="afterUpload">true when triggered by a score upload; only these refreshes record a play change.</param>
        internal void RequestRefresh(RankService service, bool afterUpload, int delaySeconds)
        {
            var tracker = GetTracker(service);
            if (tracker == null || _cts.IsCancellationRequested)
                return;

            tracker.Pending = true;
            tracker.PendingAfterUpload |= afterUpload;
            if (delaySeconds > 0)
            {
                var notBefore = DateTime.Now.AddSeconds(delaySeconds);
                if (notBefore > tracker.NotBefore)
                    tracker.NotBefore = notBefore;
                _ = DelayThenRunAsync(tracker, delaySeconds);
                return;
            }
            TryRun(tracker);
        }

        private async Task DelayThenRunAsync(ServiceTracker tracker, int delaySeconds)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            TryRun(tracker);
        }

        private void RunPending()
        {
            foreach (var tracker in _trackers.Values)
                TryRun(tracker);
        }

        private void TryRun(ServiceTracker tracker)
        {
            if (!tracker.Pending || tracker.Busy || _cts.IsCancellationRequested)
                return;
            if (DateTime.Now < tracker.NotBefore)
                return; // the delayed call will run it
            if (!_menuState.IsInMenu)
                return; // EnteredMenu will run it
            _ = RunAsync(tracker);
        }

        private async Task RunAsync(ServiceTracker tracker)
        {
            tracker.Busy = true;
            var afterUpload = tracker.PendingAfterUpload;
            tracker.Pending = false;
            tracker.PendingAfterUpload = false;
            try
            {
                if (tracker.Status != FetchStatus.Loaded)
                {
                    tracker.Status = FetchStatus.Loading;
                    DataChanged?.Invoke(tracker.Service);
                }

                var userId = await GetUserIdAsync();
                if (_cts.IsCancellationRequested)
                    return;
                if (userId == null)
                {
                    tracker.Status = FetchStatus.Failed;
                    return;
                }
                if (tracker.State == null)
                {
                    // Show today's saved values while the first request is in flight.
                    tracker.State = _store.Get(tracker.Service, userId);
                    DataChanged?.Invoke(tracker.Service);
                }

                var snapshot = await _api.GetPlayerAsync(tracker.Service, userId, _cts.Token);
                if (_cts.IsCancellationRequested)
                    return;
                if (snapshot == null)
                {
                    if (tracker.Status != FetchStatus.Loaded)
                        tracker.Status = FetchStatus.Failed;
                    return;
                }

                await ApplySnapshotAsync(tracker, userId, snapshot, afterUpload);
                if (_cts.IsCancellationRequested)
                    return;
                _store.Save();
                tracker.Status = FetchStatus.Loaded;
                tracker.LastSuccess = DateTime.Now;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"{tracker.Service} refresh failed: {ex.Message}");
                Plugin.Log.Debug(ex);
                if (tracker.Status != FetchStatus.Loaded)
                    tracker.Status = FetchStatus.Failed;
            }
            finally
            {
                tracker.Busy = false;
                if (!_cts.IsCancellationRequested)
                {
                    DataChanged?.Invoke(tracker.Service);
                    TryRun(tracker);
                }
            }
        }

        private async Task ApplySnapshotAsync(ServiceTracker tracker, string userId, PlayerSnapshot snapshot, bool afterUpload)
        {
            var state = tracker.State;
            var previous = state.Latest;

            var day = GameDay();
            if (state.BaselineDay != day || state.Baseline == null)
            {
                // A new game day starts from the value before the first play of the day.
                state.Baseline = afterUpload && previous != null ? previous : snapshot;
                state.BaselineDay = day;
            }

            if (afterUpload && previous != null && Math.Abs(snapshot.PP - previous.PP) >= 0.005)
            {
                var recent = await _api.GetRecentPlayAsync(tracker.Service, userId, _cts.Token);
                state.LastChange = new PlayChange
                {
                    SongName = recent?.SongName,
                    Mapper = recent?.Mapper,
                    Difficulty = recent?.Difficulty,
                    DeltaPP = snapshot.PP - previous.PP,
                    BeforePP = previous.PP,
                    Time = DateTime.Now.ToString("o")
                };
            }

            state.Latest = snapshot;
        }

        // Same source as ScoreSaber (Legato GamePlatformAdapter) on 1.44: IPlatform.user.userId.
        private Task<string> GetUserIdAsync()
        {
            if (_userId == null)
            {
                try
                {
                    var user = _platform?.user;
                    if (user != null && user.userId != 0)
                        _userId = user.userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else
                        Plugin.Log.Warn("Platform user is not available yet.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Failed to get the platform user: {ex.Message}");
                    Plugin.Log.Debug(ex);
                }
            }
            return Task.FromResult(_userId);
        }

        internal static string GameDay()
        {
            var hour = Math.Max(0, Math.Min(23, PluginConfig.Instance.DateChangeHour));
            return DateTime.Now.AddHours(-hour).ToString("yyyy-MM-dd");
        }
    }
}
