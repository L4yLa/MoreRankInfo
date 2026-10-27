using TMPro;
using UnityEngine;

namespace MoreRankInfo.UI
{
    /// <summary>
    /// Scrolls a single-line text to the left in an endless loop when it is wider than its viewport
    /// (the viewport clips it with a RectMask2D). The text is drawn twice with a gap so the loop is seamless,
    /// and it pauses briefly each time the start comes back into place.
    /// The component disables itself when the text fits, and Update never runs while the menu (and so this
    /// object) is inactive, e.g. during gameplay.
    /// </summary>
    internal class MarqueeText : MonoBehaviour
    {
        private const float Speed = 8f;          // units per second
        private const float PauseSeconds = 1.5f;
        private const string Gap = "<space=2em>";

        private RectTransform _viewport;
        private TextMeshProUGUI _text;
        private float _cycle;
        private float _offset;
        private float _pause;

        internal void Init(RectTransform viewport, TextMeshProUGUI text)
        {
            _viewport = viewport;
            _text = text;
            enabled = false;
        }

        internal void SetText(string text)
        {
            if (_text == null)
                return;

            var rect = _text.rectTransform;
            var single = _text.GetPreferredValues(text).x;
            var available = _viewport.rect.width;
            if (string.IsNullOrEmpty(text) || available <= 0f || single <= available)
            {
                _text.text = text;
                rect.sizeDelta = new Vector2(Mathf.Max(single, available), rect.sizeDelta.y);
                rect.anchoredPosition = Vector2.zero;
                enabled = false;
                return;
            }

            var doubled = text + Gap + text;
            var doubledWidth = _text.GetPreferredValues(doubled).x;
            _text.text = doubled;
            rect.sizeDelta = new Vector2(doubledWidth, rect.sizeDelta.y);
            // One loop = width of "text + gap"; after that the second copy is exactly where the first started.
            _cycle = doubledWidth - single;
            _offset = 0f;
            _pause = PauseSeconds;
            rect.anchoredPosition = Vector2.zero;
            enabled = _cycle > 0f;
        }

        private void Update()
        {
            if (_pause > 0f)
            {
                _pause -= Time.deltaTime;
                return;
            }
            _offset += Speed * Time.deltaTime;
            if (_offset >= _cycle)
            {
                _offset -= _cycle;
                _pause = PauseSeconds;
            }
            _text.rectTransform.anchoredPosition = new Vector2(-_offset, 0f);
        }
    }
}
