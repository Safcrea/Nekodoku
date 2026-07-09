using System.Collections;
#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin.Controllers;
#endif
using DG.Tweening;
using TMPro;
using UnityEngine;

public class NoInternetPopup : MonoBehaviour
{
    [SerializeField] private Transform Background;
    [SerializeField] private Transform noInternetPopup;
    [SerializeField] private int checkInterval;
    [SerializeField] private TextMeshProUGUI buttonText;
#if USE_AVNADS_PLUGIN
    // Start is called before the first frame update
    void Start()
    {
        InvokeRepeating(nameof(CheckInternetConnectivity), 2, checkInterval);
        if (buttonText == null) return;
        if (GetAndroidApiLevel() < 29)
            buttonText.text = "Enable WiFi";
        else
            buttonText.text = "Connect";
    }

    public bool CheckInternetConnectivity()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.NotReachable:
                {
                    if (Background.gameObject.activeInHierarchy) return false;

                    OpenPopup();
                    return false;
                }
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                {

                    return true;
                }
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                {

                    return true;
                }
            default:
                return true;
        }
    }
    void OpenPopup()
    {
        Time.timeScale = 0f;
        noInternetPopup.localScale = Vector3.one * 0.8f;
        Background.gameObject.SetActive(true);
        noInternetPopup.DOScale(Vector3.one, 1f).SetEase(Ease.InOutBack).SetUpdate(true);
        StartCoroutine(nameof(RetryCheck));
        AVNPlugin.PluginInstance.SuppressAppOpenInterstitial();

    }
    public void ClosePopup()
    {
        StopCoroutine(nameof(RetryCheck));
        Background.gameObject.SetActive(false);
        Time.timeScale = 1f;


        // Reset a previously-failed AdMob SDK state so the retry sequence can
        // actually call MobileAds.Initialize again (instead of immediately returning false).
#if USE_ADMOB
        AVN.AdsPlugin.Services.AdMobSdkInitializer.Reset();
#endif
#if USE_AVNADS_PLUGIN
        AVNPlugin.PluginInstance?.RetryInitializationIfNeeded();
        AVNPlugin.PluginInstance?.UnsuppressAppOpenInterstitial();
#endif
    }

    public IEnumerator RetryCheck()
    {
        while (true)
        {
            switch (Application.internetReachability)
            {
                case NetworkReachability.ReachableViaCarrierDataNetwork:
                case NetworkReachability.ReachableViaLocalAreaNetwork:
                    {

                        noInternetPopup.localScale = Vector3.zero;
                        ClosePopup();
                        yield break;   // Exit the coroutine
                    }
                case NetworkReachability.NotReachable:
                    {

                        yield return new WaitForSecondsRealtime(checkInterval);  // Use realtime to work during pause
                        break;
                    }
            }
        }
    }

    public int GetAndroidApiLevel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return versionClass.GetStatic<int>("SDK_INT");
            }
#else
        return -1;
#endif
    }

    public void OpenWifiSettings()
    {
#if UNITY_ANDROID //&& !UNITY_EDITOR
        try
        {
            using AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", "android.settings.panel.action.INTERNET_CONNECTIVITY"))
            {
                activity.Call("startActivity", intent);
            }
        }
        catch (System.Exception)
        {
            // Try to enable WiFi automatically
            try
            {
                using (AndroidJavaObject contextObject = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject wifiManager = contextObject.Call<AndroidJavaObject>("getSystemService", new AndroidJavaClass("android.content.Context").GetStatic<string>("WIFI_SERVICE")))
                {
                    AVNPlugin.PluginInstance.GenericLoaderManager.ShowLoader("Enabling WiFi...", 4f);
                    bool result = wifiManager.Call<bool>("setWifiEnabled", true);
                    if (result)
                    {

                    }
                    else
                    {

                    }
                }
            }
            catch (System.Exception)
            {
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
    try
    {
        // Open the Settings app
        // Application.OpenURL("App-Prefs:root=WIFI"); // Deep link to WiFi settings
        AVNPlugin.DTInstance.GenericLoaderManager.ShowLoader("Enable WiFi", 5f);

    }
    catch (System.Exception e)
    {
        Debug.LogWarning("Cannot open WiFi settings on iOS: " + e.Message);
    }
#else
        Debug.Log("OpenWifiSettings is only supported on Android devices.");
#endif
    }
#endif

}
