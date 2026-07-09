#if USE_AVNADS_PLUGIN
using System;
using System.Collections.Generic;
using UnityEngine;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.Services;

namespace AVN.AdsPlugin.UserHandlers
{
    /// <summary>
    /// Abstract MonoBehaviour wiring bridge.
    /// Attach ONE concrete subclass (e.g. GameHandlerSetup) to the AVNPlugin prefab.
    /// Override only the methods your game needs — all default to safe no-op / allow-all behaviour.
    /// AVNPlugin.Awake() discovers this component automatically via GetComponentInChildren.
    /// </summary>
    public abstract class AVNHandlerSetup
    {
        //------------------ Ad Rule Thresholds ------------------//
        // Read-only accessors sourced from AVNPlugin (inspector + remote-config driven).

        protected static int EnableInterFromLevel => AVNPlugin.PluginInstance?.EnableInterFromLevel ?? 3;
        protected static int ShowRateUsOnLevel => AVNPlugin.PluginInstance?.ShowRateUsOnLevel ?? 4;
        protected static int ShowNotificationOnLevel => AVNPlugin.PluginInstance?.ShowNotificationOnLevel ?? 5;

        //------------------ AVNPlugin Checks ------------------//

        /// <summary>Return false to block interstitial ads from showing.</summary>
        public virtual bool CanShowInterstitial() => true;

        /// <summary>Return false to block the first inter ad delay.</summary>
        public virtual bool CanUseFirstAdDelay() => true;

        /// <summary>Return false to block the Rate Us dialog.</summary>
        public virtual bool CanShowRateUsDialog() => true;

        /// <summary>Return false to block the notification prompt.</summary>
        public virtual bool CanShowNotification() => true;


        //------------------ Remote Config ------------------//

        /// <summary>Override to call RegisterKey() for each custom remote config key you need.</summary>
        protected virtual void OnRegisterKeys() { }

        /// <summary>Called after every successful Firebase Remote Config fetch.</summary>
        protected virtual void OnConfigFetched() { }

        /// <summary>
        /// Registers a custom Remote Config key. The callback fires immediately if a cached
        /// value is present in PlayerPrefs, then again when fresh data arrives from Firebase.
        /// Call only inside OnRegisterKeys().
        /// </summary>
#pragma warning disable CS0618
        protected void RegisterKey(string keyId, RemoteDataType type, Action<string> onFetched)
            => AVNPlugin.DTInstance?.RegisterCustomRemoteConfigKey(keyId, type, onFetched);
#pragma warning restore CS0618

#if USE_IAP
        //------------------ In-App Purchasing ------------------//
        /// <summary>
        /// Override to register per-product callbacks using SetProduct*Callback().
        /// Called after the purchasing service is created, before the store connection opens.
        /// </summary>
        public virtual void OnSetupCallbacks() { }

        /// <summary>Called when RemoveAds is purchased for the first time. Ads are already disabled.</summary>
        protected virtual void OnRemoveAdsPurchased() { }

        /// <summary>Called when RemoveAds is restored on reinstall. Ads are already disabled.</summary>
        protected virtual void OnRemoveAdsRestored() { }

        /// <summary>Called when RemoveAds is refunded. Ads are re-enabled.</summary>
        protected virtual void OnRemoveAdsRefunded() { }

        /// <summary>Called for every successful purchase. productId is the raw store product ID.</summary>
        protected virtual void OnProductPurchased(string productId) { }

        /// <summary>Called after the full restore-purchases flow completes.</summary>
        protected virtual void OnPurchaseRestored() { }

        /// <summary>Register a callback that fires when a named product is purchased. Call inside OnSetupCallbacks().</summary>
#pragma warning disable CS0618
        protected void SetProductPurchasedCallback(string productName, Action callback)
            => AVNPlugin.DTInstance?.RegisterProductPurchasedCallback(productName, callback);

        /// <summary>Register a callback that fires when a named product is restored. Call inside OnSetupCallbacks().</summary>
        protected void SetProductRestoredCallback(string productName, Action callback)
            => AVNPlugin.DTInstance?.RegisterProductRestoredCallback(productName, callback);
        protected void SetProductExpiredCallback(string productName, Action callback)
            => AVNPlugin.DTInstance?.SetProductExpiredCallback(productName, callback);

        /// <summary>Register a callback that fires when a named product is refunded. Call inside OnSetupCallbacks().</summary>
        protected void SetProductRefundedCallback(string productName, Action callback)
            => AVNPlugin.DTInstance?.RegisterProductRefundedCallback(productName, callback);
#pragma warning restore CS0618
#endif
        //------------------ Internal dispatch (called by AVNPlugin) ------------------//

        internal void SetupRemoteConfig() => OnRegisterKeys();
        internal void NotifyConfigFetched() => OnConfigFetched();
#if USE_IAP
        internal void DispatchRemoveAdsPurchased() => OnRemoveAdsPurchased();
        internal void DispatchRemoveAdsRestored() => OnRemoveAdsRestored();
        internal void DispatchRemoveAdsRefunded() => OnRemoveAdsRefunded();
        internal void DispatchProductPurchased(string productId) => OnProductPurchased(productId);
        internal void DispatchPurchaseRestored() => OnPurchaseRestored();
#endif
    }
}
#endif
