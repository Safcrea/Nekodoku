using System;

namespace Sych.QuickActionsAssets.Runtime
{
    public interface IQuickActionsBridge
    {
        string LastPerformedQuickActionId { get; }

        event Action<string> QuickActionPerformed;

        void ResetLastPerformedQuickActionId();
        bool AddQuickAction(string json);
        bool RemoveQuickAction(string id);
        void RemoveAllQuickActions();
        string GetAllQuickActions();
    }
}