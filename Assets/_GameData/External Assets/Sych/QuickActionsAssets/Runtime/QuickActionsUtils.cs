using System;
using System.Collections.Generic;
using Sych.QuickActionsAssets.Runtime.Tools;

namespace Sych.QuickActionsAssets.Runtime
{
    public static class QuickActionsUtils
    {
        /// <summary>
        /// Serialize QuickActionItem to json string.
        /// </summary>
        public static string ItemToJson(this QuickActionItem quickActionItem) =>
            Json.Serialize(ItemToDictionary(quickActionItem));

        /// <summary>
        /// Serialize an enumerator QuickActionItems to json string.
        /// </summary>
        public static string ItemsToJson(this IEnumerable<QuickActionItem> items)
        {
            if (items == null)
                throw new Exception($"{nameof(items)} can`t be null");

            var itemsRaw = new List<Dictionary<string, object>>();
            foreach (var item in items)
                itemsRaw.Add(ItemToDictionary(item));
            return Json.Serialize(itemsRaw);
        }

        /// <summary>
        /// Try to deserialize json as list of QuickActionItems.
        /// </summary>
        public static bool TryToCreateItemsFromJson(string json, out List<QuickActionItem> items)
        {
            items = new List<QuickActionItem>();

            if (string.IsNullOrEmpty(json))
                return false;

            if (Json.Deserialize(json) is not List<object> itemsRaw || itemsRaw.Count == 0)
                return false;

            foreach (var itemRaw in itemsRaw)
            {
                if (itemRaw is not Dictionary<string, object> shortcutItem)
                    continue;

                items.Add(CreateItemFromDictionary(shortcutItem));
            }

            return items.Count != 0;
        }

        /// <summary>
        /// Try to deserialize json as QuickActionItem.
        /// </summary>
        public static bool TryToCreateItemFromJson(string json, out QuickActionItem item)
        {
            item = null;

            if (string.IsNullOrEmpty(json))
                return false;

            if (Json.Deserialize(json) is not Dictionary<string, object> itemsRaw)
                return false;

            if (itemsRaw.Count == 0)
                return false;

            item = CreateItemFromDictionary(itemsRaw);
            return item != null;
        }

        private static QuickActionItem CreateItemFromDictionary(Dictionary<string, object> itemsRaw)
        {
            if (!itemsRaw.TryGetValue("id", out var id))
                throw new Exception($"{nameof(id)} not exist in json");

            if (!itemsRaw.TryGetValue("title", out var title))
                throw new Exception($"{nameof(title)} not exist in json");

            itemsRaw.TryGetValue("subTitle", out var subTitle);
            itemsRaw.TryGetValue("iconType", out var iconType);
            itemsRaw.TryGetValue("userInfo", out var userInfo);

            return new QuickActionItem(
                id as string,
                title as string,
                subTitle as string,
                Enum.TryParse(iconType as string, out IconType icon) ? icon : IconType.None,
                userInfo as string
            );
        }

        private static Dictionary<string, object> ItemToDictionary(QuickActionItem item)
        {
            if (item == null)
                throw new NullReferenceException(nameof(item));

            return new Dictionary<string, object>
            {
                { "id", item.Id },
                { "title", item.Title },
                { "subTitle", item.SubTitle },
                { "iconType", item.IconType.ToString() },
                { "userInfo", item.UserInfo },
            };
        }
    }
}
