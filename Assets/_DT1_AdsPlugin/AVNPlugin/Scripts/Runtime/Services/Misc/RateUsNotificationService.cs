#if USE_AVNADS_PLUGIN

using UnityEngine;
using System.Collections;
using AVN.AdsPlugin.Testing;

#if UNITY_ANDROID
using Google.Play.Review;
using Google.Play.Common;
#endif

namespace AVN.AdsPlugin.Services
{
    public sealed class RateUsNotificationService
    {
        private MonoBehaviour _coroutineRunner;

        public RateUsNotificationService(MonoBehaviour coroutineRunner = null)
        {
            _coroutineRunner = coroutineRunner;
        }

        public void SetCoroutineRunner(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }
        public void ShowRateUs()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Loading, "Attempting to show native Rate Us prompt");
#if UNITY_IOS
            ShowRateUsIOS();
#elif UNITY_ANDROID
            ShowRateUsAndroid();
#else
           DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Failed, "Rate Us not supported on this platform");
#endif
        }

#if UNITY_IOS
        private void ShowRateUsIOS()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Loading, "Requesting app rate us on iOS");
            try
            {
                UnityEngine.iOS.Device.RequestStoreReview();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Loaded, "App rate us request sent to iOS");
            }
            catch (System.Exception e)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Failed, $"Failed to request app rate us on iOS: {e.Message}");
            }
        }
#elif UNITY_ANDROID
        private void ShowRateUsAndroid()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Loading, "Requesting in-app review flow on Android");
            _coroutineRunner.StartCoroutine(ShowRateUsAndroidCoroutine());
        }

        private IEnumerator ShowRateUsAndroidCoroutine()
        {
            ReviewManager reviewManager = new ReviewManager();

            PlayAsyncOperation<PlayReviewInfo, ReviewErrorCode> requestFlowTask = reviewManager.RequestReviewFlow();
            yield return requestFlowTask;

            if (requestFlowTask.Error != ReviewErrorCode.NoError)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Failed, $"Failed to request review info on Android: {requestFlowTask.Error}");

            }

            PlayReviewInfo reviewInfo = requestFlowTask.GetResult();
            PlayAsyncOperation<VoidResult, ReviewErrorCode> launchFlowTask = reviewManager.LaunchReviewFlow(reviewInfo);
            yield return launchFlowTask;

            if (launchFlowTask.Error != ReviewErrorCode.NoError)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Failed, $"Failed to launch review flow on Android: {launchFlowTask.Error}");
            }
            else
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.NativeRateUs, DiagnosticStateAVNPlugin.Loaded, "In-app review flow launched on Android");
            }
        }
#endif

        public void ShowNotification()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loading, "Checking notification permission");

#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
            {
                //AppOpenAdManager.ResumeFromAds = true; - manualcall
                UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loaded, "Notification permission requested on Android");
            }
            else
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loaded, "Notification permission already granted on Android");
            }
#elif UNITY_IOS
            StartCloudMessaging();
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loaded, "Notification permission flow started on iOS");
#endif
        }
#if UNITY_IOS
    public static void StartCloudMessaging()
    {
        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loading, "Starting Firebase Cloud Messaging for iOS");
        Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
        Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
    }

    static void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
    {
        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loaded, "Received notification token on iOS");
    }

    static void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
    {
        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.Notifcation, DiagnosticStateAVNPlugin.Loaded, "Received a new notification message on iOS");    
    }
#endif
    }

}
#endif