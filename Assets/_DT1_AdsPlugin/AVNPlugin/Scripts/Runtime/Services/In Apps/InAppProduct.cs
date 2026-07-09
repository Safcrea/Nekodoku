#if USE_AVNADS_PLUGIN
using System;
using UnityEngine;
#if USE_IAP
using UnityEngine.Purchasing;
#endif
namespace AVN.AdsPlugin.Services
{
    /// <summary>
    /// Defines an in-app product to register with the purchasing service.
    /// Assign these in the AVNPlugin inspector under "In-App Purchasing".
    /// </summary>
    #if USE_IAP
    [Serializable]
    public struct InAppProduct
    {
        /// <summary>Friendly identifier used at runtime — e.g. "500Coins", "RemoveAds".</summary>
        public string ProductName;

        /// <summary>The Unity IAP product type (Consumable, NonConsumable, Subscription).</summary>
        public ProductType ProductType;
        /// <summary>The store-specific product ID — e.g. "com.studio.game.500coins".</summary>
        public string ProductId;
    }
    #endif
}
#endif
