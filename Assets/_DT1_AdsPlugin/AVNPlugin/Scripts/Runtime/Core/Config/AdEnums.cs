#if USE_AVNADS_PLUGIN
namespace AVN.AdsPlugin
{
    public enum BannerAdTypes
    {
        BANNER, ADAPTIVE, MREC
    }

    public enum AdType
    {
        INTERSTITIAL, REWARDED, LP_INTERSTITIAL_REWARDED, OPEN_AD, NO_INTERSTITIAL, NO_APP_OPEN, NO_REWARD, NO_INTERSTITIAL_REWARD
    }
    public enum BannerPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
    public enum PluginInitMode
    {
        StartOnInit,
        StartByCMP,
        StartManually
    }
}
#endif
