#if USE_AVNADS_PLUGIN
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Analytics;
using AppsFlyerSDK;
using AVN.AdsPlugin;
using GoogleMobileAds.Api;
using AVN.AdsPlugin.Controllers;
using System.ComponentModel;
using Sirenix.OdinInspector;
public class FinzAnalysisManager : MonoBehaviour
{
    // Variables
    #region Variables

    public static FinzAnalysisManager Instance;
    // Property to access the instance
    public static FinzAnalysisManager instance
    {

        get
        {
            if (Instance == null)
            {
                //?Debug.LogError("FinzAnalysisManager instance is null. Make sure the instance is initialized before accessing it.");
                return null;
            }

            return Instance;
        }
    }
    private string _currentAdNetwork = "";
    public string CurrentAdNetworkForAnalysis
    {
        get => _currentAdNetwork;
        set
        {
            _currentAdNetwork = value;
        }
    }
    #endregion



    public void Awake()
    {


        // Ensure that only one instance of the singleton class exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }



    #region Ads Analysis
    public void BannerAdAnalysis(BannerAdTypes adType)
    {
        AdAnalysis(GetBannerEventName(adType));
    }

    //* Used by Interstitial and Rewarded Inter Ads and AppOpen Inter 
    public void InterShownAdAnalysis(AdType adType, int InterAdSlot = 0)
    {
        AdAnalysis(GetAdEventName(adType), InterAdSlot);
    }
    //* Used only By Reward Show
    public void RewardShownAdAnalysis()
    {
        AdAnalysis(GetAdEventName(AdType.REWARDED));
    }
    public void NoRewardAdAnalysis(string message)
    {
        AdAnalysis(GetAdEventName(AdType.NO_REWARD), 0, message);
    }
    //* Used by Interstitial and Rewarded Inter Ads and AppOpen Inter
    public void InterNotShownAdAnalysis(AdType adType, int InterAdSlot, string message)
    {
        AdAnalysis(GetAdEventName(adType), InterAdSlot, message);
    }

    private void AdAnalysis(string adType, int InterAdSlot = 0, string message = "")
    {
        try
        {
            switch (adType)
            {
                case "ad_interstitial":
                    Parameter[] parameters =
                    {
                        new Parameter("COUNT",AVNPluginConstants.InterCount.ToString()),
                        new Parameter("SLOT", InterAdSlot.ToString())
                    };
                    FirebaseAnalytics.LogEvent(adType, parameters);
                    break;
                case "ad_no_app_open":
                    Parameter[] openAdParameters =
                    {
                        new Parameter("ERROR", message)
                    };
                    FirebaseAnalytics.LogEvent(adType, openAdParameters);
                    break;
                case "ad_no_interstitial":
                    Parameter[] interstitialParameters =
                {
                        new Parameter("SLOT", InterAdSlot.ToString()),
                        new Parameter("ERROR", message)
                    };
                    FirebaseAnalytics.LogEvent(adType, interstitialParameters);
                    break;
                default:
                    FirebaseAnalytics.LogEvent(adType); // sending ads type send from Adcontroller                    
                    break;

            }

        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    string GetAdEventName(AdType adType)
    {
        switch (adType)
        {
            case AdType.INTERSTITIAL:
                return "ad_interstitial";
            case AdType.REWARDED:
                return "ad_rewarded";
            case AdType.LP_INTERSTITIAL_REWARDED:
                return "ad_interstitial_rewarded";
            case AdType.OPEN_AD:
                return "ad_app_open";
            case AdType.NO_INTERSTITIAL:
                return "ad_no_interstitial";
            case AdType.NO_APP_OPEN:
                return "ad_no_app_open";
            case AdType.NO_REWARD:
                return "ad_no_reward";
            case AdType.NO_INTERSTITIAL_REWARD:
                return "ad_no_interstitial_reward";
            default:
                return "unknown_ad_type";
        }
    }
    string GetBannerEventName(BannerAdTypes adType)
    {
        switch (adType)
        {
            case BannerAdTypes.BANNER:
                return "ad_banner";
            case BannerAdTypes.ADAPTIVE:
                return "ad_adaptive_banner";
            case BannerAdTypes.MREC:
                return "ad_mrec";
            default:
                return "unknown_banner_type";
        }
    }

    #endregion
    #region PaidAd Analytics
#if USE_LEVELPLAY
    public void PaidAdAnalytics(Unity.Services.LevelPlay.LevelPlayImpressionData impressionData)
    {
        if (impressionData != null)
        {
            Debug.Log("-------- IS Paid Ad Evemt " + impressionData.AdFormat + " " + impressionData.InstanceName);
        }
        try
        {

            Double revenue = (double)impressionData.Revenue;
            if (impressionData != null)
            {
                Firebase.Analytics.Parameter[] AdParameters = {
             new Firebase.Analytics.Parameter("ad_platform", "ironSource"),
              new Firebase.Analytics.Parameter("ad_source", impressionData.AdNetwork),
              new Firebase.Analytics.Parameter("ad_unit_name", impressionData.InstanceName),
            new Firebase.Analytics.Parameter("ad_format", impressionData.AdFormat),
              new Firebase.Analytics.Parameter("currency","USD"),
            new Firebase.Analytics.Parameter("value", revenue)
        };

                    FirebaseAnalytics.LogEvent("ad_impression", AdParameters);
            }

            Dictionary<string, string> additionalParams = new Dictionary<string, string>();

            additionalParams.Add(AdRevenueScheme.AD_UNIT, impressionData.InstanceName);
            additionalParams.Add(AdRevenueScheme.AD_TYPE, impressionData.AdFormat);
            var logRevenue = new AFAdRevenueData(impressionData.AdNetwork, MediationNetwork.IronSource, "USD", (double)impressionData.Revenue);
            AppsFlyer.logAdRevenue(logRevenue, additionalParams);
            Taichi1Event((double)impressionData.Revenue);
            Taichi2Event((double)impressionData.Revenue);
            //* custom condiion for event
            if (impressionData.AdFormat.Contains("inter") || impressionData.AdFormat.Contains("Inter"))
            {
                CurrentAdNetworkForAnalysis = impressionData.AdNetwork;
                AVNPlugin.DTInstance?.SendUserInterStatus(UserReturnStatus.SERVED_AD, CurrentAdNetworkForAnalysis);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }

    }
#endif
#if USE_MAX
    public void PaidAdAnalytics(string adString, MaxSdkBase.AdInfo adInfo)
    {

        double revenue = adInfo.Revenue;
        //* Firebase Analytics
        var impressionParameters = new[]
        {
                     new Parameter("ad_platform", "AppLovin"),
                     new Parameter("ad_source", adInfo.NetworkName),
                     new Parameter("ad_unit_name", adInfo.AdUnitIdentifier),
                     new Parameter("ad_format", adInfo.AdFormat),
                     new Parameter("value", revenue),
                     new Parameter("currency", "USD"), // All AppLovin revenue is sent in USD
                };
        FirebaseAnalytics.LogEvent("ad_impression", impressionParameters);
        //* Appsflyer Analytics
        Dictionary<string, string> additionalParams = new Dictionary<string, string>();

        additionalParams.Add(AdRevenueScheme.AD_UNIT, adInfo.AdFormat);
        additionalParams.Add(AdRevenueScheme.AD_TYPE, adInfo.AdUnitIdentifier);
        var logRevenue = new AFAdRevenueData(adInfo.NetworkName, MediationNetwork.ApplovinMax, "USD", (double)adInfo.Revenue);
        AppsFlyer.logAdRevenue(logRevenue, additionalParams);

        Taichi1Event((double)adInfo.Revenue);
        Taichi2Event((double)adInfo.Revenue);
    }
#endif
#if USE_ADMOB
    public void PaidAdAnalytics(string adString, ResponseInfo info, GoogleMobileAds.Api.AdValue adValue)
    {
        if (info != null && adValue != null)
        {
            DebugLogger.AVNLog("-------- ADMob Paid Ad Event " + info.GetMediationAdapterClassName() + " " + info.GetLoadedAdapterResponseInfo().AdSourceInstanceName);
        }
        // if adcontroller is not intialized or analytics are not enabled from user or any args or parameter is send with null
        if (info == null || adValue == null)
            return;
        try
        {
            decimal currentImpressionRevenue = (decimal)(adValue.Value / Mathf.Pow(10, 6)); // calculation impression revenue with 10^6 decimals
            decimal previousTroasCache = decimal.Parse(getAdValue(adString), System.Globalization.NumberStyles.Float); // previously cached troas
            decimal currentTroasCache = (decimal)(previousTroasCache + currentImpressionRevenue); // summing up previous and current troas to get estimated value

            Dictionary<string, string> additionalParams = new Dictionary<string, string>();

            additionalParams.Add(AdRevenueScheme.AD_UNIT, info.GetLoadedAdapterResponseInfo().AdSourceInstanceName);
            additionalParams.Add(AdRevenueScheme.AD_TYPE, adString);
            var logRevenue = new AFAdRevenueData(info.GetMediationAdapterClassName().ToString(), MediationNetwork.GoogleAdMob, adValue.CurrencyCode.ToString(),
                (double)currentImpressionRevenue);
            AppsFlyer.logAdRevenue(logRevenue, additionalParams);
            Taichi1Event((double)currentImpressionRevenue);
            Taichi2Event((double)currentImpressionRevenue);
            if (currentTroasCache >= (decimal)0.0) // avoiding minor values we do'nt need those
            {

                FirebaseAnalytics.LogEvent("ad_impression", // sending Paid events details to Firebase
               new Parameter("value", (double)currentTroasCache),
               new Parameter("currency", "" + adValue.CurrencyCode.ToString()),
               new Parameter("precision", "" + adValue.Precision.ToString()),
               new Parameter("network", "" + info.GetMediationAdapterClassName().ToString())
               ); ;

                //                CheckTheRevenue((double)currentTroasCache));
                setAdValue(adString, "0");
            }
            else
            {
                setAdValue(adString, currentTroasCache.ToString()); // else update Troas in cache
            }


        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }

    }
#endif
    #endregion

    #region Taichi 1 Event
    private double roasThreshold = 0.10; // Can be 0.05, 0.10, or 0.13 depending on the campaign

    // Enable or disable ROAS experiment logic
    public void Taichi1Event(double _rev)
    {
        double customRevenue;
        double currentAccumulatedRevenue;

        // Retrieve existing revenue from PlayerPrefs
        if (!double.TryParse(PlayerPrefs.GetString("revenue_add", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out customRevenue))
        {
            customRevenue = 0.0;
        }

        currentAccumulatedRevenue = customRevenue + _rev;

        // Update PlayerPrefs with new accumulated revenue
        PlayerPrefs.SetString("revenue_add", currentAccumulatedRevenue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();

        // Custom ROAS experimental event
        if (currentAccumulatedRevenue >= roasThreshold)
        {
            var roasParameters = new[]
            {
                new Parameter(FirebaseAnalytics.ParameterValue, currentAccumulatedRevenue),
                new Parameter(FirebaseAnalytics.ParameterCurrency, "USD")
            };
            FirebaseAnalytics.LogEvent("c_ad_impression_10", roasParameters);

            // Optional: Reset the revenue after firing the ROAS event (depending on your experiment)
            PlayerPrefs.SetString("revenue_add", "0");
            PlayerPrefs.Save();
        }
    }

    private void setAdValue(string str, string val)
    {
        PlayerPrefs.SetString("tROAS" + str, val); // Saving Troas values
    }

    private string getAdValue(string str)
    {
        return PlayerPrefs.GetString("tROAS" + str, "0"); // Getting Troas values
    }
    #endregion

    #region Taichi 2 Event
    private const string PREF_TROAS_CACHE = "TroasCache";
    private const string PREF_AD_COUNT = "AdCount";

    // Updated thresholds
    private readonly int[] AD_THRESHOLDS = { 4, 8, 12, 16, 20, 40 };

    public void Taichi2Event(double impressionRevenue)
    {
#if USE_FIREBASE
        float previousTroasCache = PlayerPrefs.GetFloat(PREF_TROAS_CACHE, 0f);
        int previousAdCount = PlayerPrefs.GetInt(PREF_AD_COUNT, 0);

        float currentTroasCache = previousTroasCache + (float)impressionRevenue;
        int currentAdCount = previousAdCount + 1;

        // Fire event if threshold reached
        foreach (int threshold in AD_THRESHOLDS)
        {
            if (currentAdCount == threshold)
            {
                //Debug.Log("UJ:: currentAdCount is " + currentAdCount);
                string eventName = GetEventName(threshold);
                //Debug.Log("UJ:: eventName is " + eventName);
                LogTroasFirebaseAdRevenueEvent(currentTroasCache, eventName);
                break; // Only fire once per impression
            }
        }

        // Reset after highest threshold (20)
        if (currentAdCount >= 20)
        {
            //Debug.Log("UJ:: currentAdCount >= 20 SO Resetting Prefs to 0 ");
            PlayerPrefs.SetFloat(PREF_TROAS_CACHE, 0f);
            PlayerPrefs.SetInt(PREF_AD_COUNT, 0);
        }
        else
        {
            //Debug.Log("UJ:: currentAdCount is less than threshold so we are just saving the current adcount and RoasCatch ");
            PlayerPrefs.SetFloat(PREF_TROAS_CACHE, currentTroasCache);
            PlayerPrefs.SetInt(PREF_AD_COUNT, currentAdCount);
        }

        PlayerPrefs.Save();
#endif
    }

#if USE_FIREBASE
    private string GetEventName(int threshold)
    {
        switch (threshold)
        {
            case 4: return "FourAdsShown";
            case 8: return "EightAdsShown";
            case 12: return "TwelveAdsShown";
            case 16: return "SixteenAdsShown";
            case 20: return "TwentyAdsShown";
            case 40: return "FortyAdsShown";
            default: return "AdsShown";
        }
    }

    private void LogTroasFirebaseAdRevenueEvent(float totalRevenue, string eventName)
    {
        Parameter[] parameters = {
            new Parameter(FirebaseAnalytics.ParameterValue, totalRevenue),
            new Parameter(FirebaseAnalytics.ParameterCurrency, "USD")
        };

        FirebaseAnalytics.LogEvent(eventName, parameters);

        Debug.Log($"[AdAggregator] Fired '{eventName}' with revenue: {totalRevenue}");
    }
#endif
    #endregion
}
#endif