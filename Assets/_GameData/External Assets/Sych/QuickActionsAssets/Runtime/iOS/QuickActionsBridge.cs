using System;

namespace Sych.QuickActionsAssets.Runtime.iOS
{
    public sealed class QuickActionsBridge : IQuickActionsBridge
    {
        public string LastPerformedQuickActionId => QuickActionsNative.LastPerformedQuickActionId;

        public event Action<string> QuickActionPerformed
        {
            add
            {
                QuickActionsNative.EnsureQuickActionCallbackRegistered();
                QuickActionsNative.QuickActionPreformed += value;
            }
            remove => QuickActionsNative.QuickActionPreformed -= value;
        }

        public void ResetLastPerformedQuickActionId() => QuickActionsNative.ResetLastPerformedQuickActionId();

        public bool AddQuickAction(string json) => QuickActionsNative.AddQuickAction(json);

        public bool RemoveQuickAction(string id) => QuickActionsNative.RemoveQuickAction(id);

        public void RemoveAllQuickActions() => QuickActionsNative.RemoveAllQuickActions();

        public string GetAllQuickActions() => QuickActionsNative.GetAllQuickActions();
    }
}