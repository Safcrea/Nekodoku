using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The "juice" layer: floating -1 labels, sparkle bursts, the cat-found
    /// reveal sequence (pop, burst, flying letter), and clearing it all between
    /// levels. Positions everything relative to <see cref="BoardView"/> and
    /// <see cref="WordSlotsView"/> but never mutates board/word-slot state
    /// itself beyond the one letter-reveal call at the end of a cat reveal.
    /// </summary>
    public sealed class EffectsPlayer : MonoBehaviour
    {
        private const float LetterFlySeconds = 0.54f;
        private const float SparkleSeconds = 0.58f;
        private const float CatFoundPopSeconds = 0.24f;

        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color SparkleGoldColor = new Color(1f, 0.82f, 0.22f, 1f);

        [SerializeField]
        private RectTransform juiceRoot;

        private BoardView boardView;
        private WordSlotsView wordSlotsView;
        private Font font;
        private Sprite juiceDotSprite;

        public bool Validate()
        {
            return SceneValidation.LogIfMissing(juiceRoot, "Juice Root", this);
        }

        public void Initialize(BoardView board, WordSlotsView wordSlots, Font sharedFont, Sprite dotSprite)
        {
            boardView = board;
            wordSlotsView = wordSlots;
            font = sharedFont;
            juiceDotSprite = dotSprite;
        }

        public void SpawnFloatingLabel(int row, int column, string label, Color color)
        {
            if (juiceRoot == null || !boardView.TryGetCellCenterIn(juiceRoot, new Coord(row, column), out Vector2 center, out float cellSize))
            {
                return;
            }

            Text text = UiFactory.CreateText(juiceRoot, font, label, Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.36f, 30f, 46f)), FontStyle.Bold, TextAnchor.MiddleCenter, color);
            text.raycastTarget = false;
            UiFactory.SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(cellSize, cellSize));
            StartCoroutine(AnimateFloatingLabel(text, center));
        }

        private IEnumerator AnimateFloatingLabel(Text text, Vector2 start)
        {
            float elapsed = 0f;
            Color startColor = text.color;
            while (elapsed < 0.52f)
            {
                if (text == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.52f);
                text.rectTransform.anchoredPosition = start + new Vector2(0f, Mathf.SmoothStep(0f, 72f, t));
                text.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.12f, Mathf.Sin(t * Mathf.PI));
                text.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            if (text != null)
            {
                Destroy(text.gameObject);
            }
        }

        /// <summary>Pop + burst + flying letter. Caller (GameManager) owns locking input around this and refreshing afterward.</summary>
        public IEnumerator PlayCatFoundSequence(PuzzleBoard board, Coord catCoord)
        {
            yield return AnimateCatFoundPop(catCoord);
            SpawnCatBurst(catCoord.Row, catCoord.Column);
            yield return AnimateLetterFlight(board, catCoord);
        }

        private IEnumerator AnimateCatFoundPop(Coord coord)
        {
            if (!boardView.TryBeginCatFoundPop(coord, out RectTransform rect, out Outline outline))
            {
                yield break;
            }

            Color startOutlineColor = outline.effectColor;
            Vector2 startOutlineDistance = outline.effectDistance;
            float elapsed = 0f;
            while (elapsed < CatFoundPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CatFoundPopSeconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, wave);
                outline.effectColor = Color.Lerp(startOutlineColor, SparkleGoldColor, wave);
                outline.effectDistance = Vector2.Lerp(startOutlineDistance, new Vector2(6f, -6f), wave);
                yield return null;
            }

            rect.localScale = Vector3.one;
            outline.effectColor = startOutlineColor;
            outline.effectDistance = startOutlineDistance;
        }

        private void SpawnCatBurst(int row, int column)
        {
            if (juiceRoot == null || juiceDotSprite == null || !boardView.TryGetCellCenterIn(juiceRoot, new Coord(row, column), out Vector2 center, out float cellSize))
            {
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 10f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = (cellSize * 0.42f) + ((i % 2) * 18f);
                Color color = i % 2 == 0 ? SparkleGoldColor : RevealedColor;
                Image dot = CreateJuiceDot(center, Mathf.Clamp(cellSize * 0.1f, 12f, 20f), color);
                StartCoroutine(AnimateSparkle(dot, center, center + (direction * distance), i * 0.018f));
            }
        }

        private Image CreateJuiceDot(Vector2 center, float size, Color color)
        {
            GameObject dotObject = new GameObject("Juice Dot", typeof(RectTransform), typeof(Image));
            dotObject.transform.SetParent(juiceRoot, false);
            Image dot = dotObject.GetComponent<Image>();
            dot.sprite = juiceDotSprite;
            dot.raycastTarget = false;
            dot.color = color;
            UiFactory.SetRect(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(size, size));
            return dot;
        }

        private IEnumerator AnimateSparkle(Image dot, Vector2 start, Vector2 end, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (dot == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color startColor = dot.color;
            while (elapsed < SparkleSeconds)
            {
                if (dot == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SparkleSeconds);
                dot.rectTransform.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                dot.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.2f, t);
                dot.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            if (dot != null)
            {
                Destroy(dot.gameObject);
            }
        }

        private IEnumerator AnimateLetterFlight(PuzzleBoard board, Coord coord)
        {
            int wordIndex = board.GetCatIndex(coord.Row, coord.Column);
            if (wordIndex < 0
                || !boardView.TryGetCellCenterIn(juiceRoot, coord, out Vector2 start, out float cellSize)
                || !wordSlotsView.TryGetSlotCenter(juiceRoot, wordIndex, out Vector2 end, out float slotSize))
            {
                wordSlotsView.MarkLetterVisible(coord, board);
                yield break;
            }

            Text flyingLetter = UiFactory.CreateText(
                juiceRoot,
                font,
                board.GetLetterForCat(coord.Row, coord.Column).ToString(),
                Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.4f, 36f, 58f)),
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                InkColor);
            flyingLetter.raycastTarget = false;
            UiFactory.SetRect(flyingLetter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), start, new Vector2(slotSize, slotSize));

            float elapsed = 0f;
            float trailDelay = 0f;
            int trailIndex = 0;
            while (elapsed < LetterFlySeconds)
            {
                if (flyingLetter == null)
                {
                    yield break;
                }

                float delta = Time.unscaledDeltaTime;
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / LetterFlySeconds);
                float eased = NekoEasing.OutCubic(t);
                Vector2 arc = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 84f);
                Vector2 currentPosition = Vector2.Lerp(start, end, eased) + arc;
                flyingLetter.rectTransform.anchoredPosition = currentPosition;
                flyingLetter.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.08f, NekoEasing.OutBack(t));

                trailDelay -= delta;
                if (trailDelay <= 0f)
                {
                    SpawnLetterTrailDot(currentPosition, Mathf.Clamp(slotSize * 0.1f, 8f, 15f), trailIndex++);
                    trailDelay = 0.055f;
                }

                yield return null;
            }

            if (flyingLetter != null)
            {
                Destroy(flyingLetter.gameObject);
            }

            wordSlotsView.MarkLetterVisible(coord, board);
            SpawnWordSlotBurst(end, slotSize);
            yield return wordSlotsView.PlayBounce(wordIndex);
        }

        private void SpawnLetterTrailDot(Vector2 center, float size, int index)
        {
            if (juiceRoot == null || juiceDotSprite == null)
            {
                return;
            }

            float angle = index * 2.399963f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color color = index % 2 == 0 ? SparkleGoldColor : RevealedColor;
            Image dot = CreateJuiceDot(center, size, color);
            StartCoroutine(AnimateSparkle(dot, center, center + (direction * (size * 1.4f)), 0f));
        }

        private void SpawnWordSlotBurst(Vector2 center, float slotSize)
        {
            if (juiceRoot == null || juiceDotSprite == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 8f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = (slotSize * 0.28f) + ((i % 2) * 10f);
                Color color = i % 2 == 0 ? SparkleGoldColor : RevealedColor;
                Image dot = CreateJuiceDot(center, Mathf.Clamp(slotSize * 0.08f, 8f, 14f), color);
                StartCoroutine(AnimateSparkle(dot, center, center + (direction * distance), i * 0.015f));
            }
        }

        public void StopAll()
        {
            if (juiceRoot == null)
            {
                return;
            }

            for (int i = juiceRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(juiceRoot.GetChild(i).gameObject);
            }
        }
    }
}
