# AVN Plugin — Getting Started

A step-by-step checklist to get the plugin running in a new project. Follow every step in order.

---

## Step 1 — Enable Scripting Symbols

Open Unity top toolbar: `AVN/Manage Scripting Symbols`.

Enable all **Required Symbols**:
- `USE_AVNADS_PLUGIN`
- `USE_FIREBASE`
- `USE_ADMOB`
  
Choose One Of The Following **Ads Provider Symbols**:
- `USE_LEVELPLAY`
- `USE_MAX`

Optional symbols can be enabled based on project needs:
- `USE_AVNADS_PLUGIN_DEBUG`
- `USE_BYTEBREW`
- `USE_IAP`

For full details on what each optional symbol does, see the **Scripting Symbols** section in the Plugin Guide.
---

## Step 2 — Loading Scene: AdsBootstrap Prefab

Drag `AdsBootstrap.prefab` into your **loading / splash scene** — the scene that runs once at startup before the main menu.

`AdsBootstrap` is a `DontDestroyOnLoad` singleton. It must exist in only **one** scene. All SDK initialization, ad loading, and service startup happens automatically once it wakes up.

The prefab hierarchy looks like this:

```
AdsBootstrap
├── Ad Controllers          ← one child per ad type (see Step 4)
├── Services                ← AppsFlyer + ByteBrew objects (see Step 6)
└── AVNPluginCanvas         ← No-Internet popup & Toast system (see Step 7)
```

---

## Step 3 — Plugin Initialization & CMP Setup

### Configure Plugin Initialization Mode

Select `AdsBootstrap` → `AVNPlugin` component → **Variables** section.

Set `Initialize On Start` dropdown to one of:

| Value | Behavior |
|-------|----------|
| `StartOnInit` | Plugin initializes immediately in `Start()`. No external orchestration needed. |
| `StartByCMP` | **Recommended.** Plugin waits for `GoogleAppTracking` to manage initialization. CMP displays first, then calls `AVNPlugin.Initialize()`. |

**For CMP-managed flow (recommended):** Set to `StartByCMP`.

### CMP & Consent Flow

The `GoogleAppTracking.cs` script is placed on the AdsBootstrap prefab and orchestrates startup on all platforms:

1. Initializes AdMob.
2. Runs CMP (UMP) when enabled and required.
3. On iOS only, requests ATT if status is `NOT_DETERMINED`.
4. Calls `AVNPlugin.Initialize()` after the consent chain is resolved.

**Recommended settings for showing CMP:**
- `Enable AdMob CMP = true`
- `Debug Geography = Disabled` for production

For iOS, ATT is handled in code under `#if UNITY_IOS` inside `GoogleAppTracking`; a separate ATT-only prefab is no longer required in the standard flow.

---

## Step 4 — Configure Auto-Load Settings

Select the `AdsBootstrap` GameObject and look at the **Ad Loading** foldout on the `AVNPlugin` component. There is one toggle per ad type:

| Toggle | What it controls |
|--------|-----------------|
| `Auto Load Banner` | Loads the banner ad automatically on init |
| `Auto Load MREC` | Loads the MREC banner automatically on init |
| `Auto Load Rewarded` | Loads the rewarded ad automatically on init |
| `Auto Load App Open` | Loads the AdMob App Open inventory automatically on startup |
| `Auto Load Interstitial` | Loads interstitial ad(s) automatically on init |
| `Sync Loading With Provider` | Keeps loading scene locked until LevelPlay provider initialization succeeds |
| `Next Scene To Load` | Scene name loaded after the sync gate is satisfied |

When using CMP-managed startup flow from GoogleAppTracking, keep `initializeOnStart = false` on AVNPlugin so GoogleAppTracking owns initialization sequencing.

**If a toggle is OFF** you are responsible for triggering the load manually before you try to show that ad. Use the corresponding load method:

```csharp
AVNPlugin.DTInstance.LoadInterAd();
AVNPlugin.DTInstance.LoadRewardAd();
AVNPlugin.DTInstance.LoadBanner(showOnLoad: true);
```

### Does Show also trigger a Load?

| Ad Type | Show triggers load? |
|---------|-------------------|
| **Interstitial** | ✅ Yes — after a successful show/close cycle the controller automatically queues the next load internally. However, the **first-ever** load still requires either auto-load or a manual `LoadInterAd()` call. |
| **Rewarded** | ✅ Yes — after a show/close cycle the controller schedules a reload internally. Same rule applies: the first load requires auto-load or `LoadRewardAd()`. |
| **Banner / MREC** | ❌ No — banners do not self-reload on show. You must call `LoadBanner()` explicitly when auto-load is off. |
| **App Open** | ✅ Yes — the AdMob App Open service automatically reloads after each close. Startup loading can come from `Auto Load App Open`, `Show App Open On Session Start`, or both. |

### App Open startup flags

Select `AdsBootstrap → Ad Controllers → AppOpenInterAdController`.

| Field | What it controls |
|-------|------------------|
| `Show App Open On Session Start` | On cold start, upgrades the startup app-open load to `showOnLoad = true` so the first loaded App Open ad is shown immediately. |
| `Should Skip First Session` | Only checked when `Show App Open On Session Start` is enabled. If true, the very first installed session only loads the App Open ad and does not auto-show it. Later sessions can auto-show normally. |

These two flags only affect the cold-start flow. Normal background → foreground App Open behaviour stays unchanged.

---

## Step 5 — Ad IDs Configuration (ScriptableObject)

All Ad Unit IDs are now stored in a **ScriptableObject config asset** instead of directly on AVNPlugin.

### Create the Ad IDs Config Asset

1. In your project, right-click in a folder (e.g., `Assets/_DT1_AdsPlugin/Config/` or similar).
2. Select **Create → AVN Ads Plugin → Ad IDs Config**.
3. Name it something like `DefaultAdIdsConfig` or `ProductionAdIds`.

### Assign Config to AVNPlugin

1. Select `AdsBootstrap` → `AVNPlugin` component.
2. In the **Ad IDs** foldout, find the `Ad Ids Config` field.
3. Drag the newly created config asset into that field.

### Fill in the Ad IDs

Select your Ad IDs Config asset and fill in all fields:

| Field | Description |
|-------|----------|
| `Ads SDK Key` | Your ads provider SDK key (IronSource / LevelPlay SDK key) |
| `Banner Ad Unit Id` | LevelPlay banner ad unit ID |
| `Mrec Ad Unit Id` | LevelPlay MREC ad unit ID |
| `Rewarded Ad Unit Id` | LevelPlay rewarded video ad unit ID |
| `Inter Ad Unit Ids` | Array of LevelPlay interstitial ad unit IDs (supports multiple slots for backfill) |
| `App Open Ad Unit Id 1` | AdMob primary App Open ad unit ID |
| `App Open Ad Unit Id 2` | AdMob backfill App Open ad unit ID. Leave empty if you do not want a backfill slot. |

### Editor Utility Buttons

In the same config asset inspector, the **Editor** foldout provides utility buttons:

- **Build IDs JSON** — Generates a formatted JSON preview of all configured IDs
- **Copy All IDs to Clipboard** — Builds the JSON and copies it to clipboard for easy sharing with the team or for Firebase Remote Config

Firebase Remote Config is optional and only overrides these ScriptableObject values later when available. The asset values act as **fallback defaults** when the fetch fails or the device is offline.

---

## Step 6 — Firebase Remote Config Setup

The plugin uses Firebase Remote Config to receive ad IDs and settings remotely, so you can update them without a new build.

You need to create **following variables** in your Firebase project's Remote Config:

---

#### Variable 1 — Ad Configuration

| Setting | Value |
|---------|-------|
| **Parameter name** | `AdConfig` |
| **Data type** | `JSON` |
| **Default value** | *(paste the JSON below)* |

```json
{
  "EnableInterFromLevel": 5,
  "DelayBeforeFirstAd": 40,
  "InterAdDelay": 20,
  "ShowRateUsOnLevel": 5,
  "ShowNotificationOnLevel": 7,
  "InterstitialShowMode": 2,
  "-": "------------------------------------",
  "EnableBannerAd": true,
  "EnableInterAd": true,
  "EnableRewardedAd": true,
  "EnableAppOpenAd": true,
  "--": "------------------------------------",
  "meta": {
    "InterstitialShowMode": {
      "0": "Show the inter ad from ID at index 0 only",
      "1": "Iterate over each ID",
      "2": "Use ID at Index 0 to show Ad, with Treating inded 1 as a backfill",
      "3": "Use ID at Index 0 to show Ad, with Treating inded 1 and 2 as a backfill_Could be Used with Three Tier Inter",
      "4": "First Try to load Ad at ID Index 0, if it fails then to 1, if it fails then to 2"
    }
  }
}
```

`InterstitialShowMode` values: `0` = ShowDefault, `1` = ShowIterateSequence, `2` = ShowDefaultWithOneBackfill, `3` = ShowDefaultWithTwoBackfill.

#### Variable 2 — Ad Unit IDs

| Setting | Value |
|---------|-------|
| **Parameter name** | `AdIdsConfig` |
| **Data type** | `JSON` |
| **Default value** | *(paste the JSON below, filled with your real IDs)* |

```json
{
  "BannerAdUnitId": "",
  "MrecAdUnitId": "",
  "RewardedAdUnitId": "",
  "InterAdUnitIds": [
      "id_1",
      "id_2"
  ],
  "AppOpenAdUnitId1": "",
  "AppOpenAdUnitId2": ""
}
```

Fill in your actual SDK ad unit IDs plus the AdMob App Open IDs before publishing.

#### Variable 3 — CMP Eligibly Users

| Setting | Value |
|---------|-------|
| **Parameter name** | `CMPEligibleUser` |
| **Data type** | `Bool` |
| **Default value** | *true* |

Add Conditions to give True value to GDRP Countries and false to the rest

---

Once Firebase is live the plugin fetches these values on every session start and overrides the inspector-set values. The inspector values act as **fallback defaults** when the fetch fails or the device is offline. Both configs are also cached locally in `PlayerPrefs` so they load instantly on the next launch even before the fetch completes — see the Plugin Guide for details.

---

## Step 7 — In-App Purchasing (Optional)

If the game has IAP:

1. On `AdsBootstrap` → `AVNPlugin`, enable the **Use In-App Purchasing** checkbox.
2. Enter the **Remove Ads Product Id** — the store product ID for your Remove Ads SKU. This product is registered automatically as a `NonConsumable`; you do not need to add it to the list below.
3. For all other products, expand the **In App Products** list and add an entry per product:
   - `Product Name` — internal name used in code (e.g. `"GoldBundle"`)
   - `Product Id` — the store product ID (e.g. `"com.studio.game.goldbundle"`)
   - `Product Type` — `Consumable`, `NonConsumable`, or `Subscription`
4. Wire callbacks in `GameInApp.cs` → `OnSetupCallbacks()`. See the [User Setup Scripts](#user-setup-scripts) section in the Plugin Guide.

---

## Step 8 — Configure Plugin Rules

On `AdsBootstrap` → `AVNPlugin` → **Plugin Rules** foldout, set the threshold levels:

| Field | Default | Meaning |
|-------|---------|---------|
| `Enable Inter From Level` | 3 | Interstitial ads will not show on levels below this number |
| `Show Rate Us On Level` | 4 | Native Rate Us dialog is first eligible on this level |
| `Show Notification On Level` | 5 | Notification prompt is first eligible on this level |

These are the **starting defaults**. You can override them at runtime via:

```csharp
AVNPlugin.DTInstance.EnableInterFromLevel    = 5;
AVNPlugin.DTInstance.ShowRateUsOnLevel       = 6;
AVNPlugin.DTInstance.ShowNotificationOnLevel = 7;
```

---
## 9. User Setup Scripts

These four scripts live in `Assets/_DT1_AdsPlugin/AVNPlugin/Scripts/User Setup/`. They are the **only files you are expected to edit** when integrating the plugin into your game. Everything else is internal plugin code.

---

### GameAdsCheck

**File:** `GameAdsCheck.cs`

Override these three methods to add your own game-logic guards. The plugin calls these before showing ads or prompts. Return `true` to allow the action, `false` to block it.

```csharp
public class GameAdsCheck : AVNHandlerSetup
{
    public override bool CanShowInterstitial()
    {
        // Example: don't show ads during tutorial
        return GameManager.CurrentLevel >= AVNPlugin.DTInstance.EnableInterFromLevel
               && !TutorialManager.IsActive;
    }

    public override bool CanShowRateUsDialog()
    {
        // Example: only show on main menu, not mid-game
        return SceneManager.GetActiveScene().name == "MainMenu";
    }

    public override bool CanShowNotification()
    {
        return true; // show whenever the plugin decides it's time
    }
}
```

> `CanShowInterstitial()` is checked every time `ShowInterAd()` is called in addition to the plugin's own cooldown and level checks.

---

### GameRemoteConfig

**File:** `GameRemoteConfig.cs`

Register the Firebase Remote Config keys your game uses. The plugin fetches values on startup and calls the callbacks you provide with the fetched value as a string.

```csharp
public class GameRemoteConfig : AVNHandlerSetup
{
    protected override void OnRegisterKeys()
    {
        RegisterKey("hard_mode_enabled", RemoteDataType.Bool,   v => GameConfig.HardMode   = v == "true");
        RegisterKey("starting_coins",    RemoteDataType.Int,    v => GameConfig.StartCoins  = int.Parse(v));
        RegisterKey("welcome_message",   RemoteDataType.String, v => GameConfig.WelcomeMsg  = v);
        RegisterKey("GameSettings",      RemoteDataType.Json,   v =>
        {
            var settings = JsonUtility.FromJson<GameSettings>(v);
            GameConfig.Apply(settings);
        });
    }

    protected override void OnConfigFetched()
    {
        // Called after every successful fetch.
        // All registered key callbacks have already fired at this point.
        GameConfig.MarkRefreshed();
    }
}
```

**RemoteDataType values:** `Bool`, `Int`, `Float`, `String`, `Json`

> The plugin applies a cached version of the config instantly on startup (from the last successful fetch) and then re-fetches in the background. Your callbacks fire for both the cached apply and the fresh fetch.

---

### GameInApp

**File:** `GameInApp.cs`

Wire purchase, restore, and refund callbacks for each of your IAP products. The plugin calls `OnSetupCallbacks()` automatically during initialization.

```csharp
public class GameInApp : AVNHandlerSetup
{
    public override void OnSetupCallbacks()
    {
        // Consumable — only needs a purchase callback
        SetProductPurchasedCallback("GoldBundle", () => InventoryManager.AddGold(500));

        // Non-Consumable — wire purchase, restore, and refund
        SetProductPurchasedCallback("VIPPass", () => GameConfig.IsVIP = true);
        SetProductRestoredCallback ("VIPPass", () => GameConfig.IsVIP = true);
        SetProductRefundedCallback ("VIPPass", () => GameConfig.IsVIP = false);

        // Subscription
        SetProductPurchasedCallback("WeeklyPass", () => SubscriptionManager.Activate());
        SetProductRefundedCallback ("WeeklyPass", () => SubscriptionManager.Deactivate());
    }

    // RemoveAds is handled separately — override these instead of registering callbacks above
    protected override void OnRemoveAdsPurchased() { /* ads already disabled by plugin */ }
    protected override void OnRemoveAdsRestored()  { /* re-apply no-ads UI state */ }
    protected override void OnRemoveAdsRefunded()  { /* ads re-enabled by plugin */ }

    // Global catch-all — fires for every successful purchase
    protected override void OnProductPurchased(string productId)
    {
        AnalyticsManager.LogPurchase(productId);
    }

    // Called when the full restore-purchases flow completes
    protected override void OnPurchaseRestored()
    {
        UIManager.ShowRestoreSuccessMessage();
    }
}
```

> Do **not** register the RemoveAds product manually in `OnSetupCallbacks()`. It is registered automatically from the inspector field. Override `OnRemoveAdsPurchased` / `OnRemoveAdsRestored` / `OnRemoveAdsRefunded` instead.

---

### GameAnalyticsEvents

**File:** `GameAnalyticsEvents.cs`

A static helper class for sending analytics events. Two events are pre-wired for you:

| Method | Description |
|--------|-------------|
| `LevelAnalysis(int levelNo, LevelState levelState)` | Sends a standard level-flow event (start / complete / fail). Call this at the start and end of each level. |
| `UserAnalysis()` | Sends a user ID property event. Typically called once on first launch or after sign-in. |

To add custom events, add static methods to this class:

```csharp
public static void ItemPurchased(string itemId, int cost)
{
    var props = new Dictionary<string, string>
    {
        { "item_id", itemId },
        { "cost",    cost.ToString() }
    };
    AVNPlugin.DTInstance?.SendCustomGameEvent("ITEM_PURCHASED", props, logToAppsFlyer: true, logToByteBrew: true);
}
```

Call your methods from anywhere in game code:

```csharp
GameAnalyticsEvents.LevelAnalysis(currentLevel, LevelState.Start);
GameAnalyticsEvents.LevelAnalysis(currentLevel, LevelState.Complete);
GameAnalyticsEvents.ItemPurchased("sword_01", 150);
```
---

## Quick Checklist

- [ ] Opened `AVN/Manage Scripting Symbols` and enabled required symbols (`USE_AVNADS_PLUGIN`, `USE_LEVELPLAY`)
- [ ] Optional symbols enabled as needed (`USE_AVNADS_PLUGIN_DEBUG`, `USE_BYTEBREW`, `USE_IAP`) — see Plugin Guide for details
- [ ] `AppTrackingTransparency.prefab` or `GoogleAppTracking.prefab` placed in **first scene**
- [ ] `AdsBootstrap.prefab` placed in **loading scene**
- [ ] On AVNPlugin (AdsBootstrap): `Initialize On Start` set to `StartByCMP` for CMP-managed startup
- [ ] On GoogleAppTracking: `Enable AdMob CMP = true`, `Debug Geography = Disabled` (unless actively debugging consent)
- [ ] Created **Ad IDs Config** ScriptableObject asset and assigned it to `AVNPlugin → Ad Ids Config`
- [ ] All Ad Unit IDs filled in the config asset, including AdMob `App Open Ad Unit Id 1` and optional `App Open Ad Unit Id 2`
- [ ] On AVNPlugin (AdsBootstrap): `Sync Loading With Provider` and `Next Scene To Load` configured if using loading sync
- [ ] Auto-load toggles reviewed; manual `Load` calls added where needed
- [ ] Firebase Remote Config JSON files uploaded and published
- [ ] (If IAP) `Use In-App Purchasing` enabled, Remove Ads ID set, extra products added
- [ ] Plugin Rules level thresholds set to match game design
- [ ] Services configured under `AdsBootstrap → Services` (see Plugin Guide)
- [ ] User Setup scripts filled in (`GameAdsCheck`, `GameRemoteConfig`, `GameInApp`, `GameAnalyticsEvents`)
