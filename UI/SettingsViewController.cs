using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using MoreRankInfo.Configuration;

namespace MoreRankInfo.UI
{
    [ViewDefinition("MoreRankInfo.UI.Settings.bsml")]
    internal class SettingsViewController : BSMLAutomaticViewController
    {
        [UIValue("date-change-hour")]
        private int DateChangeHour
        {
            get => PluginConfig.Instance.DateChangeHour;
            set => PluginConfig.Instance.DateChangeHour = Math.Max(0, Math.Min(23, value));
        }

        [UIValue("show-ranks")]
        private bool ShowRanks
        {
            get => PluginConfig.Instance.ShowRanks;
            set => PluginConfig.Instance.ShowRanks = value;
        }

        [UIValue("font-size")]
        private float FontSize
        {
            get => PluginConfig.Instance.FontSize;
            set => PluginConfig.Instance.FontSize = value;
        }

        [UIValue("offset-y")]
        private float OffsetY
        {
            get => PluginConfig.Instance.OffsetY;
            set => PluginConfig.Instance.OffsetY = value;
        }
    }
}
