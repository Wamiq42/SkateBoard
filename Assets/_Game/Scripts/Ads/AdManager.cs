using System;

namespace Mixtape.Ads
{
    /// <summary>
    /// Global access point for ads. Defaults to <see cref="StubAdService"/>.
    /// Call <see cref="SetService"/> once at boot when the real SDK is ready.
    /// </summary>
    public static class AdManager
    {
        private static IAdService _service;

        public static IAdService Service => _service ??= new StubAdService();

        public static void SetService(IAdService service) => _service = service;

        public static void ShowRewarded(string placement, Action<bool> onComplete) =>
            Service.ShowRewarded(placement, onComplete);

        public static void ShowInterstitial(string placement, Action onClosed) =>
            Service.ShowInterstitial(placement, onClosed);
    }
}
