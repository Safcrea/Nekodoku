#if USE_AVNADS_PLUGIN && USE_MAX
using System.Collections.Generic;
using AVN.AdsPlugin;
using UnityEngine;

namespace AVN.AdsPlugin.Services
{
    /// <summary>
    /// AppLovin MAX implementation of IAdsProvider.
    /// All SDK-specific code lives here; nothing outside this file references MAX types.
    /// Fires AdEvents to communicate results back to controllers.
    /// </summary>
    public sealed class MaxAdsProvider : IAdsProvider
    {
        #region Fields
        private bool _sdkInitialized = false;
        public bool IsSdkInitialized => _sdkInitialized;
        public event System.Action SDKInitializedSuccessfully;
        public event System.Action<string> SDKInitializationFailed;

        // Ad Unit ID overrides (set via ConfigureAdIds)
        private string bannerAdUnitId = string.Empty;
        private string mrecAdUnitId = string.Empty;
        private string rewardedAdUnitId = string.Empty;

        private readonly Dictionary<int, string> interstitialSlotIds = new Dictionary<int, string>();
        private readonly Dictionary<int, bool> interstitialReadyBySlot = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> interstitialIsRewardedBySlot = new Dictionary<int, bool>();

        // Retry attempts
        private int _bannerRetryAttempt = 0;
        private int _mrecRetryAttempt = 0;
        private readonly Dictionary<int, int> _interstitialRetryBySlot = new Dictionary<int, int>();
        private int _rewardedRetryAttempt = 0;

        // Banner config cached from LoadBanner call
        private BannerPosition _cachedBannerPosition;
        private BannerPosition _cachedMrecPosition;
        private bool _cachedIsAdaptive;
        private bool _bannerCallbacksRegistered = false;
        private bool _mrecCallbacksRegistered = false;
        private bool _interstitialCallbacksRegistered = false;
        private bool _rewardedCallbacksRegistered = false;
        #endregion

        #region Initialization
        public void Initialize(string sdkKey)
        {
            if (_sdkInitialized)
            {
                SDKInitializedSuccessfully?.Invoke();
                return;
            }

            DebugLogger.AVNLog("MaxAdsProvider Initialize called");
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            MaxSdk.InitializeSdk();
        }

        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            DebugLogger.AVNLog("MaxAdsProvider SDK Initialized");
            Debug.Log("-------- MAX SDK Initialization Complete --------");
            _sdkInitialized = true;
            SDKInitializedSuccessfully?.Invoke();
        }

        public void ConfigureAdIds(string bannerId, string mrecId, string rewardedId)
        {
            DebugLogger.AVNLog("MaxAdsProvider ConfigureAdIds called");
            bannerAdUnitId = NormalizeId(bannerId);
            mrecAdUnitId = NormalizeId(mrecId);
            rewardedAdUnitId = NormalizeId(rewardedId);
        }
        #endregion

        #region Interstitial
        public void LoadInterstitial(int slotIndex, string adUnitId)
        {
            DebugLogger.AVNLog("MaxAdsProvider LoadInterstitial slot=" + slotIndex);
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                AdEvents.OnInterstitialSlotLoadFailed?.Invoke(slotIndex, "Interstitial ad unit id is empty");
                return;
            }

            interstitialSlotIds[slotIndex] = adUnitId;
            interstitialReadyBySlot[slotIndex] = false;

            RegisterInterstitialCallbacks();
            MaxSdk.LoadInterstitial(adUnitId);
            DebugLogger.AVNLog("MaxAdsProvider Interstitial load sent for slot=" + slotIndex);
        }

        public void ShowInterstitial(int slotIndex, bool isRewarded)
        {
            if (!interstitialSlotIds.TryGetValue(slotIndex, out var adUnitId) || string.IsNullOrWhiteSpace(adUnitId))
            {
                DebugLogger.AVNLog("ShowInterstitial blocked: slot not found, slot=" + slotIndex);
                Debug.Log($"-------- Interstitial slot {slotIndex} is not ready yet --------");
                return;
            }

            if (!MaxSdk.IsInterstitialReady(adUnitId))
            {
                DebugLogger.AVNLog("ShowInterstitial blocked: not ready, slot=" + slotIndex);
                Debug.Log($"-------- Interstitial slot {slotIndex} is not ready yet --------");
                return;
            }

            interstitialIsRewardedBySlot[slotIndex] = isRewarded;
            AdEvents.OnBeforeInterstitialShown?.Invoke(slotIndex, isRewarded);
            MaxSdk.ShowInterstitial(adUnitId);
            DebugLogger.AVNLog("MaxAdsProvider ShowInterstitial sent for slot=" + slotIndex);
        }

        public bool IsInterstitialReady(int slotIndex)
        {
            if (!interstitialSlotIds.TryGetValue(slotIndex, out var adUnitId) || string.IsNullOrWhiteSpace(adUnitId))
                return false;
            return MaxSdk.IsInterstitialReady(adUnitId);
        }
        #endregion

        #region Rewarded
        public void LoadRewarded()
        {
            if (string.IsNullOrWhiteSpace(rewardedAdUnitId))
            {
                AdEvents.OnRewardLoadFailed?.Invoke("Rewarded ad unit id is empty");
                return;
            }

            RegisterRewardedCallbacks();
            MaxSdk.LoadRewardedAd(rewardedAdUnitId);
            DebugLogger.AVNLog("MaxAdsProvider LoadRewarded sent");
        }

        public void ShowRewarded()
        {
            AdEvents.OnBeforeRewardedShown?.Invoke();
            MaxSdk.ShowRewardedAd(rewardedAdUnitId);
        }

        public bool IsRewardedReady()
        {
            return !string.IsNullOrWhiteSpace(rewardedAdUnitId) && MaxSdk.IsRewardedAdReady(rewardedAdUnitId);
        }
        #endregion

        #region Banner & MREC
        public void LoadBanner(BannerAdTypes type, BannerPosition position, bool isAdaptive, bool ignoreNotch)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    _cachedMrecPosition = position;
                    LoadMrecInternal(position);
                    break;
                default:
                    _cachedBannerPosition = position;
                    _cachedIsAdaptive = isAdaptive;
                    LoadBannerInternal(position, isAdaptive);
                    break;
            }
        }

        public void ShowBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    if (!string.IsNullOrWhiteSpace(mrecAdUnitId))
                        MaxSdk.ShowMRec(mrecAdUnitId);
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(bannerAdUnitId))
                        MaxSdk.ShowBanner(bannerAdUnitId);
                    break;
            }
        }

        public void HideBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    Debug.Log("--------  mrec Hiding Banner ad --------");
                    if (!string.IsNullOrWhiteSpace(mrecAdUnitId))
                        MaxSdk.HideMRec(mrecAdUnitId);
                    break;
                default:
                    Debug.Log("-------- Hiding Banner ad --------");
                    if (!string.IsNullOrWhiteSpace(bannerAdUnitId))
                        MaxSdk.HideBanner(bannerAdUnitId);
                    break;
            }
        }

        public void DestroyBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    Debug.Log("--------  mrec Destroying Banner ad --------");
                    if (!string.IsNullOrWhiteSpace(mrecAdUnitId))
                        MaxSdk.DestroyMRec(mrecAdUnitId);
                    break;
                default:
                    Debug.Log("-------- Destroying Banner ad --------");
                    if (!string.IsNullOrWhiteSpace(bannerAdUnitId))
                        MaxSdk.DestroyBanner(bannerAdUnitId);
                    break;
            }
        }

        private void LoadBannerInternal(BannerPosition position, bool isAdaptive)
        {
            if (string.IsNullOrWhiteSpace(bannerAdUnitId))
            {
                AdEvents.OnBannerLoadFailed?.Invoke("Banner ad unit id is empty");
                return;
            }

            RegisterBannerCallbacks();
            MaxSdk.SetBannerBackgroundColor(bannerAdUnitId, Color.black);
            var maxPosition = ToAdViewPosition(position);
            var adViewConfiguration = new MaxSdk.AdViewConfiguration(maxPosition);
            adViewConfiguration.IsAdaptive = isAdaptive;

            MaxSdk.CreateBanner(bannerAdUnitId, adViewConfiguration);

            if (isAdaptive)
                MaxSdk.SetBannerExtraParameter(bannerAdUnitId, "adaptive_banner", "true");

            MaxSdk.LoadBanner(bannerAdUnitId);
            DebugLogger.AVNLog("MaxAdsProvider LoadBanner sent");
        }

        private void LoadMrecInternal(BannerPosition position)
        {
            if (string.IsNullOrWhiteSpace(mrecAdUnitId))
            {
                AdEvents.OnMrecLoadFailed?.Invoke("MREC ad unit id is empty");
                return;
            }

            RegisterMrecCallbacks();

            var maxPosition = ToAdViewPosition(position);
            MaxSdk.AdViewConfiguration adViewConfiguration = new MaxSdk.AdViewConfiguration(maxPosition);
            MaxSdk.CreateMRec(mrecAdUnitId, adViewConfiguration);
            MaxSdk.LoadMRec(mrecAdUnitId);
            DebugLogger.AVNLog("MaxAdsProvider LoadMrec sent");
        }
        #endregion

        #region Banner Callbacks
        private void RegisterBannerCallbacks()
        {
            if (_bannerCallbacksRegistered) return;
            _bannerCallbacksRegistered = true;
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerRevenuePaidEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent += OnBannerExpandedEvent;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent += OnBannerCollapsedEvent;
        }

        private void OnBannerLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("-------- MAX Banner ad loaded --------");
            _bannerRetryAttempt = 0;
            AdEvents.OnBannerLoaded?.Invoke("banner loaded");
        }

        private void OnBannerLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            var message = $"Banner load failed: {errorInfo.Code}";
            Debug.Log($"-------- MAX {message} --------");
            _bannerRetryAttempt++;
            AdEvents.OnBannerLoadFailed?.Invoke(message);
        }

        private void OnBannerClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("-------- MAX Banner ad clicked --------");

        private void OnBannerRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("-------- MAX Banner ad revenue paid --------");
            TrackAdRevenue("Banner", adInfo);
        }

        private void OnBannerExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdEvents.OnBannerShown?.Invoke(adUnitId);
            Debug.Log("-------- MAX Banner ad expanded --------");
        }

        private void OnBannerCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("-------- MAX Banner ad collapsed --------");
        #endregion

        #region MREC Callbacks
        private void RegisterMrecCallbacks()
        {
            if (_mrecCallbacksRegistered) return;
            _mrecCallbacksRegistered = true;
            MaxSdkCallbacks.MRec.OnAdLoadedEvent += OnMrecLoadedEvent;
            MaxSdkCallbacks.MRec.OnAdLoadFailedEvent += OnMrecLoadFailedEvent;
            MaxSdkCallbacks.MRec.OnAdClickedEvent += OnMrecClickedEvent;
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnMrecRevenuePaidEvent;
            MaxSdkCallbacks.MRec.OnAdExpandedEvent += OnMrecExpandedEvent;
            MaxSdkCallbacks.MRec.OnAdCollapsedEvent += OnMrecCollapsedEvent;
        }

        private void OnMrecLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("-------- MAX MREC ad loaded --------");
            _mrecRetryAttempt = 0;
            AdEvents.OnMrecLoaded?.Invoke("mrec loaded");
        }

        private void OnMrecLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            var message = $"MREC load failed: {errorInfo.Code}";
            Debug.Log($"-------- MAX {message} --------");
            _mrecRetryAttempt++;
            AdEvents.OnMrecLoadFailed?.Invoke(message);
        }

        private void OnMrecClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("-------- MAX MREC ad clicked --------");

        private void OnMrecRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("-------- MAX MREC ad revenue paid --------");
            TrackAdRevenue("Native", adInfo);
        }

        private void OnMrecExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdEvents.OnMrecShown?.Invoke(adUnitId);
            Debug.Log("-------- MAX MREC ad expanded --------");
        }

        private void OnMrecCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("-------- MAX MREC ad collapsed --------");
        #endregion

        #region Interstitial Callbacks
        private void RegisterInterstitialCallbacks()
        {
            if (_interstitialCallbacksRegistered) return;
            _interstitialCallbacksRegistered = true;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaidEvent;
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            var slotIndex = FindSlotForUnitId(adUnitId);
            interstitialReadyBySlot[slotIndex] = true;
            if (_interstitialRetryBySlot.ContainsKey(slotIndex))
                _interstitialRetryBySlot[slotIndex] = 0;
            var message = $"Interstitial slot {slotIndex} loaded";
            Debug.Log("SDKCallback | InterAd | Loaded ");
            AdEvents.OnInterstitialSlotLoaded?.Invoke(slotIndex, message);
        }

        private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            var slotIndex = FindSlotForUnitId(adUnitId);
            interstitialReadyBySlot[slotIndex] = false;
            var message = $"Interstitial slot {slotIndex} load failed: {errorInfo.Code}";
            Debug.Log("SDKCallback | InterAd | LoadFailed ");
            AdEvents.OnInterstitialSlotLoadFailed?.Invoke(slotIndex, message);
        }

        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            var slotIndex = FindSlotForUnitId(adUnitId);
            interstitialReadyBySlot[slotIndex] = false;
            var message = $"Interstitial slot {slotIndex} shown";
            Debug.Log("SDKCallback | InterAd | Displayed ");
            AdEvents.OnInterstitialSlotShown?.Invoke(slotIndex, message);
        }

        private void OnInterstitialDisplayFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            var slotIndex = FindSlotForUnitId(adUnitId);
            interstitialReadyBySlot[slotIndex] = false;
            var message = $"Interstitial slot {slotIndex} failed to display: {errorInfo.Code}";
            Debug.Log("SDKCallback | InterAd | DisplayFailed ");
            AdEvents.OnInterstitialSlotFailToShow?.Invoke(slotIndex, message);
        }

        private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            var slotIndex = FindSlotForUnitId(adUnitId);
            var message = $"Interstitial slot {slotIndex} closed";
            Debug.Log("SDKCallback | InterAd | Closed ");
            AdEvents.OnInterstitialSlotClosed?.Invoke(slotIndex, message);
        }

        private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("SDKCallback | InterAd | Clicked ");

        private void OnInterstitialRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | InterAd | RevenuePaid ");
            TrackAdRevenue("Interstitial", adInfo);
        }
        #endregion

        #region Rewarded Callbacks
        private void RegisterRewardedCallbacks()
        {
            if (_rewardedCallbacksRegistered) return;
            _rewardedCallbacksRegistered = true;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedLoadFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedReceivedRewardEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHiddenEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedRevenuePaidEvent;
        }

        private void OnRewardedLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | RewardAd | Loaded ");
            _rewardedRetryAttempt = 0;
            AdEvents.OnRewardLoaded?.Invoke("Reward Ad Loaded");
        }

        private void OnRewardedLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _rewardedRetryAttempt++;
            var message = $"Rewarded load failed: {errorInfo.Code}";
            Debug.Log("SDKCallback | RewardAd | LoadFailed ");
            AdEvents.OnRewardLoadFailed?.Invoke(message);
        }

        private void OnRewardedDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | RewardAd | Displayed ");
            AdEvents.OnRewardShown?.Invoke(adUnitId);
        }

        private void OnRewardedDisplayFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            var message = $"Rewarded failed to display: {errorInfo.Code}";
            Debug.Log("SDKCallback | RewardAd | DisplayFailed ");
            AdEvents.OnRewardFailToShow?.Invoke(message);
        }

        private void OnRewardedReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | RewardAd | Rewarded ");
            AdEvents.OnRewardEarned?.Invoke(reward.Label);
        }

        private void OnRewardedHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | RewardAd | Closed ");
            AdEvents.OnRewardClosed?.Invoke(adUnitId);
        }

        private void OnRewardedClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) =>
            Debug.Log("SDKCallback | RewardAd | Clicked ");

        private void OnRewardedRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("SDKCallback | RewardAd | RevenuePaid ");
            TrackAdRevenue("Rewarded", adInfo);
        }
        #endregion

        #region Paid Impression
        private static void TrackAdRevenue(string adFormat, MaxSdkBase.AdInfo adInfo)
        {
            if (FinzAnalysisManager.instance)
                FinzAnalysisManager.instance.PaidAdAnalytics(adFormat, adInfo);
        }
        #endregion

        #region Helpers
        private static string NormalizeId(string id) =>
            string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        private int FindSlotForUnitId(string adUnitId)
        {
            foreach (var kvp in interstitialSlotIds)
            {
                if (kvp.Value == adUnitId)
                    return kvp.Key;
            }
            return 0;
        }

        private static MaxSdkBase.AdViewPosition ToAdViewPosition(BannerPosition pos)
        {
            return pos switch
            {
                BannerPosition.TopLeft => MaxSdkBase.AdViewPosition.TopLeft,
                BannerPosition.TopCenter => MaxSdkBase.AdViewPosition.TopCenter,
                BannerPosition.TopRight => MaxSdkBase.AdViewPosition.TopRight,
                BannerPosition.CenterLeft => MaxSdkBase.AdViewPosition.CenterLeft,
                BannerPosition.Center => MaxSdkBase.AdViewPosition.Centered,
                BannerPosition.CenterRight => MaxSdkBase.AdViewPosition.CenterRight,
                BannerPosition.BottomLeft => MaxSdkBase.AdViewPosition.BottomLeft,
                BannerPosition.BottomCenter => MaxSdkBase.AdViewPosition.BottomCenter,
                BannerPosition.BottomRight => MaxSdkBase.AdViewPosition.BottomRight,
                _ => MaxSdkBase.AdViewPosition.BottomCenter
            };
        }
        #endregion
    }
}
#endif
