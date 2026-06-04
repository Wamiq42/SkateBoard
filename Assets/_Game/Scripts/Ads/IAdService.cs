using System;

namespace Mixtape.Ads
{
    /// <summary>
    /// Abstraction over the ad network. The whole game talks to this interface only,
    /// so the real SDK (Unity Ads / AdMob) can be dropped in later by implementing it
    /// and registering it with <see cref="AdManager"/> — no gameplay/UI changes needed.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady { get; }

        /// <summary>Show a rewarded ad. onComplete(true) = reward earned, onComplete(false) = skipped/failed.</summary>
        void ShowRewarded(string placement, Action<bool> onComplete);

        /// <summary>Show an interstitial (e.g. used for "watch ad to skip cutscene").</summary>
        void ShowInterstitial(string placement, Action onClosed);
    }
}
