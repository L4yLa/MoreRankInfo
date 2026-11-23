using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Set when the data needed for the display could not be loaded. No request is made until the
        /// Retry button succeeds; score uploads are ignored meanwhile.
        /// </summary>
        internal bool Stopped { get; set; }

        internal bool Busy;
        internal bool Pending;
        internal bool PendingAfterUpload;
        internal bool PendingIsRetry;
        internal bool PendingManual;
        internal DateTime NotBefore = DateTime.MinValue;

        internal ServiceTracker(RankService service)
        {
            Service = service;
        }
    }

    /// <summary>
    /// Fetches player data once when the menu starts and after every score upload, keeps the daily baseline
    /// and the last pp-changing play. Data is also fetched when the shown values belong to an earlier game day.
    /// All network work starts only while the menu is active; requests made during gameplay are kept pending
    /// and run when the menu becomes active again.
    /// </summary>
    internal class RankDataManager : IInitializable, IDisposable
    {
        private const int RetryDelaySeconds = 5;

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

        /// <summary>Fetches only when there is nothing to show for the current game day.</summary>
        internal void RefreshIfNeeded(RankService service)
        {
            var tracker = GetTracker(service);
            if (tracker == null || tracker.Stopped || tracker.Busy || tracker.Pending || tracker.Status == FetchStatus.NotLoaded)
                return;
            var state = tracker.State;
            if (state?.Latest != null && state.Baseline != null && state.BaselineDay == GameDay())
                return;
            RequestRefresh(service, false, 0);
        }

        /// <param name="afterUpload">true when triggered by a score upload; only these refreshes record a play change.</param>
        internal void RequestRefresh(RankService service, bool afterUpload, int delaySeconds)
        {
            var tracker = GetTracker(service);
            if (tracker == null || tracker.Stopped || _cts.IsCancellationRequested)
                return;
            Schedule(tracker, afterUpload, false, false, delaySeconds);
        }

        /// <summary>Called by the Retry button while the service is stopped. One attempt, no automatic retry.</summary>
        internal void RetryNow(RankService service)
        {
            var tracker = GetTracker(service);
            if (tracker == null || !tracker.Stopped || tracker.Busy || tracker.Pending || _cts.IsCancellationRequested)
                return;

            tracker.Status = FetchStatus.Loading;
            DataChanged?.Invoke(service);
            Schedule(tracker, false, false, true, 0);
        }

        private void Schedule(ServiceTracker tracker, bool afterUpload, bool isRetry, bool manual, int delaySeconds)
        {
            // A new request replaces a pending retry, so it gets its own retry.
            tracker.PendingIsRetry = tracker.Pending ? tracker.PendingIsRetry && isRetry : isRetry;
            tracker.Pending = true;
            tracker.PendingAfterUpload |= afterUpload;
            tracker.PendingManual |= manual;
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
            if (tracker.Stopped && !tracker.PendingManual)
            {
                ClearPending(tracker);
                return;
            }
            if (DateTime.Now < tracker.NotBefore)
                return; // the delayed call will run it
            if (!_menuState.IsInMenu)
                return; // EnteredMenu will run it
            _ = RunAsync(tracker);
        }

        private static void ClearPending(ServiceTracker tracker)
        {
            tracker.Pending = false;
            tracker.PendingAfterUpload = false;
            tracker.PendingIsRetry = false;
            tracker.PendingManual = false;
        }

        private async Task RunAsync(ServiceTracker tracker)
        {
            var service = tracker.Service;
            var afterUpload = tracker.PendingAfterUpload;
            var isRetry = tracker.PendingIsRetry;
            var manual = tracker.PendingManual;
            ClearPending(tracker);
            tracker.Busy = true;
            var failed = false;
            try
            {
                if (tracker.Status != FetchStatus.Loaded && tracker.Status != FetchStatus.Loading)
                {
                    tracker.Status = FetchStatus.Loading;
                    DataChanged?.Invoke(service);
                }

                var userId = GetUserId();
                if (userId == null)
                {
                    failed = true;
                    return;
                }
                if (tracker.State == null)
                {
                    // Show today's saved values while the first request is in flight.
                    tracker.State = _store.Get(service, userId);
                    DataChanged?.Invoke(service);
                }

                var snapshot = await _api.GetPlayerAsync(service, userId, _cts.Token);
                if (_cts.IsCancellationRequested)
                    return;
                if (snapshot == null)
                {
                    failed = true;
                    return;
                }

                await ApplySnapshotAsync(tracker, userId, snapshot, afterUpload);
                if (_cts.IsCancellationRequested)
                    return;
                _store.Save();
                tracker.Status = FetchStatus.Loaded;
                if (tracker.Stopped)
                {
                    tracker.Stopped = false;
                    Plugin.Log.Info($"{service}: data loaded, resumed.");
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"{service} refresh failed: {ex.Message}");
                Plugin.Log.Debug(ex);
                failed = true;
            }
            finally
            {
                tracker.Busy = false;
                if (!_cts.IsCancellationRequested)
                {
                    if (failed)
                        HandleFailure(tracker, afterUpload, isRetry, manual);
                    DataChanged?.Invoke(service);
                    TryRun(tracker);
                }
            }
        }

        private void HandleFailure(ServiceTracker tracker, bool afterUpload, bool isRetry, bool manual)
        {
            var service = tracker.Service;
            if (!isRetry && !manual)
            {
                Plugin.Log.Debug($"{service}: fetch failed, retrying in {RetryDelaySeconds}s.");
                Schedule(tracker, afterUpload, true, false, RetryDelaySeconds);
                return;
            }
            if (afterUpload && tracker.Status == FetchStatus.Loaded)
            {
                Plugin.Log.Warn($"{service}: refresh after the score upload failed; skipped.");
                return;
            }
            tracker.Stopped = true;
            tracker.Status = FetchStatus.Failed;
            Plugin.Log.Warn($"{service}: data could not be loaded; stopped until Retry is pressed.");
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
                var recent = await GetRecentPlayWithRetryAsync(tracker.Service, userId);
                if (_cts.IsCancellationRequested)
                    return;
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

        /// <summary>Returns null when both attempts failed. The retry waits until the menu is active.</summary>
        private async Task<RecentPlay> GetRecentPlayWithRetryAsync(RankService service, string userId)
        {
            var recent = await _api.GetRecentPlayAsync(service, userId, _cts.Token);
            if (recent != null || _cts.IsCancellationRequested)
                return recent;

            Plugin.Log.Debug($"{service}: recent play fetch failed, retrying in {RetryDelaySeconds}s.");
            await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), _cts.Token);
            await WaitForMenuAsync();
            recent = await _api.GetRecentPlayAsync(service, userId, _cts.Token);
            if (recent == null && !_cts.IsCancellationRequested)
                Plugin.Log.Warn($"{service}: recent play could not be loaded; the song is shown as unknown.");
            return recent;
        }

        private Task WaitForMenuAsync()
        {
            if (_menuState.IsInMenu)
                return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            Action handler = null;
            handler = () =>
            {
                _menuState.EnteredMenu -= handler;
                tcs.TrySetResult(true);
            };
            _menuState.EnteredMenu += handler;
            _cts.Token.Register(() =>
            {
                _menuState.EnteredMenu -= handler;
                tcs.TrySetCanceled();
            });
            return tcs.Task;
        }

        // Same source as ScoreSaber (Legato GamePlatformAdapter) on 1.44: IPlatform.user.userId.
        private string GetUserId()
        {
            if (_userId == null)
            {
                try
                {
                    var user = _platform?.user;
                    if (user != null && user.userId != 0)
                        _userId = user.userId.ToString(CultureInfo.InvariantCulture);
                    else
                        Plugin.Log.Warn("Platform user is not available yet.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Failed to get the platform user: {ex.Message}");
                    Plugin.Log.Debug(ex);
                }
            }
            return _userId;
        }

        internal static string GameDay()
        {
            var hour = Math.Max(0, Math.Min(23, PluginConfig.Instance.DateChangeHour));
            return DateTime.Now.AddHours(-hour).ToString("yyyy-MM-dd");
        }
    }
}
