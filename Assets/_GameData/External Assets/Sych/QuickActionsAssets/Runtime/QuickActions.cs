using System;
using System.Collections.Generic;
using System.Linq;
using Sych.QuickActionsAssets.Runtime.Tools;
using UnityEngine;
using static Sych.QuickActionsAssets.Runtime.QuickActionsConstants;
using static Sych.QuickActionsAssets.Runtime.Tools.Logger;

namespace Sych.QuickActionsAssets.Runtime
{
    /// <summary>
    /// Control iOS and tvOS app quick actions on the Home Screen and in App Library.
    /// </summary>
    public static class QuickActions
    {
        public const string ContinueGameId = "nekodoku.continue_game";
        public const string RestartLevelId = "nekodoku.restart_level";
        public const string ShowHintId = "nekodoku.show_hint";

        private static readonly IQuickActionsBridge Bridge;

        private static readonly QuickActionItem[] GameQuickActions =
        {
            new QuickActionItem(
                ContinueGameId,
                "Continue Puzzle",
                "Return to your current level",
                IconType.Play,
                string.Empty
            ),
            new QuickActionItem(
                RestartLevelId,
                "Restart Level",
                "Replay the current puzzle",
                IconType.Update,
                string.Empty
            ),
            new QuickActionItem(
                ShowHintId,
                "Get a Hint",
                "Open your puzzle with a hint",
                IconType.Search,
                string.Empty
            ),
        };

        private static readonly string[] LegacyGameQuickActionIds =
        {
            "RestoreStoppedWatch",
            "RestoreGameBoy",
            "NextRestorationReady",
            "RestoreClassicGuitar",
            "PumpFlatFootball",
        };

        /// <summary>
        /// Checks if the platform is supported to add quick actions.
        /// </summary>
        public static bool IsPlatformSupported { get; }

        /// <summary>
        /// Enable or disable logging.
        /// true - by default.
        /// </summary>
        public static bool LoggingEnable
        {
            set => Tools.Logger.LoggingEnable = value;
        }

        /// <summary>
        /// Retrieves the Id of the last performed quick action.
        /// If app launched from quick action this property will contain the id of performed quick action.
        /// </summary>
        public static string LastPerformedId => Bridge.LastPerformedQuickActionId;

        /// <summary>
        /// Raised when a quick action is performed
        /// Returns Id of quick action.
        /// </summary>
        public static event Action<string> Performed
        {
            add => Bridge.QuickActionPerformed += value;
            remove => Bridge.QuickActionPerformed -= value;
        }

        static QuickActions()
        {
            switch (Application.isEditor)
            {
                case false
                    when Application.platform == RuntimePlatform.Android
                        && ReflectionUtils.TryToCreateInstance<IQuickActionsBridge>(
                            AndroidQuickActionsBridgeType,
                            AndroidQuickActionsAssembly,
                            out var androidBridge
                        ):
                    IsPlatformSupported = true;
                    Bridge = androidBridge;
                    break;
                case false
                    when Application.platform == RuntimePlatform.IPhonePlayer
                        && ReflectionUtils.TryToCreateInstance<IQuickActionsBridge>(
                            IOSQuickActionsBridgeType,
                            IOSQuickActionsAssembly,
                            out var iosBridge
                        ):
                    IsPlatformSupported = true;
                    Bridge = iosBridge;
                    break;
                default:
                    IsPlatformSupported = false;
                    Bridge = new NullQuickActionsBridge();
                    break;
            }
        }

        /// <summary>
        /// Registers Nekodoku's Home Screen actions. Existing app-owned actions are updated when
        /// their title, subtitle, icon, or user info changes; unrelated actions are left untouched.
        /// </summary>
        public static void AddGameQuickActions()
        {
            if (!IsPlatformSupported)
                return;

            var addedActions = GetAll();
            foreach (var legacyActionId in LegacyGameQuickActionIds)
            {
                if (addedActions.All(action => action.Id != legacyActionId))
                    continue;

                Remove(legacyActionId);
                addedActions.RemoveAll(action => action.Id == legacyActionId);
            }

            foreach (var desiredAction in GameQuickActions)
            {
                var addedAction = addedActions.FirstOrDefault(action => action.Id == desiredAction.Id);
                if (addedAction != null && HasSameDefinition(addedAction, desiredAction))
                    continue;

                if (addedAction != null)
                    Remove(addedAction.Id);

                Add(desiredAction);
            }
        }

        /// <summary>
        /// Returns true when the supplied identifier belongs to a Nekodoku Home Screen action.
        /// </summary>
        public static bool IsGameAction(string id) =>
            id == ContinueGameId || id == RestartLevelId || id == ShowHintId;

        /// <summary>
        /// Retrieves and clears the game action that launched the app, if there is one.
        /// </summary>
        public static bool TryConsumeLastPerformedGameAction(out string id)
        {
            id = LastPerformedId;
            if (!IsGameAction(id))
            {
                id = null;
                return false;
            }

            ResetLastPerformedQuickActionId();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AddGameQuickActionsOnStartup() => AddGameQuickActions();

        private static bool HasSameDefinition(QuickActionItem first, QuickActionItem second) =>
            string.Equals(first.Title, second.Title, StringComparison.Ordinal)
            && string.Equals(first.SubTitle, second.SubTitle, StringComparison.Ordinal)
            && first.IconType == second.IconType
            && string.Equals(first.UserInfo, second.UserInfo, StringComparison.Ordinal);

        /// <summary>
        /// Reset the Id of the last performed quick action.
        /// </summary>
        public static void ResetLastPerformedQuickActionId() =>
            Bridge.ResetLastPerformedQuickActionId();

        /// <summary>
        /// Add new quick action.
        /// Can only add quick actions with an Id that has not yet been added.
        /// Will return false if quick action with the same Id already exists or if adding failed.
        /// Will return true if quick action added successfully.
        /// </summary>
        public static bool Add(QuickActionItem item)
        {
            if (item == null)
                throw new NullReferenceException(nameof(item));

            try
            {
                if (IsAdded(item.Id))
                {
                    // Exception(
                    //     LogTag,
                    //     new Exception($"Quick action with id: '{item.Id}' already exists")
                    // );
                    return false;
                }

                var json = item.ItemToJson();
                var result = Bridge.AddQuickAction(json);
                Log(
                    LogTag,
                    $"Item with id: '{item.Id}' {(result ? "added successfully" : "add failed")}\nJson: {json}"
                );
                return result;
            }
            catch (Exception e)
            {
                Exception(LogTag, e);
                return false;
            }
        }

        /// <summary>
        /// Add multiple quick actions.
        /// Can only add quick actions with an Id that has not yet been added.
        /// </summary>
        public static void Add(List<QuickActionItem> items)
        {
            if (items == null || items.Count == 0)
                throw new Exception($"{nameof(items)} can`t be null or empty");

            foreach (var shortcutItem in items)
                Add(shortcutItem);
        }

        /// <summary>
        /// Retrieves all added quick actions.
        /// </summary>
        public static List<QuickActionItem> GetAll()
        {
            var json = Bridge.GetAllQuickActions();
            if (string.IsNullOrEmpty(json))
                return new List<QuickActionItem>();

            return !QuickActionsUtils.TryToCreateItemsFromJson(json, out var shortcutItems)
                ? new List<QuickActionItem>()
                : shortcutItems;
        }

        /// <summary>
        /// Retrieves quick action by Id.
        /// Null will be returned if quick action does not exist.
        /// </summary>
        public static QuickActionItem Get(string id) =>
            GetAll().FirstOrDefault(item => item.Id == id);

        /// <summary>
        /// Remove quick action by item Id.
        /// Will return false if quick action with the same Id does not exist or if removing failed.
        /// Will return true if quick action removed successfully.
        /// </summary>
        public static bool Remove(QuickActionItem item)
        {
            if (item == null)
                throw new NullReferenceException(nameof(item));

            return Remove(item.Id);
        }

        /// <summary>
        /// Remove quick action by Id.
        /// Will return false if quick action with the same Id does not exist or if removing failed.
        /// Will return true if quick action removed successfully.
        /// </summary>
        public static bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new Exception($"{nameof(id)} can`t be empty");

            var result = Bridge.RemoveQuickAction(id);
            Log(
                LogTag,
                result ? $"Removed item with id: {id}" : $"Failed to remove item with id: {id}"
            );
            return result;
        }

        /// <summary>
        /// Removes all quick actions.
        /// </summary>
        public static void RemoveAll()
        {
            Bridge.RemoveAllQuickActions();
            Log(LogTag, "Removed all items");
        }

        /// <summary>
        /// Checks by item Id if quick action is already added.
        /// </summary>
        public static bool IsAdded(QuickActionItem item)
        {
            if (item == null)
                throw new NullReferenceException(nameof(item));

            return IsAdded(item.Id);
        }

        /// <summary>
        /// Checks by Id if quick action is already added.
        /// </summary>
        public static bool IsAdded(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new Exception($"{nameof(id)} can`t be empty");

            var quickActions = GetAll();
            return quickActions.Any(action => action.Id == id);
        }
    }
}
