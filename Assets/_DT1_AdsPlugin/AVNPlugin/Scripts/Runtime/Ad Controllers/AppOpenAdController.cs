#if USE_AVNADS_PLUGIN
using System;
using System.Collections;
using AVN.AdsPlugin.Services;
using AVN.AdsPlugin.Testing;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AVN.AdsPlugin.Controllers
{
    public sealed class AppOpenAdController : MonoBehaviour
    {
        #region State Machine
        private enum AppOpenAdState
        {
            NotLoaded,  // no ad ready, no load in flight
            Loading,    // load call sent, awaiting SDK callback
            Ready,      // ad loaded, eligible to show on next foreground resume
            Showing     // ad UI is currently visible
        }
        #endregion

        #region Fields
        [FoldoutGroup("Delays")]
        [SerializeField] private int reactivateAfterSeconds = 2;
        [FoldoutGroup("Delays")]
        [SerializeField] private int minSecondsBetweenAppOpenAds = 10;
        [FoldoutGroup("On Session Start Settings")]
        [SerializeField] private bool showAppOpenOnSessionStart = false;
        [FoldoutGroup("On Session Start Settings")]
        [SerializeField] private bool shouldSkipFirstSession = true;
        [FoldoutGroup("On Session Start Settings")]
        [SerializeField] private int loadingSceneIndex = 0;
        [FoldoutGroup("Show Modes")]
        [SerializeField] private AppOpenShowMode appOpenShowModeAndroid = AppOpenShowMode.Pause;
        [FoldoutGroup("Show Modes")]
        [SerializeField] private AppOpenShowMode appOpenShowModeiOS = AppOpenShowMode.Pause;
        [Tooltip("Build index of the scene in which the session-start AppOpen ad is allowed to show. Ad will not show if the active scene is different.")]
        private IAppOpenAdService _appOpenService;
        private AppOpenAdState _adState = AppOpenAdState.NotLoaded;
        private bool _suppressed;           // true while another full-screen ad is (or was recently) active
        private bool isFirstResumeHandled;
        private bool appWasInBackground;
        private bool pendingShowOnLoad;
        private bool _pendingSessionStartLoadRequest;
        private bool _capturedStartupState;
        private bool _wasFirstSessionAtStartup;
        private bool _sessionStartLoadHandled;
        private bool _firstSessionShowAllowed;  // false if shouldSkipFirstSession && first session
        private float _lastAppOpenShownRealtime = -99999f;
        private Coroutine _deactivateAdCoroutine;
        private string appOpenId1 = string.Empty;
        private string appOpenId2 = string.Empty;
        private string interShowErrorMessage = string.Empty;
        #endregion

        #region Initialization
        public void Initialize(IAppOpenAdService appOpenService)
        {
            _appOpenService = appOpenService;
            if (!_capturedStartupState)
            {
                _wasFirstSessionAtStartup = AVNPluginConstants.IsFirstSession;
                _capturedStartupState = true;
                // Determine if we should allow showing on first session
                _firstSessionShowAllowed = !shouldSkipFirstSession || !_wasFirstSessionAtStartup;
            }
            _appOpenService?.ConfigureAdIds(appOpenId1, appOpenId2);
            TryHandlePendingSessionStartLoadRequest();
        }

        public void SetAppOpenInterstitialId(string id)
        {
            SetAppOpenInterstitialIds(id, string.Empty);
        }

        public void SetAppOpenInterstitialIds(string primaryId, string backfillId)
        {
            appOpenId1 = NormalizeId(primaryId);
            appOpenId2 = NormalizeId(backfillId);
            _appOpenService?.ConfigureAdIds(appOpenId1, appOpenId2);
            _adState = AppOpenAdState.NotLoaded;
        }
        #endregion

        #region Lifecycle
        private void OnEnable()
        {
            AdEvents.OnAppOpenLoaded += HandleLoaded;
            AdEvents.OnAppOpenShown += HandleShown;
            AdEvents.OnAppOpenClosed += HandleClosed;
            AdEvents.OnAppOpenLoadFailed += HandleLoadFailed;
            AdEvents.OnAppOpenFailToShow += HandleShowFailed;
            AdEvents.OnBeforeAppOpenShown += HandleBeforeAppOpenShown;

            AdEvents.OnBeforeInterstitialShown += HandleBeforeInterShown;
            AdEvents.OnInterstitialSlotClosed += HandleInterSlotClosed;
            InterAdController.OnInterstitialForceRecovered += HandleInterSlotClosed;

            AdEvents.OnBeforeRewardedShown += HandleBeforeRewardedShown;
            AdEvents.OnRewardClosed += HandleRewardAdClosed;
        }

        private void OnDisable()
        {
            AdEvents.OnAppOpenLoaded -= HandleLoaded;
            AdEvents.OnAppOpenShown -= HandleShown;
            AdEvents.OnAppOpenClosed -= HandleClosed;
            AdEvents.OnAppOpenLoadFailed -= HandleLoadFailed;
            AdEvents.OnAppOpenFailToShow -= HandleShowFailed;

            AdEvents.OnBeforeInterstitialShown -= HandleBeforeInterShown;
            AdEvents.OnInterstitialSlotClosed -= HandleInterSlotClosed;
            InterAdController.OnInterstitialForceRecovered -= HandleInterSlotClosed;

            AdEvents.OnBeforeRewardedShown -= HandleBeforeRewardedShown;
            AdEvents.OnBeforeAppOpenShown -= HandleBeforeAppOpenShown;
            AdEvents.OnRewardClosed -= HandleRewardAdClosed;
        }
        #endregion

        #region Public Methods
        public bool ShouldLoadOnSessionStart => showAppOpenOnSessionStart;

        /// <summary>
        /// Extends the app-open ad suppression window to prevent ads from showing during critical UI flows
        /// (e.g., store overlays, rate-us dialogs, notification permission prompts).
        /// Uses Mathf.Max to ensure an existing longer suppression window is never shortened.
        /// </summary>
        public void Suppress(int reactivationDelay)
        {
            if (_deactivateAdCoroutine != null) StopCoroutine(_deactivateAdCoroutine);
            _suppressed = true;
            _deactivateAdCoroutine = StartCoroutine(DelayDeactivateAd(reactivationDelay));
        }

        public void LoadForSessionStart()
        {
            if (_sessionStartLoadHandled)
            {
                Load();
                return;
            }

            _sessionStartLoadHandled = true;
            var showOnLoad = showAppOpenOnSessionStart && _firstSessionShowAllowed;

            Load(showOnLoad);
        }

        public void RequestSessionStartLoad()
        {
            _pendingSessionStartLoadRequest = true;
            TryHandlePendingSessionStartLoadRequest();
        }

        private void TryHandlePendingSessionStartLoadRequest()
        {
            if (!_pendingSessionStartLoadRequest)
            {
                return;
            }

            if (_appOpenService == null || !_appOpenService.IsSdkInitialized)
            {
                return;
            }

            _pendingSessionStartLoadRequest = false;
            LoadForSessionStart();
        }
        #endregion

        #region Application Lifecycle
        private void OnApplicationPause(bool pauseStatus)
        {
            HandleLifecycleEvent(AppOpenShowMode.Pause, !pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            HandleLifecycleEvent(AppOpenShowMode.Focus, hasFocus);
        }


        private void HandleLifecycleEvent(AppOpenShowMode sourceMode, bool gainedFocus)
        {
            var showMode = GetPlatformShowMode();
            var shouldHandle = (showMode == AppOpenShowMode.Pause && sourceMode == AppOpenShowMode.Pause) ||
                               (showMode == AppOpenShowMode.Focus && sourceMode == AppOpenShowMode.Focus);
            if (!shouldHandle)
            {
                return;
            }

            HandleFocusChange(gainedFocus);
        }

        private void HandleFocusChange(bool gainedFocus)
        {
            if (!gainedFocus)
            {
                appWasInBackground = true;
                return;
            }

            TryHandleForegroundResume();
        }

        private void TryHandleForegroundResume()
        {
            if (!isFirstResumeHandled)
            {
                isFirstResumeHandled = true;
                appWasInBackground = false;
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Idle, "First foreground resume ignored");
                return;
            }

            if (!appWasInBackground)
            {
                return;
            }

            appWasInBackground = false;

            if (_suppressed)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Idle, "Foreground resume suppressed: full-screen ad active");
                return;
            }
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Loading, "Foreground resume: attempting app-open show");
            TryShow();
        }

        private AppOpenShowMode GetPlatformShowMode()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return appOpenShowModeAndroid;
                case RuntimePlatform.IPhonePlayer:
                    return appOpenShowModeiOS;
                default:
                    // Default to Android mapping for non-mobile/editor runs.
                    return appOpenShowModeAndroid;
            }
        }
        #endregion

        #region Show & Load Logic
        private bool TryShow()
        {
            if (_appOpenService == null || !_appOpenService.IsSdkInitialized || !AVNPluginConstants.AdsEnabledMaster || !AVNPluginConstants.EnableAppOpenAd)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, "TryShow blocked: config/app-open sdk check failed");
                return false;
            }

            if (_adState == AppOpenAdState.Showing)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Showing, "TryShow blocked: ad already showing");
                return false;
            }

            if (minSecondsBetweenAppOpenAds > 0)
            {
                var secondsSinceLastShown = Time.realtimeSinceStartup - _lastAppOpenShownRealtime;
                if (secondsSinceLastShown < minSecondsBetweenAppOpenAds)
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(
                        DiagnosticKeysAVNPlugin.AdAppOpen,
                        DiagnosticStateAVNPlugin.Idle,
                        $"TryShow blocked: cooldown active ({secondsSinceLastShown:F1}/{minSecondsBetweenAppOpenAds}s)");
                    return false;
                }
            }

            if (!_appOpenService.IsAnyAdReady)
            {
                if (_appOpenService.IsLoadInProgress)
                {
                    // Load already in flight — do not set pendingShowOnLoad here.
                    // pendingShowOnLoad is only set via LoadForSessionStart (startup path).
                    // Foreground-resume callers simply wait for the next resume once the ad is ready.
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Loading, "TryShow blocked: load in progress");
                    return false;
                }

                ReportAppOpenUnavailable("App-open not ready");
                // Load for the next opportunity but never auto-show from here.
                // showOnLoad is only for the session-start path (LoadForSessionStart).
                Load(false);
                return false;
            }

            DiagnosticsHubAVNPlugin.IncrementShowCallSent(DiagnosticKeysAVNPlugin.AdAppOpen);
            if (!_appOpenService.Show())
            {
                return false;
            }

            pendingShowOnLoad = false;
            _adState = AppOpenAdState.Showing;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Showing, "Showing app-open interstitial");
            return true;
        }

        public void Load(bool showOnLoad = false)
        {
            if (showOnLoad)
            {
                pendingShowOnLoad = true;
            }

            if (_appOpenService == null || !_appOpenService.IsSdkInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, "Load blocked: app-open sdk not initialized");
                return;
            }

            if (!AVNPluginConstants.AdsEnabledMaster || !AVNPluginConstants.EnableAppOpenAd)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, "Load blocked: app-open disabled");
                return;
            }

            if (string.IsNullOrWhiteSpace(appOpenId1) && string.IsNullOrWhiteSpace(appOpenId2))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, "Load blocked: both app-open ad unit ids are empty");
                return;
            }

            _adState = _appOpenService.IsAnyAdReady ? AppOpenAdState.Ready : AppOpenAdState.Loading;
            DiagnosticsHubAVNPlugin.IncrementLoadCallSent(DiagnosticKeysAVNPlugin.AdAppOpen);
            _appOpenService.Load();

            if (showOnLoad && !_suppressed && _appOpenService.IsAnyAdReady && IsInLoadingScene() && _firstSessionShowAllowed)
            {
                TryShow();
            }

            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.AdAppOpen,
                _appOpenService.IsAnyAdReady ? DiagnosticStateAVNPlugin.Loaded : DiagnosticStateAVNPlugin.Loading,
                showOnLoad ? "Loading app-open interstitial with show-on-load" : "Loading app-open interstitial");
        }
        #endregion

        #region Event Callbacks
        private void HandleLoaded(string message)
        {
            _adState = AppOpenAdState.Ready;
            interShowErrorMessage = string.Empty;
            DiagnosticsHubAVNPlugin.IncrementAdsLoaded(DiagnosticKeysAVNPlugin.AdAppOpen);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Loaded, message);

            if (!pendingShowOnLoad)
            {
                return;
            }

            var canAutoShow = !_suppressed &&
                AVNPluginConstants.AdsEnabledMaster &&
                AVNPluginConstants.EnableAppOpenAd &&
                IsInLoadingScene() &&
                _firstSessionShowAllowed;
            pendingShowOnLoad = false;
            if (canAutoShow)
            {
                TryShow();
            }
        }

        private void HandleLoadFailed(string message)
        {
            _adState = _appOpenService != null && _appOpenService.IsAnyAdReady
                ? AppOpenAdState.Ready
                : AppOpenAdState.NotLoaded;
            interShowErrorMessage = message;
            DiagnosticsHubAVNPlugin.IncrementLoadCallsFailed(DiagnosticKeysAVNPlugin.AdAppOpen, message);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, message);
        }

        private void HandleShown(string message)
        {
            _adState = AppOpenAdState.Showing;
            DiagnosticsHubAVNPlugin.IncrementAdsDisplayed(DiagnosticKeysAVNPlugin.AdAppOpen);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Showed, message);
        }

        private void HandleClosed(string message)
        {
            // Service auto-triggers Load() on close, so move to Loading rather than NotLoaded.
            _adState = AppOpenAdState.Loading;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Closed, message);
            // SuppressAppOpen() was called from HandleBeforeAppOpenShown — without starting
            // the reactivation coroutine here, _suppressed stays true permanently and blocks
            // every subsequent foreground resume.
            ReactivateAppOpen();
        }

        private void HandleShowFailed(string message)
        {
            // Service auto-triggers Load() on show failure, so move to Loading.
            _adState = AppOpenAdState.Loading;
            pendingShowOnLoad = false;
            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdAppOpen, message);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, message);
            FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(AdType.NO_APP_OPEN, 0, message);
        }

        private void HandleBeforeInterShown(int _, bool __) => SuppressAppOpen();
        private void HandleBeforeRewardedShown() => SuppressAppOpen();
        private void HandleBeforeAppOpenShown()
        {
            _adState = AppOpenAdState.Showing;
            _lastAppOpenShownRealtime = Time.realtimeSinceStartup;
            FinzAnalysisManager.Instance?.InterShownAdAnalysis(AdType.OPEN_AD);
            SuppressAppOpen();
        }

        public void SuppressAppOpen()
        {
            if (_deactivateAdCoroutine != null) StopCoroutine(_deactivateAdCoroutine);
            _suppressed = true;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Idle, "App-open suppressed: full-screen ad active");
        }

        private void HandleInterSlotClosed(int __, string _) => ReactivateAppOpen();

        private void HandleRewardAdClosed(string _) => ReactivateAppOpen();

        public void ReactivateAppOpen()
        {
            if (_deactivateAdCoroutine != null) StopCoroutine(_deactivateAdCoroutine);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Closed, $"re-enabling app-open in {reactivateAfterSeconds}s");
            _deactivateAdCoroutine = StartCoroutine(DelayDeactivateAd(reactivateAfterSeconds));
        }

        private IEnumerator DelayDeactivateAd(int waitForSeconds)
        {
            yield return new WaitForSeconds(waitForSeconds);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Idle, "App-open re-enabled after suppression window");
            _suppressed = false;
            _deactivateAdCoroutine = null;
        }

        private bool IsInLoadingScene() =>
            SceneManager.GetActiveScene().buildIndex == loadingSceneIndex;

        private static string NormalizeId(string id) =>
            string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        private static void ReportAppOpenUnavailable(string message)
        {
            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdAppOpen, message);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdAppOpen, DiagnosticStateAVNPlugin.Failed, message);
            FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(AdType.NO_APP_OPEN, 0, message);
        }
        #endregion
        public enum AppOpenShowMode
        {
            Focus,
            Pause
        }
    }
}
#endif