#if USE_AVNADS_PLUGIN && USE_FIREBASE
using System;
using System.Collections.Generic;
using AVN.AdsPlugin.Testing;
using UnityEngine;
using Firebase.Extensions;
using Firebase.RemoteConfig;

namespace AVN.AdsPlugin.Services
{
    public sealed class FirebaseRemoteConfigService
    {
        private const string AdConfigKey = "AdConfig";
        private const string AdIdsConfigKey = "AdIdsConfig";
        private const string ShowCMPToUserKey = "CMPEligibleUser";

        private sealed class CustomKeyRegistration
        {
            public string KeyId;
            public RemoteDataType DataType;
            public Action<string> Callback;
        }

        private readonly Dictionary<string, CustomKeyRegistration> registrations = new Dictionary<string, CustomKeyRegistration>();
        private bool isFetching;

        public event Action OnConfigFetched;
        public event Action<string> OnConfigFetchFailed;

        public bool TryGetCachedAdIdsConfig(out AdIdsRemoteConfigData config)
        {
            config = null;
            if (!PlayerPrefs.HasKey(DT1AdsPluginPrefKeys.RemoteConfigAdIdsConfig))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Failed, "No cached AdIdsConfig found");
                return false;
            }

            var json = PlayerPrefs.GetString(DT1AdsPluginPrefKeys.RemoteConfigAdIdsConfig, string.Empty);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loading, "Attempting to deserialize cached AdIdsConfig");
            return TryDeserializeAdIdsConfig(json, out config);
        }

        public void ApplyCachedConfig()
        {
            if (PlayerPrefs.HasKey(DT1AdsPluginPrefKeys.RemoteConfigAdConfig))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loading, "Applying cached AdConfig");
                var json = PlayerPrefs.GetString(DT1AdsPluginPrefKeys.RemoteConfigAdConfig, string.Empty);
                TryApplyAdConfig(json, true);
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Idle,
                    "Applied cached AdConfig",
                    new Dictionary<string, string> { { "AdConfig", json.ToString() } });
            }
        }

        public void RegisterCustomKey(string keyId, RemoteDataType type, Action<string> onFetched)
        {
            if (string.IsNullOrWhiteSpace(keyId) || onFetched == null)
            {
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Idle, $"Registered custom key={keyId}, type={type}");

            registrations[keyId] = new CustomKeyRegistration
            {
                KeyId = keyId,
                DataType = type,
                Callback = onFetched
            };

            var cachedValue = PlayerPrefs.GetString(DT1AdsPluginPrefKeys.RemoteConfigCustom(keyId), string.Empty);
            if (!string.IsNullOrEmpty(cachedValue))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loaded, $"Using cached custom key value for key={keyId}");
                onFetched.Invoke(cachedValue);
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loaded, $"Custom key={keyId} value loaded from cache: {cachedValue.ToString()}");
            }
        }

        public void FetchRemoteConfig()
        {
            if (isFetching)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Failed, "FetchRemoteConfig ignored: fetch already in progress");
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Initializing, "FetchRemoteConfig requested");
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Initializing, "Fetching remote config");

            isFetching = true;
            FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero).ContinueWithOnMainThread(fetchTask =>
            {
                if (fetchTask.IsCanceled || fetchTask.IsFaulted)
                {
                    isFetching = false;
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Failed, "FetchRemoteConfig failed during fetch phase");
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Failed, "Remote config fetch failed");
                    OnConfigFetchFailed?.Invoke("Remote config fetch failed.");
                    return;
                }

                FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(activateTask =>
                {
                    isFetching = false;
                    if (activateTask.IsCanceled || activateTask.IsFaulted)
                    {
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Failed, "FetchRemoteConfig failed during activate phase");
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Failed, "Remote config activation failed");
                        OnConfigFetchFailed?.Invoke("Remote config activation failed.");
                        return;
                    }

                    var adConfigJson = FirebaseRemoteConfig.DefaultInstance.GetValue(AdConfigKey).StringValue;
                    TryApplyAdConfig(adConfigJson, false);

                    var adIdsConfigJson = FirebaseRemoteConfig.DefaultInstance.GetValue(AdIdsConfigKey).StringValue;
                    TryCacheAdIdsConfig(adIdsConfigJson);

                    var showCmpToUserRaw = FirebaseRemoteConfig.DefaultInstance.GetValue(ShowCMPToUserKey).StringValue;
                    TryApplyShowCMPToUser(showCmpToUserRaw);

                    DispatchCustomKeys();
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Initialized, "FetchRemoteConfig success: config fetched and activated");
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkRemoteConfig, DiagnosticStateAVNPlugin.Initialized, "Remote config fetched and activated");
                    OnConfigFetched?.Invoke();
                });
            });
        }

        private void DispatchCustomKeys()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loading, $"DispatchCustomKeys processing {registrations.Count} registrations");
            foreach (var pair in registrations)
            {
                var raw = ReadRemoteValue(pair.Value.KeyId, pair.Value.DataType);
                if (raw == null)
                {
                    continue;
                }

                PlayerPrefs.SetString(DT1AdsPluginPrefKeys.RemoteConfigCustom(pair.Value.KeyId), raw);
                pair.Value.Callback?.Invoke(raw);
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loaded, $"Dispatched custom key={pair.Value.KeyId} with value: {raw}");
            }

            PlayerPrefs.Save();
        }

        private string ReadRemoteValue(string keyId, RemoteDataType dataType)
        {
            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(keyId);
            switch (dataType)
            {
                case RemoteDataType.Int:
                    return value.LongValue.ToString();
                case RemoteDataType.Bool:
                    return value.BooleanValue ? "true" : "false";
                case RemoteDataType.Json:
                case RemoteDataType.String:
                default:
                    return value.StringValue;
            }
        }

        private void TryApplyAdConfig(string json, bool fromCache)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Failed, $"TryApplyAdConfig empty payload, fromCache={fromCache}");
                if (!fromCache)
                {
                    OnConfigFetchFailed?.Invoke("AdConfig was empty.");
                }
                return;
            }

            try
            {
                var config = JsonUtility.FromJson<PluginRemoteConfigData>(json);
                if (config == null)
                {
                    throw new InvalidOperationException("AdConfig json was invalid.");
                }

                config.Apply();
                // Note: RemoveAdInAppPrice is now handled by the purchase handler callback system
                // The "inappremoveadprice" key is registered separately via AVNPlugin.SetupRemoveAdsPriceCallback()
                PlayerPrefs.SetString(DT1AdsPluginPrefKeys.RemoteConfigAdConfig, json);
                PlayerPrefs.Save();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Loaded, $"TryApplyAdConfig success, fromCache={fromCache}");
                
                // Merge data with existing status to preserve AdIdsConfig
                var mergedData = new Dictionary<string, string> { { "AdConfig", json } };
                if (DiagnosticsHubAVNPlugin.TryGetStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, out var existingStatus) && 
                    existingStatus.Data != null)
                {
                    foreach (var kvp in existingStatus.Data)
                    {
                        if (!mergedData.ContainsKey(kvp.Key))
                        {
                            mergedData[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Loaded,
                    fromCache ? "AdConfig loaded from cache :" : "AdConfig updated from fetch :",
                    mergedData);
            }
            catch (Exception exception)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Failed, $"TryApplyAdConfig failed: {exception.Message}");
                Debug.LogError($"Failed to parse AdConfig: {exception.Message}");
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, DiagnosticStateAVNPlugin.Failed, exception.Message);
                if (!fromCache)
                {
                    OnConfigFetchFailed?.Invoke("Failed to parse AdConfig json.");
                }
            }
        }

        private void TryCacheAdIdsConfig(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Failed,
                    "AdIdsConfig was empty");
                return;
            }

            if (!TryDeserializeAdIdsConfig(json, out _))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Failed,
                    "AdIdsConfig json was invalid");
                return;
            }

            PlayerPrefs.SetString(DT1AdsPluginPrefKeys.RemoteConfigAdIdsConfig, json);
            PlayerPrefs.Save();
            
            // Merge data with existing status to preserve AdConfig
            var mergedData = new Dictionary<string, string> { { "AdIdsConfig", json } };
            if (DiagnosticsHubAVNPlugin.TryGetStatus(DiagnosticKeysAVNPlugin.RemoteConfigValues, out var existingStatus) && 
                existingStatus.Data != null)
            {
                foreach (var kvp in existingStatus.Data)
                {
                    if (!mergedData.ContainsKey(kvp.Key))
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.RemoteConfigValues,
                DiagnosticStateAVNPlugin.Loaded,
                "AdIdsConfig updated from fetch :" + json.ToString());
        }

        private static bool TryDeserializeAdIdsConfig(string json, out AdIdsRemoteConfigData config)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<AdIdsRemoteConfigData>(json);
                return config != null;
            }
            catch
            {
                return false;
            }
        }

        private void TryApplyShowCMPToUser(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Failed,
                    $"{ShowCMPToUserKey} was empty; keeping existing IsUserInCMPCountry={AVNPluginConstants.CMPEligibleUser}");
                return;
            }

            if (!bool.TryParse(rawValue, out var showCMPToUser))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.RemoteConfigValues,
                    DiagnosticStateAVNPlugin.Failed,
                    $"{ShowCMPToUserKey} parse failed for value='{rawValue}'; keeping existing IsUserInCMPCountry={AVNPluginConstants.CMPEligibleUser}");
                return;
            }

            AVNPluginConstants.CMPEligibleUser = showCMPToUser;
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.RemoteConfigValues,
                DiagnosticStateAVNPlugin.Loaded,
                $"Applied {ShowCMPToUserKey}={showCMPToUser} to IsUserInCMPCountry");
        }
    }
}
#endif
#if USE_AVNADS_PLUGIN 
    public static class DT1AdsPluginPrefKeys
    {
        public const string AdsEnabled = "DT1AdsPlugin_AdsEnabled";
        public const string RemoteConfigAdConfig = "DT1AdsPlugin_RC_AdConfig";
        public const string RemoteConfigAdIdsConfig = "DT1AdsPlugin_RC_AdIdsConfig";

        public static string RemoteConfigCustom(string keyId)
        {
            return $"DT1AdsPlugin_RC_{keyId}";
        }
    }
#endif