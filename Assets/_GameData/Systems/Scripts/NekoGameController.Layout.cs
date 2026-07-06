using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void BuildWordSlotsForLevel()
        {
            if (wordRoot == null || board == null)
            {
                return;
            }

            for (int i = wordRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(wordRoot.GetChild(i).gameObject);
            }

            wordSlotTexts = new Text[board.Size];
            wordSlotImages = new Image[board.Size];
            for (int i = 0; i < board.Size; i++)
            {
                RectTransform slot = CreatePanel(wordRoot, $"Word Slot {i + 1}", new Color(1f, 0.99f, 0.96f, 0.92f));
                Outline outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.26f);
                outline.effectDistance = new Vector2(2f, -2f);

                Text text = CreateText(slot, string.Empty, 44, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
                text.raycastTarget = false;
                SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                wordSlotImages[i] = slot.GetComponent<Image>();
                wordSlotTexts[i] = text;
            }

            RefreshWordSlots();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(wordRoot);
        }

        private void RefreshWordSlots()
        {
            if (board == null || wordSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < wordSlotTexts.Length; i++)
            {
                NekoCoord coord = board.Level.Solution[i];
                bool visible = board.HasRevealedCat(coord.Row, coord.Column) && letterVisibleCats.Contains(coord);
                wordSlotTexts[i].text = visible ? board.Level.TargetWord[i].ToString() : string.Empty;
                wordSlotTexts[i].color = visible ? InkColor : SoftInkColor;
                if (wordSlotImages != null && i < wordSlotImages.Length && wordSlotImages[i] != null)
                {
                    wordSlotImages[i].color = visible ? new Color(1f, 0.96f, 0.82f, 0.96f) : new Color(1f, 0.99f, 0.96f, 0.92f);
                }
            }
        }

        private bool TryGetWordSlotCenter(int wordIndex, out Vector2 center, out float slotSize)
        {
            center = Vector2.zero;
            slotSize = 0f;
            if (juiceRoot == null
                || wordSlotTexts == null
                || wordIndex < 0
                || wordIndex >= wordSlotTexts.Length
                || wordSlotTexts[wordIndex] == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            RectTransform slotRect = (RectTransform)wordSlotTexts[wordIndex].transform.parent;
            Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
            center = juiceRoot.InverseTransformPoint(worldCenter);

            Vector3 worldMin = slotRect.TransformPoint(new Vector3(slotRect.rect.xMin, slotRect.rect.yMin, 0f));
            Vector3 worldMax = slotRect.TransformPoint(new Vector3(slotRect.rect.xMax, slotRect.rect.yMax, 0f));
            Vector3 localMin = juiceRoot.InverseTransformPoint(worldMin);
            Vector3 localMax = juiceRoot.InverseTransformPoint(worldMax);
            slotSize = Mathf.Min(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));
            return slotSize > 0f;
        }
    }
}
