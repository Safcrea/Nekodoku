using System;
using UnityEngine;

namespace Sych.QuickActionsAssets.Runtime
{
    [Serializable]
    public sealed class QuickActionItem
    {
        /// <summary>
        /// Unique identifier of quick action for management and identification.
        /// </summary>
        [field: SerializeField] public string Id { get; private set; }

        /// <summary>
        /// User-visible title for the quick action.
        /// </summary>
        [field: SerializeField] public string Title { get; private set; }

        /// <summary>
        /// User-visible subtitle for the quick action.
        /// </summary>
        [field: SerializeField] public string SubTitle { get; private set; }

        /// <summary>
        /// Constants for system-provided icons 
        /// </summary>
        [field: SerializeField] public IconType IconType { get; private set; }

        /// <summary>
        /// App-specific information that you can provide for use when your app performs the quick action.
        /// </summary>
        [field: SerializeField] public string UserInfo { get; private set; }

        public QuickActionItem(string id, string title, string subTitle, IconType iconType, string userInfo)
        {
            Id = string.IsNullOrEmpty(id) ? throw new Exception($"{nameof(id)} can`t be empty") : id;
            Title = string.IsNullOrEmpty(title) ? throw new Exception($"{nameof(title)} can`t be empty") : title;
            SubTitle = subTitle;
            IconType = iconType;
            UserInfo = userInfo;
        }
    }
}