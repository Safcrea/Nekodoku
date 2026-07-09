#if USE_AVNADS_PLUGIN
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if USE_IAP
using UnityEngine.Purchasing;
#endif
using AppsFlyerSDK;
using Firebase.Analytics;

using AVN.AdsPlugin.Testing;
using AVN.AdsPlugin.Controllers;

namespace AVN.AdsPlugin.Services
{
    /// <summary>
    /// Manages in-app purchasing through Unity Purchasing v5.
    /// Products are registered by name (e.g. "500Coins") and looked up via BuyProduct / GetProductPrice.
    /// RemoveAds is handled separately with dedicated callbacks on AVNPlugin.
    /// </summary>
    public sealed class InAppPurchasingService : IDisposable
    {
        #if  USE_IAP
        #region Fields & Internal Types
        private sealed class ProductEntry
        {
            public string ProductId;
            public string ProductName;
            public ProductType Type;
            public Action OnPurchased;
            public Action OnRestored;
            public Action OnRefunded;
            public Action OnExpired;
        }
        private const string PRODUCT_ID_PARAMETER = "product_id";
        private readonly Dictionary<string, ProductEntry> productsByName = new();
        private readonly Dictionary<string, ProductEntry> productsById   = new();

        private string[] consumableIds    = Array.Empty<string>();
        private string[] nonConsumableIds = Array.Empty<string>();
        private string[] subscriptionIds  = Array.Empty<string>();

        private const string OwnedProductKeyPrefix = "IAP_Owned_";

        private StoreController storeController;
        private int maxRetryAttempts = 3;
        private float retryDelay = 1f;
        private int currentRetryCount;
        private AnalyticsEventService gameEventService;
        private bool isDisposed;
        private bool subscriptionCheckOnly;
        private bool autoRestoreNonConsumablesOnInit = false;
        private bool isInitFetch = false;

        #endregion

        #region Events & State

        public event Action<string> OnPurchaseSuccessful;
        public event Action OnRestoreSuccessful;
        public event Action<string> PurchaseFailedCallback;
        public event Action<string> OnInitializationFailed;
        public event Action<string> OnSubscriptionExpired;

        public bool IsInitialized => storeController != null;

        #endregion

        #region Configuration

        public void SetGameEventService(AnalyticsEventService service)
        {
            gameEventService = service;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, "SetGameEventService assigned");
        }

        public void SetRetryConfig(int maxRetries, float delay)
        {
            maxRetryAttempts = maxRetries;
            retryDelay = delay;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, $"SetRetryConfig maxRetries={maxRetryAttempts}, retryDelay={retryDelay}");
        }

        public void SetAutoRestoreNonConsumablesOnInit(bool enabled)
        {
            autoRestoreNonConsumablesOnInit = enabled;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, $"SetAutoRestoreNonConsumablesOnInit: {enabled}");
        }

        #endregion

        #region Product Registration

        /// <summary>
        /// Registers a product for purchasing. Call before Initialize().
        /// Use ProductName as the identifier in BuyProduct / GetProductPrice.
        /// </summary>
        public void RegisterProduct(InAppProduct product)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId) || string.IsNullOrWhiteSpace(product.ProductName))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "RegisterProduct: skipped — ProductId or ProductName is empty");
                return;
            }

            var entry = new ProductEntry
            {
                ProductId    = product.ProductId,
                ProductName  = product.ProductName,
                Type         = product.ProductType
            };

            productsByName[product.ProductName] = entry;
            productsById[product.ProductId]     = entry;

            switch (product.ProductType)
            {
                case ProductType.Consumable:    AppendToArray(ref consumableIds,    product.ProductId); break;
                case ProductType.NonConsumable: AppendToArray(ref nonConsumableIds, product.ProductId); break;
                case ProductType.Subscription:  AppendToArray(ref subscriptionIds,  product.ProductId); break;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, $"RegisterProduct: {product.ProductName} => {product.ProductId} ({product.ProductType})");
        }

        /// <summary>Sets the callback invoked when a product is successfully purchased.</summary>
        public void SetPurchasedCallback(string productName, Action callback)
        {
            var entry = GetEntryByName(productName);
            if (entry != null) entry.OnPurchased = callback;
            else DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"SetPurchasedCallback: '{productName}' not registered");
        }

        /// <summary>Sets the callback invoked when a product purchase is restored.</summary>
        public void SetRestoredCallback(string productName, Action callback)
        {
            var entry = GetEntryByName(productName);
            if (entry != null) entry.OnRestored = callback;
            else DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"SetRestoredCallback: '{productName}' not registered");
        }

        /// <summary>Sets the callback invoked when a product is refunded.</summary>
        public void SetRefundedCallback(string productName, Action callback)
        {
            var entry = GetEntryByName(productName);
            if (entry != null) entry.OnRefunded = callback;
            else DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"SetRefundedCallback: '{productName}' not registered");
        }

        /// <summary>Sets the callback invoked when a subscription expires (was active, now absent from store orders).</summary>
        public void SetExpiredCallback(string productName, Action callback)
        {
            var entry = GetEntryByName(productName);
            if (entry != null) entry.OnExpired = callback;
            else DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"SetExpiredCallback: '{productName}' not registered");
        }

        #endregion

        #region Price

        /// <summary>
        /// Returns the localized store price when initialized, otherwise "0.00".
        /// </summary>
        public string GetProductPrice(string productName)
        {
            var entry = GetEntryByName(productName);
            if (entry == null) return "0.00";

            if (IsInitialized)
            {
                var product = storeController.GetProducts()
                    ?.FirstOrDefault(p => p.definition.id == entry.ProductId);
                if (product != null && !string.IsNullOrWhiteSpace(product.metadata.localizedPriceString))
                    return product.metadata.localizedPriceString;
            }

            return "0.00";
        }

        /// <summary>Returns the ISO currency code for a product. Pass isProductName=false to look up by store product ID.</summary>
        public string GetCurrencyCode(string identifier, bool isProductName)
        {
            var entry = isProductName ? GetEntryByName(identifier) : GetEntryById(identifier);
            if (entry == null) return "USD";

            if (IsInitialized)
            {
                var product = storeController.GetProducts()
                    ?.FirstOrDefault(p => p.definition.id == entry.ProductId);
                if (product != null && !string.IsNullOrWhiteSpace(product.metadata.isoCurrencyCode))
                    return product.metadata.isoCurrencyCode;
            }

            return "USD";
        }

        /// <summary>
        /// Returns a clean numeric price string for analytics (e.g. "0.99").
        /// Pass isProductName=false to look up by store product ID.
        /// Returns "0.00" if the store price is not available.
        /// </summary>
        public string GetProductPrice(string identifier, bool isProductName)
        {
            var entry = isProductName ? GetEntryByName(identifier) : GetEntryById(identifier);
            if (entry == null) return "0.00";

            if (IsInitialized)
            {
                var product = storeController.GetProducts()
                    ?.FirstOrDefault(p => p.definition.id == entry.ProductId);
                if (product != null)
                {
                    if (product.metadata.localizedPrice != decimal.Zero)
                        return product.metadata.localizedPrice.ToString("F2", CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(product.metadata.localizedPriceString))
                    {
                        var parsed = ParseNumericPrice(product.metadata.localizedPriceString);
                        if (parsed.HasValue)
                            return parsed.Value.ToString("F2", CultureInfo.InvariantCulture);
                    }
                }
            }

            return "0.00";
        }
        //* Use this to get Subscrip period in form of Week, 2 Days, 3 Years etc. 
        public string GetSubscriptionPeriodLabel(string productName) => TimeSpanToPeriodLabel(GetSubscriptionPeriod(productName) ?? TimeSpan.Zero);
        /// <summary>
        /// Returns the subscription billing period for a registered subscription product.
        /// Returns null if not found, not a subscription, or the period cannot be determined.
        /// </summary>
        public TimeSpan? GetSubscriptionPeriod(string productName)
        {
            var entry = GetEntryByName(productName);
            if (entry == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"GetSubscriptionPeriod: '{productName}' not registered");
                return null;
            }

            if (entry.Type != ProductType.Subscription)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"GetSubscriptionPeriod: '{productName}' is not a subscription");
                return null;
            }

            if (!IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "GetSubscriptionPeriod: service not initialized");
                return null;
            }

            var product = storeController.GetProducts()?.FirstOrDefault(p => p.definition.id == entry.ProductId);
            if (product == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"GetSubscriptionPeriod: store product not found for '{productName}'");
                return null;
            }

            try
            {
                string iso8601Period = null;
#if UNITY_ANDROID
                try
                {
                    var googleMetadata = product.metadata.GetGoogleProductMetadata();
                    iso8601Period = googleMetadata?.subscriptionPeriod;
                }
                catch { }
#endif
                if (!string.IsNullOrWhiteSpace(iso8601Period))
                {
                    var parsed = ParseIso8601Period(iso8601Period);
                    if (parsed > TimeSpan.Zero) return parsed;
                }

                return null;
            }
            catch (Exception ex)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"GetSubscriptionPeriod exception: {ex.Message}");
                return null;
            }
        }

        private static decimal? ParseNumericPrice(string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText)) return null;

            var cleaned = new string(priceText.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-' || c == '+').ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) return null;

            if (cleaned.Count(c => c == '.') > 0 && cleaned.Count(c => c == ',') > 0)
            {
                var lastDot   = cleaned.LastIndexOf('.');
                var lastComma = cleaned.LastIndexOf(',');
                cleaned = lastDot > lastComma
                    ? cleaned.Replace(",", string.Empty)
                    : cleaned.Replace(".", string.Empty).Replace(",", ".");
            }
            else if (cleaned.Count(c => c == ',') > 0)
            {
                cleaned = cleaned.Replace(",", ".");
            }

            if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }

        /// <summary>
        /// Parses an ISO 8601 duration string (e.g. "P1W", "P1M", "P3M", "P1Y") into a TimeSpan.
        /// Supported units: D (days), W (weeks), M (months ~30 days), Y (years ~365 days).
        /// Returns TimeSpan.Zero for unrecognised formats.
        /// </summary>
        private static TimeSpan ParseIso8601Period(string iso8601)
        {
            if (string.IsNullOrWhiteSpace(iso8601) || iso8601[0] != 'P')
                return TimeSpan.Zero;

            var span = iso8601.AsSpan(1);
            double total = 0;
            int start = 0;

            for (int i = 0; i <= span.Length; i++)
            {
                if (i == span.Length || !char.IsDigit(span[i]))
                {
                    if (i > start && i < span.Length)
                    {
                        if (double.TryParse(span.Slice(start, i - start).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                        {
                            switch (span[i])
                            {
                                case 'D': total += num;        break;
                                case 'W': total += num * 7;    break;
                                case 'M': total += num * 30;   break;
                                case 'Y': total += num * 365;  break;
                            }
                        }
                    }
                    start = i + 1;
                }
            }

            return TimeSpan.FromDays(total);
        }

        #endregion

        #region Purchase Operations

        /// <summary>
        /// Initiates a purchase using the product's friendly ProductName (e.g. "500Coins").
        /// </summary>
        public void BuyProduct(string productName)
        {
            var entry = GetEntryByName(productName);
            if (entry == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"BuyProduct: '{productName}' not registered");
                PurchaseFailedCallback?.Invoke($"Product not found: {productName}");
                return;
            }
            BuyProductById(entry.ProductId);
        }

        /// <summary>Initiates a purchase using the raw store product ID.</summary>
        public void BuyProductById(string productId)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"BuyProductById: {productId}");
            if (string.IsNullOrWhiteSpace(productId))
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Product ID is empty");
                PurchaseFailedCallback?.Invoke("Invalid product ID");
                return;
            }

            if (!IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Purchasing not initialized");
                PurchaseFailedCallback?.Invoke("Purchasing not initialized");
                Initialize();
                return;
            }

            var product = storeController.GetProducts()?.FirstOrDefault(p => p.definition.id == productId);
            if (product == null || !product.availableToPurchase)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"Product not found or unavailable: {productId}");
                PurchaseFailedCallback?.Invoke($"Product unavailable: {productId}");
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"Initiating purchase: {productId}");
            storeController.PurchaseProduct(product);
        }

        /// <summary>Manually fires the refund handler for a product by its ProductName.</summary>
        public void ProcessProductRefund(string productName)
        {
            var entry = GetEntryByName(productName);
            if (entry == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"ProcessProductRefund: '{productName}' not registered");
                return;
            }
            entry.OnRefunded?.Invoke();
            DispatchRefundEvent(entry.ProductId);
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"ProcessProductRefund: {productName}");
        }

        /// <summary>Triggers a purchase restore (IAP v5 FetchPurchases).</summary>
        public void RestorePurchases()
        {
            if (!IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Purchasing not initialized");
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "Restoring purchases");
            storeController.FetchPurchases();
        }

        /// <summary>
        /// Fetches store purchases and restores non-consumable products not yet recorded locally.
        /// Active non-consumable products will have OnRestored invoked.
        /// </summary>
        public void CheckAndRestoreNonConsumables()
        {
            if (!IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "CheckAndRestoreNonConsumables: service not initialized");
                return;
            }

            if (nonConsumableIds.Length == 0)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, "CheckAndRestoreNonConsumables: no non-consumable products registered");
                return;
            }

            subscriptionCheckOnly = false;
            storeController.FetchPurchases();
        }

        /// <summary>
        /// Fetches store purchases and checks subscription status only.
        /// Active subscriptions not yet recorded locally will have OnRestored invoked.
        /// Previously active subscriptions no longer in store orders will have OnExpired invoked and OnSubscriptionExpired raised.
        /// Non-consumable products are not touched.
        /// </summary>
        public void CheckAndRestoreSubscriptions()
        {
            if (!IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "CheckAndRestoreSubscriptions: service not initialized");
                return;
            }

            if (subscriptionIds.Length == 0)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Idle, "CheckAndRestoreSubscriptions: no subscriptions registered");
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "CheckAndRestoreSubscriptions: fetching purchases");
            subscriptionCheckOnly = true;
            storeController.FetchPurchases();
        }

        #endregion

        #region Initialization

        public async void Initialize()
        {
            if (isDisposed)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Initialize ignored: service disposed");
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initializing, "Initialize requested");
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initializing, "Initializing purchasing service (IAP v5)");

            if (IsInitialized)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initialized, "Initialize ignored: already initialized");
                return;
            }

            try
            {
                if (storeController != null)
                    UnsubscribeFromStoreEvents(storeController);

                storeController = UnityIAPServices.StoreController();

                if (storeController == null)
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Initialize failed: StoreController unavailable");
                    HandleInitializationFailure("StoreController unavailable");
                    return;
                }

                SubscribeToStoreEvents(storeController);

                await storeController.Connect();
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "Initialize: connected to store");
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "Connected to store, fetching products");

                var productDefs = BuildProductDefinitions();
                if (productDefs.Count > 0)
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"Initialize: fetching {productDefs.Count} products");
                    storeController.FetchProducts(productDefs);
                }
                else
                {
                    currentRetryCount = 0;
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initialized, "Initialize: no products configured");
                    OnRestoreSuccessful?.Invoke();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"Initialize exception: {ex.Message}");
                HandleInitializationFailure(ex.Message);
            }
            PurchaseFailedCallback += (message) => AVNPlugin.PluginInstance.ToastManager.ShowToast("Purchase Unsuccessful!", Color.red);
            OnPurchaseSuccessful += (message) => AVNPlugin.PluginInstance.ToastManager.ShowToast("Purchase Successful!", Color.green);
        }

        private List<ProductDefinition> BuildProductDefinitions()
        {
            var list = new List<ProductDefinition>();
            foreach (var id in consumableIds)
                if (!string.IsNullOrWhiteSpace(id))
                    list.Add(new ProductDefinition(id, ProductType.Consumable));
            foreach (var id in nonConsumableIds)
                if (!string.IsNullOrWhiteSpace(id))
                    list.Add(new ProductDefinition(id, ProductType.NonConsumable));
            foreach (var id in subscriptionIds)
                if (!string.IsNullOrWhiteSpace(id))
                    list.Add(new ProductDefinition(id, ProductType.Subscription));
            return list;
        }

        #endregion

        #region Store Event Handlers

        private void HandleProductsFetched(List<Product> products)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"Products fetched: {products.Count}");
            isInitFetch = true;
            storeController.FetchPurchases();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "HandleProductsFetchFailed");
            HandleInitializationFailure("Product fetch failed");
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "HandlePurchasesFetched");
            currentRetryCount = 0;

            if (orders == null)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "HandlePurchasesFetched: orders is null");
                OnRestoreSuccessful?.Invoke();
                return;
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"HandlePurchasesFetched: confirmed orders count = {orders.ConfirmedOrders?.Count ?? 0}");
            DetectAndProcessRefunds(orders);

            // Build set of confirmed owned product IDs
            var ownedIds = new HashSet<string>();
            foreach (var confirmedOrder in orders.ConfirmedOrders)
            {
                var purchasedProducts = confirmedOrder.Info?.PurchasedProductInfo;
                if (purchasedProducts == null) continue;
                foreach (var info in purchasedProducts)
                {
                    if (string.IsNullOrWhiteSpace(info.productId)) continue;
                    ownedIds.Add(info.productId);
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"HandlePurchasesFetched: found owned productId = {info.productId}");
                }
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"HandlePurchasesFetched: total owned product IDs = {ownedIds.Count}");

            // Restore non-consumables (skipped when only checking subscriptions, or when init fetch and auto-restore is disabled)
            bool prefsDirty = false;
            bool skipNonConsumableRestore = subscriptionCheckOnly || (isInitFetch && !autoRestoreNonConsumablesOnInit);
            isInitFetch = false;
            if (!skipNonConsumableRestore)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "HandlePurchasesFetched: restoring non-consumables");
                foreach (var entry in productsById.Values)
                {
                    if (entry.Type != ProductType.NonConsumable) continue;
                    if (!ownedIds.Contains(entry.ProductId))
                    {
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Non-consumable not owned: {entry.ProductId}");
                        continue;
                    }

                    bool alreadyRecorded = PlayerPrefs.GetInt(OwnedProductKeyPrefix + entry.ProductId, 0) == 1;
                    if (alreadyRecorded)
                    {
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Non-consumable already recorded: {entry.ProductId}");
                        continue;
                    }

                    PlayerPrefs.SetInt(OwnedProductKeyPrefix + entry.ProductId, 1);
                    prefsDirty = true;
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Non-consumable restored: {entry.ProductId}");
                    entry.OnRestored?.Invoke();
                    DispatchRestoreEvent(entry.ProductId);
                }
            }
            else
            {
                var skipReason = subscriptionCheckOnly ? "subscriptionCheckOnly" : "autoRestoreNonConsumablesOnInit is false (init fetch)";
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"HandlePurchasesFetched: skipping non-consumable restore ({skipReason})");
            }

            subscriptionCheckOnly = false;

            // Handle subscriptions: restore active ones, detect expired ones
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "HandlePurchasesFetched: checking subscriptions");
            foreach (var entry in productsById.Values)
            {
                if (entry.Type != ProductType.Subscription) continue;

                bool isCurrentlyActive  = ownedIds.Contains(entry.ProductId);
                bool wasPreviouslyOwned = PlayerPrefs.GetInt(OwnedProductKeyPrefix + entry.ProductId, 0) == 1;

                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription check: {entry.ProductId}, currentlyActive={isCurrentlyActive}, previouslyOwned={wasPreviouslyOwned}");

                if (isCurrentlyActive)
                {
                    if (!wasPreviouslyOwned)
                    {
                        PlayerPrefs.SetInt(OwnedProductKeyPrefix + entry.ProductId, 1);
                        prefsDirty = true;
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription restored: {entry.ProductId}");
                        entry.OnRestored?.Invoke();
                        DispatchRestoreEvent(entry.ProductId);
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription restored callback dispatched: {entry.ProductId}");
                    }
                    else
                    {
                        DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription still active: {entry.ProductId}");
                    }
                }
                else if (wasPreviouslyOwned)
                {
                    PlayerPrefs.SetInt(OwnedProductKeyPrefix + entry.ProductId, 0);
                    prefsDirty = true;
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription expired detected: {entry.ProductId}");
                    entry.OnExpired?.Invoke();
                    OnSubscriptionExpired?.Invoke(entry.ProductId);
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"Subscription expired: {entry.ProductId}");
                }
                else
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"Subscription not active and not previously owned: {entry.ProductId}");
                }
            }

            if (prefsDirty)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "HandlePurchasesFetched: saving PlayerPrefs");
                PlayerPrefs.Save();
            }
            else
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, "HandlePurchasesFetched: no PlayerPrefs changes");
            }

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initialized, "Purchases fetched, service ready");
            OnRestoreSuccessful?.Invoke();
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "Purchase pending");
            ProcessPendingPurchase(order);
        }

        private void ProcessPendingPurchase(PendingOrder order)
        {
            try
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, "ProcessPendingPurchase start");

                // Extract product ID from the pending order cart
                string productId = null;
                try { productId = order.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id; }
                catch (Exception) { }

                var transactionId = "pending_" + order.GetHashCode();
                var price    = GetProductPrice(productId ?? string.Empty, false);
                var currency = GetCurrencyCode(productId ?? string.Empty, false);

                // Confirm the purchase with the store
                storeController.ConfirmPurchase(order);
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Showing, $"Purchase confirmed: {productId ?? "unknown"}");

                if (!string.IsNullOrWhiteSpace(productId))
                {
                    DispatchPurchaseEvent(productId, price, currency);

                    if (productsById.TryGetValue(productId, out var entry))
                    {
                        entry.OnPurchased?.Invoke();
                        // Track ownership for Android refund detection
                        if (entry.Type == ProductType.NonConsumable)
                        {
                            PlayerPrefs.SetInt(OwnedProductKeyPrefix + productId, 1);
                            PlayerPrefs.Save();
                        }
                    }

                    OnPurchaseSuccessful?.Invoke(productId);
                }

                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, "ProcessPendingPurchase confirmed");
            }
            catch (Exception ex)
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"ProcessPendingPurchase exception: {ex.Message}");
                PurchaseFailedCallback?.Invoke($"Process pending purchase failed: {ex.Message}");
            }
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            const string message = "Purchase failed";
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, message);
            PurchaseFailedCallback?.Invoke(message);
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, $"HandleStoreDisconnected: {failure?.Message}");
            HandleInitializationFailure($"Store disconnected: {failure?.Message}");
        }

        #endregion

        #region Refund Detection (Android)

        private void DetectAndProcessRefunds(Orders orders)
        {
#if UNITY_ANDROID
            if (orders == null) return;

            var ownedProductIds = new HashSet<string>();
            foreach (var confirmedOrder in orders.ConfirmedOrders)
            {
                var receipt = confirmedOrder.Info?.Receipt;
                if (string.IsNullOrWhiteSpace(receipt)) continue;
                var purchasedProducts = confirmedOrder.Info?.PurchasedProductInfo;
                if (purchasedProducts == null) continue;
                foreach (var info in purchasedProducts)
                    if (!string.IsNullOrWhiteSpace(info.productId))
                        ownedProductIds.Add(info.productId);
            }

            bool dirty = false;
            foreach (var entry in productsById.Values)
            {
                if (entry.Type != ProductType.NonConsumable) continue;
                bool isCurrentlyOwned   = ownedProductIds.Contains(entry.ProductId);
                bool wasPreviouslyOwned = PlayerPrefs.GetInt(OwnedProductKeyPrefix + entry.ProductId, 0) == 1;

                // Refund detector should only handle ownership loss. Ownership gain is handled by restore flow.
                if (wasPreviouslyOwned && !isCurrentlyOwned)
                {
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loaded, $"DetectAndProcessRefunds: refund detected for {entry.ProductId}");
                    PlayerPrefs.SetInt(OwnedProductKeyPrefix + entry.ProductId, 0);
                    dirty = true;
                    entry.OnRefunded?.Invoke();
                    DispatchRefundEvent(entry.ProductId);
                }
            }

            if (dirty) PlayerPrefs.Save();
#endif
        }

        #endregion

        #region Retry

        private void HandleInitializationFailure(string reason)
        {
            if (isDisposed) return;

            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, reason);

            if (currentRetryCount < maxRetryAttempts)
            {
                currentRetryCount++;
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"Initialization retry scheduled: attempt {currentRetryCount}");
                _ = RetryInitializeAsync();
            }
            else
            {
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Failed, "Initialization retries exhausted");
                OnInitializationFailed?.Invoke(reason);
            }
        }

        private async Task RetryInitializeAsync()
        {
            if (isDisposed) return;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Loading, $"RetryInitializeAsync: waiting {retryDelay}s");
            if (retryDelay > 0f)
                await Task.Delay(TimeSpan.FromSeconds(retryDelay));
            if (isDisposed) return;
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Initializing, "RetryInitializeAsync: invoking Initialize");
            Initialize();
        }

        #endregion

        #region Analytics Dispatch
        private void DispatchPurchaseEvent(string productId, string price, string currency, int quantity = 1)
        {
            if (gameEventService == null) return;
            gameEventService.SendCustomEvent("in_app_purchase", new Dictionary<string, string>
            {
                { PRODUCT_ID_PARAMETER, productId },
                { FirebaseAnalytics.ParameterPrice, price },
                { FirebaseAnalytics.ParameterCurrency, currency },
                { FirebaseAnalytics.ParameterQuantity, quantity.ToString() }
            }, true, false);
            var afValues = new Dictionary<string, string>
            {
                { AFInAppEvents.CONTENT_ID, productId },
                { AFInAppEvents.CURRENCY, currency },
                { AFInAppEvents.REVENUE,  price },
                { AFInAppEvents.QUANTITY, quantity.ToString() }
            };
            AppsFlyer.sendEvent(AFInAppEvents.PURCHASE, afValues);
        }

        private void DispatchRestoreEvent(string productId)
        {
            if (gameEventService == null) return;
            gameEventService.SendCustomEvent("purchase_restored", new Dictionary<string, string>
            {
                { PRODUCT_ID_PARAMETER, productId }
            }, true, false);
        }

        private void DispatchRefundEvent(string productId)
        {
            if (gameEventService == null) return;
            gameEventService.SendCustomEvent("purchase_refunded", new Dictionary<string, string>
            {
                { PRODUCT_ID_PARAMETER, productId }
            }, true, false);
        }

        #endregion

        #region Internal Utilities

        private ProductEntry GetEntryByName(string productName)
        {
            if (!string.IsNullOrWhiteSpace(productName) && productsByName.TryGetValue(productName, out var entry))
                return entry;
            return null;
        }

        private ProductEntry GetEntryById(string productId)
        {
            if (!string.IsNullOrWhiteSpace(productId) && productsById.TryGetValue(productId, out var entry))
                return entry;
            return null;
        }

        private static void AppendToArray(ref string[] array, string value)
        {
            if (array.Contains(value)) return;
            var updated = new string[array.Length + 1];
            Array.Copy(array, updated, array.Length);
            updated[array.Length] = value;
            array = updated;
        }

        private void SubscribeToStoreEvents(StoreController controller)
        {
            controller.OnPurchasePending     += HandlePurchasePending;
            controller.OnPurchasesFetched    += HandlePurchasesFetched;
            controller.OnPurchaseFailed      += HandlePurchaseFailed;
            controller.OnProductsFetched     += HandleProductsFetched;
            controller.OnProductsFetchFailed += HandleProductsFetchFailed;
            controller.OnStoreDisconnected   += HandleStoreDisconnected;
        }

        private void UnsubscribeFromStoreEvents(StoreController controller)
        {
            controller.OnPurchasePending     -= HandlePurchasePending;
            controller.OnPurchasesFetched    -= HandlePurchasesFetched;
            controller.OnPurchaseFailed      -= HandlePurchaseFailed;
            controller.OnProductsFetched     -= HandleProductsFetched;
            controller.OnProductsFetchFailed -= HandleProductsFetchFailed;
            controller.OnStoreDisconnected   -= HandleStoreDisconnected;
        }
            private static string TimeSpanToPeriodLabel(System.TimeSpan period)
    {
        Debug.Log($"Subscription period in days: {period.TotalDays}");
        int days = (int)System.Math.Round(period.TotalDays);
        if (days <= 1)  return "Day";
        if (days <= 3)  return $"{days} Days";
        if (days <= 7)  return "Week";
        if (days <= 14) return "2 Weeks";
        if (days <= 31) return "Month";
        if (days <= 92) return "3 Months";
        if (days <= 183) return "6 Months";
        return "Year";
    }

        #endregion
        #region Disposal

        public void Dispose()
        {
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.InAppPurchaserScript, DiagnosticStateAVNPlugin.Destroyed, "Dispose called");
            isDisposed = true;
            if (storeController != null)
                UnsubscribeFromStoreEvents(storeController);
        }

        #endregion
    #else
    public void Dispose()
        {
            
        }
        #endif
        
    }
}
#endif
