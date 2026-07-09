#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin.UserHandlers;
public class GameInApp : AVNHandlerSetup
{
#if USE_IAP
    public override void OnSetupCallbacks()
    {
        //* Consumable
        // SetProductPurchasedCallback("GoldBundle", () => InventoryManager.AddGold(500));

        //* Non-Consumable — wire purchase, restore, and refund
        // SetProductPurchasedCallback("VIPPass", () => GameConfig.IsVIP = true);
        // SetProductRestoredCallback ("VIPPass", () => GameConfig.IsVIP = true);
        // SetProductRefundedCallback ("VIPPass", () => GameConfig.IsVIP = false);

        //* Subscription
        // SetProductPurchasedCallback("WeeklyPass", () => SubscriptionManager.Activate());
        // SetProductRefundedCallback ("WeeklyPass", () => SubscriptionManager.Deactivate());
     
    }

    // ── RemoveAds (configured separately in the AVNPlugin inspector) ───────────

    protected override void OnRemoveAdsPurchased()
    {
        //* Ads are already disabled by the plugin at this point.

    }

    protected override void OnRemoveAdsRestored()
    {
        //* Called on reinstall when RemoveAds was previously purchased.
    }

    protected override void OnRemoveAdsRefunded()
    {
        //* Ads are re-enabled by the plugin at this point.
    }

    // ── General catch-alls ────────────────────────────────────────────────────

    protected override void OnProductPurchased(string productId)
    {
        //* Fires for every successful purchase.
        //* Prefer per-product callbacks above for product-specific logic.
        //* Analytics.Log(productId);
    }

    protected override void OnPurchaseRestored()
    {
        //* Full restore-purchases flow completed.
        //* UIManager.ShowRestoreSuccessMessage();
    }
#endif
}
#endif