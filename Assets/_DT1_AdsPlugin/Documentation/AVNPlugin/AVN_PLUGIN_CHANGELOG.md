# AVN Plugin — Changelog
## Version : 1.1.1


## [v1.1.1] — 2026-05-13


### Added
- Added new Inter Show Mode: ShowThreeTierInterstitial
- Added seperate show mode options for Android and iOS

### Changes
- defaultInterIdIndex variable in InterAdController made readonly
- Removed Multiple Events of Taichi1 and PaidEvent and made it to send only one event
- Analytics Event Service, Level Event Parameters made into constant variable for reusability


---
## [v1.1.0] — 2026-05-11


### Added
- Added support for InterAdDelay Before First Ad
- Added Taichi 2 Event

---

## [v1.0.9] — 2026-05-06

### Fixed
- Restore Purchases wasn't restoring RemoveAds properly so fixed it's flow.
### Added
- Added event callbacks for Plugin InitStart and Completed and OnConsentFlow Completed 
- Added Enum to manage Plugin Init Types 
- Added CMPEligibleUser Variable to make sure the CMP isn't checked for users not in GDRP regions, connected it to firebase as well, so another variable is added to the remote config. 
- Ad Ids are now stored in a scriptable object, instead of as fields inside AVNPlugin. 
- AppOpenAd Delay added
- Option Added to set which ROAS experiment event to send in FinzAnalysisManager, new, old, or both. 
---

## [v1.0.8] — 2026-04-23

### Added

#### Banner Ad Controller — BannerBg Functionality
- Added `BannerBg` functionality to the Banner Ad Controller for enhanced banner ad customization and background management.

#### AppLovin MAX Provider (`MaxAdsProvider`)
- Created `MaxAdsProvider.cs` — a clean `IAdsProvider` implementation locked behind `#if USE_AVNADS_PLUGIN && USE_MAX`.
- Mirrors the slot-based interstitial architecture of `LevelPlayAdsProvider`: maintains a `Dictionary<int, string>` mapping `slotIndex → adUnitId` and registers MAX callbacks lazily via guard flags to prevent duplicate subscriptions.
- Implements all `IAdsProvider` contract methods: `Initialize`, `ConfigureAdIds`, `LoadInterstitial`, `ShowInterstitial`, `IsInterstitialReady`, `LoadRewarded`, `ShowRewarded`, `LoadBanner`, `ShowBanner`, `HideBanner`, `DestroyBanner`.
- All MAX callbacks translate directly to provider-agnostic `AdEvents` (`OnBannerLoaded`, `OnMrecLoaded`, `OnInterstitialSlotLoaded`, `OnRewardLoaded`, `OnRewardEarned`, etc.) — no SDK types leak outside the provider.
- Added `BannerPosition` → `MaxSdkBase.BannerPosition` and `BannerPosition` → `MaxSdkBase.AdViewPosition` helper mappings.
- Paid impression events routed through `FinzAnalysisManager.PaidAdAnalytics` (`TrackAdRevenue`), consistent with the old `AdController` pattern.
- No manual AdMob backfill logic — ad availability and retry are owned by the Ad Controllers (`BannerAdController`, `InterAdController`, `RewardAdController`) as per the provider-agnostic design.
- App Open Ads are intentionally excluded from `MaxAdsProvider` — `AdMobAppOpenService` continues to own the App Open lifecycle regardless of the active provider.

#### Scripting Symbols Manager — Ad Provider Region
- Added a dedicated **"Ad SDK Provider (select one)"** region to `ScriptingDefinesWindow.cs` using a new `ExclusiveSymbolState` class.
- Enabling `USE_MAX` automatically removes `USE_LEVELPLAY` and vice versa — only one provider define can be active at a time.
- `USE_LEVELPLAY` moved from the **Required Symbols** group into the new **Ad SDK Provider** group.
- `ExclusiveSymbolState` extends the existing `SymbolState` with conflict-aware `Toggle()` logic that strips all conflicting symbols before writing the selected one.

#### AVNPlugin — Provider Selection
- `ConstructDependencies()` now uses compile-time preprocessor directives to instantiate the correct provider:
  - `USE_MAX` defined → `MaxAdsProvider` is created.
  - Otherwise (`USE_LEVELPLAY` or default) → `LevelPlayAdsProvider` is created.

#### InterAdController — UseAdDelay Configuration
- Added `UseAdDelay` (`bool`) inspector field to `InterAdController` to enable/disable ad delay usage in the game.
- When enabled, interstitial ads will respect the configured delay before being shown. When disabled, ads display immediately after they are ready.

#### In-App Purchasing — Purchase Toast Notifications
- Added toast notifications for In-App Purchase status updates:
  - Toast displayed on **Purchase Successful** — notifies the player that the purchase completed.
  - Toast displayed on **Purchase Failed** — notifies the player that the purchase encountered an error.
  - Toast displayed on **Purchase Restored** — notifies the player that a non-consumable purchase was successfully restored.
- Toast messages provide real-time feedback for all purchase-related events.

#### AdMob App Open — Paid Event Tracking
- Added paid impression event tracking for AdMob App Open ads via `AdMobAppOpenService`.
- Paid ad revenue is now routed through `FinzAnalysisManager.PaidAdAnalytics` (`TrackAdRevenue`) for App Open ads, consistent with other ad formats.

### Fixed

#### AVNPlugin — AppOpenInterAd Cleanup
- **Fixed**: Removed stale `AppOpenInterAd` assignment from `AVNPlugin` that was no longer in use. This eliminates unnecessary processing and prevents potential conflicts with the updated ad management system.

---

## [v1.0.7] — 2026-04-22

### Added

#### Scene Loading & Startup Sync (`AVNPlugin`)
- Added **Scene Loading** inspector foldout on `AVNPlugin` with two new fields:
  - `Sync Loading With Provider` (`bool`) — when enabled, AVNPlugin holds in the current scene until LevelPlay is initialized (or has conclusively failed with internet available).
  - `Next Scene To Load` (`string`) — scene name to load when the gate opens.
- Public getters `SyncLoadingWithProvider` and `NextSceneToLoad` exposed for external scripts.
- `TryLoadNextSceneIfReady()` private method owns all scene-transition logic. It is called from:
  - `HandleProviderInitializedSuccessfully()` — fires when LevelPlay init succeeds.
  - `HandleProviderInitializationFailed()` — fires the fallback when init fails.
  - `Initialize()` — handles edge case where provider was already initialized before the call.
  - `Update()` — polls every frame while `sceneLoadTriggered = false`, ensuring the gate is re-evaluated after reconnect even if a callback was missed.
- **Provider Failure Fallback**: if provider init fails but internet is reachable, scene load proceeds anyway so users cannot get permanently stuck at the loading screen. A `LogWarning` marks this fallback path.
- **Offline guard**: scene load is blocked when `Application.internetReachability == NotReachable`. A `waitingForInternetToLoadScene` flag prevents log spam. Once connectivity returns, the Update loop retries automatically.
- Duplicate-load protection via `sceneLoadTriggered` flag (one-shot, never loads twice).
- Missing `nextSceneToLoad` is caught with a one-time `LogWarning` via `missingNextSceneLogged` flag.
- Same-scene guard: if the active scene is already `nextSceneToLoad`, marks triggered without reloading.

#### GoogleAppTracking — CMP Startup Orchestrator
- `GoogleAppTracking` now owns the AdMob/CMP → plugin init handoff sequence, removing scene-loading responsibility from the CMP layer.
- Clear four-step startup contract:
  1. Initialize AdMob SDK (`AdMobSdkInitializer.EnsureInitialized`).
  2. Run UMP/CMP consent form only where legally required. Skipped automatically outside GDPR regions.
  3. Resolve ATT on iOS when required.
  4. Call `AVNPlugin.Initialize()`. All scene transitions are then handled by AVNPlugin.
- Added iOS ATT stage (`#if UNITY_IOS`) between CMP completion and plugin initialization:
  - If ATT status is already resolved (`AUTHORIZED`, `DENIED`, `RESTRICTED`), startup continues immediately.
  - If ATT status is `NOT_DETERMINED`, ATT is requested and startup continues from ATT callback.
- Added one-shot flow guard and ATT callback unsubscribe cleanup to prevent duplicate startup continuation and stale event handlers across scene reloads.
- Added `Enable AdMob CMP` inspector toggle to disable the consent form entirely (soft-launch / non-GDPR builds).
- Added `Debug Geography` inspector field (`DebugGeography` enum) to force EEA behavior during development.
- `IsManagingLoadingScene` static property for external scripts to detect whether scene-sync is active.
- If AdMob SDK fails (no internet at launch), CMP is skipped and AVNPlugin still initializes so the provider retry path can run.
- Removed all scene-transition flags and `SceneManager.LoadScene` calls from `GoogleAppTracking` — these now live exclusively in `AVNPlugin`.

#### NoInternetPopup — Retry Path
- `ClosePopup()` resets `AdMobSdkInitializer` state via `AdMobSdkInitializer.Reset()` before calling `RetryInitializationIfNeeded()`, ensuring AdMob can re-attempt initialization after connectivity is restored.

#### In-App Purchasing — Auto-Restore Non-Consumables (`AVNPlugin` & `InAppPurchasingService`)
- Added **In-App Purchasing** inspector field on `AVNPlugin`:
  - `Auto Restore Non Consumable Items On Init` (`bool`, default: `false`) — when enabled, non-consumable products purchased on the device will be automatically restored when the In-App Purchasing service initializes.
- When enabled, after the purchasing service is initialized and the initial FetchPurchases call is sent, any non-consumable products found in the store will have their `OnRestored` callback invoked and be marked as owned locally.
- When disabled (default), users must manually call `RestorePurchases()` to restore non-consumable products.
- Added `InAppPurchasingService.SetAutoRestoreNonConsumablesOnInit(bool enabled)` method to configure the auto-restore behavior.
- Added `InAppPurchasingService.CheckAndRestoreNonConsumables()` method to fetch and restore only non-consumable products (similar to `CheckAndRestoreSubscriptions()` for subscriptions).
- Auto-restore is triggered during the initialization sequence in `RunServiceDependenciesInitializationSequence()`, after the purchasing service is fully initialized.

---

### Changed

#### AVNPlugin — Initialization
- `Update()` now polls `TryLoadNextSceneIfReady()` whenever `syncLoadingWithProvider` is enabled and scene has not yet loaded, regardless of provider state. Previously only polled when `IsAdsProviderInitialized` was already true — this missed the failure-fallback path.
- `HandleProviderInitializationFailed()` now calls `TryLoadNextSceneIfReady()` so the fallback fires immediately on failure rather than waiting for the next Update tick.

#### Firebase Initialization (`FinzFirebaseInitilizationManager`)
- **Fixed**: failure callback was incorrectly sending `"Firebase Initialized Successfully"` as the error message. Now sends `"Firebase initialization failed. Dependency status: <status>"`.
- **Fixed**: `catch (Exception e)` block was silently swallowing all exceptions. Now calls `Debug.LogError` with the full exception and invokes `OnFirebaseInitilizaFailed` so AVNPlugin's timeout/sequence continues correctly.
- **Fixed**: `Crashlytics.IsCrashlyticsCollectionEnabled = true` was set before confirming dependencies were available. Moved inside the `DependencyStatus.Available` branch to prevent a `NullReferenceException` on devices where Firebase dependencies are missing.

---

### Fixed

- Scene could switch immediately when starting offline because scene gate had no internet check. Fixed by `HasInternetConnection()` guard inside `TryLoadNextSceneIfReady()`.
- CMP callback was previously responsible for calling `SceneManager.LoadScene`. This caused scene transitions to happen from the wrong layer. Scene transitions are now exclusively owned by `AVNPlugin`.
- `initializeOnStart = true` + `GoogleAppTracking` in the same scene would run two independent init sequences, breaking CMP timing. Documented as a required constraint and guarded in Getting Started docs.

---

### Documentation

- **Plugin Guide** (`AVN_PLUGIN_GUIDE.md`):
  - Added **Section 3 — Scene Loading & Startup Sync**: inspector fields, startup flow diagrams for online and offline scenarios, provider failure fallback behavior, and stuck-at-loading-scene edge case table.
  - Added **Section 3b — GoogleAppTracking — CMP Startup Orchestrator**: inspector fields, startup responsibilities, and `initializeOnStart` constraint.
  - Updated Table of Contents.
- **Getting Started** (`AVN_PLUGIN_GETTING_STARTED.md`): `Sync Loading With Provider` and `Next Scene To Load` fields noted in Ad Loading section, `initializeOnStart = false` requirement noted for GoogleAppTracking users.
