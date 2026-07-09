#if USE_AVNADS_PLUGIN && USE_ADMOB
using System;
using System.Collections.Generic;
using AVN.AdsPlugin;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.Services;
using Balaso;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.Events;

namespace GpAppTrackingg
{
    public class GoogleAppTracking : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Plugin References")]
        [Tooltip("AVNPlugin living in this scene. Must have initializeOnStart = false.")]
        [SerializeField] private AVNPlugin avnPlugin;

        [Tooltip("AppOpenAdController living in this scene (same prefab as AVNPlugin).")]
        [SerializeField] private AppOpenAdController appOpenAdController;

        [Header("CMP Settings")]
        [Tooltip("Disable to skip the consent form entirely (useful for soft-launch or non-GDPR builds).")]
        [SerializeField] private bool enableAdMobCMP = true;

        [Tooltip("Geography to use during CMP debug sessions. Set to EEA to force the consent form.")]
        [SerializeField] private DebugGeography debugGeography = DebugGeography.Disabled;
        [SerializeField] private UnityEvent OnConsentFlowCompleted; // Fires when the CMP flow is completed, regardless of outcome.


        // ──────────────────────────────────────────────────────────────────────
        // Private state
        // ──────────────────────\────────────────────────────────────────────────

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        private bool startupFlowCompleted;
        private bool postConsentFlowStarted;

        [SerializeField]
        private readonly List<string> TEST_DEVICE_IDS = new List<string>
        {
            AdRequest.TestDeviceSimulator,
            // Add your test device IDs (replace with your own device IDs).
#if UNITY_IPHONE
                "96e23e80653bb28980d3f40beb58915c",
#elif UNITY_ANDROID
                "75EF8D155528C04DACBBA6F36F433035",
                "44B7DFFCFDD7D8B16F8586C971B85468", // UMP debug device hash (from logcat)
#endif
        };

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (!AVNPluginConstants.CMPEligibleUser)
            {
                Debug.Log("GoogleAppTracking: User not in CMP country. Skipping GoogleAppTracking flow and initializing AVNPlugin directly.");
                TriggerPostConsentFlow(true);
                return;
            }

            // CMP-country flow: initialize AdMob here, then run consent flow.
            AdMobSdkInitializer.EnsureInitialized(HandleAdMobInitializationCompleted, TEST_DEVICE_IDS);
        }

        private void OnDestroy()
        {
#if UNITY_IOS
            AppTrackingTransparency.OnAuthorizationRequestDone -= HandleAttAuthorizationRequestDone;
#endif
        }

        // ──────────────────────────────────────────────────────────────────────
        // AdMob SDK init callback
        // ──────────────────────────────────────────────────────────────────────

        private void HandleAdMobInitializationCompleted(bool initializedSuccessfully)
        {
            if (initializedSuccessfully)
            {
                Debug.Log("GoogleAppTracking: Google Mobile Ads initialization complete.");

                // CMP only makes sense when the SDK is up.
                if (enableAdMobCMP)
                {
                    InitializeGoogleMobileAdsConsent();
                    return;
                }

                Debug.Log("GoogleAppTracking: CMP disabled via inspector flag. Skipping consent form.");
                TriggerPostConsentFlow(consentGranted: true);
            }
            else
            {
                // SDK failed (most likely no internet). Skip CMP — it requires AdMob to be up.
                // Do NOT trigger AppOpen here; AppOpenAdController._sessionStartLoadHandled stays
                // false so LoadAdsCoroutine can call LoadForSessionStart() properly after retry.
                Debug.LogWarning("GoogleAppTracking: AdMob SDK init failed. Skipping CMP and AppOpen. Plugin will initialize now; AppOpen will load once SDK retries successfully.");
                TriggerPostConsentFlow(consentGranted: false);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // CMP flow
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeGoogleMobileAdsConsent()
        {
            Debug.Log("GoogleAppTracking: Gathering consent.");

            GatherConsent((string error) =>
            {
                if (error != null)
                {
                    Debug.LogError("GoogleAppTracking: Failed to gather consent with error: " + error);
                }
                else
                {
                    Debug.Log("GoogleAppTracking: Consent updated. Status=" + ConsentInformation.ConsentStatus);
                }

                TriggerPostConsentFlow(consentGranted: CanRequestAds);
            });
        }

        public void GatherConsent(Action<string> onComplete)
        {
            Debug.Log("GoogleAppTracking: GatherConsent called.");

            var requestParameters = new ConsentRequestParameters
            {
                // False means users are not under age.
                TagForUnderAgeOfConsent = false,
                ConsentDebugSettings = new ConsentDebugSettings
                {
                    // For debugging consent settings by geography.
                    DebugGeography = debugGeography,
                    // https://developers.google.com/admob/unity/test-ads
                    TestDeviceHashedIds = TEST_DEVICE_IDS,
                }
            };

            // The Google Mobile Ads SDK provides the User Messaging Platform (Google's
            // IAB Certified consent management platform) as one solution to capture
            // consent for users in GDPR impacted countries. This is an example and
            // you can choose another consent management platform to capture consent.
            ConsentInformation.Update(requestParameters, (FormError updateError) =>
            {
                if (updateError != null)
                {
                    onComplete(updateError.Message);
                    return;
                }

                // Determine the consent-related action to take based on the ConsentStatus.
                if (CanRequestAds)
                {
                    // Consent has already been gathered or not required.
                    // Return control back to the user.
                    onComplete(null);
                    return;
                }

                // Consent not obtained and is required.
                // Load the initial consent request form for the user.
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                    {
                        // Form showing failed.
                        onComplete?.Invoke(showError.Message);
                    }
                    else
                    {
                        // Form showing succeeded (user made a choice).
                        onComplete?.Invoke(null);
                    }
                });
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Post-consent: fire events, trigger AppOpen, kick off plugin init
        // ──────────────────────────────────────────────────────────────────────

        private void TriggerPostConsentFlow(bool consentGranted)
        {
            if (postConsentFlowStarted)
            {
                return;
            }

            postConsentFlowStarted = true;

            ResolveIosAttThenContinue(consentGranted);
        }

        private void ResolveIosAttThenContinue(bool consentGranted)
        {
#if UNITY_IOS
            var attStatus = AppTrackingTransparency.TrackingAuthorizationStatus;
            if (attStatus != AppTrackingTransparency.AuthorizationStatus.NOT_DETERMINED)
            {
                Debug.Log($"GoogleAppTracking: ATT already resolved with status={attStatus}. Continuing startup.");
                CompleteConsentFlowAndContinue(consentGranted);
                return;
            }

            AppTrackingTransparency.OnAuthorizationRequestDone -= HandleAttAuthorizationRequestDone;
            AppTrackingTransparency.OnAuthorizationRequestDone += HandleAttAuthorizationRequestDone;
            Debug.Log("GoogleAppTracking: CMP completed. Requesting iOS ATT authorization.");
            AppTrackingTransparency.RequestTrackingAuthorization();
#else
            CompleteConsentFlowAndContinue(consentGranted);
#endif
        }

#if UNITY_IOS
        private void HandleAttAuthorizationRequestDone(AppTrackingTransparency.AuthorizationStatus status)
        {
            AppTrackingTransparency.OnAuthorizationRequestDone -= HandleAttAuthorizationRequestDone;
            Debug.Log($"GoogleAppTracking: ATT callback received. Status={status}. Continuing startup.");
            CompleteConsentFlowAndContinue(consentGranted: CanRequestAds);
        }
#endif

        private void CompleteConsentFlowAndContinue(bool consentGranted)
        {
            OnConsentFlowCompleted?.Invoke();
            ContinueToPluginInitialization(consentGranted);
        }

        private void ContinueToPluginInitialization(bool consentGranted, bool requestStartupAppOpen = true)
        {
            if (startupFlowCompleted)
            {
                return;
            }

            startupFlowCompleted = true;
            Debug.Log($"GoogleAppTracking: Post-consent startup flow continuing. consentGranted={consentGranted}");

            // Request startup app-open loading first; the controller will defer execution
            // until its app-open service dependency has been initialized by AVNPlugin.
            if (requestStartupAppOpen && AdMobSdkInitializer.IsInitialized && appOpenAdController != null)
            {
                appOpenAdController.RequestSessionStartLoad();
                Debug.Log("GoogleAppTracking: Startup AppOpen request queued.");
            }
            else if (requestStartupAppOpen && !AdMobSdkInitializer.IsInitialized)
            {
                Debug.Log("GoogleAppTracking: AdMob SDK not ready - AppOpen startup request deferred to retry path.");
            }
            else if (requestStartupAppOpen)
            {
                Debug.LogWarning("GoogleAppTracking: appOpenAdController reference is not assigned. AppOpen ad will not be triggered from here.");
            }

            // 2. Initialize the AVN plugin (synchronous setup; async init coroutines start inside).
            //    This must run first so AppOpenAdController gets its service and ad unit IDs assigned.
            if (avnPlugin != null && avnPlugin.GetPluginInitMode() == PluginInitMode.StartByCMP)
            {
                avnPlugin.Initialize();
                Debug.Log("GoogleAppTracking: AVNPlugin.Initialize() called.");
            }
            else
            {
                Debug.LogWarning("GoogleAppTracking: avnPlugin reference is not assigned. Plugin will not initialize from here.");
            }
        }

        private bool IsSyncLoadingEnabled()
        {
            return avnPlugin != null && avnPlugin.SyncLoadingWithProvider;
        }
    }
}

#endif