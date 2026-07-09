#if USE_AVNADS_PLUGIN
using System;

namespace AVN.AdsPlugin
{
    /// <summary>
    /// Contract every ad SDK provider must fulfil.
    /// No SDK types appear in any signature — fully provider-agnostic.
    /// Providers fire AdEvents to communicate back to controllers.
    /// </summary>
    public interface IAdsProvider
    {
        bool IsSdkInitialized { get; }
        event Action SDKInitializedSuccessfully;
        event Action<string> SDKInitializationFailed;

        void Initialize(string appKey);
        void ConfigureAdIds(string bannerId, string mrecId, string rewardedId);

        // Interstitial
        void LoadInterstitial(int slotIndex, string adUnitId);
        void ShowInterstitial(int slotIndex, bool isRewarded);
        bool IsInterstitialReady(int slotIndex);

        // Rewarded
        void LoadRewarded();
        void ShowRewarded();
        bool IsRewardedReady();

        // Banner & MREC
        void LoadBanner(BannerAdTypes type, BannerPosition position, bool isAdaptive, bool ignoreNotch);
        void ShowBanner(BannerAdTypes type);
        void HideBanner(BannerAdTypes type);
        void DestroyBanner(BannerAdTypes type);
    }
}
#endif
