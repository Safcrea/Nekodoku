using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The word bar: one slot per letter of the target word, rebuilt each level
    /// (slot count depends on word length, so unlike the board this genuinely
    /// has to be built at runtime). Also owns which cat letters are currently
    /// revealed - the two are the same concern, since a letter only "counts" as
    /// visible once its slot animation has shown it.
    /// </summary>
    public sealed class WordSlotsView : MonoBehaviour
    {
        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color SoftInkColor = new Color(0.43f, 0.39f, 0.36f, 1f);

        [SerializeField]
        private RectTransform wordRoot;

        private readonly HashSet<Coord> letterVisibleCats = new HashSet<Coord>();
        private Text[] wordSlotTexts;
        private Image[] wordSlotImages;
        private Font font;

        public IEnumerable<Coord> VisibleLetters => letterVisibleCats;

        public bool Validate()
        {
            return SceneValidation.LogIfMissing(wordRoot, "Word Root", this);
        }

        public void SetFont(Font sharedFont)
        {
            font = sharedFont;
        }

        public void BuildForLevel(PuzzleBoard board)
        {
            for (int i = wordRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(wordRoot.GetChild(i).gameObject);
            }

            wordSlotTexts = new Text[board.Size];
            wordSlotImages = new Image[board.Size];
            for (int i = 0; i < board.Size; i++)
            {
                RectTransform slot = UiFactory.CreatePanel(wordRoot, $"Word Slot {i + 1}", new Color(1f, 0.99f, 0.96f, 0.92f));
                Outline outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.26f);
                outline.effectDistance = new Vector2(2f, -2f);

                Text text = UiFactory.CreateText(slot, font, string.Empty, 44, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
                text.raycastTarget = false;
                UiFactory.SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                wordSlotImages[i] = slot.GetComponent<Image>();
                wordSlotTexts[i] = text;
            }

            Refresh(board);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(wordRoot);
        }

        public void Refresh(PuzzleBoard board)
        {
            if (wordSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < wordSlotTexts.Length; i++)
            {
                Coord coord = board.Level.Solution[i];
                bool visible = board.HasRevealedCat(coord.Row, coord.Column) && letterVisibleCats.Contains(coord);
                wordSlotTexts[i].text = visible ? board.Level.TargetWord[i].ToString() : string.Empty;
                wordSlotTexts[i].color = visible ? InkColor : SoftInkColor;
                if (wordSlotImages != null && i < wordSlotImages.Length && wordSlotImages[i] != null)
                {
                    wordSlotImages[i].color = visible ? new Color(1f, 0.96f, 0.82f, 0.96f) : new Color(1f, 0.99f, 0.96f, 0.92f);
                }
            }
        }

        public bool IsLetterVisible(Coord coord)
        {
            return letterVisibleCats.Contains(coord);
        }

        public void MarkLetterVisible(Coord coord, PuzzleBoard board)
        {
            letterVisibleCats.Add(coord);
            Refresh(board);
        }

        public void ResetToLockedCats(PuzzleBoard board)
        {
            letterVisibleCats.Clear();
            foreach (Coord coord in board.Level.LockedCats)
            {
                letterVisibleCats.Add(coord);
            }
        }

        public void RestoreVisibleLetters(Coord[] visibleLetters)
        {
            letterVisibleCats.Clear();
            if (visibleLetters == null)
            {
                return;
            }

            for (int i = 0; i < visibleLetters.Length; i++)
            {
                letterVisibleCats.Add(visibleLetters[i]);
            }
        }

        public bool TryGetSlotCenter(RectTransform relativeTo, int wordIndex, out Vector2 center, out float slotSize)
        {
            center = Vector2.zero;
            slotSize = 0f;
            if (relativeTo == null
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
            center = relativeTo.InverseTransformPoint(worldCenter);

            Vector3 worldMin = slotRect.TransformPoint(new Vector3(slotRect.rect.xMin, slotRect.rect.yMin, 0f));
            Vector3 worldMax = slotRect.TransformPoint(new Vector3(slotRect.rect.xMax, slotRect.rect.yMax, 0f));
            Vector3 localMin = relativeTo.InverseTransformPoint(worldMin);
            Vector3 localMax = relativeTo.InverseTransformPoint(worldMax);
            slotSize = Mathf.Min(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));
            return slotSize > 0f;
        }

        /// <summary>Yieldable - the caller awaits it (e.g. to keep input locked until it finishes).</summary>
        public IEnumerator PlayBounce(int wordIndex)
        {
            if (wordSlotTexts == null || wordIndex < 0 || wordIndex >= wordSlotTexts.Length || wordSlotTexts[wordIndex] == null)
            {
                yield break;
            }

            RectTransform slot = (RectTransform)wordSlotTexts[wordIndex].transform.parent;
            float elapsed = 0f;
            const float seconds = 0.18f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                slot.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, wave);
                yield return null;
            }

            slot.localScale = Vector3.one;
        }

        public void ResetSlotScales()
        {
            if (wordSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < wordSlotTexts.Length; i++)
            {
                if (wordSlotTexts[i] != null)
                {
                    wordSlotTexts[i].transform.parent.localScale = Vector3.one;
                }
            }
        }
    }
}
