#if USE_AVNADS_PLUGIN
using System;

namespace AVN.AdsPlugin
{
    /// <summary>
    /// Static event bus for all ad lifecycle events.
    /// Providers fire these; controllers subscribe to them.
    /// No SDK types appear in any signature — fully provider-agnostic.
    /// </summary>
    public static class AdEvents
    {
        //---========================= Banner CALLBACKS =========================//
        public static Action<string> OnBannerLoadFailed;
        public static Action<string> OnBannerLoaded;
        public static Action<string> OnBannerShown;
        public static Action<string> OnBannerFailToShow;

        //---========================= Mrec CALLBACKS =========================//
        public static Action<string> OnMrecLoadFailed;
        public static Action<string> OnMrecLoaded;
        public static Action<string> OnMrecShown;
        public static Action<string> OnMrecFailToShow;

        //---========================= Interstitial Slot CALLBACKS =========================//
        public static Action<int, string> OnInterstitialSlotLoadFailed;
        public static Action<int, string> OnInterstitialSlotLoaded;
        public static Action<int, string> OnInterstitialSlotShown;
        public static Action<int, string> OnInterstitialSlotClosed;
        public static Action<int, string> OnInterstitialSlotFailToShow;

        //---========================= AppOpen Ad CALLBACKS =========================//
        public static Action<string> OnAppOpenLoadFailed;
        public static Action<string> OnAppOpenLoaded;
        public static Action<string> OnAppOpenShown;
        public static Action<string> OnAppOpenClosed;
        public static Action<string> OnAppOpenFailToShow;

        //---========================= Reward CALLBACKS =========================//
        public static Action<string> OnRewardLoadFailed;
        public static Action<string> OnRewardLoaded;
        public static Action<string> OnRewardShown;
        public static Action<string> OnRewardEarned;
        public static Action<string> OnRewardClosed;
        public static Action<string> OnRewardFailToShow;

        //---========================= Pre-Show CALLBACKS =========================//
        // Fired synchronously before ShowAd() — reliable alternative to OnAdDisplayed
        // which fires late due to SetPauseGame(true).
        public static Action<int, bool> OnBeforeInterstitialShown;
        public static Action OnBeforeRewardedShown;
        public static Action OnBeforeAppOpenShown;
    }
}
#endif
