#if USE_AVNADS_PLUGIN

using System.Collections;
using AVN.AdsPlugin;
using AVN.AdsPlugin.Services;
using AVN.AdsPlugin.Testing;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AVN.AdsPlugin.Controllers
{
    public sealed class BannerAdController : MonoBehaviour
    {
        #region Fields
        [FoldoutGroup("Banner Positions")]
        [SerializeField] private BannerPosition bannerPosition = BannerPosition.BottomCenter;
        [FoldoutGroup("Banner Positions")]
        [SerializeField] private BannerPosition mrecBannerPosition = BannerPosition.TopCenter;

        [FoldoutGroup("Banner Settings")]
        public bool showBannerOnInit = false;
        [FoldoutGroup("Banner Settings")]
        [SerializeField] private bool isAdaptiveBanner = false;
        [FoldoutGroup("Banner Settings")]
        [SerializeField] private bool shouldBannerIgnoreNotchArea = false;
        [FoldoutGroup("Banner Settings")]
        [SerializeField] private bool bannerPersistLoadCalls = false;

        [FoldoutGroup("Banner BG")]
        [SerializeField] private bool useBannerBG = false;
        [FoldoutGroup("Banner BG")]
        [ShowIf(nameof(useBannerBG))]
        [SerializeField] private GameObject bannerBG;

        private IAdsProvider _provider;
        private BannerAdStatus bannerStatus = BannerAdStatus.NotLoaded;
        private BannerAdStatus mrecStatus = BannerAdStatus.NotLoaded;
        private BannerAdTypes pendingBannerType;
        private Coroutine _bannerRetryCoroutine;
        private Coroutine _mrecRetryCoroutine;
        private bool _showBannerOnLoad;
        private int _bannerRetryAttempt = 0;
        private int _mrecRetryAttempt = 0;
        #endregion

        #region Initialization
        public void Initialize(IAdsProvider provider)
        {
            _provider = provider;
            pendingBannerType = BannerAdTypes.BANNER;
            bannerBG?.SetActive(false);
        }
        private void OnEnable()
        {
            AdEvents.OnBannerLoaded += HandleBannerLoaded;
            AdEvents.OnBannerShown += HandleBannerShown;
            AdEvents.OnBannerFailToShow += HandleBannerShowFailed;

            AdEvents.OnBannerLoadFailed += HandleBannerLoadFailed;

            AdEvents.OnMrecLoaded += HandleMrecLoaded;
            AdEvents.OnMrecShown += HandleMrecShown;
            AdEvents.OnMrecFailToShow += HandleMrecShowFailed;

            AdEvents.OnMrecLoadFailed += HandleMrecLoadFailed;
        }

        private void OnDisable()
        {
            AdEvents.OnBannerLoaded -= HandleBannerLoaded;
            AdEvents.OnBannerShown -= HandleBannerShown;
            AdEvents.OnBannerFailToShow -= HandleBannerShowFailed;

            AdEvents.OnBannerLoadFailed -= HandleBannerLoadFailed;

            AdEvents.OnMrecLoaded -= HandleMrecLoaded;
            AdEvents.OnMrecShown -= HandleMrecShown;
            AdEvents.OnMrecFailToShow -= HandleMrecShowFailed;

            AdEvents.OnMrecLoadFailed -= HandleMrecLoadFailed;
        }
        #endregion

        #region Public Methods
        public void ShowBanner(BannerAdTypes? type = null)
        {
            if (!CanUseBanner())
            {
                PublishStatusForType(type ?? BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, "Banner blocked by config");
                return;
            }

            pendingBannerType = type ?? BannerAdTypes.BANNER;
            // * If Banner is already showing, ignore the call
            if (IsShowingForType(pendingBannerType))
            {
                PublishStatusForType(pendingBannerType, DiagnosticStateAVNPlugin.Failed, $"Show ignored: {pendingBannerType} already showing");
                return;
            }

            // * If Banner is ready, show it
            if (IsReadyForType(pendingBannerType))
            {

                DiagnosticsHubAVNPlugin.IncrementShowCallSent(GetDiagnosticKey(pendingBannerType));
                _provider.ShowBanner(pendingBannerType);
                // Set Showing optimistically: LevelPlay's OnAdDisplayed does not re-fire for
                // subsequent ShowAd() calls after HideAd(), so we cannot rely on the callback.
                SetShowingForType(pendingBannerType, true);
                PublishStatusForType(pendingBannerType, DiagnosticStateAVNPlugin.Showing, $"Showing {pendingBannerType}");
                return;
            }

            // * If Banner is loading, ignore the call (don't want multiple loads in flight)
            if (IsLoadingForType(pendingBannerType))
            {
                DiagnosticsHubAVNPlugin.IncrementShowCallSent(GetDiagnosticKey(pendingBannerType));
                PublishStatusForType(pendingBannerType, DiagnosticStateAVNPlugin.Loading, $"Show ignored for {pendingBannerType}; load already in progress");
                return;
            }


            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(GetDiagnosticKey(pendingBannerType), "Not ready yet");
            PublishStatusForType(pendingBannerType, DiagnosticStateAVNPlugin.Failed, $"Show failed: {pendingBannerType} not ready yet");
        }
        public void UpdateShowBannerOnLoad(bool value)
        {
            if (bannerStatus == BannerAdStatus.NotLoaded)
                _showBannerOnLoad = value;
        }
        public void LoadBanner(BannerAdTypes? type = null)
        {
            if (!CanUseBanner())
            {
                PublishStatusForType(type ?? BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, "Banner blocked by config");
                return;
            }

            var targetType = type ?? BannerAdTypes.BANNER;
            if (IsShowingForType(targetType))
            {
                PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Showing, $"Load blocked for {targetType}; already showing");
                return;
            }

            if (IsLoadingForType(targetType))
            {
                PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Loading, $"Load ignored for {targetType}; already loading");
                return;
            }
            if (IsReadyForType(targetType))
            {
                PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Loaded, $"Load ignored for {targetType}; already loaded");
                return;
            }

            pendingBannerType = targetType;
            IncrementLoadSentForType(targetType);
            SetLoadingForType(targetType, true);
            _provider.LoadBanner(targetType,
                targetType == BannerAdTypes.MREC ? mrecBannerPosition : bannerPosition,
                isAdaptiveBanner, shouldBannerIgnoreNotchArea);
            PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Loading, $"Loading {targetType}");
        }
        public void HideBanner(BannerAdTypes? type = null)
        {
            if (_provider == null)
            {
                return;
            }

            var targetType = type ?? pendingBannerType;
            if (!IsShowingForType(targetType))
            {
                PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Closed, $"Hide ignored for {targetType}; not currently showing");
                return;
            }

            _provider.HideBanner(targetType);
            SetReadyForType(targetType, true);
            PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Closed, $"Hiding {targetType}");
        }
        public void DestroyBanner(BannerAdTypes? type = null)
        {
            if (_provider == null)
            {
                return;
            }

            var targetType = type ?? pendingBannerType;
            if (!IsShowingForType(targetType) && !IsReadyForType(targetType) && !IsLoadingForType(targetType))
            {
                PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Destroyed, $"Destroy ignored for {targetType}; already idle");
                return;
            }

            _provider.DestroyBanner(targetType);
            SetShowingForType(targetType, false);
            SetLoadingForType(targetType, false);
            SetReadyForType(targetType, false);
            PublishStatusForType(targetType, DiagnosticStateAVNPlugin.Destroyed, $"Destroying {targetType}");
        }

        public bool IsReady(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC ? mrecStatus == BannerAdStatus.Loaded : bannerStatus == BannerAdStatus.Loaded;
        }

        public bool IsShowing(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC ? mrecStatus == BannerAdStatus.Showing : bannerStatus == BannerAdStatus.Showing;
        }

        public BannerAdTypes CurrentPendingType()
        {
            return pendingBannerType;
        }
        #endregion

        #region Helpers
        private bool CanUseBanner()
        {
            return CanUseType(BannerAdTypes.BANNER);
        }

        private bool CanUseType(BannerAdTypes type)
        {
            if (_provider == null || !AVNPluginConstants.AdsEnabledMaster)
            {
                return false;
            }

            if (type == BannerAdTypes.MREC)
            {
                return true;
            }

            return AVNPluginConstants.EnableBannerAd;
        }

        private bool IsReadyForType(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC ? mrecStatus == BannerAdStatus.Loaded : bannerStatus == BannerAdStatus.Loaded;
        }

        private bool IsLoadingForType(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC ? mrecStatus == BannerAdStatus.Loading : bannerStatus == BannerAdStatus.Loading;
        }

        private bool IsShowingForType(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC ? mrecStatus == BannerAdStatus.Showing : bannerStatus == BannerAdStatus.Showing;
        }

        private void SetReadyForType(BannerAdTypes type, bool value)
        {
            if (type == BannerAdTypes.MREC)
            {
                mrecStatus = value ? BannerAdStatus.Loaded : BannerAdStatus.NotLoaded;
                return;
            }
            bannerStatus = value ? BannerAdStatus.Loaded : BannerAdStatus.NotLoaded;
        }

        private void SetLoadingForType(BannerAdTypes type, bool value)
        {
            if (type == BannerAdTypes.MREC)
            {
                // Bug fix: don't overwrite Ready/Showing when clearing loading flag
                if (!value && mrecStatus != BannerAdStatus.Loading) return;
                mrecStatus = value ? BannerAdStatus.Loading : BannerAdStatus.NotLoaded;
                return;
            }
            // Bug fix: don't overwrite Ready/Showing when clearing loading flag
            if (!value && bannerStatus != BannerAdStatus.Loading) return;
            bannerStatus = value ? BannerAdStatus.Loading : BannerAdStatus.NotLoaded;
        }

        private void SetShowingForType(BannerAdTypes type, bool value)
        {
            if (type == BannerAdTypes.MREC)
            {
                mrecStatus = value ? BannerAdStatus.Showing : BannerAdStatus.NotLoaded;
                return;
            }
            if (useBannerBG) bannerBG?.SetActive(value);
            bannerStatus = value ? BannerAdStatus.Showing : BannerAdStatus.NotLoaded;
        }
        private static string GetDiagnosticKey(BannerAdTypes type)
        {
            return type == BannerAdTypes.MREC
                ? DiagnosticKeysAVNPlugin.AdBannerMrec
                : DiagnosticKeysAVNPlugin.AdBannerStandard;
        }

        private static void PublishStatusForType(BannerAdTypes type, DiagnosticStateAVNPlugin state, string message)
        {
            var key = GetDiagnosticKey(type);
            DiagnosticsHubAVNPlugin.PublishStatus(key, state, message);
        }

        private static void IncrementLoadSentForType(BannerAdTypes type)
        {
            DiagnosticsHubAVNPlugin.IncrementLoadCallSent(GetDiagnosticKey(type));
        }

        private static void IncrementLoadedForType(BannerAdTypes type)
        {
            DiagnosticsHubAVNPlugin.IncrementAdsLoaded(GetDiagnosticKey(type));
        }

        private static void IncrementLoadFailedForType(BannerAdTypes type, string message)
        {
            DiagnosticsHubAVNPlugin.IncrementLoadCallsFailed(GetDiagnosticKey(type), message);
        }
        #endregion

        #region Event Callbacks
        private void HandleBannerLoaded(string _)
        {
            if (IsShowingForType(BannerAdTypes.BANNER))
            {
                // IronSource refreshed the banner internally — still showing, so no state/retry changes.
                // Log diagnostics only so the hub tracks the refresh load.
                IncrementLoadedForType(BannerAdTypes.BANNER);
                PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Loaded, "Banner refresh loaded by IronSource while showing");
                StopBannerRetry(BannerAdTypes.BANNER);
                return;
            }

            // Guard against stale callbacks from a previous ad instance
            if (!IsLoadingForType(BannerAdTypes.BANNER))
            {

                PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, "Received Banner Loaded callback while not in Loading state — IS probably sent this loaded call");
                IncrementLoadedForType(BannerAdTypes.BANNER);
                return;
            }

            SetReadyForType(BannerAdTypes.BANNER, true);
            SetLoadingForType(BannerAdTypes.BANNER, false);
            IncrementLoadedForType(BannerAdTypes.BANNER);
            StopBannerRetry(BannerAdTypes.BANNER);
            PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Loaded, "Banner loaded");
            if (_showBannerOnLoad) ShowBanner(BannerAdTypes.BANNER);
            if (pendingBannerType == BannerAdTypes.MREC)
            {
                return;
            }
        }

        private void HandleBannerShown(string _)
        {
            SetShowingForType(BannerAdTypes.BANNER, true);
            StopBannerRetry(BannerAdTypes.BANNER);
            FinzAnalysisManager.Instance?.BannerAdAnalysis(isAdaptiveBanner ? BannerAdTypes.ADAPTIVE : BannerAdTypes.BANNER);
            DiagnosticsHubAVNPlugin.IncrementAdsDisplayed(DiagnosticKeysAVNPlugin.AdBannerStandard);
            PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Showing, "Banner displayed");
        }

        private void HandleBannerShowFailed(string message)
        {
            SetShowingForType(BannerAdTypes.BANNER, false);
            SetReadyForType(BannerAdTypes.BANNER, false);
            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdBannerStandard, message);
            PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, message);
        }



        private void HandleBannerLoadFailed(string message)
        {
            if (IsShowingForType(BannerAdTypes.BANNER))
            {
                // IronSource refresh failed — banner is still displaying, no retry needed.
                // Log diagnostics only so the hub tracks the refresh failure.
                IncrementLoadFailedForType(BannerAdTypes.BANNER, message);
                PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, $"Banner refresh load failed by IronSource — banner still showing: {message}");
                StopBannerRetry(BannerAdTypes.BANNER);
                return;
            }

            // Also ignore failure if ad is already ready
            if (IsReadyForType(BannerAdTypes.BANNER))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(GetDiagnosticKey(BannerAdTypes.BANNER), DiagnosticStateAVNPlugin.Failed, $"Received Banner Load Failed callback while already ready — assuming late failure call: {message}");
                return;
            }

            SetReadyForType(BannerAdTypes.BANNER, false);
            SetLoadingForType(BannerAdTypes.BANNER, false);
            IncrementLoadFailedForType(BannerAdTypes.BANNER, message);
            StartBannerRetry(BannerAdTypes.BANNER);
            PublishStatusForType(BannerAdTypes.BANNER, DiagnosticStateAVNPlugin.Failed, message);
        }

        private void HandleMrecLoaded(string _)
        {
            if (IsShowingForType(BannerAdTypes.MREC))
            {
                // IronSource refreshed the MREC internally — still showing, so no state/retry changes.
                // Log diagnostics only so the hub tracks the refresh load.
                IncrementLoadedForType(BannerAdTypes.MREC);
                PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Loaded, "MREC refresh loaded by IronSource while showing");
                StopBannerRetry(BannerAdTypes.MREC);
                return;
            }

            // Guard against stale callbacks from a previous ad instance
            if (!IsLoadingForType(BannerAdTypes.MREC))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(GetDiagnosticKey(BannerAdTypes.MREC), DiagnosticStateAVNPlugin.Failed, "Received MREC Loaded callback while not in Loading state — IS probably sent this loaded call");
                return;
            }

            SetReadyForType(BannerAdTypes.MREC, true);
            SetLoadingForType(BannerAdTypes.MREC, false);
            IncrementLoadedForType(BannerAdTypes.MREC);
            StopBannerRetry(BannerAdTypes.MREC);
            PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Loaded, "MREC loaded");
            if (pendingBannerType != BannerAdTypes.MREC)
            {
                return;
            }
        }

        private void HandleMrecShown(string _)
        {
            SetShowingForType(BannerAdTypes.MREC, true);
            StopBannerRetry(BannerAdTypes.MREC);
            FinzAnalysisManager.Instance?.BannerAdAnalysis(BannerAdTypes.MREC);
            DiagnosticsHubAVNPlugin.IncrementAdsDisplayed(DiagnosticKeysAVNPlugin.AdBannerMrec);
            PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Showing, "MREC displayed");
        }

        private void HandleMrecShowFailed(string message)
        {
            SetShowingForType(BannerAdTypes.MREC, false);
            SetReadyForType(BannerAdTypes.MREC, false);
            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdBannerMrec, message);
            PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Failed, message);
        }



        private void HandleMrecLoadFailed(string message)
        {
            if (IsShowingForType(BannerAdTypes.MREC))
            {
                // IronSource refresh failed — MREC is still displaying, no retry needed.
                // Log diagnostics only so the hub tracks the refresh failure.
                IncrementLoadFailedForType(BannerAdTypes.MREC, message);
                PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Failed, $"MREC refresh load failed by IronSource — MREC still showing: {message}");
                StopBannerRetry(BannerAdTypes.MREC);
                return;
            }

            // Also ignore failure if ad is already ready
            if (IsReadyForType(BannerAdTypes.MREC))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(GetDiagnosticKey(BannerAdTypes.MREC), DiagnosticStateAVNPlugin.Failed, $"HandleMrecLoadFailed ignored while ready: {message} — assuming late callback");
                return;
            }


            SetReadyForType(BannerAdTypes.MREC, false);
            SetLoadingForType(BannerAdTypes.MREC, false);
            IncrementLoadFailedForType(BannerAdTypes.MREC, message);
            StartBannerRetry(BannerAdTypes.MREC);
            PublishStatusForType(BannerAdTypes.MREC, DiagnosticStateAVNPlugin.Failed, message);
        }

        private void StartBannerRetry(BannerAdTypes type)
        {
            if (!bannerPersistLoadCalls)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(GetDiagnosticKey(type), DiagnosticStateAVNPlugin.Failed, $"Not starting retry for {type} because persistent retries are disabled");
                SetLoadingForType(type, true);
                return;
            }
            if (type == BannerAdTypes.MREC)
            {
                if (_mrecRetryCoroutine != null) { StopCoroutine(_mrecRetryCoroutine); _mrecRetryCoroutine = null; }
                _mrecRetryCoroutine = StartCoroutine(PersistentBannerRetry(type, _mrecRetryAttempt));
            }
            else
            {
                if (_bannerRetryCoroutine != null) { StopCoroutine(_bannerRetryCoroutine); _bannerRetryCoroutine = null; }
                _bannerRetryCoroutine = StartCoroutine(PersistentBannerRetry(type, _bannerRetryAttempt));
            }
        }

        private void StopBannerRetry(BannerAdTypes type)
        {
            if (type == BannerAdTypes.MREC)
            {
                if (_mrecRetryCoroutine == null) return;
                StopCoroutine(_mrecRetryCoroutine);
                _mrecRetryCoroutine = null;
                _mrecRetryAttempt = 0;
            }
            else
            {
                if (_bannerRetryCoroutine == null) return;
                StopCoroutine(_bannerRetryCoroutine);
                _bannerRetryCoroutine = null;
                _bannerRetryAttempt = 0;
            }
        }

        private IEnumerator PersistentBannerRetry(BannerAdTypes type, int startAttempt)
        {
            var delays = new[] { 5f, 10f, 15f };
            var attemptIndex = startAttempt;
            while (true)
            {
                var delay = attemptIndex < delays.Length ? delays[attemptIndex++] : 15f;

                // Update the attempt counter in the field
                if (type == BannerAdTypes.MREC)
                    _mrecRetryAttempt = attemptIndex;
                else
                    _bannerRetryAttempt = attemptIndex;

                yield return new WaitForSeconds(delay);

                if (IsLoadingForType(type) || IsReadyForType(type) || IsShowingForType(type))
                    yield break;

                LoadBanner(type);
            }
        }
        #endregion
    }
    public enum BannerAdStatus
    {
        NotLoaded,
        Loading,
        Loaded,
        Showing
    }
}
#endif