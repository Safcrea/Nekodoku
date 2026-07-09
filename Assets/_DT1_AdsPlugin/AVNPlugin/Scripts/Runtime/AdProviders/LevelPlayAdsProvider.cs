#if USE_AVNADS_PLUGIN && USE_LEVELPLAY
using System.Collections.Generic;
using AVN.AdsPlugin;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace AVN.AdsPlugin.Services
{
    /// <summary>
    /// LevelPlay (IronSource) implementation of IAdsProvider.
    /// All SDK-specific code lives here; nothing outside this file references LevelPlay types.
    /// Fires AdEvents to communicate results back to controllers.
    /// </summary>
    public sealed class LevelPlayAdsProvider : IAdsProvider
    {
        #region Fields
        private bool _sdkInitialized = false;
        public bool IsSdkInitialized => _sdkInitialized;
        public event System.Action SDKInitializedSuccessfully;
        public event System.Action<string> SDKInitializationFailed;

        // Ad Unit ID overrides (set via ConfigureAdIds)
        private string bannerAdUnitIdOverride = string.Empty;
        private string mrecAdUnitIdOverride = string.Empty;
        private string rewardedAdUnitIdOverride = string.Empty;

        private readonly Dictionary<int, string> interstitialSlotIds = new Dictionary<int, string>();
        private readonly Dictionary<int, bool> interstitialReadyBySlot = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> interstitialIsRewardedBySlot = new Dictionary<int, bool>();

        // Ad objects
        private LevelPlayRewardedAd rewardedVideoAd = null;
        private LevelPlayBannerAd bannerAd = null;
        private LevelPlayBannerAd mrecBannerAd = null;
        private readonly Dictionary<int, LevelPlayInterstitialAd> interstitialAds = new Dictionary<int, LevelPlayInterstitialAd>();

        private bool isAdaptiveBanner;
        #endregion

        #region Initialization
        public void Initialize(string appKey)
        {
            if (_sdkInitialized)
            {
                SDKInitializedSuccessfully?.Invoke();
                return;
            }

            DebugLogger.AVNLog("LevelPlayAdsProvider Initialize called");
            SetLevelPlayMetaData();
            LevelPlay.ValidateIntegration();
            LevelPlay.OnInitSuccess -= SdkInitializationCompletedEvent;
            LevelPlay.OnInitFailed -= SdkInitializationFailedEvent;
            LevelPlay.OnInitSuccess += SdkInitializationCompletedEvent;
            LevelPlay.OnInitFailed += SdkInitializationFailedEvent;
            InitializePaidAdEvent();
            LevelPlay.Init(appKey);
        }

        private void SdkInitializationFailedEvent(LevelPlayInitError error)
        {
            _sdkInitialized = false;
            DebugLogger.AVNLog("SdkInitializationFailedEvent: " + error.ErrorMessage);
            Debug.Log("------ [LevelPlaySample] Received SdkInitializationFailedEvent with Error:" + error.ErrorMessage);
            SDKInitializationFailed?.Invoke(error.ErrorMessage);
        }

        private void SdkInitializationCompletedEvent(LevelPlayConfiguration configuration)
        {
            DebugLogger.AVNLog("SdkInitializationCompletedEvent");
            Debug.Log("-------- IS SDK INITILIZATION COMPLETE--------");
            _sdkInitialized = true;
            SDKInitializedSuccessfully?.Invoke();
        }

        private void SetLevelPlayMetaData()
        {
            LevelPlay.SetMetaData("do_not_sell", "false");
            LevelPlay.SetMetaData("is_child_directed", "false");
            LevelPlay.SetMetaData("is_deviceid_optout", "false");
            LevelPlay.SetMetaData("is_child_directed", "false");
            LevelPlay.SetMetaData("BidMachine_COPPA", "true");
            LevelPlay.SetMetaData("Yandex_COPPA", "true");
            LevelPlay.SetMetaData("Meta_Mixed_Audience", "true");
            LevelPlay.SetMetaData("UnityAds_coppa", "true");
            LevelPlay.SetMetaData("Pangle_COPPA", "0");
            LevelPlay.SetMetaData("Mintegral_COPPA", "true");
            LevelPlay.SetMetaData("Vungle_coppa", "true");
            LevelPlay.SetMetaData("InMobi_AgeRestricted", "false");
            LevelPlay.SetMetaData("AdMob_TFCD", "false");
            LevelPlay.SetMetaData("DT_IsChild", "false");
            LevelPlay.SetMetaData("DT_COPPA", "true");
            LevelPlay.SetConsent(true);
            SetPauseGame(true);
        }

        public void ConfigureAdIds(string bannerId, string mrecId, string rewardedId)
        {
            DebugLogger.AVNLog("ConfigureAdIds called");
            bannerAdUnitIdOverride = NormalizeId(bannerId);
            mrecAdUnitIdOverride = NormalizeId(mrecId);
            rewardedAdUnitIdOverride = NormalizeId(rewardedId);
        }
        #endregion

        #region Interstitial
        public void LoadInterstitial(int slotIndex, string adUnitId)
        {
            DebugLogger.AVNLog("LoadInterstitial slot=" + slotIndex);
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                RaiseInterstitialLoadFailed(slotIndex, "Interstitial ad unit id is empty");
                return;
            }

            // Destroy the old ad object before replacing it. Without this, the abandoned
            // LevelPlayInterstitialAd still has active SDK event handlers and can fire
            // stale OnAdLoadFailed callbacks that corrupt _threeTierCurrentIdIndex.
            if (interstitialAds.TryGetValue(slotIndex, out var oldAd) && oldAd != null)
            {
                oldAd.DestroyAd();
                interstitialAds[slotIndex] = null;
            }

            var slotAd = new LevelPlayInterstitialAd(adUnitId);
            interstitialAds[slotIndex] = slotAd;
            interstitialSlotIds[slotIndex] = adUnitId;
            interstitialReadyBySlot[slotIndex] = false;

            slotAd.OnAdLoaded += info => OnInterstitialLoadedForSlot(slotIndex, info);
            slotAd.OnAdLoadFailed += error => OnInterstitialLoadFailedForSlot(slotIndex, error);
            slotAd.OnAdDisplayed += info => OnInterstitialDisplayedForSlot(slotIndex, info);
            slotAd.OnAdDisplayFailed += (info, error) => OnInterstitialDisplayFailedForSlot(slotIndex, info, error);
            slotAd.OnAdClicked += info => OnInterstitialClickedForSlot(slotIndex, info);
            slotAd.OnAdClosed += info => OnInterstitialClosedForSlot(slotIndex, info);
            slotAd.OnAdInfoChanged += info => OnInterstitialInfoChangedForSlot(slotIndex, info);

            slotAd.LoadAd();
            DebugLogger.AVNLog("Interstitial load sent for slot=" + slotIndex);
        }

        public void ShowInterstitial(int slotIndex, bool isRewarded)
        {
            if (!interstitialAds.TryGetValue(slotIndex, out var slotAd) || slotAd == null)
            {
                DebugLogger.AVNLog("ShowInterstitial blocked: slot not ready, slot=" + slotIndex);
                Debug.Log($"-------- Interstitial slot {slotIndex} is not ready yet --------");
                return;
            }

            interstitialIsRewardedBySlot[slotIndex] = isRewarded;
            AdEvents.OnBeforeInterstitialShown?.Invoke(slotIndex, isRewarded);
            slotAd.ShowAd();
            DebugLogger.AVNLog("ShowInterstitial sent for slot=" + slotIndex);
        }

        public bool IsInterstitialReady(int slotIndex)
        {
            if (!interstitialAds.TryGetValue(slotIndex, out var slotAd) || slotAd == null)
                return false;
            return slotAd.IsAdReady();
        }
        #endregion

        #region Rewarded
        public void LoadRewarded()
        {
            var rewardedUnitId = GetRewardedAdUnitId();
            if (string.IsNullOrWhiteSpace(rewardedUnitId))
            {
                AdEvents.OnRewardLoadFailed?.Invoke("Rewarded ad unit id is empty");
                return;
            }

            rewardedVideoAd = new LevelPlayRewardedAd(rewardedUnitId);
            rewardedVideoAd.OnAdLoaded += RewardedVideoOnLoadedEvent;
            rewardedVideoAd.OnAdLoadFailed += RewardedVideoOnAdLoadFailedEvent;
            rewardedVideoAd.OnAdDisplayed += RewardedVideoOnAdDisplayedEvent;
            rewardedVideoAd.OnAdDisplayFailed += RewardedVideoOnAdDisplayedFailedEvent;
            rewardedVideoAd.OnAdRewarded += RewardedVideoOnAdRewardedEvent;
            rewardedVideoAd.OnAdClicked += RewardedVideoOnAdClickedEvent;
            rewardedVideoAd.OnAdClosed += RewardedVideoOnAdClosedEvent;
            rewardedVideoAd.OnAdInfoChanged += RewardedVideoOnAdInfoChangedEvent;
            rewardedVideoAd.LoadAd();
            DebugLogger.AVNLog("LoadRewarded sent");
        }

        public void ShowRewarded()
        {
            AdEvents.OnBeforeRewardedShown?.Invoke();
            rewardedVideoAd.ShowAd();
        }

        public bool IsRewardedReady()
        {
            return rewardedVideoAd != null && rewardedVideoAd.IsAdReady();
        }
        #endregion

        #region Banner & MREC
        public void LoadBanner(BannerAdTypes type, BannerPosition position, bool isAdaptive, bool ignoreNotch)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    LoadMrecBannerInternal(ToLevelPlayPosition(position));
                    break;
                default:
                    LoadBannerInternal(isAdaptive, ignoreNotch, ToLevelPlayPosition(position));
                    break;
            }
        }

        public void ShowBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    mrecBannerAd?.ShowAd();
                    break;
                default:
                    bannerAd?.ShowAd();
                    break;
            }
        }

        public void HideBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    Debug.Log("--------  mrec Hiding Banner ad --------");
                    if (mrecBannerAd != null) mrecBannerAd.HideAd();
                    break;
                default:
                    Debug.Log("-------- Hiding Banner ad --------");
                    if (bannerAd != null) bannerAd.HideAd();
                    break;
            }
        }

        public void DestroyBanner(BannerAdTypes type)
        {
            switch (type)
            {
                case BannerAdTypes.MREC:
                    Debug.Log("--------  mrec Destroying Banner ad --------");
                    if (mrecBannerAd != null) mrecBannerAd.DestroyAd();
                    mrecBannerAd = null;
                    break;
                default:
                    Debug.Log("-------- Destroying Banner ad --------");
                    if (bannerAd != null) bannerAd.DestroyAd();
                    bannerAd = null;
                    break;
            }
        }

        private void LoadBannerInternal(bool isAdaptiveBanner, bool shouldBannerIgnoreNotchArea, LevelPlayBannerPosition bannerPosition)
        {
            bannerAd = null;
            this.isAdaptiveBanner = isAdaptiveBanner;

            var configBuilder = new LevelPlayBannerAd.Config.Builder();
            configBuilder.SetSize(LevelPlayAdSize.BANNER);
            if (isAdaptiveBanner)
            {
                configBuilder.SetSize(LevelPlayAdSize.CreateAdaptiveAdSize(Screen.width));
            }
            configBuilder.SetPosition(bannerPosition);
            configBuilder.SetRespectSafeArea(shouldBannerIgnoreNotchArea);
            configBuilder.SetDisplayOnLoad(false);
            var bannerConfig = configBuilder.Build();

            var bannerUnitId = GetBannerAdUnitId();
            if (string.IsNullOrWhiteSpace(bannerUnitId))
            {
                AdEvents.OnBannerLoadFailed?.Invoke("Banner ad unit id is empty");
                return;
            }

            bannerAd = new LevelPlayBannerAd(bannerUnitId, bannerConfig);
            bannerAd.OnAdLoaded += BannerOnAdLoadedEvent;
            bannerAd.OnAdLoadFailed += BannerOnAdLoadFailedEvent;
            bannerAd.OnAdDisplayed += BannerOnAdDisplayedEvent;
            bannerAd.OnAdDisplayFailed += BannerOnAdDisplayFailedEvent;
            bannerAd.OnAdClicked += BannerOnAdClickedEvent;
            bannerAd.OnAdCollapsed += BannerOnAdCollapsedEvent;
            bannerAd.OnAdLeftApplication += BannerOnAdLeftApplicationEvent;
            bannerAd.OnAdExpanded += BannerOnAdExpandedEvent;
            bannerAd.LoadAd();
            DebugLogger.AVNLog("LoadBanner sent");
        }

        private void LoadMrecBannerInternal(LevelPlayBannerPosition mrecBannerPosition)
        {
            mrecBannerAd = null;
            var configBuilder = new LevelPlayBannerAd.Config.Builder();
            configBuilder.SetSize(LevelPlayAdSize.MEDIUM_RECTANGLE);
            configBuilder.SetPosition(mrecBannerPosition);
            configBuilder.SetRespectSafeArea(false);
            configBuilder.SetDisplayOnLoad(false);
            var bannerConfig = configBuilder.Build();

            var mrecUnitId = GetMrecAdUnitId();
            if (string.IsNullOrWhiteSpace(mrecUnitId))
            {
                AdEvents.OnMrecLoadFailed?.Invoke("MREC ad unit id is empty");
                return;
            }

            mrecBannerAd = new LevelPlayBannerAd(mrecUnitId, bannerConfig);
            mrecBannerAd.OnAdLoaded += MrecBannerOnAdLoadedEvent;
            mrecBannerAd.OnAdLoadFailed += MrecBannerOnAdLoadFailedEvent;
            mrecBannerAd.OnAdDisplayed += MrecBannerOnAdDisplayedEvent;
            mrecBannerAd.OnAdDisplayFailed += MrecBannerOnAdDisplayFailedEvent;
            mrecBannerAd.OnAdClicked += MrecBannerOnAdClickedEvent;
            mrecBannerAd.OnAdCollapsed += MrecBannerOnAdCollapsedEvent;
            mrecBannerAd.OnAdLeftApplication += MrecBannerOnAdLeftApplicationEvent;
            mrecBannerAd.OnAdExpanded += MrecBannerOnAdExpandedEvent;
            mrecBannerAd.LoadAd();
            DebugLogger.AVNLog("LoadMrecBanner sent");
        }
        #endregion

        #region Banner Callbacks
        private void BannerOnAdLoadedEvent(LevelPlayAdInfo adInfo) =>
            AdEvents.OnBannerLoaded?.Invoke("banner loaded");

        private void BannerOnAdLoadFailedEvent(LevelPlayAdError error)
        {
            var message = GetAdErrorMessage(error, "Banner load failed", "banner");
            AdEvents.OnBannerLoadFailed?.Invoke(message);
            Debug.Log("-------- Banner ad failed to load with error code: " + message + " --------");
        }

        private void BannerOnAdClickedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("-------- Banner ad Clicked: " + adInfo.AdFormat + " --------");

        private void BannerOnAdLeftApplicationEvent(LevelPlayAdInfo adInfo) { }

        private void BannerOnAdDisplayedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnBannerShown?.Invoke(adInfo.AdFormat);
            Debug.Log("unity-script: I got BannerOnAdDisplayedEvent With AdInfo " + adInfo);
        }

        private void BannerOnAdDisplayFailedEvent(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            var message = GetAdErrorMessage(error, "Banner display failed", "banner");
            AdEvents.OnBannerFailToShow?.Invoke(message);
            Debug.Log($"[LevelPlaySample] Received BannerOnAdDisplayFailedEvent With AdInfo: {adInfo} and Error: {error}");
        }

        private void BannerOnAdCollapsedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("unity-script: I got BannerOnAdCollapsedEvent With AdInfo " + adInfo);

        private void BannerOnAdExpandedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("unity-script: I got BannerOnAdExpandedEvent With AdInfo " + adInfo);
        #endregion

        #region MREC Callbacks
        private void MrecBannerOnAdLoadedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnMrecLoaded?.Invoke("mrec loaded");
            Debug.Log("-------- Mrec Banner ad Loaded ");
        }

        private void MrecBannerOnAdLoadFailedEvent(LevelPlayAdError error)
        {
            var message = GetAdErrorMessage(error, "MREC load failed", "mrec");
            AdEvents.OnMrecLoadFailed?.Invoke(message);
            Debug.Log("-------- Mrec Banner ad failed to load with error code: " + message + " --------");
        }

        private void MrecBannerOnAdClickedEvent(LevelPlayAdInfo adInfo) { }
        private void MrecBannerOnAdLeftApplicationEvent(LevelPlayAdInfo adInfo) { }

        private void MrecBannerOnAdDisplayedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnMrecShown?.Invoke(adInfo.AdFormat);
            Debug.Log("unity-script: I got BannerOnAdDisplayedEvent With AdInfo " + adInfo);
        }

        private void MrecBannerOnAdDisplayFailedEvent(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            var message = GetAdErrorMessage(error, "MREC display failed", "mrec");
            AdEvents.OnMrecFailToShow?.Invoke(message);
        }

        private void MrecBannerOnAdCollapsedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("unity-script: I got BannerOnAdCollapsedEvent With AdInfo " + adInfo);

        private void MrecBannerOnAdExpandedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("unity-script: I got BannerOnAdExpandedEvent With AdInfo " + adInfo);
        #endregion

        #region Interstitial Callbacks
        private void OnInterstitialLoadedForSlot(int slotIndex, LevelPlayAdInfo adInfo)
        {
            interstitialReadyBySlot[slotIndex] = true;
            var message = $"Interstitial slot {slotIndex} loaded";
            AdEvents.OnInterstitialSlotLoaded?.Invoke(slotIndex, message);
            Debug.Log("SDKCallback | InterAd | Loaded "); 
        }

        private void OnInterstitialLoadFailedForSlot(int slotIndex, LevelPlayAdError error)
        {
            interstitialReadyBySlot[slotIndex] = false;
            RaiseInterstitialLoadFailed(slotIndex, error.ErrorMessage);
            Debug.Log("SDKCallback | InterAd | LoadFailed "); 
        }

        private void OnInterstitialDisplayedForSlot(int slotIndex, LevelPlayAdInfo adInfo)
        {
            interstitialReadyBySlot[slotIndex] = false;
            var message = $"Interstitial slot {slotIndex} shown";
            AdEvents.OnInterstitialSlotShown?.Invoke(slotIndex, message);
            Debug.Log("SDKCallback | InterAd | Displayed "); 
        }

        private void OnInterstitialDisplayFailedForSlot(int slotIndex, LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            interstitialReadyBySlot[slotIndex] = false;
            var message = error != null ? error.ErrorMessage : $"Interstitial slot {slotIndex} failed to show";
            AdEvents.OnInterstitialSlotFailToShow?.Invoke(slotIndex, message);
            Debug.Log("SDKCallback | InterAd | DisplayFailed "); 
        }

        private void OnInterstitialClosedForSlot(int slotIndex, LevelPlayAdInfo adInfo)
        {
            var message = $"Interstitial slot {slotIndex} closed";
            AdEvents.OnInterstitialSlotClosed?.Invoke(slotIndex, message);
            Debug.Log("SDKCallback | InterAd | Closed ");
        }

        private void OnInterstitialClickedForSlot(int slotIndex, LevelPlayAdInfo adInfo) =>
            Debug.Log("SDKCallback | InterAd | Clicked ");

        private void OnInterstitialInfoChangedForSlot(int slotIndex, LevelPlayAdInfo adInfo) =>
            Debug.Log("SDKCallback | InterAd | InfoChanged ");
        private static void RaiseInterstitialLoadFailed(int slotIndex, string message) =>
            AdEvents.OnInterstitialSlotLoadFailed?.Invoke(slotIndex, message);
        #endregion

        #region Rewarded Callbacks
        private void RewardedVideoOnLoadedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnRewardLoaded?.Invoke("Reward Ad Loaded");
            Debug.Log("SDKCallback | RewardAd | Loaded ");
        }

        private void RewardedVideoOnAdLoadFailedEvent(LevelPlayAdError error)
        {
            AdEvents.OnRewardLoadFailed?.Invoke(error.ErrorMessage);
            Debug.Log("SDKCallback | RewardAd | LoadFailed ");
        }

        private void RewardedVideoOnAdDisplayedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnRewardShown?.Invoke(adInfo != null ? adInfo.ToString() : "Reward Ad shown");
            Debug.Log("SDKCallback | RewardAd | Displayed ");
        }

        private void RewardedVideoOnAdDisplayedFailedEvent(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            AdEvents.OnRewardFailToShow?.Invoke(error.ErrorMessage);
            Debug.Log("SDKCallback | RewardAd | DisplayFailed ");
        }

        private void RewardedVideoOnAdRewardedEvent(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            AdEvents.OnRewardEarned?.Invoke(reward.Name);
            Debug.Log("SDKCallback | RewardAd | Rewarded ");
        }

        private void RewardedVideoOnAdClickedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("SDKCallback | RewardAd | Clicked ");

        private void RewardedVideoOnAdClosedEvent(LevelPlayAdInfo adInfo)
        {
            AdEvents.OnRewardClosed?.Invoke(adInfo.AdFormat);
            Debug.Log("SDKCallback | RewardAd | Closed ");
        }

        private void RewardedVideoOnAdInfoChangedEvent(LevelPlayAdInfo adInfo) =>
            Debug.Log("SDKCallback | RewardAd | InfoChanged ");
        #endregion

        #region Paid Impression
        private void InitializePaidAdEvent()
        {
            LevelPlay.OnImpressionDataReady += ImpressionDataReadyEvent;
        }

        private void ImpressionDataReadyEvent(LevelPlayImpressionData impressionData)
        {
            if (FinzAnalysisManager.instance)
            {
                FinzAnalysisManager.instance.PaidAdAnalytics(impressionData);
            }
        }
        #endregion

        #region Helpers
        private static string GetAdErrorMessage(LevelPlayAdError error, string fallbackMessage, string adKind)
        {
            return error != null && !string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? error.ErrorMessage
                : fallbackMessage;
        }

        private static string NormalizeId(string id) =>
            string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        private string GetBannerAdUnitId() => bannerAdUnitIdOverride;
        private string GetMrecAdUnitId() => mrecAdUnitIdOverride;
        private string GetRewardedAdUnitId() => rewardedAdUnitIdOverride;

        private static LevelPlayBannerPosition ToLevelPlayPosition(BannerPosition pos)
        {
            return pos switch
            {
                BannerPosition.TopLeft      => LevelPlayBannerPosition.TopLeft,
                BannerPosition.TopCenter    => LevelPlayBannerPosition.TopCenter,
                BannerPosition.TopRight     => LevelPlayBannerPosition.TopRight,
                BannerPosition.CenterLeft   => LevelPlayBannerPosition.CenterLeft,
                BannerPosition.Center       => LevelPlayBannerPosition.Center,
                BannerPosition.CenterRight  => LevelPlayBannerPosition.CenterRight,
                BannerPosition.BottomLeft   => LevelPlayBannerPosition.BottomLeft,
                BannerPosition.BottomCenter => LevelPlayBannerPosition.BottomCenter,
                BannerPosition.BottomRight  => LevelPlayBannerPosition.BottomRight,
                _                           => LevelPlayBannerPosition.BottomCenter
            };
        }
        #endregion
    }
}
#endif
