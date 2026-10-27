using System;
using System.Collections.Generic;
using System.IO;
using IPA.Utilities;
using MoreRankInfo.Models;
using Newtonsoft.Json;

namespace MoreRankInfo.Services
{
    /// <summary>Loads and saves the per-player baseline / last change state as JSON in UserData.</summary>
    internal class StateStore
    {
        private static readonly string FilePath = Path.Combine(UnityGame.UserDataPath, "MoreRankInfo.state.json");

        private StateFile _file;

        internal ServiceState Get(RankService service, string userId)
        {
            var file = Load();
            if (!file.Services.TryGetValue(service.ToString(), out var users))
            {
                users = new Dictionary<string, ServiceState>();
                file.Services[service.ToString()] = users;
            }
            if (!users.TryGetValue(userId, out var state))
            {
                state = new ServiceState();
                users[userId] = state;
            }
            return state;
        }

        internal void Save()
        {
            if (_file == null)
                return;
            var tmp = FilePath + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_file, Formatting.Indented));
                if (File.Exists(FilePath))
                    File.Replace(tmp, FilePath, null);
                else
                    File.Move(tmp, FilePath);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to save {FilePath}: {ex.Message}");
                Plugin.Log.Debug(ex);
            }
        }

        private StateFile Load()
        {
            if (_file != null)
                return _file;
            try
            {
                if (File.Exists(FilePath))
                    _file = JsonConvert.DeserializeObject<StateFile>(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to read {FilePath}, starting from an empty state: {ex.Message}");
                Plugin.Log.Debug(ex);
            }
            if (_file == null)
                _file = new StateFile();
            if (_file.Services == null)
                _file.Services = new Dictionary<string, Dictionary<string, ServiceState>>();
            return _file;
        }
    }
}
