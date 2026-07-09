#if USE_AVNADS_PLUGIN && USE_FIREBASE
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Analytics;
using Firebase;
using Firebase.Crashlytics;
using AVN.AdsPlugin.Testing;

public class FirebaseInitilizationManager
{
    public event Action<string> OnFirebaseInitilizaFailed = null;
    public event Action<string> OnFirebaseInitilizaSuccess = null;

    public void startFirebaseInitialization()
    {
        DiagnosticsHubAVNPlugin.PublishStatus(
            DiagnosticKeysAVNPlugin.SdkFirebase,
            DiagnosticStateAVNPlugin.Initializing,
            "Starting Firebase initialization");
        firebaseAnalysis();
    }
    async void firebaseAnalysis()
    {

        try
        {
            Debug.Log("Checking Dependencies");
            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                var app = FirebaseApp.DefaultInstance;
                Crashlytics.IsCrashlyticsCollectionEnabled = true;

                IDictionary<Firebase.Analytics.ConsentType, Firebase.Analytics.ConsentStatus> consentValues = new Dictionary<Firebase.Analytics.ConsentType, Firebase.Analytics.ConsentStatus>
                                {
                                    { Firebase.Analytics.ConsentType.AdUserData, Firebase.Analytics.ConsentStatus.Granted },
                                    { Firebase.Analytics.ConsentType.AnalyticsStorage, Firebase.Analytics.ConsentStatus.Granted },
                                    { Firebase.Analytics.ConsentType.AdPersonalization, Firebase.Analytics.ConsentStatus.Granted },
                                    { Firebase.Analytics.ConsentType.AdStorage, Firebase.Analytics.ConsentStatus.Granted }
                                };
                Firebase.Analytics.FirebaseAnalytics.SetConsent(consentValues);

                Debug.Log("=================> Firebase Initialized Successfully =================>");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.SdkFirebase,
                    DiagnosticStateAVNPlugin.Initialized,
                    "Firebase initialized successfully");
                OnFirebaseInitilizaSuccess?.Invoke("Firebase Initialized Successfully");
                FirebaseAnalytics.SetUserId(AVNPluginConstants.UserID);
                //? GameAnalyticsEvents.UserAnalysis();
            }
            else
            {
                Debug.Log("=================> Firebase Initialization Failed =================>");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.SdkFirebase,
                    DiagnosticStateAVNPlugin.Failed,
                    $"Firebase initialization failed. Dependency status: {dependencyStatus}");
                OnFirebaseInitilizaFailed?.Invoke($"Firebase initialization failed. Dependency status: {dependencyStatus}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase initialization exception: {e.Message}\n{e}");
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.SdkFirebase,
                DiagnosticStateAVNPlugin.Failed,
                $"Firebase initialization exception: {e.Message}");
            OnFirebaseInitilizaFailed?.Invoke($"Firebase initialization exception: {e.Message}");
        }
    }
}
#endif