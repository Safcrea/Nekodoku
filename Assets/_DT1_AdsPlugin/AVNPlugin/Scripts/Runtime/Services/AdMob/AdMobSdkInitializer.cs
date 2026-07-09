#if USE_AVNADS_PLUGIN && USE_ADMOB
using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

namespace AVN.AdsPlugin.Services
{
    public enum AdMobSdkInitializationState
    {
        NotStarted,
        Initializing,
        Initialized,
        Failed
    }

    public static class AdMobSdkInitializer
    {
        private static readonly List<Action<bool>> PendingCallbacks = new List<Action<bool>>();
        private static readonly HashSet<string> ConfiguredTestDeviceIds = new HashSet<string>(StringComparer.Ordinal);

        public static event Action<InitializationStatus> InitializedSuccessfully;
        public static event Action<string> InitializationFailed;

        public static AdMobSdkInitializationState State { get; private set; } = AdMobSdkInitializationState.NotStarted;
        public static InitializationStatus LastInitializationStatus { get; private set; }
        public static string LastErrorMessage { get; private set; } = string.Empty;
        public static bool IsInitialized => State == AdMobSdkInitializationState.Initialized;
        public static bool IsTerminal =>
            State == AdMobSdkInitializationState.Initialized ||
            State == AdMobSdkInitializationState.Failed;

        /// <summary>
        /// Resets a failed initialization so that EnsureInitialized will retry the real SDK init.
        /// No-op if the SDK is already initialized or currently initializing.
        /// Call this before RetryInitializationIfNeeded when internet becomes available after a failure.
        /// </summary>
        public static void Reset()
        {
            if (State != AdMobSdkInitializationState.Failed)
            {
                return;
            }

            State = AdMobSdkInitializationState.NotStarted;
            LastErrorMessage = string.Empty;
            LastInitializationStatus = null;
            PendingCallbacks.Clear();
        }

        public static void EnsureInitialized(Action<bool> onCompleted = null, IEnumerable<string> testDeviceIds = null)
        {
            ApplySharedConfiguration(testDeviceIds);

            if (State == AdMobSdkInitializationState.Initialized)
            {
                onCompleted?.Invoke(true);
                return;
            }

            if (State == AdMobSdkInitializationState.Failed)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (onCompleted != null)
            {
                PendingCallbacks.Add(onCompleted);
            }

            if (State == AdMobSdkInitializationState.Initializing)
            {
                return;
            }

            State = AdMobSdkInitializationState.Initializing;
            LastInitializationStatus = null;
            LastErrorMessage = string.Empty;

            try
            {
                MobileAds.Initialize(initStatus =>
                {
                    if (initStatus == null)
                    {
                        CompleteFailure("Google Mobile Ads initialization returned null status.");
                        return;
                    }

                    State = AdMobSdkInitializationState.Initialized;
                    LastInitializationStatus = initStatus;
                    InitializedSuccessfully?.Invoke(initStatus);
                    FlushPendingCallbacks(true);
                });
            }
            catch (Exception exception)
            {
                CompleteFailure(exception.Message);
            }
        }

        private static void ApplySharedConfiguration(IEnumerable<string> testDeviceIds)
        {
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            if (testDeviceIds != null)
            {
                foreach (var deviceId in testDeviceIds)
                {
                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        ConfiguredTestDeviceIds.Add(deviceId.Trim());
                    }
                }
            }

            if (ConfiguredTestDeviceIds.Count == 0)
            {
                return;
            }

            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TestDeviceIds = new List<string>(ConfiguredTestDeviceIds)
            });
        }

        private static void CompleteFailure(string message)
        {
            State = AdMobSdkInitializationState.Failed;
            LastErrorMessage = string.IsNullOrWhiteSpace(message)
                ? "Google Mobile Ads initialization failed."
                : message;
            InitializationFailed?.Invoke(LastErrorMessage);
            FlushPendingCallbacks(false);
        }

        private static void FlushPendingCallbacks(bool succeeded)
        {
            if (PendingCallbacks.Count == 0)
            {
                return;
            }

            var callbacks = PendingCallbacks.ToArray();
            PendingCallbacks.Clear();
            foreach (var callback in callbacks)
            {
                try
                {
                    callback?.Invoke(succeeded);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
#endif