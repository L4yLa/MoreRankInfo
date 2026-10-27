using System;
using UnityEngine;

namespace MoreRankInfo.UI
{
    /// <summary>
    /// Attached to a leaderboard mod's view controller GameObject. Reports when the view becomes active or
    /// inactive in the hierarchy. It has no per-frame callbacks.
    /// </summary>
    internal class VisibilityProbe : MonoBehaviour
    {
        internal event Action<bool> VisibilityChanged;

        internal bool IsVisible => isActiveAndEnabled;

        private void OnEnable()
        {
            VisibilityChanged?.Invoke(true);
        }

        private void OnDisable()
        {
            VisibilityChanged?.Invoke(false);
        }
    }
}
