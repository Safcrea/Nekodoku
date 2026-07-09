# AVN Plugin — Plugin Guide

Full reference for every configurable part of the plugin after the initial setup.

---

## Table of Contents

- [AVN Plugin — Plugin Guide](#avn-plugin--plugin-guide)
  - [Table of Contents](#table-of-contents)
  - [0. Scripting Symbols](#0-scripting-symbols)
    - [Required Symbols](#required-symbols)
    - [Required Symbols](#required-symbols-1)
    - [Optional Symbols](#optional-symbols)
  - [1. AdsBootstrap Hierarchy](#1-adsbootstrap-hierarchy)
  - [2. Ad Controllers](#2-ad-controllers)
    - [BannerAdController](#banneradcontroller)
    - [InterAdController](#interadcontroller)
    - [RewardAdController](#rewardadcontroller)
    - [AppOpenInterAdController](#appopeninteradcontroller)
  - [3. Scene Loading \& Startup Sync](#3-scene-loading--startup-sync)
    - [Inspector Fields](#inspector-fields)
    - [Startup Flow — With Internet](#startup-flow--with-internet)
    - [Startup Flow — Without Internet](#startup-flow--without-internet)
    - [Provider Failure Fallback](#provider-failure-fallback)
    - [Stuck-at-Loading-Scene Edge Cases](#stuck-at-loading-scene-edge-cases)
  - [3b. GoogleAppTracking — CMP Startup Orchestrator](#3b-googleapptracking--cmp-startup-orchestrator)
    - [Inspector Fields](#inspector-fields-1)
    - [Startup Responsibilities](#startup-responsibilities)
    - [initializeOnStart Must Be Off](#initializeonstart-must-be-off)
  - [4. Remote Config Caching](#4-remote-config-caching)
    - [How it works](#how-it-works)
    - [What is cached](#what-is-cached)
    - [Ad IDs — inspector vs. remote](#ad-ids--inspector-vs-remote)
  - [4. Services](#4-services)
  - [5. AVNPluginCanvas](#5-avnplugincanvas)
    - [NoInternetPopup](#nointernetpopup)
    - [Toast System](#toast-system)
  - [6. AVNPlugin — Public Methods Reference](#6-avnplugin--public-methods-reference)
    - [Interstitial Ad Methods](#interstitial-ad-methods)
      - [`ShowInterAd(bool isRewarded = false) → bool`](#showinteradbool-isrewarded--false--bool)
      - [`LoadInterAd()`](#loadinterad)
      - [`UpdateInterDelay(float newDelay)`](#updateinterdelayfloat-newdelay)
      - [`UpdateInterstitialShowMode(int modeIndex)`](#updateinterstitialshowmodeint-modeindex)
    - [App Open Methods](#app-open-methods)
      - [`SuppressAppOpenInterstitial()`](#suppressappopeninterstitial)
      - [`UnsuppressAppOpenInterstitial()`](#unsuppressappopeninterstitial)
    - [Rewarded Ad Methods](#rewarded-ad-methods)
      - [`ShowRewardedAd(Action onRewarded, Action onNoAd = null, Action onCancelled = null) → bool`](#showrewardedadaction-onrewarded-action-onnoad--null-action-oncancelled--null--bool)
      - [`LoadRewardAd()`](#loadrewardad)
    - [Banner Ad Methods](#banner-ad-methods)
      - [`ShowBannerAd(BannerAdTypes type = BANNER)`](#showbanneradbanneradtypes-type--banner)
      - [`LoadBanner(bool showOnLoad, BannerAdTypes type = BANNER)`](#loadbannerbool-showonload-banneradtypes-type--banner)
      - [`HideBannerAd(BannerAdTypes type = BANNER)`](#hidebanneradbanneradtypes-type--banner)
      - [`DestroyBanner(BannerAdTypes type = BANNER)`](#destroybannerbanneradtypes-type--banner)
    - [Rate Us / Notification Methods](#rate-us--notification-methods)
      - [`NativeRateUsCall()`](#nativerateuscall)
      - [`NotificationCall()`](#notificationcall)
    - [SDK Management Methods](#sdk-management-methods)
      - [`SetAdsEnabled(bool enabled, bool persist = false)`](#setadsenabledbool-enabled-bool-persist--false)
      - [`GetAdsEnabled() → bool`](#getadsenabled--bool)
      - [`FetchRemoteConfigNow()`](#fetchremoteconfignow)
      - [`RetryInitializationIfNeeded()`](#retryinitializationifneeded)
    - [In-App Purchasing Methods](#in-app-purchasing-methods)
      - [`BuyProduct(string productName)`](#buyproductstring-productname)
      - [`BuyRemoveAds()`](#buyremoveads)
      - [`RestorePurchases()`](#restorepurchases)
      - [`GetProductPrice(string productName) → string`](#getproductpricestring-productname--string)
      - [`GetRemoveAdsPrice() → string`](#getremoveadsprice--string)
    - [Utility / State Query Methods](#utility--state-query-methods)

---

## 0. Scripting Symbols

Open Unity toolbar: `AVN/Manage Scripting Symbols`.

### Required Symbols

| Symbol | Required | Purpose |
|--------|----------|---------|
| `USE_AVNADS_PLUGIN` | Yes | Enables the AVN plugin runtime/editor code path. |
| `USE_ADMOB` | Yes | Enables Admob SDK which is used for CMP and showing AppOpenAd. |
| `USE_FIREBASE` | Yes | Enables Firebase Analytics and RemoteConfig |

### Required Symbols

| Symbol | Required | Purpose |
|--------|----------|---------|
| `USE_LEVELPLAY` | Yes | Enables LevelPlay ads-provider integration used by AVNPlugin. |
| `USE_MAX` | Yes | Enables MAX ads-provider integration used by AVNPlugin. |

### Optional Symbols

| Symbol | Optional | Purpose |
|--------|----------|---------|
| `USE_AVNADS_PLUGIN_DEBUG` | Yes | Enables extra debug/diagnostic paths for AVN plugin testing. |
| `USE_BYTEBREW` | Yes | Enables ByteBrew-specific integration hooks when ByteBrew is used. |
| `USE_IAP` | Yes | Enables in-app purchasing code paths and callbacks. |

Enable only the optional symbols your project needs.

---

## 1. AdsBootstrap Hierarchy

```
AdsBootstrap  (AVNPlugin component lives here)
├── Ad Controllers
│   ├── BannerAdController
│   ├── InterAdController
│   ├── RewardAdController
│   └── AppOpenInterAdController
├── Services
│   ├── AppsFlyerObject  (drag the AppsFlyer script/prefab here)
│   └── ByteBrew         (drag the ByteBrew prefab here)
└── AVNPluginCanvas
    ├── NoInternetPopup
    └── ToastSystem
```

The `AVNPlugin` MonoBehaviour on `AdsBootstrap` holds serialized references to every controller in the **Controllers** foldout. These are auto-wired in the prefab; do not remove or rename the child GameObjects.

---

## 2. Ad Controllers

Each ad type has its own controller component under `AdsBootstrap → Ad Controllers`. Select the child GameObject to configure it.

---

### BannerAdController

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Banner Position` | `BannerPosition` | `BottomCenter` | Screen position for the standard banner ad |
| `Mrec Banner Position` | `BannerPosition` | `TopCenter` | Screen position for the MREC banner ad |
| `Show Banner On Init` | `bool` | `false` | If true, the banner is shown immediately after loading (only applies to the auto-load init call) |
| `Is Adaptive Banner` | `bool` | `false` | When true, the SDK picks an adaptive banner size instead of fixed 320×50 |
| `Should Banner Ignore Notch Area` | `bool` | `false` | When true, the banner is placed below the device notch/safe area |
| `Banner Persist Load Calls` | `bool` | `false` | When true, failed banner loads are retried persistently in the background |

---

### InterAdController

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Interstitial Show Mode` | `InterstitialShowMode` | `ShowDefaultWithOneBackfill` | Controls which ad slot is selected when a show is requested. See modes below. |
| `Default Inter Id Index` | `int` | `-1` | The slot index treated as the primary / default ad. `-1` means use slot 0. |
| `Inter Ad Delay` | `float` | `30` | Minimum seconds that must pass between two consecutive interstitial shows. Can be updated at runtime with `UpdateInterDelay()`. |
| `Reset Inter Time On Reward` | `bool` | `false` | If true, the cooldown timer resets when a rewarded ad finishes, giving the player a delay-free window. |

**InterstitialShowMode values:**

| Mode | Behaviour |
|------|-----------|
| `ShowDefault` | Always tries the default slot only |
| `ShowIterateSequence` | Cycles through all configured slots in order on each call |
| `ShowDefaultWithOneBackfill` | Tries the default slot first; if not ready, falls back to the next available slot |
| `ShowDefaultWithTwoBackfill` | Same as above but tries two backfill slots before giving up |

---

### RewardAdController

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enable Inter Backfill` | `bool` | `true` | When true and no rewarded ad is loaded, the plugin automatically falls back to showing an interstitial instead of doing nothing. The callback flow still fires normally. |

---

### AppOpenInterAdController

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Reactivate After Seconds` | `int` | `2` | Seconds to wait after a full-screen ad closes before the App Open controller is allowed to show again (prevents stacking) |
| `Show App Open On Session Start` | `bool` | `false` | On cold start, calls the startup App Open load with `showOnLoad = true` so the first ready App Open ad is shown immediately. |
| `Should Skip First Session` | `bool` | `true` | Only used when `Show App Open On Session Start` is enabled. If true, the first-ever installed session loads App Open normally but does not auto-show it on startup. |

This controller now uses the dedicated AdMob App Open service. `App Open Ad Unit Id 1` is treated as the primary slot and `App Open Ad Unit Id 2` is treated as the backfill slot. These two startup flags only affect cold start and do not change the normal background → foreground App Open flow.

---

## 3. Scene Loading & Startup Sync

`AVNPlugin` can lock the player inside a loading scene and automatically switch to the next scene once the ads provider is ready (or has conclusively failed and internet is available). This replaces any hand-written loading-scene transition script.

### Inspector Fields

Found in the **Scene Loading** foldout on `AVNPlugin`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Sync Loading With Provider` | `bool` | `false` | When enabled, the plugin holds in the current scene until the gate condition is met. |
| `Next Scene To Load` | `string` | *(empty)* | Exact scene name (not path) to load when the gate opens. Must match the scene name in Build Settings. |

> When `Sync Loading With Provider` is off, scene transitions are entirely your responsibility. The plugin does nothing.

---

### Startup Flow — With Internet

```
[Loading Scene starts]
        │
        ▼
GoogleAppTracking.Start()
  └─ AdMobSdkInitializer.EnsureInitialized()
        │ success
        ▼
  CMP check (if enableAdMobCMP = true)
    ├─ Non-GDPR region: consent not required → skip form
    └─ GDPR region: show UMP consent form → wait for user
        │ either path completes
        ▼
  iOS ATT check (UNITY_IOS only)
    ├─ ATT already decided → continue immediately
    └─ ATT NOT_DETERMINED → request ATT and wait for callback
        │ ATT resolved
        ▼
  TriggerPostConsentFlow()
    ├─ AppOpenAdController.RequestSessionStartLoad()   (queued until AdMob is ready)
    └─ AVNPlugin.Initialize()                          (synchronous setup + async SDK coroutines)
        │
        ▼
AVNPlugin: Firebase → AdMob → LevelPlay init sequence runs
        │ LevelPlay success callback fires
        ▼
AVNPlugin.HandleProviderInitializedSuccessfully()
  └─ TryLoadNextSceneIfReady()
       ├─ syncLoadingWithProvider = true?  ✓
       ├─ provider initialized?            ✓
       ├─ internet reachable?              ✓
       └─ nextSceneToLoad non-empty?       ✓
              └─ SceneManager.LoadScene(nextSceneToLoad)
```

---

### Startup Flow — Without Internet

```
[Loading Scene starts — no internet]
        │
        ▼
GoogleAppTracking: AdMob init fails → CMP skipped
  └─ iOS ATT check still runs (UNITY_IOS only)
    ├─ ATT decided or callback received
    └─ TriggerPostConsentFlow(consentGranted: false)
      └─ AVNPlugin.Initialize()
        │
        ▼
AVNPlugin: LevelPlay init times out / fails
  └─ TryLoadNextSceneIfReady() called
       ├─ internet reachable? ✗  → blocked, waitingForInternetToLoadScene = true
        │
        ▼
NoInternetPopup appears (connectivity watcher fires)
        │ user regains internet
        ▼
NoInternetPopup.ClosePopup()
  ├─ AdMobSdkInitializer.Reset()
  └─ AVNPlugin.RetryInitializationIfNeeded()
        │
        ▼
AVNPlugin init sequence reruns
  └─ on LevelPlay success → TryLoadNextSceneIfReady()
       ├─ provider initialized? ✓
       ├─ internet reachable?   ✓
       └─ SceneManager.LoadScene(nextSceneToLoad)

Additionally, AVNPlugin.Update() polls TryLoadNextSceneIfReady every frame
while sceneLoadTriggered = false, so scene load fires as soon as all
conditions are satisfied even if a callback was missed.
```

---

### Provider Failure Fallback

If the ads provider **fails** to initialize (e.g. wrong SDK key, server error) but **internet is reachable**, the plugin will still load `nextSceneToLoad`. This prevents users from being permanently stuck on the loading screen.

The fallback logs a warning so you can investigate:
```
AVNPlugin: Provider initialization failed, but internet is reachable.
           Loading next scene via fallback: <sceneName>
```

The fallback triggers from two paths:
- `HandleProviderInitializationFailed()` callback.
- `Update()` loop retry (catches cases where the provider timed out after `ProviderInitTimeoutSeconds`).

---

### Stuck-at-Loading-Scene Edge Cases

| Scenario | Result | Fix |
|----------|--------|-----|
| `Next Scene To Load` is empty | Gate logs a warning and never loads | Fill in the scene name in the inspector |
| `Sync Loading With Provider` is off | No transition happens at all | Enable the toggle or add your own transition |
| `initializeOnStart = true` while using GoogleAppTracking | Init runs twice, breaking CMP timing | Set `initializeOnStart = false` on AVNPlugin |
| Provider fails AND internet is not reachable | Blocked until connectivity returns | NoInternetPopup guides the user to reconnect |
| Permanent no-internet (no popup in scene) | Stuck indefinitely | Ensure `NoInternetPopup` is in the `AVNPluginCanvas` hierarchy |

---

## 3b. GoogleAppTracking — CMP Startup Orchestrator

`GoogleAppTracking` is the startup orchestration script. It initializes AdMob first (required before CMP can run), manages the UMP consent form, then on iOS resolves ATT when required, and finally signals `AVNPlugin` to initialize. Scene loading is handled entirely by `AVNPlugin` after that.

> **Do not use** `GoogleAppTracking` and `initializeOnStart = true` on `AVNPlugin` at the same time. GoogleAppTracking owns initialization order.

### Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `AVN Plugin` | `AVNPlugin` | *(auto)* | Reference to the AVNPlugin in the scene. Auto-resolved from singleton if left empty. |
| `App Open Ad Controller` | `AppOpenAdController` | *(auto)* | Auto-resolved from AVNPlugin children if left empty. |
| `Enable AdMob CMP` | `bool` | `true` | When enabled, runs the Google UMP consent form for GDPR regions before plugin init. Disable for soft-launch or non-GDPR only builds. |
| `Debug Geography` | `DebugGeography` | `Disabled` | Forces CMP to behave as if the user is in a specific region during development. Set to `EEA` to force the consent form. Set to `Disabled` in production. |

### Startup Responsibilities

`GoogleAppTracking` does exactly four things, in order:

1. **Initialize AdMob SDK** — required before UMP/CMP can run.
2. **Run CMP** — shows the consent form only where legally required (GDPR/EEA). Outside GDPR regions the form is skipped automatically.
3. **Resolve ATT on iOS (`#if UNITY_IOS`)** — if ATT status is `NOT_DETERMINED`, request ATT and continue from callback; otherwise continue immediately.
4. **Call `AVNPlugin.Initialize()`** — hands off to the plugin.

Scene transitions, provider readiness, and ad loading are all handled by `AVNPlugin` after step 3.

### initializeOnStart Must Be Off

On the `AVNPlugin` component, set **`Initialize On Start` = false** whenever `GoogleAppTracking` is in the scene. Otherwise the plugin self-initializes before CMP has run, which breaks consent sequencing.

---

## 4. Remote Config Caching

All Remote Config values fetched from Firebase are **cached in `PlayerPrefs`** on the device so they survive offline sessions and app restarts.

### How it works

1. On every launch, the plugin calls `ApplyCachedConfig()` **immediately during initialization** — before any network call — so the last-known good values are active from frame one.
2. A fresh Firebase fetch then runs in the background. When it succeeds, the new values are applied and the cache is updated.
3. If the fetch fails (no internet, Firebase unavailable, timeout), the plugin continues using the cached values from the previous session.

### What is cached

| PlayerPrefs Key | Contents |
|-----------------|----------|
| *(internal)* `RemoteConfigAdConfig` | Last fetched `AdConfig` JSON — ad rules, delays, level thresholds, enable flags |
| *(internal)* `RemoteConfigAdIdsConfig` | Last fetched `AdIdsConfig` JSON — all ad unit IDs |
| *(internal)* `RemoteConfigCustom_<keyId>` | One entry per custom key registered via `GameRemoteConfig.RegisterKey()` |

### Ad IDs — inspector vs. remote

The ad unit IDs filled in the **Ad IDs** inspector foldout are the **hardcoded defaults**. On startup the plugin:
1. Reads the inspector IDs as defaults.
2. Immediately applies any cached `AdIdsConfig` from `PlayerPrefs` (overrides inspector values).
3. Applies the freshly fetched `AdIdsConfig` once the Firebase fetch completes (updates the cache for next session).

This means the device always uses the most recently fetched IDs — even without internet — and the inspector IDs only take effect on first-ever launch before any fetch has succeeded. For App Open, the runtime reads `AppOpenAdUnitId1` as the primary AdMob slot and `AppOpenAdUnitId2` as the backfill slot. The legacy `AppOpenInterAdUnitId` key is still read as a fallback into slot 1 for older configs.

---

## 4. Services

Under `AdsBootstrap → Services`, two third-party SDK objects need to be present:

| Child Object | What to add |
|--------------|-------------|
| **AppsFlyerObject** | Drag the `AppsFlyerObjectScript` component / prefab supplied by the AppsFlyer Unity SDK into this child. This is the standard AppsFlyer GameObject that holds your Dev Key and App ID. |
| **ByteBrew** | Drag the ByteBrew SDK prefab here. The plugin calls `ByteBrewService.Initialize()` internally — you only need to ensure the ByteBrew GameObject with its SDK key is present. |

> If either SDK object is missing, the plugin logs a warning and continues. Attribution and analytics events will simply not fire.

---

## 5. AVNPluginCanvas

The `AVNPluginCanvas` child holds two UI systems. Both are fully optional to customise.

### NoInternetPopup

A full-screen overlay shown automatically when the device loses connectivity during SDK initialization. 
- Replace the background / icon sprites inside this prefab to match your game's art style.
- The popup dismisses itself when connectivity is restored.

### Toast System

Small non-intrusive notification toasts shown for:
- "No rewarded ad available" (fires when `ShowRewardedAd` is called but no ad is loaded)
- "Reward cancelled" (fires when the user skips a rewarded ad mid-watch)

To customise:
- Replace the sprite / text style on the toast prefab variants inside `AVNPluginCanvas → ToastSystem`.
- You can also suppress toasts entirely by not assigning a `ToastManager` reference on `AVNPlugin`, but this is not recommended — it removes useful player feedback.

---

## 6. AVNPlugin — Public Methods Reference

Access all methods via the singleton: `AVNPlugin.DTInstance.MethodName(...)`.

---

### Interstitial Ad Methods

#### `ShowInterAd(bool isRewarded = false) → bool`
Attempts to show an interstitial ad.
- `isRewarded` — when `true`, marks this show as a "rewarded interstitial" internally (used for backfill tracking). Default is `false`.
- Returns `true` if the ad was successfully triggered, `false` if it was blocked (e.g. cooldown not elapsed, no ad loaded, `CanShowInterstitial()` returned false, or ads are disabled).

```csharp
bool shown = AVNPlugin.DTInstance.ShowInterAd();
```

#### `LoadInterAd()`
Manually requests a load for the interstitial ad. Use this when `Auto Load Interstitial` is disabled.

```csharp
AVNPlugin.DTInstance.LoadInterAd();
```

#### `UpdateInterDelay(float newDelay)`
Changes the cooldown between interstitial shows at runtime.
- `newDelay` — new minimum seconds between shows.

```csharp
AVNPlugin.DTInstance.UpdateInterDelay(45f); // 45-second cooldown
```

#### `UpdateInterstitialShowMode(int modeIndex)`
Switches the interstitial slot selection strategy at runtime.
- `modeIndex` — integer value of the `InterstitialShowMode` enum: `0` = ShowDefault, `1` = ShowIterateSequence, `2` = ShowDefaultWithOneBackfill, `3` = ShowDefaultWithTwoBackfill.

```csharp
AVNPlugin.DTInstance.UpdateInterstitialShowMode(2); // ShowDefaultWithOneBackfill
```

---

### App Open Methods

App Open is now managed automatically by `AppOpenInterAdController` plus the AdMob App Open service. There is no public `ShowAppOpenAd()` method on `AVNPlugin`; the plugin loads and shows App Open from startup and foreground-resume hooks, while these public methods control suppression only.

#### `SuppressAppOpenInterstitial()`
Permanently suppresses the App Open ad until `UnsuppressAppOpenInterstitial()` is called. Use this before showing your own full-screen UI (e.g. a level-complete screen) to prevent App Open ads from stacking on top.

#### `UnsuppressAppOpenInterstitial()`
Lifts a permanent suppression applied by `SuppressAppOpenInterstitial()`.

> The plugin also auto-suppresses App Open internally for a few seconds whenever an interstitial or rewarded ad is shown — you don't need to handle that manually.

---

### Rewarded Ad Methods

#### `ShowRewardedAd(Action onRewarded, Action onNoAd = null, Action onCancelled = null) → bool`
Attempts to show a rewarded ad.

| Parameter | Description |
|-----------|-------------|
| `onRewarded` | Called when the player completes the ad and earns the reward. **Required.** |
| `onNoAd` | Called when no rewarded ad is available. A toast is also shown automatically. Optional. |
| `onCancelled` | Called when the player skips/closes the ad before earning the reward. A toast is also shown. Optional. |

Returns `true` if the ad started showing, `false` otherwise.

```csharp
AVNPlugin.DTInstance.ShowRewardedAd(
    onRewarded:  () => GivePlayerCoins(100),
    onNoAd:      () => Debug.Log("No ad available"),
    onCancelled: () => Debug.Log("Player cancelled")
);
```

#### `LoadRewardAd()`
Manually requests a load for the rewarded ad. Use when `Auto Load Rewarded` is disabled.

---

### Banner Ad Methods

#### `ShowBannerAd(BannerAdTypes type = BANNER)`
Shows the banner or MREC. The banner must already be loaded.
- `type` — `BANNER` or `MREC`. Passing `ADAPTIVE` is automatically remapped to `BANNER`; adaptive sizing is configured on the `BannerAdController` component, not here.

```csharp
AVNPlugin.DTInstance.ShowBannerAd(BannerAdTypes.BANNER);
```

#### `LoadBanner(bool showOnLoad, BannerAdTypes type = BANNER)`
Loads (and optionally shows) a banner.
- `showOnLoad` — if `true`, the banner becomes visible as soon as it finishes loading.
- `type` — `BANNER` or `MREC`.

```csharp
AVNPlugin.DTInstance.LoadBanner(showOnLoad: true);
```

#### `HideBannerAd(BannerAdTypes type = BANNER)`
Hides the banner without destroying it. The ad remains in memory and can be re-shown with `ShowBannerAd`.

#### `DestroyBanner(BannerAdTypes type = BANNER)`
Destroys the banner and frees SDK resources. You will need to call `LoadBanner` again before showing.

---

### Rate Us / Notification Methods

#### `NativeRateUsCall()`
Triggers the native rate-us dialog. Checks `GameAdsCheck.CanShowRateUsDialog()` first — if that returns `false`, the dialog is suppressed. The App Open ad is also briefly suppressed to avoid overlap.

#### `NotificationCall()`
Triggers the native notification permission prompt. Checks `GameAdsCheck.CanShowNotification()` first.

---

### SDK Management Methods

#### `SetAdsEnabled(bool enabled, bool persist = false)`
Enables or disables all ads globally.
- `enabled` — `false` hides the banner immediately and blocks future ad shows.
- `persist` — if `true`, the state is saved to `PlayerPrefs` and survives app restarts.

```csharp
// Disable ads after a Remove Ads purchase (persisted)
AVNPlugin.DTInstance.SetAdsEnabled(false, persist: true);
```

#### `GetAdsEnabled() → bool`
Returns the current global ads enabled state.

#### `FetchRemoteConfigNow()`
Forces an immediate Firebase Remote Config fetch, ignoring the retry counter. Use this when you know connectivity was just restored.

#### `RetryInitializationIfNeeded()`
Re-runs the SDK initialization sequence if the SDK failed to initialize (e.g. first-run without internet). Safe to call repeatedly — it does nothing if the SDK is already initialized.

---

### In-App Purchasing Methods

#### `BuyProduct(string productName)`
Initiates a purchase for any registered product by its internal `ProductName`.
- The App Open ad is briefly suppressed to avoid showing over the store sheet.

```csharp
AVNPlugin.DTInstance.BuyProduct("GoldBundle");
```

#### `BuyRemoveAds()`
Shorthand for purchasing the Remove Ads product. Equivalent to `BuyProduct("RemoveAds")`.

#### `RestorePurchases()`
Triggers a restore-purchases flow (required on iOS). Fires `OnRemoveAdsRestored` and any other per-product restored callbacks.

#### `GetProductPrice(string productName) → string`
Returns the localized price string for any registered product (e.g. `"$0.99"`). Returns `string.Empty` if the product is not found or the store catalog has not loaded yet.

#### `GetRemoveAdsPrice() → string`
Shorthand for `GetProductPrice("RemoveAds")`. Returns `"0.00"` if not available.

---

### Utility / State Query Methods

These methods are useful for UI state, debug overlays, or conditional logic.

| Method | Returns | Description |
|--------|---------|-------------|
| `IsBannerReady(type)` | `bool` | Whether the specified banner type is loaded and ready to show |
| `IsBannerShowing(type)` | `bool` | Whether the specified banner type is currently visible |
| `GetCurrentBannerType()` | `BannerAdTypes` | The banner type that was last loaded/shown |
| `IsRewardedReady()` | `bool` | Whether a rewarded ad is loaded and ready |
| `IsRewardedRequestActive()` | `bool` | Whether a show request is currently in flight (ad is showing or closing) |
| `IsRewardedWaitingFallback()` | `bool` | Whether the rewarded controller is waiting for an interstitial backfill to complete |
| `WasRewardFallbackShown()` | `bool` | Whether an interstitial was shown as a backfill for the last rewarded request |
| `GetInterstitialShowModeName()` | `string` | Human-readable name of the current `InterstitialShowMode` |
| `GetInterstitialFlowState()` | `InterFlowState` | Current state machine state of the interstitial controller (`NotLoaded`, `Loading`, `Loaded`, `ShowCalled`, `Showing`) |
| `GetCurrentInterstitialSlot()` | `int` | Slot index of the ad currently being shown (`-1` if none) |
| `GetInterstitialLoadingSlot()` | `int` | Slot index currently being loaded (`-1` if no load in flight) |
| `GetInterstitialSlotStates()` | `List<InterSlotDebugState>` | Per-slot debug state list (ready, queued, loading, etc.) |
| `GetRuntimeBannerAdId()` | `string` | The banner ad unit ID actually in use at runtime (may differ from inspector if Remote Config overrode it) |
| `GetRuntimeMrecAdId()` | `string` | Runtime MREC ad unit ID |
| `GetRuntimeRewardedAdId()` | `string` | Runtime rewarded ad unit ID |
| `GetRuntimeAppOpenAdId()` | `string` | Runtime primary App Open ad unit ID |
| `GetRuntimeAppOpenBackfillAdId()` | `string` | Runtime backfill App Open ad unit ID |
| `GetRuntimeInterstitialAdIds()` | `string[]` | Runtime interstitial ad unit ID array |
| `GetAdsEnabled()` | `bool` | Global ads enabled flag |

---