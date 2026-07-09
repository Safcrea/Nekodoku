#if USE_AVNADS_PLUGIN
using System;
using System.Collections;
using System.Collections.Generic;

using AVN.AdsPlugin.Services;
using AVN.AdsPlugin.Testing;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AVN.AdsPlugin.Controllers
{
    public sealed class InterAdController : MonoBehaviour
    {
        public sealed class InterSlotDebugState
        {
            public int SlotIndex;
            public string AdUnitId;
            public bool IsConfigured;
            public bool IsReady;
            public bool IsQueued;
            public bool IsLoading;
            public bool IsCurrentShown;
        }

        #region Enums
        public enum InterstitialShowMode
        {
            ShowDefault,
            ShowIterateSequence,
            ShowDefaultWithOneBackfill,
            ShowDefaultWithTwoBackfill,
            ShowThreeTierInterstitial,
        }

        public enum InterFlowState
        {
            NotLoaded,
            Loading,
            Loaded,
            ShowCalled,
            Showing
        }
        #endregion

        #region Fields

        [FoldoutGroup("Show Mode")]
        [SerializeField] private InterstitialShowMode interstitialShowMode = InterstitialShowMode.ShowDefault;
        [FoldoutGroup("Show Mode")]
        [ReadOnly]
        [SerializeField] private int defaultInterIdIndex = 0;

        [FoldoutGroup("Settings")]
        [SerializeField] private bool useAdDelay = true;

        [FoldoutGroup("Settings")]
        [ShowIf("useAdDelay")]
        [SerializeField] public float InterAdDelay = 30f;
        [FoldoutGroup("Settings")]
        [SerializeField] private bool resetInterTimeOnReward = false;
        [FoldoutGroup("Enable Condition")]
        [SerializeField] private int enableInterFromLevel = 3;
        [FoldoutGroup("Enable Condition")]
        [SerializeField] private bool useDelayBeforeFirstAd = false;
        [FoldoutGroup("Enable Condition")]
        [ShowIf(nameof(useDelayBeforeFirstAd))]
        [SerializeField] private float delayBeforeFirstAdSeconds = 30f;

        [FoldoutGroup("Ad Break Panel")]
        [SerializeField] private GameObject adBreakPanel;
        [FoldoutGroup("Ad Break Panel")]
        [SerializeField] private float adBreakDuration = 2f;
        [FoldoutGroup("Ad Break Panel")]
        [SerializeField] private bool showMREConAdBreak = false;

        [FoldoutGroup("Test Settings")]
        [SerializeField] private bool sendAdDelayEvent = false;

        private string[] interstitialAdUnitIds = Array.Empty<string>();
        private IAdsProvider _provider;
        private static float _interAdDelayLastShownTime = -999f;
        private float _interFirstAdDelayLastShownTime = -1f;
        private bool hasShownInterstitialThisSession;
        private readonly Dictionary<int, InterFlowState> slotStates = new Dictionary<int, InterFlowState>();
        private readonly Queue<int> primaryLoadQueue = new Queue<int>();
        private readonly Queue<int> backfillLoadQueue = new Queue<int>();
        private readonly HashSet<int> primaryQueuedSlots = new HashSet<int>();
        private readonly HashSet<int> backfillQueuedSlots = new HashSet<int>();
        private readonly Dictionary<int, int> slotLoadFailures = new Dictionary<int, int>();

        private Coroutine pendingAdBreakCoroutine;
        private int pendingAdBreakSlot = -1;
        private string pendingAdBreakMessage = string.Empty;
        private bool mrecShownByAdBreak;

        private InterFlowState flowState = InterFlowState.NotLoaded;
        private int primaryLoadingSlot = -1;
        private int backfillLoadingSlot = -1;
        private int currentShownSlot = -1;
        private int iterateCurrentIndex = -1;

        // Three-tier waterfall: index (0-2) into interstitialAdUnitIds[] currently being attempted
        private int _threeTierCurrentIdIndex = 0;
        private const int ThreeTierMaxIds = 3;
        // Tracks which ID index was actually used at show time, for analytics reporting
        private int _threeTierAnalyticsSlot = -1;

        private string showErrorCachedMessage;
        private bool _pendingIsRewarded;

        // Watchdog: recovers from SDK missed close callbacks
        private Coroutine _showWatchdog;
        private const int ShowWatchdogTimeoutSeconds = 3;

        /// <summary>
        /// Raised when the watchdog detects the SDK failed to fire OnInterstitialSlotClosed.
        /// RewardAdController subscribes to clear waitingInterFallback.
        /// </summary>
        public static event Action<int, string> OnInterstitialForceRecovered;
        #endregion

        #region Initialization
        public void Initialize(IAdsProvider provider)
        {
            _provider = provider;
            ResetInterAdDelaysLastShownTime();
        }

        public void SetInterstitialIds(string[] ids)
        {
            interstitialAdUnitIds = ids ?? Array.Empty<string>();
            for (var i = 0; i < interstitialAdUnitIds.Length; i++)
            {
                interstitialAdUnitIds[i] = interstitialAdUnitIds[i] == null ? string.Empty : interstitialAdUnitIds[i].Trim();
            }

            slotStates.Clear();
            primaryLoadQueue.Clear();
            backfillLoadQueue.Clear();
            primaryQueuedSlots.Clear();
            backfillQueuedSlots.Clear();
            slotLoadFailures.Clear();
            primaryLoadingSlot = -1;
            backfillLoadingSlot = -1;
            currentShownSlot = -1;
            iterateCurrentIndex = GetFirstValidSlotIndex();
            _threeTierCurrentIdIndex = 0;
            flowState = InterFlowState.NotLoaded;
        }
        #endregion

        #region Lifecycle
        private void OnEnable()
        {
            AdEvents.OnInterstitialSlotLoaded += HandleLoaded;
            AdEvents.OnInterstitialSlotShown += HandleShown;
            AdEvents.OnBeforeInterstitialShown += HandleBeforeShow;
            AdEvents.OnInterstitialSlotClosed += HandleClosed;
            AdEvents.OnInterstitialSlotLoadFailed += HandleLoadFailed;
            AdEvents.OnInterstitialSlotFailToShow += HandleShowFailed;
            AdEvents.OnRewardEarned += HandleRewardEarned;
        }

        private void OnDisable()
        {
            AdEvents.OnInterstitialSlotLoaded -= HandleLoaded;
            AdEvents.OnInterstitialSlotShown -= HandleShown;
            AdEvents.OnBeforeInterstitialShown -= HandleBeforeShow;
            AdEvents.OnInterstitialSlotClosed -= HandleClosed;
            AdEvents.OnInterstitialSlotLoadFailed -= HandleLoadFailed;
            AdEvents.OnInterstitialSlotFailToShow -= HandleShowFailed;
            AdEvents.OnRewardEarned -= HandleRewardEarned;

            ResetPendingAdBreakState();
        }
        #endregion

        #region Public Methods
        public bool TryShow(Func<bool> canShowPredicate, Func<bool> canUseFirstAdDelay, bool isRewarded, bool showAdBreakPanel)
        {
            if (_provider == null || !AVNPluginConstants.AdsEnabledMaster || !AVNPluginConstants.EnableInterAd)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "TryShow blocked: config/_provider/ads disabled");
                return false;
            }

            // A show is already in flight — never start another.
            if (flowState == InterFlowState.Showing || flowState == InterFlowState.ShowCalled)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "TryShow rejected: interstitial already showing (current state: " + flowState + ")");
                return false;
            }

            // A load is already running — reject the show but DON'T enqueue another load.
            // This is what prevents the duplicate LoadInterstitial calls; the in-flight load
            // will flip us to Loaded (or NotLoaded on failure) on its own.
            if (flowState == InterFlowState.Loading)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle, "TryShow rejected: load already in progress");
                return false;
            }

            // Not loaded (e.g. auto-load retries were exhausted) — kick off ONE fresh load so a
            // later TryShow can succeed, then reject this call. Load()'s own guards stop it from
            // stacking a second load when one is already in flight.
            if (flowState == InterFlowState.NotLoaded)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "TryShow rejected: not loaded, restarting load queue");
                Load();
                return false;
            }

            // Past this point flowState == Loaded: fall through to the show-mode switch below.
            if (!_provider.IsSdkInitialized)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "TryShow blocked: SDK not initialized");
                return false;
            }

            if (canShowPredicate != null && !canShowPredicate.Invoke() && !isRewarded)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "CanShow False blocked display");
                return false;
            }

            if ((Time.realtimeSinceStartup - _interAdDelayLastShownTime < InterAdDelay) && !isRewarded && useAdDelay)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "Cooldown active Remaining Time: " + (InterAdDelay - (Time.realtimeSinceStartup - _interAdDelayLastShownTime)));
                return false;
            }

            if (canUseFirstAdDelay != null && canUseFirstAdDelay.Invoke() && !isRewarded && ShouldBlockByDelayBeforeFirstAd(out var remainingDelay))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "Delay before first interstitial active Remaining Time: " + remainingDelay);
                return false;
            }

            // Show AdBreakPanel only for non-rewarded calls, after a ready slot is already resolved.
            if (showAdBreakPanel && !isRewarded && adBreakPanel != null)
            {
                if (!TryResolveReadySlotForCurrentMode(out int readySlot, out string readyMessage))
                {
                    flowState = ComputeFlowState();
                    DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdInterstitial, "No interstitial slot ready");
                    FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(
                        isRewarded ? AdType.NO_INTERSTITIAL_REWARD : AdType.NO_INTERSTITIAL,
                        readySlot,
                        "No interstitial slot ready");
                    return false;
                }

                if (pendingAdBreakCoroutine != null)
                {
                    ResetPendingAdBreakState();
                }

                _pendingIsRewarded = isRewarded;
                pendingAdBreakSlot = readySlot;
                pendingAdBreakMessage = readyMessage;
                pendingAdBreakCoroutine = StartCoroutine(ShowAdBreakPanelThenAd());
                return true;
            }

            _pendingIsRewarded = isRewarded;
            flowState = InterFlowState.ShowCalled;

            bool showResult;
            switch (interstitialShowMode)
            {
                case InterstitialShowMode.ShowIterateSequence:
                    showResult = TryShowIterateMode(isRewarded);
                    break;
                case InterstitialShowMode.ShowDefaultWithOneBackfill:
                    showResult = TryShowBackfillMode(isRewarded);
                    break;
                case InterstitialShowMode.ShowDefaultWithTwoBackfill:
                    showResult = TryShowTwoBackfillMode(isRewarded);
                    break;
                case InterstitialShowMode.ShowThreeTierInterstitial:
                    showResult = TryShowThreeTierMode(isRewarded);
                    break;
                case InterstitialShowMode.ShowDefault:
                default:
                    showResult = TryShowDefaultMode(isRewarded);
                    break;
            }

            if (!showResult)
            {
                flowState = ComputeFlowState();
                DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdInterstitial, "No interstitial slot ready");
                FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(
                    _pendingIsRewarded ? AdType.NO_INTERSTITIAL_REWARD : AdType.NO_INTERSTITIAL,
                    -1,
                    "No interstitial slot ready");
            }
            return showResult;
        }

        private IEnumerator ShowAdBreakPanelThenAd()
        {
            if (showMREConAdBreak)
            {
                AVNPlugin.DTInstance?.ShowBannerAd(BannerAdTypes.MREC);
                mrecShownByAdBreak = true;
            }

            adBreakPanel.SetActive(true);
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle, $"Ad Break Panel shown for {adBreakDuration} seconds");

            yield return new WaitForSecondsRealtime(adBreakDuration);

            adBreakPanel.SetActive(false);
            pendingAdBreakCoroutine = null;

            if (mrecShownByAdBreak)
            {
                AVNPlugin.DTInstance?.HideBannerAd(BannerAdTypes.MREC);
                mrecShownByAdBreak = false;
            }

            flowState = InterFlowState.ShowCalled;

            bool showResult = TryShowResolvedSlotWithoutReadinessChecks(
                pendingAdBreakSlot,
                pendingAdBreakMessage,
                _pendingIsRewarded
            );

            pendingAdBreakSlot = -1;
            pendingAdBreakMessage = string.Empty;

            if (!showResult)
            {
                flowState = ComputeFlowState();
                DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(DiagnosticKeysAVNPlugin.AdInterstitial, "Failed to dispatch interstitial show after ad break panel");
                FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(
                    _pendingIsRewarded ? AdType.NO_INTERSTITIAL_REWARD : AdType.NO_INTERSTITIAL,
                    -1,
                    "Failed to dispatch interstitial show after ad break panel");
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "Ad Break Panel: Failed to dispatch show call after panel timeout");
            }
        }

        public void Load()
        {
            if (_provider == null || !AVNPluginConstants.AdsEnabledMaster || !AVNPluginConstants.EnableInterAd)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "Load blocked: config/_provider/ads disabled");
                return;
            }

            if (!_provider.IsSdkInitialized)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, "Load blocked: SDK not initialized");
                return;
            }

            if (flowState == InterFlowState.Showing || flowState == InterFlowState.ShowCalled)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Showing, "Load ignored: interstitial currently showing");
                return;
            }

            if (flowState == InterFlowState.Loading || flowState == InterFlowState.Loaded)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle, $"Load ignored: state is {flowState}");
                return;
            }

            var handlesLoadDirectly = false;
            switch (interstitialShowMode)
            {
                case InterstitialShowMode.ShowIterateSequence:
                    EnqueueLoadForIterateMode();
                    break;
                case InterstitialShowMode.ShowDefaultWithOneBackfill:
                    EnqueueLoadForBackfillMode();
                    break;
                case InterstitialShowMode.ShowDefaultWithTwoBackfill:
                    EnqueueLoadForTwoBackfillMode();
                    break;
                case InterstitialShowMode.ShowThreeTierInterstitial:
                    EnqueueLoadForThreeTierMode();
                    handlesLoadDirectly = true;
                    break;
                case InterstitialShowMode.ShowDefault:
                default:
                    EnqueueLoadForDefaultMode();
                    break;
            }

            if (!handlesLoadDirectly)
            {
                ProcessLoadQueues();
            }
            else
            {
                flowState = ComputeFlowState();
            }
        }
        public string GetShowModeName()
        {
            return interstitialShowMode.ToString();
        }

        public void SetShowMode(InterstitialShowMode mode)
        {
            interstitialShowMode = mode;
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle, $"InterstitialShowMode updated to {mode}");
#pragma warning disable CS0618 // Type or member is obsolete
            AVNPlugin.PluginInstance.SendCustomGameEvent("INTER_SHOW_MODE", new Dictionary<string, string> { { "MODE", mode.ToString() } }, true, false);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public int EnableInterFromLevel
        {
            get => enableInterFromLevel;
            set => enableInterFromLevel = Mathf.Max(0, value);
        }

        public void SetUseAdDelay(bool canUseDelay) => useAdDelay = canUseDelay;

        public bool UseDelayBeforeFirstAd
        {
            get => useDelayBeforeFirstAd;
            set => useDelayBeforeFirstAd = value;
        }

        public float DelayBeforeFirstAdSeconds
        {
            get => delayBeforeFirstAdSeconds;
            set => delayBeforeFirstAdSeconds = Mathf.Max(0f, value);
        }

        public void ResetInterAdDelaysLastShownTime()
        {
            _interFirstAdDelayLastShownTime = Time.realtimeSinceStartup;
            _interAdDelayLastShownTime = Time.realtimeSinceStartup;
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle, "Delay before first interstitial start time reset");
        }
        public int GetCurrentShownSlot()
        {
            return currentShownSlot;
        }

        public int GetLoadingSlot()
        {
            return primaryLoadingSlot >= 0 ? primaryLoadingSlot : backfillLoadingSlot;
        }

        public bool IsLoadingInProgress()
        {
            return primaryLoadingSlot >= 0 || backfillLoadingSlot >= 0 || primaryLoadQueue.Count > 0 || backfillLoadQueue.Count > 0;
        }

        public bool IsShowAttemptInProgress()
        {
            return flowState == InterFlowState.Showing || flowState == InterFlowState.ShowCalled;
        }

        public int GetConfiguredSlotCount()
        {
            return interstitialAdUnitIds?.Length ?? 0;
        }

        public List<InterSlotDebugState> GetSlotDebugStates()
        {
            var result = new List<InterSlotDebugState>();
            if (interstitialAdUnitIds == null)
            {
                return result;
            }

            for (var i = 0; i < interstitialAdUnitIds.Length; i++)
            {
                result.Add(new InterSlotDebugState
                {
                    SlotIndex = i,
                    AdUnitId = interstitialAdUnitIds[i],
                    IsConfigured = HasValidSlot(i),
                    IsReady = IsSlotReady(i),
                    IsQueued = primaryQueuedSlots.Contains(i) || backfillQueuedSlots.Contains(i),
                    IsLoading = slotStates.TryGetValue(i, out var state) && state == InterFlowState.Loading,
                    IsCurrentShown = currentShownSlot == i
                });
            }

            return result;
        }
        public bool IsCurrentInterLoaded()
        {
            return currentShownSlot >= 0 && IsSlotReady(currentShownSlot) && _provider.IsInterstitialReady(currentShownSlot);
        }

        public bool HasReadyInterstitial()
        {
            if (_provider == null)
            {
                return false;
            }

            switch (interstitialShowMode)
            {
                case InterstitialShowMode.ShowIterateSequence:
                    return IsSlotProviderReady(GetIterateSlotIndex());
                case InterstitialShowMode.ShowDefaultWithOneBackfill:
                    return IsSlotProviderReady(0) || IsSlotProviderReady(1);
                case InterstitialShowMode.ShowDefaultWithTwoBackfill:
                    return IsSlotProviderReady(0) || IsSlotProviderReady(1) || IsSlotProviderReady(2);
                case InterstitialShowMode.ShowThreeTierInterstitial:
                    return IsSlotProviderReady(0);
                case InterstitialShowMode.ShowDefault:
                default:
                    return IsSlotProviderReady(0);
            }
        }
        #endregion

        #region Event Callbacks
        private void HandleLoaded(int slotIndex, string message)
        {
            slotStates[slotIndex] = InterFlowState.Loaded;
            DiagnosticsHubAVNPlugin.IncrementAdsLoaded(GetSlotDiagnosticKey(slotIndex));
            slotLoadFailures[slotIndex] = 0;
            if (primaryLoadingSlot == slotIndex)
            {
                primaryLoadingSlot = -1;
            }
            else if (backfillLoadingSlot == slotIndex)
            {
                backfillLoadingSlot = -1;
            }

            flowState = ComputeFlowState();

            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Loaded, message, slotIndex);

            if (interstitialShowMode == InterstitialShowMode.ShowDefaultWithOneBackfill)
            {
                EnqueueLoadForBackfillMode();
            }
            else if (interstitialShowMode == InterstitialShowMode.ShowDefaultWithTwoBackfill)
            {
                EnqueueLoadForTwoBackfillMode();
            }
            showErrorCachedMessage = string.Empty;
            ProcessLoadQueues();
        }

        private void HandleShown(int slotIndex, string message)
        {
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Showed, message, slotIndex);
        }
        private void HandleBeforeShow(int slotIndex, bool isRewarded)
        {
            slotStates[slotIndex] = InterFlowState.Showing;
            currentShownSlot = slotIndex;
            flowState = InterFlowState.Showing;
            hasShownInterstitialThisSession = true;

            DiagnosticsHubAVNPlugin.IncrementAdsDisplayed(GetSlotDiagnosticKey(slotIndex));
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Showing, isRewarded ? "Rewarded" : "Non-rewarded", slotIndex);
            AVNPluginConstants.InterCount++;
            var analyticsSlot = (interstitialShowMode == InterstitialShowMode.ShowThreeTierInterstitial && _threeTierAnalyticsSlot >= 0)
                ? _threeTierAnalyticsSlot
                : slotIndex;
            FinzAnalysisManager.Instance?.InterShownAdAnalysis(isRewarded ? AdType.LP_INTERSTITIAL_REWARDED : AdType.INTERSTITIAL, analyticsSlot);
            // Start watchdog: if SDK drops the close callback, auto-recover after timeout
            if (_showWatchdog != null) StopCoroutine(_showWatchdog);
            _showWatchdog = StartCoroutine(ShowWatchdogCoroutine(slotIndex));
        }

        private void HandleClosed(int slotIndex, string message)
        {
            // Cancel watchdog — normal SDK close fired, no forced recovery needed
            if (_showWatchdog != null) { StopCoroutine(_showWatchdog); _showWatchdog = null; }

            // Guard against duplicate close callbacks (e.g. watchdog forced-close fires first,
            // then the real SDK OnAdClosed arrives moments later for the same slot).
            if (currentShownSlot != slotIndex)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Info,
                    $"HandleClosed ignored: slot {slotIndex} is not the current shown slot ({currentShownSlot})", slotIndex);
                return;
            }

            //* Send event to track if user returned after watching an ad
            AVNPlugin.DTInstance.SendUserInterStatus(UserReturnStatus.RETURNED_FROM_AD, FinzAnalysisManager.instance?.CurrentAdNetworkForAnalysis);
            FinzAnalysisManager.instance.CurrentAdNetworkForAnalysis = string.Empty;

            slotStates[slotIndex] = InterFlowState.NotLoaded;
            slotLoadFailures[slotIndex] = 0;
            currentShownSlot = -1;
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Closed, message, slotIndex);
            //* resetting interb ad delay
            _interAdDelayLastShownTime = Time.realtimeSinceStartup;

            switch (interstitialShowMode)
            {
                case InterstitialShowMode.ShowIterateSequence:
                    {
                        iterateCurrentIndex = GetNextValidSlotIndex(slotIndex);
                        EnqueueLoadForIterateMode();
                        break;
                    }
                case InterstitialShowMode.ShowDefaultWithOneBackfill:
                    if (IsPrimarySlot(slotIndex))
                    {
                        EnqueuePrimarySlotForLoad(slotIndex);
                    }
                    else
                    {
                        EnqueueBackfillSlotForLoad(slotIndex);
                    }
                    EnqueueLoadForBackfillMode();
                    break;
                case InterstitialShowMode.ShowDefaultWithTwoBackfill:
                    if (IsPrimarySlot(slotIndex))
                    {
                        EnqueuePrimarySlotForLoad(slotIndex);
                    }
                    else
                    {
                        EnqueueBackfillSlotForLoad(slotIndex);
                    }
                    EnqueueLoadForTwoBackfillMode();
                    break;
                case InterstitialShowMode.ShowThreeTierInterstitial:
                    // After each show, reset waterfall to ID[0] and reload
                    EnqueueLoadForThreeTierMode();
                    break;
                case InterstitialShowMode.ShowDefault:
                default:
                    EnqueueLoadForDefaultMode();
                    break;
            }

            flowState = ComputeFlowState();
            ProcessLoadQueues();
        }
        private void HandleRewardEarned(string message)
        {
            //Reset Inter Time On Reward Earned
            if (resetInterTimeOnReward)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Info, "Resetting interstitial timer due to reward earned");
                _interAdDelayLastShownTime = Time.realtimeSinceStartup;
            }
            else
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Info, "Reward earned, but interstitial timer not reset due to config");
            }
        }
        #endregion

        private bool ShouldBlockByDelayBeforeFirstAd(out float remainingDelay)
        {
            remainingDelay = 0f;

            if (!useDelayBeforeFirstAd || hasShownInterstitialThisSession)
            {
                return false;
            }

            if (_interFirstAdDelayLastShownTime < 0f)
            {
                ResetInterAdDelaysLastShownTime();
            }

            var elapsed = Time.realtimeSinceStartup - _interFirstAdDelayLastShownTime;
            remainingDelay = Mathf.Max(0f, delayBeforeFirstAdSeconds - elapsed);
            return remainingDelay > 0f;
        }

        #region Event Callbacks
        private void HandleLoadFailed(int slotIndex, string message)
        {
            slotStates[slotIndex] = InterFlowState.NotLoaded;
            Debug.LogWarning($"Interstitial load failed (slot {slotIndex}): {message}");
            DiagnosticsHubAVNPlugin.IncrementLoadCallsFailed(GetSlotDiagnosticKey(slotIndex), message);
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, message, slotIndex);

            // Clear the loading slot before any retry so queues can be processed
            if (primaryLoadingSlot == slotIndex) primaryLoadingSlot = -1;
            else if (backfillLoadingSlot == slotIndex) backfillLoadingSlot = -1;

            showErrorCachedMessage = message;

            // Three-tier mode: advance to the next ID instead of retrying the same one
            if (interstitialShowMode == InterstitialShowMode.ShowThreeTierInterstitial && slotIndex == 0)
            {
                HandleThreeTierLoadFailed(slotIndex, message);
                flowState = ComputeFlowState();
                return;
            }

            // Only retry once: if failures < 2, enqueue for retry; otherwise give up
            var failureCount = slotLoadFailures.TryGetValue(slotIndex, out var count) ? count : 0;
            failureCount++;
            slotLoadFailures[slotIndex] = failureCount;

            if (failureCount < 2)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Loading, $"Load failed, retrying (attempt {failureCount + 1})", slotIndex);
                EnqueueAndProcessRetry(slotIndex);
            }
            else
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, $"Load retries exhausted after {failureCount} attempts", slotIndex);
            }

            flowState = ComputeFlowState();
            ProcessLoadQueues();
        }

        private void HandleShowFailed(int slotIndex, string message)
        {
            // Cancel watchdog — show failed before ad displayed, no stuck state
            if (_showWatchdog != null) { StopCoroutine(_showWatchdog); _showWatchdog = null; }

            slotStates[slotIndex] = InterFlowState.NotLoaded;
            currentShownSlot = -1;
            Debug.LogWarning($"Interstitial show failed (slot {slotIndex}): {message}");

            DiagnosticsHubAVNPlugin.IncrementShowCallsFailed(GetSlotDiagnosticKey(slotIndex), message);
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, message, slotIndex);
            FinzAnalysisManager.Instance?.InterNotShownAdAnalysis(_pendingIsRewarded ? AdType.NO_INTERSTITIAL_REWARD : AdType.NO_INTERSTITIAL, slotIndex, message);

            flowState = ComputeFlowState();
        }
        #endregion

        #region Show & Load Logic
        private bool TryShowDefaultMode(bool isRewarded)
        {
            const int defaultSlot = 0;
            if (TryShowSlot(defaultSlot, "Showing default slot 0", isRewarded))
            {
                return true;
            }

            // Not ready; report failure (same path as IS SDK show failure) then enqueue a load.
            // Note: no OnAdClosed follows for a never-shown ad, so we enqueue reload here directly.
            slotLoadFailures[defaultSlot] = 0;
            EnqueuePrimarySlotForLoad(defaultSlot);
            ProcessLoadQueues();
            return false;
        }

        private bool TryShowIterateMode(bool isRewarded)
        {
            var slot = GetIterateSlotIndex();

            if (TryShowSlot(slot, $"Showing iterate slot {slot}", isRewarded))
            {
                return true;
            }

            // Not ready; report failure then enqueue a load.
            slotLoadFailures[slot] = 0;
            EnqueuePrimarySlotForLoad(slot);
            ProcessLoadQueues();
            return false;
        }

        private bool TryShowBackfillMode(bool isRewarded)
        {
            const int primarySlot = 0;
            const int secondarySlot = 1;

            if (TryShowSlot(primarySlot, "Showing primary slot 0", isRewarded))
            {
                return true;
            }

            if (TryShowSlot(secondarySlot, "Showing backfill fallback slot 1", isRewarded))
            {
                return true;
            }

            // Neither slot ready; report failure then enqueue load of primary (secondary enqueued when primary loads).
            slotLoadFailures[primarySlot] = 0;
            slotLoadFailures[secondarySlot] = 0;
            EnqueuePrimarySlotForLoad(primarySlot);
            EnqueueBackfillSlotForLoad(secondarySlot);
            ProcessLoadQueues();
            return false;
        }

        private bool TryShowTwoBackfillMode(bool isRewarded)
        {
            const int primarySlot = 0;
            const int backfillSlot1 = 1;
            const int backfillSlot2 = 2;

            if (TryShowSlot(primarySlot, "Showing primary slot 0", isRewarded))
            {
                return true;
            }

            if (TryShowSlot(backfillSlot1, "Showing two-backfill slot 1", isRewarded))
            {
                return true;
            }

            if (TryShowSlot(backfillSlot2, "Showing two-backfill slot 2", isRewarded))
            {
                return true;
            }

            // No slot ready; enqueue primary — backfill slots chain-load as primary loads.
            slotLoadFailures[primarySlot] = 0;
            slotLoadFailures[backfillSlot1] = 0;
            slotLoadFailures[backfillSlot2] = 0;
            EnqueuePrimarySlotForLoad(primarySlot);
            EnqueueBackfillSlotForLoad(backfillSlot1);
            EnqueueBackfillSlotForLoad(backfillSlot2);
            ProcessLoadQueues();
            return false;
        }

        private bool TryShowSlot(int slotIndex, string message, bool isRewarded)
        {
            if (!HasValidSlot(slotIndex))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, $"Slot {slotIndex} is not configured", slotIndex);
                return false;
            }

            var slotState = slotStates.TryGetValue(slotIndex, out var state) ? state : InterFlowState.NotLoaded;
            if (!IsSlotProviderReady(slotIndex))
            {
                if (slotState == InterFlowState.Loaded)
                {
                    slotStates[slotIndex] = InterFlowState.NotLoaded;
                }
                DiagnosticsHubAVNPlugin.PublishStatus(GetSlotDiagnosticKey(slotIndex), DiagnosticStateAVNPlugin.Failed, $"Slot {(HasValidSlot(slotIndex) ? "valid" : "invalid")}: '{interstitialAdUnitIds[slotIndex]}'");
                DiagnosticsHubAVNPlugin.PublishStatus(GetSlotDiagnosticKey(slotIndex), DiagnosticStateAVNPlugin.Failed, $"Slot Ready State In Provider: {IsSlotProviderReady(slotIndex)}");

                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed, $"Slot {slotIndex} not ready in provider (state: {slotState})", slotIndex);
                return false;
            }

            DiagnosticsHubAVNPlugin.IncrementShowCallSent(GetSlotDiagnosticKey(slotIndex));

            currentShownSlot = slotIndex;
            slotStates[slotIndex] = InterFlowState.Showing;
            flowState = InterFlowState.Showing;

            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Showing, message, slotIndex);

            if (sendAdDelayEvent)
            {
                AVNPlugin.PluginInstance.SendCustomGameEvent("InterAdInterval_" + InterAdDelay, null, true, false);
                AVNPlugin.PluginInstance.SendCustomGameEvent("InterAdIntervalForFirstAd_" + delayBeforeFirstAdSeconds, null, true, false);
            }
            _provider.ShowInterstitial(slotIndex, isRewarded);
            return true;
        }

        private void ResetPendingAdBreakState()
        {
            if (pendingAdBreakCoroutine != null)
            {
                StopCoroutine(pendingAdBreakCoroutine);
                pendingAdBreakCoroutine = null;
            }

            pendingAdBreakSlot = -1;
            pendingAdBreakMessage = string.Empty;

            if (adBreakPanel != null)
            {
                adBreakPanel.SetActive(false);
            }

            if (mrecShownByAdBreak)
            {
                AVNPlugin.DTInstance?.HideBannerAd(BannerAdTypes.MREC);
                mrecShownByAdBreak = false;
            }
        }

        private bool TryResolveReadySlotForCurrentMode(out int slotIndex, out string message)
        {
            slotIndex = -1;
            message = string.Empty;

            switch (interstitialShowMode)
            {
                case InterstitialShowMode.ShowIterateSequence:
                    {
                        int iterateSlot = GetIterateSlotIndex();
                        return TryResolveReadySlot(iterateSlot, $"Showing iterate slot {iterateSlot}", out slotIndex, out message);
                    }
                case InterstitialShowMode.ShowDefaultWithOneBackfill:
                    if (TryResolveReadySlot(0, "Showing primary slot 0", out slotIndex, out message))
                        return true;
                    return TryResolveReadySlot(1, "Showing backfill fallback slot 1", out slotIndex, out message);
                case InterstitialShowMode.ShowDefaultWithTwoBackfill:
                    if (TryResolveReadySlot(0, "Showing primary slot 0", out slotIndex, out message))
                        return true;
                    if (TryResolveReadySlot(1, "Showing two-backfill slot 1", out slotIndex, out message))
                        return true;
                    return TryResolveReadySlot(2, "Showing two-backfill slot 2", out slotIndex, out message);
                case InterstitialShowMode.ShowThreeTierInterstitial:
                    return TryResolveReadySlot(0, "Three-tier: showing slot 0", out slotIndex, out message);
                case InterstitialShowMode.ShowDefault:
                default:
                    return TryResolveReadySlot(0, "Showing default slot 0", out slotIndex, out message);
            }
        }

        private bool TryResolveReadySlot(int candidateSlot, string candidateMessage, out int slotIndex, out string message)
        {
            slotIndex = -1;
            message = string.Empty;

            if (!HasValidSlot(candidateSlot))
                return false;

            if (!IsSlotProviderReady(candidateSlot))
                return false;

            slotIndex = candidateSlot;
            message = candidateMessage;
            return true;
        }

        private bool TryShowResolvedSlotWithoutReadinessChecks(int slotIndex, string message, bool isRewarded)
        {
            if (_provider == null || !HasValidSlot(slotIndex))
                return false;

            DiagnosticsHubAVNPlugin.IncrementShowCallSent(GetSlotDiagnosticKey(slotIndex));

            currentShownSlot = slotIndex;
            slotStates[slotIndex] = InterFlowState.Showing;
            flowState = InterFlowState.Showing;

            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Showing, message, slotIndex);
            _provider.ShowInterstitial(slotIndex, isRewarded);
            return true;
        }

        private void EnqueueLoadForDefaultMode()
        {
            EnqueuePrimarySlotForLoad(0);
        }

        private void EnqueueLoadForIterateMode()
        {
            var slot = GetIterateSlotIndex();
            EnqueuePrimarySlotForLoad(slot);
        }

        private void EnqueueLoadForBackfillMode()
        {
            EnqueuePrimarySlotForLoad(0);
            EnqueueBackfillSlotForLoad(1);
        }

        private void EnqueueLoadForTwoBackfillMode()
        {
            EnqueuePrimarySlotForLoad(0);
            EnqueueBackfillSlotForLoad(1);
            EnqueueBackfillSlotForLoad(2);
        }

        // ── Three-tier waterfall ──────────────────────────────────────────────────
        // Uses a single provider slot (slot 0) and tries up to ThreeTierMaxIds IDs
        // in sequence (interstitialAdUnitIds[0] → [1] → [2]).  After every show
        // the waterfall resets to ID index 0.

        private bool TryShowThreeTierMode(bool isRewarded)
        {
            const int slot = 0;
            _threeTierAnalyticsSlot = _threeTierCurrentIdIndex;
            if (TryShowSlot(slot, "Three-tier: showing slot 0", isRewarded))
                return true;

            if (primaryLoadingSlot == slot || (slotStates.TryGetValue(slot, out var currentState) && currentState == InterFlowState.Loading))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle,
                    "Three-tier: slot 0 is already loading, skipping duplicate load request", slot);
                return false;
            }

            // Not ready — restart waterfall from the first ID
            _threeTierCurrentIdIndex = 0;
            slotLoadFailures[slot] = 0;
            LoadThreeTierSlot();
            return false;
        }

        private void EnqueueLoadForThreeTierMode()
        {
            // Always restart from the first ID on a fresh load request
            _threeTierCurrentIdIndex = 0;
            LoadThreeTierSlot();
        }

        /// <summary>
        /// Directly initiates a load for provider slot 0 using the ID at
        /// <see cref="_threeTierCurrentIdIndex"/>, bypassing the generic load queue.
        /// </summary>
        private void LoadThreeTierSlot()
        {
            const int slot = 0;

            if (primaryLoadingSlot == slot || (slotStates.TryGetValue(slot, out var currentState) && currentState == InterFlowState.Loading))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Idle,
                    "Three-tier: slot 0 load already in progress", slot);
                return;
            }

            if (interstitialAdUnitIds == null || _threeTierCurrentIdIndex >= interstitialAdUnitIds.Length)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed,
                    $"Three-tier: ID index {_threeTierCurrentIdIndex} is out of range");
                return;
            }

            var adUnitId = interstitialAdUnitIds[_threeTierCurrentIdIndex];
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed,
                    $"Three-tier: ID at index {_threeTierCurrentIdIndex} is empty");
                return;
            }

            // Claim the primary loading slot directly (no queue needed)
            primaryQueuedSlots.Remove(slot);
            primaryLoadingSlot = slot;
            slotStates[slot] = InterFlowState.Loading;
            flowState = InterFlowState.Loading;

            DiagnosticsHubAVNPlugin.IncrementLoadCallSent(GetSlotDiagnosticKey(slot));
            _provider.LoadInterstitial(slot, adUnitId);
            PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Loading,
                $"Three-tier: loading slot 0 with ID index {_threeTierCurrentIdIndex} ({adUnitId})", slot);
        }

        /// <summary>
        /// Called by <see cref="HandleLoadFailed"/> when in three-tier mode.
        /// Advances to the next ID and retries, or gives up when all IDs are exhausted.
        /// </summary>
        private void HandleThreeTierLoadFailed(int slotIndex, string message)
        {
            _threeTierCurrentIdIndex++;

            if (_threeTierCurrentIdIndex < ThreeTierMaxIds && HasValidSlot(_threeTierCurrentIdIndex))
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Loading,
                    $"Three-tier: ID index {_threeTierCurrentIdIndex - 1} failed, trying index {_threeTierCurrentIdIndex}", slotIndex);
                LoadThreeTierSlot();
            }
            else
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed,
                    $"Three-tier: all IDs exhausted ({_threeTierCurrentIdIndex} tried)", slotIndex);
                _threeTierCurrentIdIndex = 0; // reset so next Load() starts fresh
            }
        }
        // ─────────────────────────────────────────────────────────────────────────

        private void EnqueuePrimarySlotForLoad(int slotIndex)
        {
            if (!HasValidSlot(slotIndex))
            {
                return;
            }

            if (primaryLoadingSlot == slotIndex || IsSlotQueued(slotIndex) || IsSlotReady(slotIndex))
            {
                return;
            }

            primaryQueuedSlots.Add(slotIndex);
            primaryLoadQueue.Enqueue(slotIndex);
        }

        private void EnqueueBackfillSlotForLoad(int slotIndex)
        {
            if (!HasValidSlot(slotIndex) || IsPrimarySlot(slotIndex))
            {
                return;
            }

            if (backfillLoadingSlot == slotIndex || IsSlotQueued(slotIndex) || IsSlotReady(slotIndex))
            {
                return;
            }

            backfillQueuedSlots.Add(slotIndex);
            backfillLoadQueue.Enqueue(slotIndex);
        }

        private void ProcessLoadQueues()
        {
            if (_provider == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Failed, "ProcessLoadQueues blocked: no provider");
                return;
            }

            ProcessLoadQueue(primaryLoadQueue, primaryQueuedSlots, true);
            ProcessLoadQueue(backfillLoadQueue, backfillQueuedSlots, false);

            flowState = ComputeFlowState();
        }

        private void ProcessLoadQueue(Queue<int> queue, HashSet<int> queuedSlots, bool isPrimaryQueue)
        {
            if (isPrimaryQueue)
            {
                if (primaryLoadingSlot >= 0)
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Failed, "ProcessLoadQueue blocked: primary slot already loading");
                    return;
                }
            }
            else if (backfillLoadingSlot >= 0)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Failed, "ProcessLoadQueue blocked: backfill slot already loading");
                return;
            }

            while (queue.Count > 0)
            {
                var slotIndex = queue.Dequeue();
                queuedSlots.Remove(slotIndex);
                if (!HasValidSlot(slotIndex))
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, DiagnosticStateAVNPlugin.Failed, $"ProcessLoadQueue blocked: invalid slot {slotIndex}");
                    continue;
                }

                if (IsSlotReady(slotIndex))
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(GetSlotDiagnosticKey(slotIndex), DiagnosticStateAVNPlugin.Failed, $"ProcessLoadQueue: slot {slotIndex} already loaded");
                    continue;
                }

                if (isPrimaryQueue)
                {
                    primaryLoadingSlot = slotIndex;
                }
                else
                {
                    backfillLoadingSlot = slotIndex;
                }

                slotStates[slotIndex] = InterFlowState.Loading;
                flowState = InterFlowState.Loading;

                var adUnitId = interstitialAdUnitIds[slotIndex];
                DiagnosticsHubAVNPlugin.IncrementLoadCallSent(GetSlotDiagnosticKey(slotIndex));
                _provider.LoadInterstitial(slotIndex, adUnitId);
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Loading, $"Loading interstitial slot {slotIndex}", slotIndex);
                return;
            }
        }

        private InterFlowState ComputeFlowState()
        {
            if (currentShownSlot >= 0)
            {
                return InterFlowState.Showing;
            }

            // ShowCalled is set eagerly in TryShow; it should never persist past the show attempt
            // so we don't return it here — falling through recomputes the real state.

            if (primaryLoadingSlot >= 0 || backfillLoadingSlot >= 0 || primaryLoadQueue.Count > 0 || backfillLoadQueue.Count > 0)
            {
                return InterFlowState.Loading;
            }

            foreach (var kv in slotStates)
            {
                if (kv.Value == InterFlowState.Loaded)
                {
                    return InterFlowState.Loaded;
                }
            }

            return InterFlowState.NotLoaded;
        }

        private int GetIterateSlotIndex()
        {
            if (!HasValidSlot(iterateCurrentIndex))
            {
                iterateCurrentIndex = GetFirstValidSlotIndex();
            }

            return iterateCurrentIndex;
        }

        private int GetFirstValidSlotIndex()
        {
            var start = GetEffectiveDefaultIndex();
            if (HasValidSlot(start))
            {
                return start;
            }

            if (interstitialAdUnitIds == null)
            {
                return -1;
            }

            for (var i = 0; i < interstitialAdUnitIds.Length; i++)
            {
                if (HasValidSlot(i))
                {
                    return i;
                }
            }

            return -1;
        }
        #endregion

        #region Utilities
        public InterFlowState GetCurrentInterAdState()
        {
            return flowState;
        }
        private int GetEffectiveDefaultIndex()
        {
            if (interstitialAdUnitIds == null || interstitialAdUnitIds.Length == 0)
            {
                return -1;
            }

            if (defaultInterIdIndex < 0 || defaultInterIdIndex >= interstitialAdUnitIds.Length)
            {
                return 0;
            }

            return defaultInterIdIndex;
        }

        private int GetNextValidSlotIndex(int currentSlot)
        {
            if (interstitialAdUnitIds == null || interstitialAdUnitIds.Length == 0)
            {
                return -1;
            }

            for (var offset = 1; offset <= interstitialAdUnitIds.Length; offset++)
            {
                var index = (currentSlot + offset) % interstitialAdUnitIds.Length;
                if (HasValidSlot(index))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool HasValidSlot(int slotIndex)
        {
            if (interstitialAdUnitIds == null || slotIndex < 0 || slotIndex >= interstitialAdUnitIds.Length)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(interstitialAdUnitIds[slotIndex]);
        }

        private bool IsSlotReady(int slotIndex)
        {
            return slotStates.TryGetValue(slotIndex, out var state) && state == InterFlowState.Loaded;
        }

        private bool IsSlotProviderReady(int slotIndex)
        {
            return HasValidSlot(slotIndex) && _provider != null && _provider.IsInterstitialReady(slotIndex);
        }

        private static string GetSlotDiagnosticKey(int slotIndex)
        {
            return DiagnosticKeysAVNPlugin.AdInterstitial + "." + slotIndex;
        }

        private static string GetRetryKey(int slotIndex)
        {
            return "inter.slot." + slotIndex;
        }

        private bool IsPrimarySlot(int slotIndex)
        {
            return slotIndex == 0;
        }

        private bool IsSlotQueued(int slotIndex)
        {
            return primaryQueuedSlots.Contains(slotIndex) || backfillQueuedSlots.Contains(slotIndex);
        }

        private IEnumerator ShowWatchdogCoroutine(int watchedSlot)
        {
            yield return new WaitForSeconds(ShowWatchdogTimeoutSeconds);
            if (currentShownSlot == watchedSlot && flowState == InterFlowState.Showing)
            {
                PublishDiagnosticStatus(DiagnosticStateAVNPlugin.Failed,
                    $"Watchdog: slot {watchedSlot} stuck Showing after {ShowWatchdogTimeoutSeconds}s — SDK dropped close callback. Forcing recovery.",
                    watchedSlot);
                OnInterstitialForceRecovered?.Invoke(watchedSlot, "Watchdog: forced close (SDK missed close callback)");
                HandleClosed(watchedSlot, "Watchdog: forced close (SDK missed close callback)");
            }
            _showWatchdog = null;
        }

        private void EnqueueAndProcessRetry(int slotIndex)
        {
            if (IsPrimarySlot(slotIndex))
            {
                EnqueuePrimarySlotForLoad(slotIndex);
            }
            else
            {
                EnqueueBackfillSlotForLoad(slotIndex);
            }

            ProcessLoadQueues();
        }

        private static void PublishDiagnosticStatus(DiagnosticStateAVNPlugin state, string message, int slotIndex = -1)
        {
            if (slotIndex >= 0)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(GetSlotDiagnosticKey(slotIndex), state, message);
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.AdInterstitial, state, message);
        }
        #endregion
    }
}
#endif