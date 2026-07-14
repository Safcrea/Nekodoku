using System;
using Sych.QuickActionsAssets.Example.Tools;
using Sych.QuickActionsAssets.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Sych.QuickActionsAssets.Example
{
    public class ExampleController : MonoBehaviour
    {
        [SerializeField]
        private LogView _logView;

        [SerializeField]
        private Button _addQuickAction;

        [SerializeField]
        private Button _removeQuickActionById;

        [SerializeField]
        private Button _removeAllQuickActions;

        [SerializeField]
        private Button _checkQuickActionById;

        [SerializeField]
        private Button _logAllQuickActionIds;

        [SerializeField]
        private InputField _actionId;

        [SerializeField]
        private InputField _actionTitle;

        [SerializeField]
        private InputField _actionSubtitle;

        [SerializeField]
        private InputField _actionIconType;

        [SerializeField]
        private Text _launchedAction;

        [SerializeField]
        private Text _title;

        private string InputId => _actionId.text;
        private string InputTitle => _actionTitle.text;
        private string InputSubtitle => _actionSubtitle.text;
        private IconType InputIconType =>
            Enum.TryParse(_actionIconType.text, out IconType iconType) ? iconType : IconType.None;

        private void Awake()
        {
            _addQuickAction.onClick.AddListener(AddActionClicked);
            _removeQuickActionById.onClick.AddListener(RemoveActionByIdClicked);
            _removeAllQuickActions.onClick.AddListener(RemoveAllActionsClicked);
            _checkQuickActionById.onClick.AddListener(CheckActionByIdClicked);
            _logAllQuickActionIds.onClick.AddListener(LogAllQuickActionIdsClicked);

            _actionId.text = "test_id";
            _actionTitle.text = "test_title";
            _actionSubtitle.text = "test_subtitle";
            _actionIconType.text = "Compose";

            _logView.LogMessage($"{_title.text} started.");

            if (QuickActions.LastPerformedId != null)
                SetPerformedId(QuickActions.LastPerformedId);

            QuickActions.Performed += SetPerformedId;
        }

        private void OnDestroy()
        {
            _addQuickAction.onClick.RemoveAllListeners();
            _removeQuickActionById.onClick.RemoveAllListeners();
            _removeAllQuickActions.onClick.RemoveAllListeners();
            _checkQuickActionById.onClick.RemoveAllListeners();
            _logAllQuickActionIds.onClick.RemoveAllListeners();

            QuickActions.Performed -= SetPerformedId;
        }

        private void SetPerformedId(string id)
        {
            _logView.LogMessage($"Quick action id: '{id}' performed");
            _launchedAction.text = $"Performed action id: {id}";
        }

        private void AddActionClicked()
        {
            var item = new QuickActionItem(
                InputId,
                InputTitle,
                InputSubtitle,
                InputIconType,
                string.Empty
            );
            var result = QuickActions.Add(item);

            if (result)
                _logView.LogMessage($"Quick action id: '{InputId}' added");
            else
                _logView.LogError($"Quick action: '{InputId}' add failed");
        }

        private void RemoveActionByIdClicked()
        {
            var result = QuickActions.Remove(InputId);
            if (result)
                _logView.LogMessage($"Quick action id: '{InputId}' removed");
            else
                _logView.LogError($"Quick action: '{InputId}' remove failed");
        }

        private void RemoveAllActionsClicked()
        {
            if (Application.isEditor)
                _logView.LogError("Remove all actions failed");
            else
            {
                QuickActions.RemoveAll();
                _logView.LogMessage("All actions removed");
            }
        }

        private void CheckActionByIdClicked()
        {
            var result = QuickActions.IsAdded(InputId);
            if (result)
                _logView.LogMessage($"Quick action: '{InputId}' added");
            else
                _logView.LogError($"Quick action: '{InputId}' not added yet");
        }

        private void LogAllQuickActionIdsClicked()
        {
            var items = QuickActions.GetAll();
            if (items == null || items.Count == 0)
            {
                _logView.LogMessage("Any quick action not added yet");
                return;
            }

            var ids = string.Empty;
            foreach (var item in items)
                ids += $" '{item.Id}' ";
            _logView.LogMessage($"All quick actions ids: {ids}");
        }
    }
}
