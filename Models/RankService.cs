namespace MoreRankInfo.Models
{
    internal enum RankService
    {
        ScoreSaber,
        BeatLeader
    }

    internal static class RankServiceInfo
    {
        /// <summary>BSIPA plugin id of the leaderboard mod.</summary>
        internal static string PluginId(this RankService service)
        {
            return service == RankService.ScoreSaber ? "ScoreSaber" : "BeatLeader";
        }

        internal static string DisplayName(this RankService service)
        {
            return service == RankService.ScoreSaber ? "ScoreSaber" : "BeatLeader";
        }
    }
}
