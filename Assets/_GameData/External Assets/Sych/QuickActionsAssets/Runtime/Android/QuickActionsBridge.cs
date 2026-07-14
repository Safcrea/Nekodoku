using System;

namespace Sych.QuickActionsAssets.Runtime.Android
{
    public sealed class QuickActionsBridge : IQuickActionsBridge
    {
        public string LastPerformedQuickActionId => QuickActionsNative.LastPerformedQuickActionId;

        public event Action<string> QuickActionPerformed
        {
            add => QuickActionsNative.QuickActionPerformed += value;
            remove => QuickActionsNative.QuickActionPerformed -= value;
        }

        public QuickActionsBridge() => QuickActionsNative.Initialize();

        public void ResetLastPerformedQuickActionId() => QuickActionsNative.ResetLastPerformedQuickActionId();

        public bool AddQuickAction(string json) => QuickActionsNative.AddQuickAction(json);

        public bool RemoveQuickAction(string id) => QuickActionsNative.RemoveQuickAction(id);

        public void RemoveAllQuickActions() => QuickActionsNative.RemoveAllQuickActions();

        public string GetAllQuickActions() => QuickActionsNative.GetAllQuickActions();
    }
}