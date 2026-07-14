using System;

namespace Sych.QuickActionsAssets.Runtime
{
    public sealed class NullQuickActionsBridge : IQuickActionsBridge
    {
        public string LastPerformedQuickActionId => null;

        public event Action<string> QuickActionPerformed;

        public void ResetLastPerformedQuickActionId() { }

        public bool AddQuickAction(string json) => false;

        public bool RemoveQuickAction(string id) => false;

        public void RemoveAllQuickActions() { }

        public string GetAllQuickActions() => "[]";
    }
}