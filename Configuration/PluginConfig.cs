using System;

namespace MoreRankInfo.Configuration
{
    public class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        /// <summary>Hour of the day (0-23) at which "today" starts.</summary>
        public virtual int DateChangeHour { get; set; } = 3;

        /// <summary>
        /// Maximum font size of the "LastPlayed" line; the other lines use a fixed ratio of it.
        /// Text is shrunk automatically to fit the width of the leaderboard panel.
        /// </summary>
        public virtual float FontSize { get; set; } = 10f;

        /// <summary>Show the global / country rank line.</summary>
        public virtual bool ShowRanks { get; set; } = true;

        /// <summary>Vertical offset added to the default text position.</summary>
        public virtual float OffsetY { get; set; } = 0f;

        /// <summary>Wait after a ScoreSaber upload before querying the API (ScoreSaber updates pp asynchronously).</summary>
        public virtual int ScoreSaberFetchDelaySeconds { get; set; } = 5;

        /// <summary>Wait after a BeatLeader upload before querying the API.</summary>
        public virtual int BeatLeaderFetchDelaySeconds { get; set; } = 1;

        /// <summary>Raised after the config was changed (settings UI or file reload).</summary>
        public event Action ConfigChanged;

        /// <summary>Called by BSIPA whenever the config changes.</summary>
        public virtual void Changed()
        {
            ConfigChanged?.Invoke();
        }
    }
}
