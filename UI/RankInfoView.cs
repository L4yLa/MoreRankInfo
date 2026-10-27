using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using HMUI;
using MoreRankInfo.Configuration;
using MoreRankInfo.Models;
using MoreRankInfo.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace MoreRankInfo.UI
{
    /// <summary>
    /// Shown above the leaderboard for the leaderboard mod that is currently visible:
    /// <code>
    /// [icon] Today +12.34pp (+0.112%)          small
    ///        #1234 (+5)   JP #56 (+1)          small, optional (ShowRanks)
    ///        LastPlayed: +3.21pp (+0.026%)     large
    ///        Song - Mapper [Expert+]           small, scrolls when wider than the panel
    /// </code>
    /// It is a child of PlatformLeaderboardViewController, so it is hidden together with the leaderboard
    /// and is inactive during gameplay. Only the song line has a per-frame update, and only while it is
    /// visible and too long to fit.
    /// </summary>
    internal class RankInfoView : IInitializable, IDisposable
    {
        // Same width and x position as LeaderboardCore's panel screen (localPosition (3, 50), size 100 x 25),
        // placed just above it.
        private const float Width = 100f;
        private const float PanelTop = 50f + 25f / 2f;
        private const float SmallLine = 6f;
        private const float BigLine = 7f;
        private const float IconSize = 11f;
        private const float IconGap = 2f;
        private const float SmallRatio = 0.8f;

        private const string Positive = "#7CFC7C";
        private const string Negative = "#FF6B6B";
        private const string Neutral = "#BBBBBB";

        private readonly PlatformLeaderboardViewController _leaderboardViewController;
        private readonly LeaderboardVisibilityWatcher _visibility;
        private readonly RankDataManager _dataManager;

        private SynchronizationContext _mainThread;
        private RectTransform _root;
        private RectTransform _content;
        private ImageView _icon;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _last;
        private RectTransform _songViewport;
        private TextMeshProUGUI _song;
        private MarqueeText _marquee;
        private string _songText;
        private RankService? _iconService;
        private bool _hasIcon;
        private int _iconRequest;

        public RankInfoView(PlatformLeaderboardViewController leaderboardViewController, LeaderboardVisibilityWatcher visibility, RankDataManager dataManager)
        {
            _leaderboardViewController = leaderboardViewController;
            _visibility = visibility;
            _dataManager = dataManager;
        }

        public void Initialize()
        {
            _mainThread = SynchronizationContext.Current;
            _visibility.VisibleChanged += OnVisibleChanged;
            _dataManager.DataChanged += OnDataChanged;
            PluginConfig.Instance.ConfigChanged += OnConfigChanged;
            OnVisibleChanged();
        }

        public void Dispose()
        {
            _visibility.VisibleChanged -= OnVisibleChanged;
            _dataManager.DataChanged -= OnDataChanged;
            PluginConfig.Instance.ConfigChanged -= OnConfigChanged;
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
        }

        private void OnVisibleChanged()
        {
            var service = _visibility.Visible;
            if (service == null)
            {
                if (_root != null)
                    _root.gameObject.SetActive(false);
                return;
            }
            _dataManager.RefreshIfStale(service.Value);
            Render();
        }

        private void OnDataChanged(RankService service)
        {
            if (service == _visibility.Visible)
                Render();
        }

        private void OnConfigChanged()
        {
            // BSIPA may raise this from its file watcher thread.
            if (_mainThread != null && SynchronizationContext.Current != _mainThread)
            {
                _mainThread.Post(_ => OnConfigChanged(), null);
                return;
            }
            if (_root == null)
                return;
            _songText = null; // re-measure the song line with the new layout
            ApplyLayout();
            Render();
        }

        private void Render()
        {
            var service = _visibility.Visible;
            if (service == null)
                return;
            var tracker = _dataManager.GetTracker(service.Value);
            if (tracker == null)
                return;

            EnsureView();
            if (!_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);
            if (_iconService != service)
                _ = SetIconAsync(service.Value);

            BuildTexts(service.Value, tracker, _hasIcon, PluginConfig.Instance.ShowRanks, out var summary, out var last, out var song);
            _summary.text = summary;
            _last.text = last;
            if (song != _songText)
            {
                _songText = song;
                _marquee.SetText(song);
            }
        }

        // ------------------------------------------------------------------ layout

        private void EnsureView()
        {
            if (_root != null)
                return;
            var center = new Vector2(0.5f, 0.5f);

            _root = NewRect("MoreRankInfo", _leaderboardViewController.transform);
            _root.anchorMin = _root.anchorMax = _root.pivot = center;

            var iconRect = NewRect("Icon", _root);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            _icon = iconRect.gameObject.AddComponent<ImageView>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.enabled = false;

            _content = NewRect("Content", _root);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.pivot = center;

            _summary = CreateText("Summary", _content, true);
            _last = CreateText("LastPlayed", _content, true);

            _songViewport = NewRect("SongViewport", _content);
            _songViewport.gameObject.AddComponent<RectMask2D>();
            _song = CreateText("Song", _songViewport, false);
            var songRect = _song.rectTransform;
            songRect.anchorMin = new Vector2(0f, 0f);
            songRect.anchorMax = new Vector2(0f, 1f);
            songRect.pivot = new Vector2(0f, 0.5f);
            songRect.anchoredPosition = Vector2.zero;
            songRect.sizeDelta = Vector2.zero;
            _marquee = _songViewport.gameObject.AddComponent<MarqueeText>();
            _marquee.Init(_songViewport, _song);

            ApplyLayout();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, bool autoSize)
        {
            var text = BeatSaberUI.CreateCurvedUIText(parent, string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.gameObject.name = name;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            text.enableAutoSizing = autoSize;
            text.fontSizeMin = 1f;
            return text;
        }

        private void ApplyLayout()
        {
            var config = PluginConfig.Instance;
            var summaryHeight = SmallLine * (config.ShowRanks ? 2 : 1);
            var height = summaryHeight + BigLine + SmallLine;
            var bigMax = Math.Max(1f, config.FontSize);
            var smallMax = bigMax * SmallRatio;

            _root.sizeDelta = new Vector2(Width, height);
            _root.anchoredPosition = new Vector2(3f, PanelTop + 1f + height / 2f + config.OffsetY);

            var iconRect = _icon.rectTransform;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            _content.offsetMin = new Vector2(_hasIcon ? IconSize + IconGap : 0f, 0f);
            _content.offsetMax = Vector2.zero;

            PlaceRow(_summary.rectTransform, 0f, summaryHeight);
            PlaceRow(_last.rectTransform, summaryHeight, BigLine);
            PlaceRow(_songViewport, summaryHeight + BigLine, SmallLine);

            _summary.fontSizeMax = smallMax;
            _last.fontSizeMax = bigMax;
            // The song line scrolls instead of shrinking: fixed size limited by its row height.
            _song.fontSize = Math.Min(smallMax, SmallLine * 0.85f);
        }

        /// <summary>Stretches a row horizontally across the content area, <paramref name="top"/> units below its top.</summary>
        private static void PlaceRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private async Task SetIconAsync(RankService service)
        {
            _iconService = service;
            var request = ++_iconRequest;
            var icon = await IconProvider.GetAsync(service);
            if (request != _iconRequest || _icon == null)
                return; // the visible service changed or the view was destroyed meanwhile

            var hadIcon = _hasIcon;
            _hasIcon = icon != null;
            _icon.enabled = _hasIcon;
            if (_hasIcon)
            {
                _icon.sprite = icon.Sprite;
                _icon.material = icon.Material;
            }
            if (hadIcon != _hasIcon)
            {
                _songText = null; // the content width changed
                ApplyLayout();
            }
            Render(); // the service name is written as text only when there is no icon
        }

        // ------------------------------------------------------------------ text

        private static void BuildTexts(RankService service, ServiceTracker tracker, bool hasIcon, bool showRanks,
            out string summary, out string last, out string song)
        {
            var prefix = hasIcon ? string.Empty : $"<b>{service.DisplayName()}</b>  ";
            var state = tracker.State;
            var current = state?.Latest;
            var baseline = state?.Baseline;
            last = string.Empty;
            song = string.Empty;

            // Values saved on an earlier game day are not today's; wait for fresh data.
            if (current == null || baseline == null || state.BaselineDay != RankDataManager.GameDay())
            {
                summary = prefix + (tracker.Status == FetchStatus.Failed
                    ? $"<color={Negative}>Failed to load</color>"
                    : $"<color={Neutral}>Loading...</color>");
                return;
            }

            // Today's pp gain and rate (relative to the total pp at the start of the day)
            var sb = new StringBuilder();
            var todayPP = current.PP - baseline.PP;
            sb.Append(prefix).Append("Today ").Append(PPChange(todayPP, baseline.PP, "0.000"));

            if (showRanks)
            {
                var country = string.IsNullOrEmpty(current.Country) ? "Local" : current.Country;
                sb.Append('\n')
                  .Append('#').Append(current.Rank.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(RankDelta(baseline.Rank, current.Rank))
                  .Append("   ").Append(Escape(country)).Append(" #").Append(current.CountryRank.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(RankDelta(baseline.CountryRank, current.CountryRank));
            }
            summary = sb.ToString();

            // The latest play that changed the total pp; the rate is relative to the total pp before that play.
            var change = state.LastChange;
            if (change == null)
            {
                last = $"LastPlayed: <color={Neutral}>-</color>";
                return;
            }
            last = "LastPlayed: " + PPChange(change.DeltaPP, change.BeforePP, "0.00");

            var songBuilder = new StringBuilder(Escape(change.SongName ?? "(unknown)"));
            if (!string.IsNullOrEmpty(change.Mapper))
                songBuilder.Append(" - ").Append(Escape(change.Mapper));
            if (!string.IsNullOrEmpty(change.Difficulty))
                songBuilder.Append(" [").Append(Escape(DifficultyLabel(change.Difficulty))).Append(']');
            song = songBuilder.ToString();
        }

        /// <summary>"+12.34pp (+0.112%)"; the rate is omitted when the reference pp is unknown.</summary>
        private static string PPChange(double delta, double reference, string rateFormat)
        {
            var text = Colored(delta, FormatSigned(delta, "0.00") + "pp");
            if (reference > 0)
                text += " " + Colored(delta, "(" + FormatSigned(delta / reference * 100.0, rateFormat) + "%)");
            return text;
        }

        /// <summary>Rank improvement since the baseline: a smaller rank number is better.</summary>
        private static string RankDelta(int baselineRank, int currentRank)
        {
            if (baselineRank <= 0 || currentRank <= 0)
                return string.Empty;
            var up = baselineRank - currentRank;
            return Colored(up, "(" + FormatSigned(up, "0") + ")");
        }

        private static string FormatSigned(double value, string format)
        {
            var text = Math.Abs(value).ToString(format, CultureInfo.InvariantCulture);
            if (text.Trim('0', '.') == string.Empty)
                return "±" + text;
            return (value > 0 ? "+" : "-") + text;
        }

        private static string Colored(double value, string text)
        {
            var color = value > 0.0049 ? Positive : value < -0.0049 ? Negative : Neutral;
            return $"<color={color}>{text}</color>";
        }

        private static string DifficultyLabel(string difficulty)
        {
            return difficulty == "ExpertPlus" ? "Expert+" : difficulty;
        }

        /// <summary>Song names may contain '&lt;' which TextMeshPro would treat as a tag.</summary>
        private static string Escape(string text)
        {
            return "<noparse>" + text.Replace("</noparse>", "") + "</noparse>";
        }
    }
}
