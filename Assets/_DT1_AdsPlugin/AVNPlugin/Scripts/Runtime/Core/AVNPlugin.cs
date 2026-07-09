#if USE_AVNADS_PLUGIN
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;

using AVN.AdsPlugin.Services;
using AVN.AdsPlugin.UserHandlers;
#if USE_IAP
using UnityEngine.Purchasing;
#endif
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif
namespace AVN.AdsPlugin.Controllers
{
    public sealed class AVNPlugin : MonoBehaviour
    {
        #region Singleton
        public static AVNPlugin PluginInstance { get; private set; }
        public static AVNPlugin DTInstance { get; private set; }
        public event Action LevelPlayInitializationSucceeded;
        public bool IsAdsProviderInitialized => (_provider != null && _provider.IsSdkInitialized) || providerInitSucceeded;

        #endregion

        #region Variables
        [SerializeField] private PluginInitMode initializeOnStart = PluginInitMode.StartByCMP;
        [Space]
        //------------------ Controllers ------------------//
        [FoldoutGroup("Controllers")]
        [SerializeField] private BannerAdController bannerAdController;
        [FoldoutGroup("Controllers")]
        [SerializeField] private InterAdController interAdController;
        [FoldoutGroup("Controllers")]
        [SerializeField] private RewardAdController rewardAdController;
        [FoldoutGroup("Controllers")]
        [SerializeField] private AppOpenAdController appOpenInterAdController;
        [FoldoutGroup("Controllers")]
        public GenericLoaderManager GenericLoaderManager;
        [FoldoutGroup("Controllers")]
        public ToastManager ToastManager;
        //------------------ Scene Loading ------------------//
        [Space]
        [FoldoutGroup("Scene Loading")]
        [SerializeField] private bool syncLoadingWithProvider = false;
        [FoldoutGroup("Scene Loading")]
        [SerializeField] private string nextSceneToLoad = string.Empty;
        [FoldoutGroup("Scene Loading")]
        [SerializeField] private int additionalDelayInLoadingNextScene = 0;
        [Space]
        //------------------ Ad Loading ------------------//
        [Space]
        [FoldoutGroup("Ad Loading")]
        [SerializeField] private bool autoLoadBanner = true;
        [FoldoutGroup("Ad Loading")]
        [SerializeField] private bool autoLoadMrec = true;
        [FoldoutGroup("Ad Loading")]
        [SerializeField] private bool autoLoadRewarded = true;
        [FoldoutGroup("Ad Loading")]
        [SerializeField] private bool autoLoadAppOpen = true;
        [FoldoutGroup("Ad Loading")]
        [SerializeField] private bool autoLoadInterstitial = true;
        //------------------ Ad IDs ------------------//
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private AVNAdIdsConfig adIdsConfig;

        [Space]

        //------------------ In-App Purchasing ------------------//
#if USE_IAP
        [FoldoutGroup("In-App Purchasing")]
        [SerializeField] private bool useInAppPurchasing = false;

        [FoldoutGroup("In-App Purchasing")]
        [ShowIf(nameof(useInAppPurchasing))]
        [SerializeField] private string removeAdsProductId = string.Empty;

        [FoldoutGroup("In-App Purchasing")]
        [ShowIf(nameof(useInAppPurchasing))]
        [SerializeField] private int maxRetryAttempts = 3;

        [FoldoutGroup("In-App Purchasing")]
        [ShowIf(nameof(useInAppPurchasing))]
        [SerializeField] private float retryDelaySeconds = 1f;

        [FoldoutGroup("In-App Purchasing")]
        [ShowIf(nameof(useInAppPurchasing))]
        [SerializeField] private List<InAppProduct> inAppProducts = new List<InAppProduct>();

        [FoldoutGroup("In-App Purchasing")]
        [ShowIf(nameof(useInAppPurchasing))]
        [SerializeField] private bool autoRestoreNonConsumablesOnInit = false;
#endif
        //------------------ Remote Config ------------------//
        [FoldoutGroup("Remote Config")]
        [SerializeField] private int remoteConfigRetryMaxPower = 5;

        //------------------ RateUs/Notification Condition ------------------//
        [FoldoutGroup("RateUs | Notification Condition")]
        [SerializeField] private int _showRateUsOnLevel = 4;
        [FoldoutGroup("RateUs | Notification Condition")]
        [SerializeField] private int _showNotificationOnLevel = 5;
        //------------------ Event Callbacks ------------------//
        [Space]
        [Tooltip("Fires when the Plugin Init is called")]
        [FoldoutGroup("Event Callbacks")]
        public UnityEvent OnPluginInitCalled;
        [Tooltip("Fires when the Ads SDK Provider Initializes")]
        [FoldoutGroup("Event Callbacks")]
        public UnityEvent OnPluginInitCompleted;

        public int EnableInterFromLevel
        {
            get => interAdController != null ? interAdController.EnableInterFromLevel : 3;
            set
            {
                if (interAdController != null)
                {
                    interAdController.EnableInterFromLevel = value;
                }
            }
        }

        public int ShowRateUsOnLevel { get => _showRateUsOnLevel; set => _showRateUsOnLevel = value; }
        public int ShowNotificationOnLevel { get => _showNotificationOnLevel; set => _showNotificationOnLevel = value; }

        private IAdsProvider _provider;
        private IAppOpenAdService _appOpenAdService;
#if USE_FIREBASE
        private FirebaseRemoteConfigService _remoteConfigService;
#endif
#if USE_BYTEBREW
        private ByteBrewService _byteBrewService;
#endif
        private AnalyticsEventService _gameEventService;
        private RateUsNotificationService _rateUsandNotificationService;
        private InAppPurchasingService _purchasingService;
#if USE_FIREBASE
        private FirebaseInitilizationManager _firebaseInitManager;
#endif
        private bool initialized;
        public static bool FirebaseInitCompleted;
        public static bool FirebaseInitSucceeded;
        private bool providerInitCompleted;
        private bool providerInitSucceeded;
        private bool sceneLoadTriggered;
        private bool waitingForInternetToLoadScene;
        private bool missingNextSceneLogged;
        private bool firstAdDelayStartInitialized;
        private int remoteConfigRetryAttempts;

        private const string RemoveAdsProductName = "RemoveAds";
        private string defaultBannerAdUnitId = string.Empty;
        private string defaultMrecAdUnitId = string.Empty;
        private string defaultRewardedAdUnitId = string.Empty;
        private string defaultAppOpenAdUnitId1 = string.Empty;
        private string defaultAppOpenAdUnitId2 = string.Empty;
        private string[] defaultInterAdUnitIds = Array.Empty<string>();
        private AVNAdIdsConfig runtimeAdIds;

        private const float FirebaseInitTimeoutSeconds = 20f;
        private const float ProviderInitTimeoutSeconds = 20f;

        private AVNHandlerSetup _remoteConfigHandler;
        private AVNHandlerSetup _purchaseHandler;
        private AVNHandlerSetup _adsHandler;


        public bool SyncLoadingWithProvider => syncLoadingWithProvider;
        public AVNAdIdsConfig ActiveAdIdsConfig => runtimeAdIds != null ? runtimeAdIds : adIdsConfig;

        #endregion

        #region Essentials
        private void Awake()
        {
            if (PluginInstance == null || DTInstance == null)
            {
                PluginInstance = this;
                DTInstance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (PluginInstance != this || DTInstance != this)
            {
                Debug.LogError("Multiple instances of Singleton Script Detected: " + gameObject.name);
                Destroy(gameObject);
            }

            PublishDiagnosticStatus("SDK.Firebase", "Unknown", "Waiting for initialization");
            PublishDiagnosticStatus("SDK.ByteBrew", "Unknown", "Waiting for initialization");
            PublishDiagnosticStatus("SDK.RemoteConfig", "Unknown", "Waiting for fetch");
            PublishDiagnosticStatus("SDK.AppsFlyer", "Unknown", "Waiting for initialization");
            PublishDiagnosticStatus("SDK.AdMob", "Unknown", "Waiting for initialization");
            PublishDiagnosticStatus("SDK.IronSource", "Unknown", "Waiting for initialization");
        }

        private void Start()
        {
            DebugLogger.AVNLog("Start invoked; applying runtime IDs");
            EnsureRuntimeAdIds();
            ApplyRuntimeAdIdOverrides();
            interAdController?.SetInterstitialIds(GetRuntimeInterstitialAdIds());
            appOpenInterAdController?.SetAppOpenInterstitialIds(GetRuntimeAppOpenAdId(), GetRuntimeAppOpenBackfillAdId());

            if (initializeOnStart == PluginInitMode.StartOnInit)
            {
                Initialize();
            }
        }

        private void OnDisable()
        {
#if USE_FIREBASE
            if (_firebaseInitManager != null)
            {
                _firebaseInitManager.OnFirebaseInitilizaSuccess -= HandleFirebaseReady;
                _firebaseInitManager.OnFirebaseInitilizaFailed -= HandleFirebaseFailed;
            }
            if (_remoteConfigService != null)
            {
                _remoteConfigService.OnConfigFetched -= HandleRemoteConfigFetched;
                _remoteConfigService.OnConfigFetchFailed -= HandleRemoteConfigFailed;
            }
#endif
#if USE_BYTEBREW
            if (_byteBrewService != null)
            {
                _byteBrewService.OnInitialized -= HandleByteBrewReady;
                _byteBrewService.OnInitializationFailed -= HandleByteBrewFailed;
            }
#endif

            if (_provider != null)
            {
                _provider.SDKInitializedSuccessfully -= HandleProviderInitializedSuccessfully;
                _provider.SDKInitializationFailed -= HandleProviderInitializationFailed;
            }
#if USE_IAP
            if (_purchasingService != null)
            {
                _purchasingService.OnPurchaseSuccessful -= HandlePurchaseSuccessful;
                _purchasingService.OnRestoreSuccessful -= HandleRestoreSuccessful;
            }
#endif
        }
        #region Initialization

        public void Initialize()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            VerifyAdsScripts();
            if (!firstAdDelayStartInitialized)
            {
                interAdController?.ResetInterAdDelaysLastShownTime();
                firstAdDelayStartInitialized = true;
            }
            ConstructDependencies();
            _remoteConfigHandler.SetupRemoteConfig();
            ApplyPersistedAdsState();
            CacheInspectorDefaultAdIds();
            ApplyCachedAdIdsOrDefaults();
#if USE_FIREBASE
            _remoteConfigService.ApplyCachedConfig();
#endif
            InitializeAdsDependencies();
            InitializeServiceDependencies();

            if (IsAdsProviderInitialized)
            {
                TryLoadNextSceneIfReady();
            }

            //* Updating First Session Value
            if (AVNPluginConstants.IsFirstSession)
            {
                AVNPluginConstants.IsFirstSession = false;
            }
            OnPluginInitCalled?.Invoke();
        }

        void InitializeAdsDependencies()
        {
            bannerAdController?.Initialize(_provider);
            interAdController?.Initialize(_provider);
            interAdController?.SetInterstitialIds(GetRuntimeInterstitialAdIds());
            rewardAdController?.Initialize(_provider);
            appOpenInterAdController?.Initialize(_appOpenAdService);
            appOpenInterAdController?.SetAppOpenInterstitialIds(GetRuntimeAppOpenAdId(), GetRuntimeAppOpenBackfillAdId());
        }
        void InitializeServiceDependencies()
        {
            if (initialized)
            {
                DebugLogger.AVNLog("Initialize ignored: already initialized");
                return;
            }

            initialized = true;
            DebugLogger.AVNLog("Initialize accepted: starting initialization sequence");
            PublishDiagnosticEvent("SDK", "Init", "AdsManager initialization sequence started");
#if USE_FIREBASE
            StartCoroutine(InitializeFirebaseInSequence());
#else
                StartCoroutine(RunServiceDependenciesInitializationSequence());
#endif

        }
        void ConstructDependencies()
        {
#if USE_MAX
            _provider ??= new MaxAdsProvider();
#elif USE_LEVELPLAY
            _provider ??= new LevelPlayAdsProvider();
#endif
#if USE_ADMOB
            _appOpenAdService ??= new AdMobAppOpenService();
#endif
#if USE_FIREBASE
            _remoteConfigService ??= new FirebaseRemoteConfigService();
            _firebaseInitManager ??= new FirebaseInitilizationManager();
            _remoteConfigHandler ??= new GameRemoteConfig();
#endif
#if USE_BYTEBREW
            _byteBrewService ??= new ByteBrewService();
#endif
            _gameEventService ??= new AnalyticsEventService();
            _rateUsandNotificationService ??= new RateUsNotificationService(this);
#if USE_IAP
            _purchaseHandler ??= new GameInApp();
#endif
            _adsHandler ??= new GameAdsCheck();
            RegisterProviderCallbacks();
            ToastManager?.Initialize();
            AssignCallbacks();
#if USE_IAP
            //* Purchasing Service is Conditional
            if (useInAppPurchasing)
            {
                _purchasingService ??= new InAppPurchasingService();
                _gameEventService ??= new AnalyticsEventService();
                _purchasingService.SetGameEventService(_gameEventService);
                _purchasingService.SetRetryConfig(maxRetryAttempts, retryDelaySeconds);
                _purchasingService.SetAutoRestoreNonConsumablesOnInit(autoRestoreNonConsumablesOnInit);

                // Register all inspector-configured products
                foreach (var product in inAppProducts)
                {
                    _purchasingService.RegisterProduct(product);
                }

                // Register hardcoded RemoveAds product LAST so callbacks and product type can't be overridden by inspector entries.
                if (!string.IsNullOrWhiteSpace(removeAdsProductId))
                {
                    _purchasingService.RegisterProduct(new InAppProduct
                    {
                        ProductName = RemoveAdsProductName,
                        ProductType = ProductType.NonConsumable,
                        ProductId = removeAdsProductId
                    });
                    _purchasingService.SetPurchasedCallback(RemoveAdsProductName, HandleRemoveAdsPurchased);
                    _purchasingService.SetRestoredCallback(RemoveAdsProductName, HandleRemoveAdsRestored);
                    _purchasingService.SetRefundedCallback(RemoveAdsProductName, HandleRemoveAdsRefunded);
                    DebugLogger.AVNLog($"RemoveAds product configured: {removeAdsProductId}");
                }

                _purchaseHandler.OnSetupCallbacks();
            }
#endif
        }

        private void RegisterProviderCallbacks()
        {
            if (_provider == null)
            {
                return;
            }

            _provider.SDKInitializedSuccessfully -= HandleProviderInitializedSuccessfully;
            _provider.SDKInitializationFailed -= HandleProviderInitializationFailed;
            _provider.SDKInitializedSuccessfully += HandleProviderInitializedSuccessfully;
            _provider.SDKInitializationFailed += HandleProviderInitializationFailed;
        }
        void AssignCallbacks()
        {
#if USE_FIREBASE
            if (_firebaseInitManager != null)
            {
                _firebaseInitManager.OnFirebaseInitilizaSuccess += HandleFirebaseReady;
                _firebaseInitManager.OnFirebaseInitilizaFailed += HandleFirebaseFailed;
            }

            if (_remoteConfigService != null)
            {
                _remoteConfigService.OnConfigFetched += HandleRemoteConfigFetched;
                _remoteConfigService.OnConfigFetchFailed += HandleRemoteConfigFailed;
            }
#endif
#if USE_BYTEBREW
            if (_byteBrewService != null)
            {
                _byteBrewService.OnInitialized += HandleByteBrewReady;
                _byteBrewService.OnInitializationFailed += HandleByteBrewFailed;
            }
#endif
#if USE_IAP
            if (_purchasingService != null)
            {
                _purchasingService.OnPurchaseSuccessful += HandlePurchaseSuccessful;
                _purchasingService.OnRestoreSuccessful += HandleRestoreSuccessful;
            }
#endif
        }
        IEnumerator LoadAdsCoroutine()
        {
            yield return new WaitForSeconds(3f);
            yield return LoadIfEnabled(autoLoadInterstitial, () => interAdController?.Load());
            yield return LoadIfEnabled(autoLoadRewarded, () => rewardAdController?.Load());
            var shouldLoadAppOpenOnSessionStart = autoLoadAppOpen || (appOpenInterAdController != null && appOpenInterAdController.ShouldLoadOnSessionStart);
            yield return LoadIfEnabled(shouldLoadAppOpenOnSessionStart, () => appOpenInterAdController?.LoadForSessionStart());
            yield return LoadIfEnabled(autoLoadMrec, () => bannerAdController?.LoadBanner(BannerAdTypes.MREC));
            //* using banner ad controllers variable to make sure that banner shows or not shows on Init load call
            bannerAdController?.UpdateShowBannerOnLoad(bannerAdController.showBannerOnInit);
            yield return LoadIfEnabled(autoLoadBanner, () => bannerAdController?.LoadBanner(BannerAdTypes.BANNER));
        }

        private IEnumerator LoadIfEnabled(bool shouldLoad, Action loadAction)
        {
            if (!shouldLoad)
            {
                yield break;
            }

            loadAction();
            yield return new WaitForSeconds(1f);
        }
        private IEnumerator RunServiceDependenciesInitializationSequence()
        {

            PublishDiagnosticStatus("SDK.AdsProvider", "Initializing", "Starting SDK");
            providerInitCompleted = false;
            providerInitSucceeded = false;
            _provider.Initialize(GetRuntimeSdkKey());
            var providerElapsed = 0f;
            while (!providerInitCompleted && providerElapsed < ProviderInitTimeoutSeconds)
            {
                providerElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!providerInitCompleted)
            {
                PublishDiagnosticStatus("SDK.AdsProvider", "Failed", "Ads provider initialization timed out");
            }
#if USE_ADMOB
            var adMobInitCompleted = false;
            var adMobInitSucceeded = false;

            PublishDiagnosticStatus("SDK.AdMob", "Initializing", "Initializing AdMob SDK");
            AdMobSdkInitializer.EnsureInitialized(success =>
            {
                adMobInitCompleted = true;
                adMobInitSucceeded = success;
            });

            while (!adMobInitCompleted)
            {
                yield return null;
            }

            PublishDiagnosticStatus(
                "SDK.AdMob",
                adMobInitSucceeded ? "Initialized" : "Failed",
                adMobInitSucceeded
                    ? "AdMob SDK initialized"
                    : string.IsNullOrWhiteSpace(AdMobSdkInitializer.LastErrorMessage)
                        ? "AdMob SDK initialization failed"
                        : AdMobSdkInitializer.LastErrorMessage);
#endif
#if USE_BYTEBREW
            PublishDiagnosticStatus("SDK.ByteBrew", "Initializing", "Initializing ByteBrew");
            _byteBrewService.Initialize();
            yield return null;
#endif
#if USE_FIREBASE
            PublishDiagnosticStatus("SDK.RemoteConfig", "Initializing", "Fetching remote config");
            _remoteConfigService.FetchRemoteConfig();
            yield return null;
#endif
#if USE_IAP
            PublishDiagnosticStatus("SDK.InAppsPurchaser", "Initializing", "Initializing In-App Purchasing");
            _purchasingService?.Initialize();
            yield return null;

            // Auto-restore non-consumables if enabled
            if (useInAppPurchasing && autoRestoreNonConsumablesOnInit && _purchasingService != null)
            {
                PublishDiagnosticStatus("SDK.InAppsPurchaser", "Initializing", "Auto-restoring non-consumable products");
                _purchasingService.CheckAndRestoreNonConsumables();
                yield return new WaitForSeconds(0.5f); // Wait for restore to complete
            }
#endif

            yield return new WaitForSeconds(1f); // Small delay to ensure all SDKs are fully initialized before loading ads
            OnPluginInitCompleted?.Invoke();
            StartCoroutine(LoadAdsCoroutine());
        }
#if USE_FIREBASE
        private IEnumerator InitializeFirebaseInSequence()
        {
            FirebaseInitCompleted = false;
            FirebaseInitSucceeded = false;
            PublishDiagnosticStatus("SDK.Firebase", "Initializing", "Initializing Firebase");


            if (_firebaseInitManager == null)
            {
                Debug.LogWarning("Firebase init manager not found. Continuing sequence.");
                PublishDiagnosticStatus("SDK.Firebase", "Failed", "Firebase init manager not found");
                StartCoroutine(RunServiceDependenciesInitializationSequence());
                yield break;
            }

            _firebaseInitManager.startFirebaseInitialization();

            var elapsed = 0f;
            while (!FirebaseInitCompleted && elapsed < FirebaseInitTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!FirebaseInitCompleted)
            {
                Debug.LogWarning("Firebase initialization timed out. Continuing sequence.");
                PublishDiagnosticStatus("SDK.Firebase", "Failed", "Firebase initialization timed out");
                StartCoroutine(RunServiceDependenciesInitializationSequence());
                yield break;
            }

            if (!FirebaseInitSucceeded)
            {
                Debug.LogWarning("Firebase initialization failed. Continuing sequence.");
                PublishDiagnosticStatus("SDK.Firebase", "Failed", "Firebase initialization failed");
            }

            StartCoroutine(RunServiceDependenciesInitializationSequence());
        }
#endif
        #endregion  //Initialization

        #endregion //Essentials

        #region Public Methods

        //------------------ Inter Ad Methods ------------------//
        //* Show Inter Ad
        public bool ShowInterAd(bool isRewarded = false, bool showAdBreak = false)
        {
            var result = interAdController != null && interAdController.TryShow(
                () => _adsHandler.CanShowInterstitial(),
                () => _adsHandler.CanUseFirstAdDelay(),
                isRewarded, showAdBreak);
            DebugLogger.AVNLog($"ShowInterAd result={result}");
            return result;
        }

        public void LoadInterAd()
        {
            DebugLogger.AVNLog("LoadInterAd requested");
            interAdController?.Load();
        }

        public void UpdateInterstitialShowMode(int modeIndex)
        {
            var mode = (InterAdController.InterstitialShowMode)modeIndex;
            DebugLogger.AVNLog($"UpdateInterstitialShowMode requested mode={mode}");
            interAdController?.SetShowMode(mode);
        }
        //------------------ AppOpn Ad Methods ------------------//
        public void SuppressAppOpenInterstitial()
        {
            appOpenInterAdController?.SuppressAppOpen();
        }
        public void UnsuppressAppOpenInterstitial()
        {
            appOpenInterAdController?.ReactivateAppOpen();
        }
        //------------------ Reward Ad Methods ------------------//
        //* Show Rewarded Ad
        public bool ShowRewardedAd(Action onRewarded, Action onNoAd = null, Action onCancelled = null)
        {
            Action wrappedOnRewarded = () =>
            {
                ToastManager?.ShowRewardGrantedToast();
                onRewarded?.Invoke();
            };
            Action wrappedOnNoAd = () =>
            {
                ToastManager?.ShowNoRewardedAdToast();
                onNoAd?.Invoke();
            };
            Action wrappedOnCancelled = () =>
            {
                ToastManager?.ShowRewardCanceledToast();
                onCancelled?.Invoke();
            };
            var result = rewardAdController != null && rewardAdController.TryShow(onRewarded, wrappedOnCancelled, wrappedOnNoAd);
            DebugLogger.AVNLog($"ShowRewardAd result={result}");
            return result;
        }

        //* Load Rewarded Ad
        public void LoadRewardAd()
        {
            DebugLogger.AVNLog("LoadRewardAd requested");
            rewardAdController?.Load();
        }

        //------------------ Banner Ad Methods ------------------//
        //* Show Banner Ad
        public void ShowBannerAd(BannerAdTypes type = BannerAdTypes.BANNER)
        {
            if (type == BannerAdTypes.ADAPTIVE)
                type = BannerAdTypes.BANNER; // Adaptive Banner is Selected In BannerAdController settings not here as a prameter
            DebugLogger.AVNLog($"ShowBanner requested type={type}");
            bannerAdController?.ShowBanner(type);
        }
        public void LoadBanner(bool showOnLoad, BannerAdTypes type = BannerAdTypes.BANNER)
        {
            DebugLogger.AVNLog($"LoadBanner requested type={type}");
            bannerAdController?.UpdateShowBannerOnLoad(showOnLoad);
            bannerAdController?.LoadBanner(type);
        }
        //* Hide Banner Ad
        public void HideBannerAd(BannerAdTypes type = BannerAdTypes.BANNER)
        {
            DebugLogger.AVNLog($"HideBanner requested type={type}");
            bannerAdController?.HideBanner(type);
        }
        //* Destroy Banner Ad
        public void DestroyBanner(BannerAdTypes type = BannerAdTypes.BANNER)
        {
            DebugLogger.AVNLog($"DestroyBanner requested type={type}");
            bannerAdController?.DestroyBanner(type);
        }

        //------------------ RateUs/Notification Methods ------------------//
        //* Show Rate Us
        public void NativeRateUsCall()
        {
            if (_adsHandler == null || _adsHandler.CanShowRateUsDialog())
            {
                appOpenInterAdController?.Suppress(3);
                _rateUsandNotificationService.ShowRateUs();
            }
        }

        //* Show Notification
        public void NotificationCall()
        {
            if (_adsHandler == null || _adsHandler.CanShowNotification())
            {
                appOpenInterAdController?.Suppress(3);
                _rateUsandNotificationService.ShowNotification();
            }
        }

        //------------------ SDK Management ------------------//
#if USE_FIREBASE
        //* Force Fetch Remote Config
        public void FetchRemoteConfigNow()
        {
            DebugLogger.AVNLog("FetchRemoteConfigNow requested");
            PublishDiagnosticStatus("SDK.RemoteConfig", "Initializing", "Manual fetch requested");
            remoteConfigRetryAttempts = 0;
            _remoteConfigService.FetchRemoteConfig();
        }
#endif
        //* Enable/Disable Ads
        public void SetAdsEnabled(bool enabled, bool persist = false)
        {
            AVNPluginConstants.AdsEnabledMaster = enabled;
            if (persist)
            {
                PlayerPrefs.SetInt(DT1AdsPluginPrefKeys.AdsEnabled, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (!enabled)
            {
                bannerAdController?.HideBanner();
            }
        }
        //* Get Ads Enabled State
        public bool GetAdsEnabled()
        {
            return AVNPluginConstants.AdsEnabledMaster;
        }
        //* Retry SDK Initialization (e.g. after regaining connectivity)
        //* This will only retry if the SDK is not currently initialized to avoid disrupting active sessions.
        //*
        public void RetryInitializationIfNeeded()
        {
            if (_provider == null || _provider.IsSdkInitialized)
            {
                return;
            }

            initialized = false;
            FirebaseInitCompleted = false;
            FirebaseInitSucceeded = false;
            InitializeServiceDependencies();
        }
        //------------------ In-App Purchasing ------------------//
#if USE_IAP
        //* In-App Purchasing
        public void BuyProduct(string productName)
        {
            appOpenInterAdController?.Suppress(3);
            _purchasingService?.BuyProduct(productName);
        }

        public void BuyRemoveAds()
        {
            appOpenInterAdController?.Suppress(3);
            _purchasingService?.BuyProduct(RemoveAdsProductName);
        }
        public string GetSubscritionPeriod(string productName)
        {
            return _purchasingService?.GetSubscriptionPeriodLabel(productName) ?? string.Empty;
        }
        //* Restore Purchases
        public void RestorePurchases()
        {
            _purchasingService?.RestorePurchases();
            ToastManager.ShowToast("Restoring purchases...", Color.green);
        }

        //* Get Product Price
        public string GetProductPrice(string productName)
        {
            return _purchasingService?.GetProductPrice(productName) ?? string.Empty;
        }

        /// <summary>
        /// Gets the localized price of the RemoveAds product.
        /// </summary>
        public string GetRemoveAdsPrice()
        {
            return _purchasingService?.GetProductPrice(RemoveAdsProductName) ?? "0.00";
        }
#endif
        #endregion // Public Methods

        #region Utility Public Methods
        public bool IsBannerReady(BannerAdTypes type)
        {
            return bannerAdController != null && bannerAdController.IsReady(type);
        }

        public bool IsBannerShowing(BannerAdTypes type)
        {
            return bannerAdController != null && bannerAdController.IsShowing(type);
        }

        public BannerAdTypes GetCurrentBannerType()
        {
            return bannerAdController != null ? bannerAdController.CurrentPendingType() : BannerAdTypes.BANNER;
        }

        public bool IsRewardedReady()
        {
            return rewardAdController != null && rewardAdController.IsReady();
        }

        public bool IsRewardedRequestActive()
        {
            return rewardAdController != null && rewardAdController.HasActiveRequest();
        }

        public bool IsRewardedWaitingFallback()
        {
            return rewardAdController != null && rewardAdController.IsWaitingForFallback();
        }

        public bool WasRewardFallbackShown()
        {
            return rewardAdController != null && rewardAdController.FallbackWasShown();
        }
        public bool IsInterstitialReady()
        {
            return interAdController != null && interAdController.HasReadyInterstitial();
        }
        public string GetInterstitialShowModeName()
        {
            return interAdController != null ? interAdController.GetShowModeName() : "Unknown";
        }
        public InterAdController.InterFlowState GetInterstitialFlowState()
        {
            return interAdController.GetCurrentInterAdState();
        }

        public void ResetDelayBeforeFirstAdStartTime()
        {
            DebugLogger.AVNLog("ResetDelayBeforeFirstAdStartTime requested");
            interAdController?.ResetInterAdDelaysLastShownTime();
        }

        public float GetDelayBeforeFirstAdSeconds()
        {
            return interAdController != null ? interAdController.DelayBeforeFirstAdSeconds : 0f;
        }

        public bool IsUsingDelayBeforeFirstAd()
        {
            return interAdController != null && interAdController.UseDelayBeforeFirstAd;
        }

        public void ResetInterDelay()
        {
            DebugLogger.AVNLog("ResetInterDelay requested");
            interAdController.InterAdDelay = AVNPluginConstants.DefaultInterAdDelay;
        }
        public void UpdateInterDelay(float newDelay)
        {
            DebugLogger.AVNLog($"UpdateInterDelay requested newDelay={newDelay}");
            interAdController.InterAdDelay = newDelay;
        }

        public void UpdateDelayBeforeFirstAd(float newDelay)
        {
            DebugLogger.AVNLog($"UpdateDelayBeforeFirstAd requested newDelay={newDelay}");
            if (interAdController != null)
            {
                interAdController.DelayBeforeFirstAdSeconds = newDelay;
            }
        }

        public void UpdateUseDelayBeforeFirstAd(bool useDelay)
        {
            DebugLogger.AVNLog($"UpdateUseDelayBeforeFirstAd requested useDelay={useDelay}");
            if (interAdController != null)
            {
                interAdController.UseDelayBeforeFirstAd = useDelay;
            }
        }
        public int GetCurrentInterstitialSlot()
        {
            return interAdController != null ? interAdController.GetCurrentShownSlot() : -1;
        }

        public int GetInterstitialLoadingSlot()
        {
            return interAdController != null ? interAdController.GetLoadingSlot() : -1;
        }
        public void UpdateUseAdDelay(bool useDelay)
        {
            interAdController.SetUseAdDelay(useDelay);
        }
        public List<InterAdController.InterSlotDebugState> GetInterstitialSlotStates()
        {
            return interAdController != null ? interAdController.GetSlotDebugStates() : new List<InterAdController.InterSlotDebugState>();
        }

        public string GetRuntimeBannerAdId()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.BannerAdUnitId : string.Empty);
        }

        public string GetRuntimeMrecAdId()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.MrecAdUnitId : string.Empty);
        }

        public string GetRuntimeRewardedAdId()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.RewardedAdUnitId : string.Empty);
        }

        public string GetRuntimeAppOpenAdId()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.AppOpenAdUnitId1 : string.Empty);
        }

        public string GetRuntimeAppOpenBackfillAdId()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.AppOpenAdUnitId2 : string.Empty);
        }

        public string[] GetRuntimeInterstitialAdIds()
        {
            return NormalizeAdIdArray(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.InterAdUnitIds : Array.Empty<string>());
        }
        public PluginInitMode GetPluginInitMode()
        {
            return initializeOnStart;
        }
        #endregion //Utility Public Methods
        #region Plugin Public Methods

#if USE_IAP
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use SetProductPurchasedCallback() inside GameInApp.OnSetupCallbacks() instead of calling AVNPlugin directly.", false)]
        public void RegisterProductPurchasedCallback(string productName, Action callback)
            => _purchasingService?.SetPurchasedCallback(productName, callback);

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use SetProductRestoredCallback() inside GameInApp.OnSetupCallbacks() instead of calling AVNPlugin directly.", false)]
        public void RegisterProductRestoredCallback(string productName, Action callback)
            => _purchasingService?.SetRestoredCallback(productName, callback);

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use SetProductRefundedCallback() inside GameInApp.OnSetupCallbacks() instead of calling AVNPlugin directly.", false)]
        public void SetProductExpiredCallback(string productName, Action callback)
            => _purchasingService?.SetExpiredCallback(productName, callback);
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use SetProductRefundedCallback() inside GameInApp.OnSetupCallbacks() instead of calling AVNPlugin directly.", false)]
        public void RegisterProductRefundedCallback(string productName, Action callback)
            => _purchasingService?.SetRefundedCallback(productName, callback);
#endif
        //* Send Custom Game Event
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Add a method in GameEvents instead of calling AVNPlugin directly.", false)]
        public void SendCustomGameEvent(string eventName, Dictionary<string, string> parameters, bool sendToFirebase = true, bool sendToByteBrew = true)
        {
            _gameEventService?.SendCustomEvent(eventName, parameters, sendToFirebase, sendToByteBrew);
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use GameEvents.LevelAnalysis() instead of calling AVNPlugin directly.", false)]
        public bool SendLevelAnalysisEvent(int levelNumber, LevelState levelState, LevelMode levelMode)
        {
            return _gameEventService != null && _gameEventService.LevelAnalysisEvent(levelNumber, levelState, levelMode);
        }
        public bool SendUserInterStatus(UserReturnStatus userReturnStatus, string adAdapter)
        {
            var payload = new Dictionary<string, string>
            {
                { userReturnStatus.ToString(), adAdapter }
            };
            return _gameEventService != null && _gameEventService.SendCustomEvent("USER_RETURN_STATUS", payload);
        }

        //* Register Custom Remote Config Key
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use RegisterKey() inside GameRemoteConfig.OnRegisterKeys() instead of calling AVNPlugin directly.", false)]
        public void RegisterCustomRemoteConfigKey(string keyId, RemoteDataType type, Action<string> onFetched)
        {
#if USE_FIREBASE
            _remoteConfigService.RegisterCustomKey(keyId, type, onFetched);
#endif
        }
        #endregion //Plugin Public Methods

        #region Private Helpers

        private void ApplyPersistedAdsState()
        {
            if (PlayerPrefs.HasKey(DT1AdsPluginPrefKeys.AdsEnabled))
            {
                AVNPluginConstants.AdsEnabledMaster = PlayerPrefs.GetInt(DT1AdsPluginPrefKeys.AdsEnabled, 1) == 1;
            }

            // Re-apply RemoveAds state so ad controllers start in the correct state
            if (AVNPluginConstants.RemoveAdsPurchased)
            {
                Debug.Log("[ApplyPersistedAdsState] EnableInterAd set to: " + false);

                AVNPluginConstants.EnableInterAd = false;
                AVNPluginConstants.EnableBannerAd = false;
                AVNPluginConstants.EnableAppOpenAd = false;
            }
        }
        private void VerifyAdsScripts()
        {
            if (bannerAdController == null)
            {
                bannerAdController = GetComponentInChildren<BannerAdController>(true);
            }

            if (interAdController == null)
            {
                interAdController = GetComponentInChildren<InterAdController>(true);
            }

            if (rewardAdController == null)
            {
                rewardAdController = GetComponentInChildren<RewardAdController>(true);
            }

            if (appOpenInterAdController == null)
            {
                appOpenInterAdController = GetComponentInChildren<AppOpenAdController>(true);
            }

            if (bannerAdController == null)
            {
                Debug.LogError("BannerAdController reference is missing on AVNPlugin. Please assign it in the inspector.");
            }

            if (interAdController == null)
            {
                Debug.LogError("InterAdController reference is missing on AVNPlugin. Please assign it in the inspector.");
            }

            if (rewardAdController == null)
            {
                Debug.LogError("RewardAdController reference is missing on AVNPlugin. Please assign it in the inspector.");
            }

            if (appOpenInterAdController == null)
            {
                Debug.LogError("AppOpenInterAdController reference is missing on AVNPlugin. Please assign it in the inspector.");
            }
        }
        private void CacheInspectorDefaultAdIds()
        {
            EnsureRuntimeAdIds();
            defaultBannerAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.BannerAdUnitId : string.Empty);
            defaultMrecAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.MrecAdUnitId : string.Empty);
            defaultRewardedAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.RewardedAdUnitId : string.Empty);
            defaultAppOpenAdUnitId1 = NormalizeAdId(adIdsConfig != null ? adIdsConfig.AppOpenAdUnitId1 : string.Empty);
            defaultAppOpenAdUnitId2 = NormalizeAdId(adIdsConfig != null ? adIdsConfig.AppOpenAdUnitId2 : string.Empty);
            defaultInterAdUnitIds = NormalizeAdIdArray(adIdsConfig != null ? adIdsConfig.InterAdUnitIds : Array.Empty<string>());
        }

        private void ApplyCachedAdIdsOrDefaults()
        {
#if USE_FIREBASE
            if (_remoteConfigService != null && _remoteConfigService.TryGetCachedAdIdsConfig(out var cached))
            {
                ApplyMergedAdIds(cached);
            }
            else
            {
                ApplyMergedAdIds(null);
            }
#else
            ApplyMergedAdIds(null);
#endif
        }

        private void ApplyRuntimeAdIdOverrides()
        {
            if (_provider == null)
            {
                return;
            }

            _provider.ConfigureAdIds(
                GetRuntimeBannerAdId(),
                GetRuntimeMrecAdId(),
                GetRuntimeRewardedAdId());
        }

        private void ApplyMergedAdIds(AdIdsRemoteConfigData remote)
        {
            EnsureRuntimeAdIds();

            if (runtimeAdIds == null)
            {
                return;
            }

            runtimeAdIds.BannerAdUnitId = PickAdId(remote?.BannerAdUnitId, defaultBannerAdUnitId);
            runtimeAdIds.MrecAdUnitId = PickAdId(remote?.MrecAdUnitId, defaultMrecAdUnitId);
            runtimeAdIds.RewardedAdUnitId = PickAdId(remote?.RewardedAdUnitId, defaultRewardedAdUnitId);
            runtimeAdIds.AppOpenAdUnitId1 = PickAdId(remote?.AppOpenAdUnitId1, defaultAppOpenAdUnitId1);
            runtimeAdIds.AppOpenAdUnitId2 = PickAdId(remote?.AppOpenAdUnitId2, defaultAppOpenAdUnitId2);
            runtimeAdIds.InterAdUnitIds = MergeInterAdIds(defaultInterAdUnitIds, remote?.InterAdUnitIds);

            ApplyRuntimeAdIdOverrides();
            interAdController?.SetInterstitialIds(GetRuntimeInterstitialAdIds());
            appOpenInterAdController?.SetAppOpenInterstitialIds(GetRuntimeAppOpenAdId(), GetRuntimeAppOpenBackfillAdId());
            if (autoLoadAppOpen && _appOpenAdService != null && _appOpenAdService.IsSdkInitialized)
            {
                appOpenInterAdController?.Load();
            }
        }

        private string GetRuntimeSdkKey()
        {
            return NormalizeAdId(ActiveAdIdsConfig != null ? ActiveAdIdsConfig.AdsSDKKey : string.Empty);
        }

        private void EnsureRuntimeAdIds()
        {
            if (runtimeAdIds != null)
            {
                return;
            }

            runtimeAdIds = adIdsConfig != null
                ? ScriptableObject.Instantiate(adIdsConfig)
                : ScriptableObject.CreateInstance<AVNAdIdsConfig>();

            runtimeAdIds.hideFlags = HideFlags.DontSave;
            runtimeAdIds.AdsSDKKey = NormalizeAdId(adIdsConfig != null ? adIdsConfig.AdsSDKKey : string.Empty);
            runtimeAdIds.BannerAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.BannerAdUnitId : string.Empty);
            runtimeAdIds.MrecAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.MrecAdUnitId : string.Empty);
            runtimeAdIds.RewardedAdUnitId = NormalizeAdId(adIdsConfig != null ? adIdsConfig.RewardedAdUnitId : string.Empty);
            runtimeAdIds.AppOpenAdUnitId1 = NormalizeAdId(adIdsConfig != null ? adIdsConfig.AppOpenAdUnitId1 : string.Empty);
            runtimeAdIds.AppOpenAdUnitId2 = NormalizeAdId(adIdsConfig != null ? adIdsConfig.AppOpenAdUnitId2 : string.Empty);
            runtimeAdIds.InterAdUnitIds = NormalizeAdIdArray(adIdsConfig != null ? adIdsConfig.InterAdUnitIds : Array.Empty<string>());
        }

        private static string PickAdId(string remoteValue, string fallback)
        {
            var normalizedRemote = NormalizeAdId(remoteValue);
            if (!string.IsNullOrEmpty(normalizedRemote))
            {
                return normalizedRemote;
            }

            return NormalizeAdId(fallback);
        }

        private static string[] MergeInterAdIds(string[] fallbackIds, string[] remoteIds)
        {
            var normalizedFallback = NormalizeAdIdArray(fallbackIds);
            var normalizedRemote = NormalizeAdIdArray(remoteIds);
            var length = Math.Max(normalizedFallback.Length, normalizedRemote.Length);
            if (length == 0)
            {
                return Array.Empty<string>();
            }

            var merged = new string[length];
            for (var i = 0; i < length; i++)
            {
                var remoteValue = i < normalizedRemote.Length ? normalizedRemote[i] : string.Empty;
                var fallbackValue = i < normalizedFallback.Length ? normalizedFallback[i] : string.Empty;
                merged[i] = !string.IsNullOrEmpty(remoteValue) ? remoteValue : fallbackValue;
            }

            return merged;
        }

        private static string[] NormalizeAdIdArray(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                copy[i] = NormalizeAdId(values[i]);
            }

            return copy;
        }

        private static string NormalizeAdId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FormatAdIdArray(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return "<empty>";
            }

            return string.Join(" | ", values);
        }

        private bool TryGetCachedAdIdsConfig(out AdIdsRemoteConfigData config)
        {
            config = null;
#if USE_FIREBASE
            return _remoteConfigService != null && _remoteConfigService.TryGetCachedAdIdsConfig(out config);
#else
            return false;
#endif
        }

        private string GetCachedAdIdsValue(Func<AdIdsRemoteConfigData, string> selector)
        {
            if (selector == null || !TryGetCachedAdIdsConfig(out var cached))
            {
                return "<none>";
            }

            return NormalizeAdId(selector.Invoke(cached));
        }

        private void TryScheduleRemoteConfigRetry(string reason)
        {
            var maxPower = Math.Max(0, remoteConfigRetryMaxPower);
            if (remoteConfigRetryAttempts >= maxPower)
            {
                PublishDiagnosticStatus("SDK.RemoteConfig", "Failed", $"Remote config retry limit reached: {reason}");
                return;
            }

            remoteConfigRetryAttempts++;
            var delay = Mathf.Min(Mathf.Pow(2f, remoteConfigRetryAttempts), 64f);
            PublishDiagnosticStatus("SDK.RemoteConfig", "Initializing", $"Scheduling remote config retry {remoteConfigRetryAttempts}/{maxPower} in {delay:0.##}s");
            StartCoroutine(RetryRemoteConfigAfterDelay(delay));
        }

        private IEnumerator RetryRemoteConfigAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
#if USE_FIREBASE
            _remoteConfigService?.FetchRemoteConfig();
#endif
        }
        #endregion
        #region Utilities
        private static Type FindType(string fullTypeName)
        {
            var directMatch = Type.GetType(fullTypeName);
            if (directMatch != null)
            {
                return directMatch;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullTypeName) ?? assembly.GetType(fullTypeName.Split('.')[^1]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private void HandleFirebaseReady(string _)
        {
            FirebaseInitSucceeded = true;
            FirebaseInitCompleted = true;
            Debug.Log("Firebase initialization succeeded");
            PublishDiagnosticStatus("SDK.Firebase", "Initialized", "Firebase initialized successfully");
        }

        private void HandleFirebaseFailed(string message)
        {
            FirebaseInitSucceeded = false;
            FirebaseInitCompleted = true;
            Debug.LogWarning($"Firebase initialization failed: {message}");
            PublishDiagnosticStatus("SDK.Firebase", "Failed", message);
        }

        private void HandleProviderInitializedSuccessfully()
        {
            providerInitSucceeded = true;
            providerInitCompleted = true;
            LevelPlayInitializationSucceeded?.Invoke();
            PublishDiagnosticStatus("SDK.IronSource", "Initialized", "Initialized IronSource SDK");
            TryLoadNextSceneIfReady();
        }

        private void HandleProviderInitializationFailed(string message)
        {
            providerInitSucceeded = false;
            providerInitCompleted = true;
            PublishDiagnosticStatus("SDK.IronSource", "Failed", message);
            TryLoadNextSceneIfReady();
        }

        private void HandleByteBrewReady()
        {
            PublishDiagnosticStatus("SDK.ByteBrew", "Initialized", "ByteBrew initialized");
        }

        private void HandleByteBrewFailed(string message)
        {
            PublishDiagnosticStatus("SDK.ByteBrew", "Failed", message);
        }

        private void HandleRemoteConfigFetched()
        {
            remoteConfigRetryAttempts = 0;
            ApplyCachedAdIdsOrDefaults();
            _remoteConfigHandler.NotifyConfigFetched();
            PublishDiagnosticStatus("SDK.RemoteConfig", "Initialized", "Remote config fetched");
        }

        private void HandleRemoteConfigFailed(string message)
        {
            PublishDiagnosticStatus("SDK.RemoteConfig", "Failed", message);
            TryScheduleRemoteConfigRetry(message);
        }
#if USE_IAP
        private void HandlePurchaseSuccessful(string productId)
        {
            _purchaseHandler.DispatchProductPurchased(productId);
        }

        private void HandleRestoreSuccessful()
        {
            _purchaseHandler.DispatchPurchaseRestored();
        }

        private void HandleRemoveAdsPurchased()
        {
            AVNPluginConstants.RemoveAdsPurchased = true;
            bannerAdController?.DestroyBanner();
            _purchaseHandler.DispatchRemoveAdsPurchased();
            DebugLogger.AVNLog("HandleRemoveAdsPurchased");
        }

        private void HandleRemoveAdsRestored()
        {
            AVNPluginConstants.RemoveAdsPurchased = true;
            bannerAdController?.DestroyBanner(BannerAdTypes.BANNER);
            bannerAdController?.DestroyBanner(BannerAdTypes.MREC);
            _purchaseHandler.DispatchRemoveAdsRestored();
            DebugLogger.AVNLog("HandleRemoveAdsRestored");
        }

        private void HandleRemoveAdsRefunded()
        {
            AVNPluginConstants.RemoveAdsPurchased = false;
            _purchaseHandler.DispatchRemoveAdsRefunded();
            DebugLogger.AVNLog("HandleRemoveAdsRefunded");
        }
#endif
        private static void PublishDiagnosticStatus(string key, string stateName, string message)
        {
            try
            {
                var hubType = FindType("AVN.AdsPlugin.Testing.AdsDiagnosticsHub");
                var stateType = FindType("AVN.AdsPlugin.Testing.AdsDiagnosticState");
                if (hubType == null || stateType == null)
                {
                    return;
                }

                var stateValue = Enum.Parse(stateType, stateName, true);
                var publishMethod = hubType.GetMethod("PublishStatus", BindingFlags.Public | BindingFlags.Static);
                publishMethod?.Invoke(null, new object[] { key, stateValue, message, null });
            }
            catch
            {
                // Diagnostics publishing should never block runtime behavior.
            }
        }

        private static void PublishDiagnosticEvent(string scope, string key, string message)
        {
            try
            {
                var hubType = FindType("AVN.AdsPlugin.Testing.AdsDiagnosticsHub");
                if (hubType == null)
                {
                    return;
                }

                var publishMethod = hubType.GetMethod("PublishEvent", BindingFlags.Public | BindingFlags.Static);
                publishMethod?.Invoke(null, new object[] { scope, key, message });
            }
            catch
            {
                // Diagnostics publishing should never block runtime behavior.
            }
        }

        private void TryLoadNextSceneIfReady()
        {
            if (sceneLoadTriggered || !syncLoadingWithProvider)
            {
                return;
            }

            var providerReady = IsAdsProviderInitialized;
            var providerFailed = providerInitCompleted && !providerInitSucceeded;
            if (!providerReady && !providerFailed)
            {
                return;
            }

            if (!HasInternetConnection())
            {
                if (!waitingForInternetToLoadScene)
                {
                    waitingForInternetToLoadScene = true;
                    Debug.Log("AVNPlugin: Scene load delayed because internet is unavailable.");
                }

                return;
            }

            waitingForInternetToLoadScene = false;

            if (string.IsNullOrWhiteSpace(nextSceneToLoad))
            {
                if (!missingNextSceneLogged)
                {
                    missingNextSceneLogged = true;
                    Debug.LogWarning("AVNPlugin: nextSceneToLoad is empty while syncLoadingWithProvider is enabled. Scene load skipped.");
                }

                return;
            }

            missingNextSceneLogged = false;
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(activeSceneName, nextSceneToLoad, StringComparison.Ordinal))
            {
                sceneLoadTriggered = true;
                return;
            }

            sceneLoadTriggered = true;
            if (providerReady)
            {
                Debug.Log("AVNPlugin: Provider ready. Loading next scene: " + nextSceneToLoad);
            }
            else
            {
                Debug.LogWarning("AVNPlugin: Provider initialization failed, but internet is reachable. Loading next scene via fallback: " + nextSceneToLoad);
            }

            if (additionalDelayInLoadingNextScene > 0)
            {
                StartCoroutine(LoadSceneWithDelay(nextSceneToLoad, additionalDelayInLoadingNextScene));
            }
            else
            {
                SceneManager.LoadScene(nextSceneToLoad);
            }
        }

        private IEnumerator LoadSceneWithDelay(string sceneName, int delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            SceneManager.LoadScene(sceneName);
        }

        private static bool HasInternetConnection()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }
        #endregion
    }
}
#endif
//hassmhelpedtoo