using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Builds Assets/_GameData/Systems/Prefabs/NekoCell.prefab: the board cell, as a real
    /// prefab asset instead of a GameObject hierarchy assembled from scratch in code every
    /// time a level loads. Re-run after changing the cell's visual structure, then re-assign
    /// it on Neko Game Controller's Cell Prefab field if it was ever unassigned.
    ///
    /// Colors here are duplicated from NekoGameController's private theme constants (there's
    /// no shared theme asset yet - that's a follow-up step). Keep them in sync until then.
    /// </summary>
    public static class CellPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_GameData/Systems/Prefabs";
        private const string PrefabPath = PrefabFolder + "/NekoCell.prefab";

        private static readonly Color TileBorderColor = Color.white;
        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color BadgeOutlineColor = new Color(0.1f, 0.09f, 0.08f, 0.65f);

        [MenuItem("Meowdoku/Scene/Build Cell Prefab")]
        public static void BuildCellPrefab()
        {
            Directory.CreateDirectory(PrefabFolder);
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject cellObject = new GameObject("Neko Cell", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline), typeof(NekoCellView));
            RectTransform rect = (RectTransform)cellObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 120f);

            Image backgroundImage = cellObject.GetComponent<Image>();
            backgroundImage.raycastTarget = true;

            Outline backgroundOutline = cellObject.GetComponent<Outline>();
            backgroundOutline.effectColor = TileBorderColor;
            backgroundOutline.effectDistance = new Vector2(3f, -3f);

            Text markText = CreateFullRectText(rect, "Mark", defaultFont, 35, InkColor);
            markText.raycastTarget = false;

            Image nekoImage = CreateNekoImage(rect);
            nekoImage.enabled = false;

            Text badgeText = CreateFullRectText(rect, "Badge", defaultFont, 38, Color.white);
            badgeText.raycastTarget = false;
            Outline badgeOutline = badgeText.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = BadgeOutlineColor;
            badgeOutline.effectDistance = new Vector2(2f, -2f);

            NekoCellView view = cellObject.GetComponent<NekoCellView>();
            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
            serializedView.FindProperty("backgroundOutline").objectReferenceValue = backgroundOutline;
            serializedView.FindProperty("canvasGroup").objectReferenceValue = cellObject.GetComponent<CanvasGroup>();
            serializedView.FindProperty("markText").objectReferenceValue = markText;
            serializedView.FindProperty("badgeText").objectReferenceValue = badgeText;
            serializedView.FindProperty("nekoImage").objectReferenceValue = nekoImage;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(cellObject, PrefabPath);
            Object.DestroyImmediate(cellObject);

            AssetDatabase.Refresh();
            Debug.Log($"Built {PrefabPath}.");
        }

        private static Text CreateFullRectText(RectTransform parent, string name, Font font, int fontSize, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            return text;
        }

        private static Image CreateNekoImage(RectTransform parent)
        {
            GameObject imageObject = new GameObject("Neko Cat", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            RectTransform imageRect = image.rectTransform;
            imageRect.anchorMin = new Vector2(0.12f, 0.12f);
            imageRect.anchorMax = new Vector2(0.88f, 0.88f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = Vector2.zero;
            return image;
        }
    }
}
