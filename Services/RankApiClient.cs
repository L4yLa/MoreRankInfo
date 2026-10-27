using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MoreRankInfo.Models;
using Newtonsoft.Json.Linq;

namespace MoreRankInfo.Services
{
    /// <summary>Queries the ScoreSaber / BeatLeader public APIs.</summary>
    internal class RankApiClient : IDisposable
    {
        private const string ScoreSaberApi = "https://scoresaber.com/api/v2";
        private const string BeatLeaderApi = "https://api.beatleader.xyz";

        private readonly HttpClient _http;

        public RankApiClient()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MoreRankInfo/0.1.0");
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        /// <summary>Returns null when the request or parsing failed.</summary>
        internal async Task<PlayerSnapshot> GetPlayerAsync(RankService service, string userId, CancellationToken token)
        {
            var json = await GetJsonAsync(service == RankService.ScoreSaber
                ? $"{ScoreSaberApi}/players/{userId}"
                : $"{BeatLeaderApi}/player/{userId}?stats=false", token);
            if (json == null)
                return null;
            try
            {
                if (service == RankService.ScoreSaber)
                {
                    // /api/v2/players/{id}: { country, stats: { totalPP, rank, countryRank } }
                    var stats = json["stats"];
                    if (stats == null)
                        return null;
                    return new PlayerSnapshot
                    {
                        PP = stats.Value<double>("totalPP"),
                        Rank = stats.Value<int>("rank"),
                        CountryRank = stats.Value<int>("countryRank"),
                        Country = json.Value<string>("country")
                    };
                }
                // /player/{id}: { pp, rank, countryRank, country }
                return new PlayerSnapshot
                {
                    PP = json.Value<double>("pp"),
                    Rank = json.Value<int>("rank"),
                    CountryRank = json.Value<int>("countryRank"),
                    Country = json.Value<string>("country")
                };
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"{service} player response could not be parsed: {ex.Message}");
                Plugin.Log.Debug(ex);
                return null;
            }
        }

        /// <summary>Returns the player's most recent score, or null.</summary>
        internal async Task<RecentPlay> GetRecentPlayAsync(RankService service, string userId, CancellationToken token)
        {
            var json = await GetJsonAsync(service == RankService.ScoreSaber
                ? $"{ScoreSaberApi}/players/{userId}/scores?sort=recent&limit=1"
                : $"{BeatLeaderApi}/player/{userId}/scores?sortBy=date&order=desc&count=1", token);
            if (json == null)
                return null;
            try
            {
                var item = (json["data"] as JArray)?.First;
                var leaderboard = item?["leaderboard"];
                if (leaderboard == null)
                    return null;
                if (service == RankService.ScoreSaber)
                {
                    // data[].leaderboard: { map: { songName, levelAuthorName }, difficulty: { difficulty, rawDifficulty } }
                    var difficulty = leaderboard["difficulty"];
                    return new RecentPlay
                    {
                        SongName = leaderboard["map"]?.Value<string>("songName"),
                        Mapper = leaderboard["map"]?.Value<string>("levelAuthorName"),
                        Difficulty = ScoreSaberDifficulty(difficulty?.Value<string>("rawDifficulty"), difficulty?.Value<int?>("difficulty"))
                    };
                }
                // data[].leaderboard: { song: { name, mapper }, difficulty: { difficultyName } }
                return new RecentPlay
                {
                    SongName = leaderboard["song"]?.Value<string>("name"),
                    Mapper = leaderboard["song"]?.Value<string>("mapper"),
                    Difficulty = leaderboard["difficulty"]?.Value<string>("difficultyName")
                };
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"{service} scores response could not be parsed: {ex.Message}");
                Plugin.Log.Debug(ex);
                return null;
            }
        }

        // Same rules as the ScoreSaber mod: rawDifficulty "_ExpertPlus_SoloStandard" first, numeric value as fallback.
        private static string ScoreSaberDifficulty(string rawDifficulty, int? difficulty)
        {
            if (!string.IsNullOrWhiteSpace(rawDifficulty))
            {
                var parts = rawDifficulty.Trim('_').Split('_');
                if (parts.Length > 0 && parts[0].Length > 0)
                    return parts[0];
            }
            switch (difficulty)
            {
                case 1: return "Easy";
                case 3: return "Normal";
                case 5: return "Hard";
                case 7: return "Expert";
                case 9: return "ExpertPlus";
                default: return null;
            }
        }

        private async Task<JObject> GetJsonAsync(string url, CancellationToken token)
        {
            try
            {
                using (var response = await _http.GetAsync(url, token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Plugin.Log.Warn($"GET {url} returned {(int)response.StatusCode}");
                        return null;
                    }
                    return JObject.Parse(await response.Content.ReadAsStringAsync());
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"GET {url} failed: {ex.Message}");
                Plugin.Log.Debug(ex);
                return null;
            }
        }
    }
}
