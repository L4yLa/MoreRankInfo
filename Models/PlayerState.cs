using System.Collections.Generic;

namespace MoreRankInfo.Models
{
    /// <summary>Player values returned by a leaderboard API.</summary>
    internal class PlayerSnapshot
    {
        public double PP { get; set; }
        public int Rank { get; set; }
        public int CountryRank { get; set; }
        public string Country { get; set; }
    }

    /// <summary>The most recent play that changed the player's total pp.</summary>
    internal class PlayChange
    {
        public string SongName { get; set; }
        public string Mapper { get; set; }
        public string Difficulty { get; set; }
        /// <summary>Total pp after the play minus total pp before it.</summary>
        public double DeltaPP { get; set; }
        /// <summary>Total pp before the play (0 for data saved by older versions).</summary>
        public double BeforePP { get; set; }
        public string Time { get; set; }
    }

    /// <summary>Persisted state for one player on one leaderboard service.</summary>
    internal class ServiceState
    {
        /// <summary>Game day (yyyy-MM-dd, shifted by DateChangeHour) the baseline belongs to.</summary>
        public string BaselineDay { get; set; }
        public PlayerSnapshot Baseline { get; set; }
        public PlayerSnapshot Latest { get; set; }
        public PlayChange LastChange { get; set; }
    }

    /// <summary>Root of the state file. Keyed by service name, then platform user id.</summary>
    internal class StateFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, Dictionary<string, ServiceState>> Services { get; set; }
            = new Dictionary<string, Dictionary<string, ServiceState>>();
    }

    /// <summary>A recent score as reported by the API.</summary>
    internal class RecentPlay
    {
        public string SongName { get; set; }
        public string Mapper { get; set; }
        public string Difficulty { get; set; }
    }
}
