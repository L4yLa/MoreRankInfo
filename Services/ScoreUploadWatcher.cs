using System;
using System.Reflection;
using System.Threading;
using IPA.Loader;
using MoreRankInfo.Configuration;
using MoreRankInfo.Models;
using MoreRankInfo.Utils;
using Zenject;

namespace MoreRankInfo.Services
{
    /// <summary>
    /// Detects completed score uploads without Harmony patches by subscribing to events the leaderboard mods expose.
    /// The handlers only mark data as stale; network work is started by <see cref="RankDataManager"/> in the menu.
    /// </summary>
    internal class ScoreUploadWatcher : IInitializable, IDisposable
    {
        // ScoreSaber 3.4.2: internal class in the menu container, event Action<ScoreSubmissionStatus> StatusChanged
        private const string ScoreSaberControllerType = "ScoreSaber.Features.ScoreSubmission.ScoreSubmissionController";
        private const string ScoreSaberStatusEvent = "StatusChanged";
        // BeatLeader 0.10.0: public class, public static event StateChangedEvent (declared on its generic base)
        private const string BeatLeaderRequestType = "BeatLeader.API.UploadReplayRequest";
        private const string BeatLeaderStateEvent = "StateChangedEvent";

        private readonly DiContainer _container;
        private readonly RankDataManager _dataManager;

        private SynchronizationContext _mainThread;

        private object _scoreSaberController;
        private EventInfo _scoreSaberEvent;
        private Delegate _scoreSaberHandler;

        private EventInfo _beatLeaderEvent;
        private Delegate _beatLeaderHandler;
        private bool _subscribingBeatLeader;

        public ScoreUploadWatcher(DiContainer container, RankDataManager dataManager)
        {
            _container = container;
            _dataManager = dataManager;
        }

        public void Initialize()
        {
            _mainThread = SynchronizationContext.Current;
            if (_dataManager.IsInstalled(RankService.ScoreSaber))
                HookScoreSaber();
            if (_dataManager.IsInstalled(RankService.BeatLeader))
                HookBeatLeader();
        }

        public void Dispose()
        {
            try
            {
                if (_scoreSaberHandler != null)
                    _scoreSaberEvent.GetRemoveMethod(true).Invoke(_scoreSaberController, new object[] { _scoreSaberHandler });
                if (_beatLeaderHandler != null)
                    _beatLeaderEvent.GetRemoveMethod(true).Invoke(null, new object[] { _beatLeaderHandler });
            }
            catch (Exception ex)
            {
                Plugin.Log.Debug(ex);
            }
            _scoreSaberHandler = null;
            _beatLeaderHandler = null;
        }

        private void HookScoreSaber()
        {
            try
            {
                var type = PluginManager.GetPluginFromId(RankService.ScoreSaber.PluginId())?.Assembly.GetType(ScoreSaberControllerType);
                if (type == null)
                    throw new MissingMemberException($"type {ScoreSaberControllerType} not found");
                var controller = _container.TryResolve(type);
                if (controller == null)
                    throw new MissingMemberException($"{ScoreSaberControllerType} is not bound in the menu container");
                var ev = type.GetEvent(ScoreSaberStatusEvent, ReflectionUtil.AnyInstance);
                if (ev == null)
                    throw new MissingMemberException($"event {ScoreSaberStatusEvent} not found");

                var handler = ReflectionUtil.CreateForwardingDelegate(ev.EventHandlerType, args => OnScoreSaberStatus(args[0]));
                ev.GetAddMethod(true).Invoke(controller, new object[] { handler });
                _scoreSaberController = controller;
                _scoreSaberEvent = ev;
                _scoreSaberHandler = handler;
                Plugin.Log.Debug("Subscribed to ScoreSaber upload status.");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"ScoreSaber upload detection is unavailable ({ex.Message}). Data refreshes only when the leaderboard is shown.");
                Plugin.Log.Debug(ex);
            }
        }

        private void HookBeatLeader()
        {
            try
            {
                var type = PluginManager.GetPluginFromId(RankService.BeatLeader.PluginId())?.Assembly.GetType(BeatLeaderRequestType);
                if (type == null)
                    throw new MissingMemberException($"type {BeatLeaderRequestType} not found");
                var ev = type.GetEvent(BeatLeaderStateEvent, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (ev == null)
                    throw new MissingMemberException($"event {BeatLeaderStateEvent} not found");

                var handler = ReflectionUtil.CreateForwardingDelegate(ev.EventHandlerType, args => OnBeatLeaderState(args[0], args[1]));
                // The event's add accessor immediately invokes the handler with the current state; ignore that call.
                _subscribingBeatLeader = true;
                try
                {
                    ev.GetAddMethod(true).Invoke(null, new object[] { handler });
                }
                finally
                {
                    _subscribingBeatLeader = false;
                }
                _beatLeaderEvent = ev;
                _beatLeaderHandler = handler;
                Plugin.Log.Debug("Subscribed to BeatLeader upload state.");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"BeatLeader upload detection is unavailable ({ex.Message}). Data refreshes only when the leaderboard is shown.");
                Plugin.Log.Debug(ex);
            }
        }

        private void OnScoreSaberStatus(object status)
        {
            // ScoreSubmissionStatus.Status : ScoreUploadStatus { Packaging, Uploading, Success, Retrying, Error, Done }
            if (ReflectionUtil.GetMember(status, "Status")?.ToString() != "Success")
                return;
            Post(() => _dataManager.RequestRefresh(RankService.ScoreSaber, true, PluginConfig.Instance.ScoreSaberFetchDelaySeconds));
        }

        private void OnBeatLeaderState(object request, object state)
        {
            if (_subscribingBeatLeader || state?.ToString() != "Finished")
                return;
            // ScoreUploadResponse.Status : ScoreUploadStatus { Uploaded = 1, NonPB = 2, Attempt = 3, Error = 4 }.
            // Only "Uploaded" (a new personal best) can change pp. If it cannot be read, refresh anyway.
            var uploadStatus = ReflectionUtil.GetMember(ReflectionUtil.GetMember(request, "Result"), "Status")?.ToString();
            if (uploadStatus != null && uploadStatus != "Uploaded")
                return;
            Post(() => _dataManager.RequestRefresh(RankService.BeatLeader, true, PluginConfig.Instance.BeatLeaderFetchDelaySeconds));
        }

        private void Post(Action action)
        {
            if (_mainThread == null || SynchronizationContext.Current == _mainThread)
                action();
            else
                _mainThread.Post(_ => action(), null);
        }
    }
}
