using System;
using UnityEngine;

namespace Mixtape.Ads
{
    /// <summary>
    /// Placeholder ad service used during development. Instantly "succeeds" so reward
    /// logic and UI can be built and tested before any real SDK is integrated.
    /// Replace by implementing <see cref="IAdService"/> with the real network.
    /// </summary>
    public class StubAdService : IAdService
    {
        public bool IsRewardedReady => true;

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            Debug.Log($"[StubAd] Rewarded shown (placement='{placement}') -> granting reward.");
            onComplete?.Invoke(true);
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            Debug.Log($"[StubAd] Interstitial shown (placement='{placement}').");
            onClosed?.Invoke();
        }
    }
}
