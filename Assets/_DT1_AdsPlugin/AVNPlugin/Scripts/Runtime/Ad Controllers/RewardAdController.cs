#if USE_AVNADS_PLUGIN
using System;
using System.Collections;
using System.Runtime.CompilerServices;

using AVN.AdsPlugin.Services;
using AVN.AdsPlugin.Testing;
using UnityEngine;

namespace AVN.AdsPlugin.Controllers
{
    public sealed class RewardAdController : MonoBehaviour
    {
        #region Nested Types
        private sealed class RewardRequest
        {
            public Action OnRewarded;
            public Action OnCancelled;
            public Action OnNoAd;
        }

        private enum RewardFlowState
        {
            NotLoaded,
            Loading,
            Loaded,
            Showing,
            Showed,
            Closing
        }
        #endregion

        #region Fields
        [SerializeField] private bool enableInterBackfill = true;

        private IAdsProvider _provider;
        private RewardRequest activeRequest;
        private bool waitingInterFallback;
        private bool interFallbackShown;
        private bool rewardGrantedForActiveShow;
        private bool deferredLoadRequested;
        private RewardFlowState state = RewardFlowState.NotLoaded;
        private Coroutine rewardShowWatchdog;

        private const int RewardShowWatchdogTimeoutSeconds = 3;

        #endregion

        #region Initialization
        public void Initialize(IAdsProvider provider)
        {
            _provider = provider;
        }
        #endregion

        #region Lifecycle
        private void OnEnable()
        {
            AdEvents.OnRewardLoaded += HandleLoaded;
            AdEvents.OnRewardShown += HandleShown;
            AdEvents.OnRewardEarned += HandleRewarded;
            AdEvents.OnBeforeRewardedShown += HandleBeforeShown;
            AdEvents.OnRewardClosed += HandleClosed;
            AdEvents.OnRewardFailToShow += HandleRewardShowFailure;
            AdEvents.OnRewardLoadFailed += HandleRewardLoadFailed;


            AdEvents.OnInterstitialSlotShown += HandleInterstitialShown;
            AdEvents.OnInterstitialSlotClosed += HandleInterstitialClosed;
            AdEvents.OnInterstitialSlotFailToShow += HandleInterstitialFailed;
            InterAdController.OnInterstitialForceRecovered += HandleInterstitialClosed;
        }

        private void OnDisable()
        {
            CancelRewardShowWatchdog();
            AdEvents.OnRewardLoaded -= HandleLoaded;
            AdEvents.OnRewardShown -= HandleShown;
            AdEvents.OnRewardEarned -= HandleRewarded;
            AdEvents.OnBeforeRewardedShown -= HandleBeforeShown;
            AdEvents.OnRewardClosed -= HandleClosed;
            AdEvents.OnRewardFailToShow -= HandleRewardShowFailure;
            AdEvents.OnRewardLoadFailed -= HandleRewardLoadFailed;
            AdEvents.OnInterstitialSlotShown -= HandleInterstitialShown;
            AdEvents.OnInterstitialSlotClosed -= HandleInterstitialClosed;
            AdEvents.OnInterstitialSlotFailToShow -= HandleInterstitialFailed;
            InterAdController.OnInterstitialForceRecovered -= HandleInterstitialClosed;
        }
        #endregion

        #region Public Methods
        public bool TryShow(Action onRewarded, Action onCancelled, Action onNoAd)
        {
            if (!AVNPluginConstants.AdsEnabledMaster || !AVNPluginConstants.EnableRewardedAd)
            {

                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "Rewarded blocked by config");
                onNoAd?.Invoke();
                return false;
            }

            if (activeRequest != null || state == RewardFlowState.Showing || state == RewardFlowState.Showed || state == RewardFlowState.Closing || waitingInterFallback)
            {
                onNoAd?.Invoke();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "Rewarded request ignored due to active show/fallback");
                return false;
            }

            if (state == RewardFlowState.Loaded && !IsRewardedProviderReady())
            {
                SetState(RewardFlowState.NotLoaded, "Provider reported rewarded not ready during TryShow");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AdRewarded,
                    DiagnosticStateAVNPlugin.Failed,
                    "Rewarded marked loaded but provider is not ready; resetting state");
            }

            if (state == RewardFlowState.NotLoaded || state == RewardFlowState.Loading)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "Rewarded not loaded");
                activeRequest = new RewardRequest
                {
                    OnRewarded = onRewarded,
                    OnNoAd = onNoAd
                };
                FinzAnalysisManager.Instance?.NoRewardAdAnalysis("Rewarded not loaded");
                if (TryShowInterstitialFallback("Rewarded not loaded"))
                {
                    return true;
                }

                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "TryShow failed: no rewarded and fallback unavailable");
                DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdRewarded, "Rewarded not loaded");
                var wasNotLoaded = state == RewardFlowState.NotLoaded;
                HandleNoAd();
                if (wasNotLoaded) Load(); // kick off a load if nothing was in flight
                return false;
            }

            activeRequest = new RewardRequest
            {
                OnRewarded = onRewarded,
                OnCancelled = onCancelled,
                OnNoAd = onNoAd
            };

            rewardGrantedForActiveShow = false;
            DebugLogger.AVNLog(
                $"Reward show requested | state={state}, providerReady={IsRewardedProviderReady()}, activeRequest={activeRequest != null}, waitingFallback={waitingInterFallback}",
                false);
            DiagnosticsHubAVNPlugin.IncrementShowCallSent(DiagnosticKeysAVNPlugin.AdRewarded);
            _provider?.ShowRewarded();
            return true;
        }

        public void Load()
        {
            if (state == RewardFlowState.Showing || state == RewardFlowState.Showed || state == RewardFlowState.Closing || waitingInterFallback)
            {
                deferredLoadRequested = true;
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Load deferred until rewarded flow closes");
                return;
            }

            if (state == RewardFlowState.Loading)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Load ignored: rewarded already loading");
                return;
            }
            if (state == RewardFlowState.Loaded)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loaded, "Load ignored: rewarded already loaded");
                return;
            }

            DiagnosticsHubAVNPlugin.IncrementLoadCallSent(DiagnosticKeysAVNPlugin.AdRewarded);
            _provider?.LoadRewarded();
            SetState(RewardFlowState.Loading, "Load requested");
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Loading rewarded");
        }

        public bool IsReady()
        {
            return state == RewardFlowState.Loaded && IsRewardedProviderReady();
        }

        public bool HasActiveRequest()
        {
            return activeRequest != null;
        }

        public bool IsWaitingForFallback()
        {
            return waitingInterFallback;
        }

        public bool FallbackWasShown()
        {
            return interFallbackShown;
        }
        #endregion

        #region Event Callbacks
        private void HandleLoaded(string _)
        {
            // Guard against stale callbacks from a previous ad instance
            // (LoadRewardedAd creates a new LevelPlayRewardedAd each time;
            //  a late OnAdLoaded from the old instance must be ignored)
            if (state != RewardFlowState.Loading)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "Reward loaded callback but State not Loading instead it is " + state);
                return;
            }

            SetState(RewardFlowState.Loaded, "Reward loaded callback");
            DiagnosticsHubAVNPlugin.IncrementAdsLoaded(DiagnosticKeysAVNPlugin.AdRewarded);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loaded, "Rewarded loaded");
        }

        private void HandleShown(string _)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Showed, "Rewarded shown");
            SetState(RewardFlowState.Showed, "Reward shown callback");
        }
        private void HandleBeforeShown()
        {
            rewardGrantedForActiveShow = false;
            SetState(RewardFlowState.Showing, "OnBeforeRewardedShown callback");
            CancelRewardShowWatchdog();
            rewardShowWatchdog = StartCoroutine(RewardShowWatchdogCoroutine());
            DiagnosticsHubAVNPlugin.IncrementAdsDisplayed(DiagnosticKeysAVNPlugin.AdRewarded);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Showing, "Rewarded Show Called");
            FinzAnalysisManager.Instance?.RewardShownAdAnalysis();
        }



        private void HandleClosed(string _)
        {
            CancelRewardShowWatchdog();
            SetState(RewardFlowState.Closing, "Reward closed callback");
            if (!waitingInterFallback)
            {
                if (activeRequest != null)
                {
                    if (rewardGrantedForActiveShow)
                    {
                        activeRequest.OnRewarded?.Invoke();
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Sent, "Reward granted on close");
                    }
                    else
                    {
                        activeRequest.OnCancelled?.Invoke();
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Sent, "Reward cancelled by user");
                    }
                }

                activeRequest = null;
                rewardGrantedForActiveShow = false;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Closed, "Rewarded closed");
            SetState(RewardFlowState.NotLoaded, "Reward flow closed");
            TriggerPostDismissLoad();
        }

        private void HandleRewardShowFailure(string message)
        {
            CancelRewardShowWatchdog();
            SetState(RewardFlowState.Closing, $"Reward show failed: {message}");
            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdRewarded, message);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, message);

            if (TryShowInterstitialFallback("Rewarded show failed"))
                return; // inter fallback started; HandleInterstitialClosed/Failed will clean up

            HandleNoAd();
            SetState(RewardFlowState.NotLoaded, "Reward show failed without fallback");
            TriggerPostDismissLoad();
        }

        private void HandleRewardLoadFailed(string message)
        {
            // Also ignore failure if ad is already ready
            if (state == RewardFlowState.Loaded)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, $"Load Fail Was Called While Ad Was Already Loaded: Ignoring. Message: {message}");
                return;
            }
            SetState(RewardFlowState.NotLoaded, $"Reward load failed: {message}");
            DiagnosticsHubAVNPlugin.IncrementLoadCallsFailed(DiagnosticKeysAVNPlugin.AdRewarded, message);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, message);
        }

        private void HandleRewarded(string _)
        {
            if (activeRequest == null || rewardGrantedForActiveShow)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "Rewarded callback received with no active request or reward already granted: Ignoring");
                return;
            }

            rewardGrantedForActiveShow = true;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Sent, "Reward trigger set; will grant on close");
        }

        private void HandleNoAd()
        {
            activeRequest?.OnNoAd?.Invoke();
            activeRequest = null;
            rewardGrantedForActiveShow = false;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "No Rewarded Ad Available");
        }
        #endregion

        #region Fallback Logic
        private bool TryShowInterstitialFallback(string reason)
        {
            if (!enableInterBackfill || activeRequest == null || waitingInterFallback)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded,
                    DiagnosticStateAVNPlugin.Failed, "Fallback Inter Cannot Be Shown");
                return false;
            }

            waitingInterFallback = true;
            interFallbackShown = false;

            var shown = AVNPlugin.PluginInstance != null && AVNPlugin.PluginInstance.ShowInterAd(isRewarded: true);
            if (!shown)
            {
                waitingInterFallback = false;
                interFallbackShown = false;
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded,
                    DiagnosticStateAVNPlugin.Failed, "Fallback Inter failed to start");
                return false;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded,
                DiagnosticStateAVNPlugin.Loading, $"Fallback Inter Triggered: {reason}");
            return true;
        }

        private void HandleInterstitialShown(int _, string __)
        {
            if (!waitingInterFallback)
            {
                return;
            }

            interFallbackShown = true;
        }

        private void HandleInterstitialClosed(int _, string __)
        {
            if (!waitingInterFallback)
            {
                return;
            }

            if (interFallbackShown)
            {
                activeRequest?.OnRewarded?.Invoke();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Sent, "Reward granted on interstitial close");
            }
            else
            {
                activeRequest?.OnNoAd?.Invoke();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, "No Rewarded Ad Available on interstitial close");
            }

            waitingInterFallback = false;
            interFallbackShown = false;
            activeRequest = null;
            rewardGrantedForActiveShow = false;
            if (state != RewardFlowState.Loading)
                SetState(RewardFlowState.NotLoaded, "Interstitial fallback closed");
            TriggerPostDismissLoad();
        }

        private void HandleInterstitialFailed(int _, string message)
        {
            if (!waitingInterFallback)
            {
                return;
            }

            waitingInterFallback = false;
            interFallbackShown = false;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Failed, $"Fallback inter failed: {message}");
            activeRequest?.OnNoAd?.Invoke();
            activeRequest = null;
            rewardGrantedForActiveShow = false;
            if (state != RewardFlowState.Loading)
                SetState(RewardFlowState.NotLoaded, $"Interstitial fallback failed: {message}");
            TriggerPostDismissLoad();
        }

        private void SetState(RewardFlowState newState, string reason, [CallerMemberName] string caller = "")
        {
            var oldState = state;
            state = newState;

            DebugLogger.AVNLog(
                $"Reward state {(oldState == newState ? "unchanged" : "transition")} {oldState} -> {newState} via {caller}: {reason} | activeRequest={activeRequest != null}, waitingFallback={waitingInterFallback}, interFallbackShown={interFallbackShown}, rewardGranted={rewardGrantedForActiveShow}, deferredLoad={deferredLoadRequested}, providerReady={IsRewardedProviderReady()}",
                false);
        }

        private bool IsRewardedProviderReady()
        {
            return _provider != null && _provider.IsRewardedReady();
        }

        private void CancelRewardShowWatchdog()
        {
            if (rewardShowWatchdog == null)
            {
                return;
            }

            StopCoroutine(rewardShowWatchdog);
            rewardShowWatchdog = null;
        }

        private IEnumerator RewardShowWatchdogCoroutine()
        {
            yield return new WaitForSeconds(RewardShowWatchdogTimeoutSeconds);

            if (waitingInterFallback || activeRequest == null)
            {
                rewardShowWatchdog = null;
                yield break;
            }

            if (state == RewardFlowState.Showing || state == RewardFlowState.Showed)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AdRewarded,
                    DiagnosticStateAVNPlugin.Failed,
                    $"Watchdog: rewarded stuck in {state} after {RewardShowWatchdogTimeoutSeconds}s; forcing close");
                rewardShowWatchdog = null;
                HandleClosed("Watchdog: forced close (SDK missed close callback)");
                yield break;
            }

            rewardShowWatchdog = null;
        }

        private void TriggerPostDismissLoad()
        {
            if (state == RewardFlowState.Showing || state == RewardFlowState.Showed || state == RewardFlowState.Closing || waitingInterFallback)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Post-dismiss load deferred until current flow closes");
                deferredLoadRequested = true;
                return;
            }

            if (state == RewardFlowState.Loading || state == RewardFlowState.Loaded)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Post-dismiss load skipped: already loading");
                return;
            }

            deferredLoadRequested = false;
            DiagnosticsHubAVNPlugin.IncrementLoadCallSent(DiagnosticKeysAVNPlugin.AdRewarded);
            _provider?.LoadRewarded();
            SetState(RewardFlowState.Loading, "Post-dismiss reload requested");
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdRewarded, DiagnosticStateAVNPlugin.Loading, "Loading rewarded after dismiss");
        }
        #endregion
    }
}
#endif