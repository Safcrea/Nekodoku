using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The level-1 onboarding: the "How to Play" panel and the pointing-hand
    /// guide that highlights the next useful tap/drag. Reads board state to
    /// figure out what to point at; never mutates it.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        private enum TutorialGuideMode
        {
            Tap,
            DoubleTap,
            Drag
        }

        private readonly struct TutorialGuideConfig
        {
            public readonly Coord StartCoord;
            public readonly Coord EndCoord;
            public readonly TutorialGuideMode Mode;
            public readonly string Message;

            public TutorialGuideConfig(Coord startCoord, Coord endCoord, TutorialGuideMode mode, string message)
            {
                StartCoord = startCoord;
                EndCoord = endCoord;
                Mode = mode;
                Message = message;
            }
        }

        private const int TutorialLevelCount = 1;
        private const float TutorialFocusRingPadding = 34f;

        [SerializeField]
        private RectTransform tutorialPanel;

        [SerializeField]
        private Text tutorialHeaderText;

        [SerializeField]
        private Text tutorialBodyText;

        [SerializeField]
        private RectTransform tutorialGuideRoot;

        [SerializeField]
        private Text tutorialGuideText;

        [SerializeField]
        private Image tutorialHandImage;

        [SerializeField]
        private Image focusRingImage;

        private BoardView boardView;
        private Coroutine tutorialPulseRoutine;
        private Coroutine tutorialGuideRoutine;
        private TutorialGuideConfig activeTutorialGuideConfig;
        private bool hasActiveTutorialGuideConfig;

        public void Initialize(BoardView board)
        {
            boardView = board;
        }

        public void UpdatePanel(int levelIndex, PuzzleBoard board, bool animate)
        {
            bool showTutorial = levelIndex < TutorialLevelCount;
            tutorialPanel.gameObject.SetActive(showTutorial);
            if (!showTutorial)
            {
                StopPulse();
                StopGuide();
                return;
            }

            tutorialHeaderText.text = "How to Play";
            tutorialBodyText.text = TutorialMessageForLevel(levelIndex, board);

            if (animate)
            {
                StopPulse();
                if (isActiveAndEnabled)
                {
                    tutorialPulseRoutine = StartCoroutine(AnimateTutorialPulse());
                }
            }
        }

        private string TutorialMessageForLevel(int index, PuzzleBoard board)
        {
            if (board == null)
            {
                return string.Empty;
            }

            if (board.IsFailed)
            {
                return "No hearts left. Restart the level and use the revealed clues.";
            }

            if (board.RevealedCatCount() >= board.Size)
            {
                return "All hidden letters are revealed. The next puzzle starts soon.";
            }

            if (index == 0)
            {
                return "Use the visible cat to place crosses, then find the next hidden cat.";
            }

            return "Find one hidden cat in every row, column, and color region.";
        }

        private IEnumerator AnimateTutorialPulse()
        {
            Vector3 baseScale = Vector3.one;
            float seconds = 1.8f;
            float elapsed = 0f;

            while (tutorialPanel != null && tutorialPanel.gameObject.activeSelf)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / seconds) * Mathf.PI);
                tutorialPanel.localScale = baseScale * Mathf.Lerp(1f, 1.025f, wave);
                if (elapsed >= seconds)
                {
                    elapsed = 0f;
                }

                yield return null;
            }

            if (tutorialPanel != null)
            {
                tutorialPanel.localScale = baseScale;
            }

            tutorialPulseRoutine = null;
        }

        public void StopPulse()
        {
            if (tutorialPulseRoutine != null)
            {
                StopCoroutine(tutorialPulseRoutine);
                tutorialPulseRoutine = null;
            }

            if (tutorialPanel != null)
            {
                tutorialPanel.localScale = Vector3.one;
            }
        }

        public void UpdateGuide(int levelIndex, PuzzleBoard board, bool animate)
        {
            if (tutorialGuideRoot == null || levelIndex >= TutorialLevelCount || board == null)
            {
                StopGuide();
                return;
            }

            if (!TryGetTutorialGuideConfig(board, out TutorialGuideConfig config))
            {
                StopGuide();
                return;
            }

            bool configChanged = !hasActiveTutorialGuideConfig || !TutorialGuideConfigMatches(activeTutorialGuideConfig, config);
            activeTutorialGuideConfig = config;
            hasActiveTutorialGuideConfig = true;

            tutorialGuideRoot.gameObject.SetActive(true);
            tutorialGuideText.text = config.Message;

            if (animate || configChanged || tutorialGuideRoutine == null)
            {
                StopGuideRoutine();
                if (isActiveAndEnabled)
                {
                    tutorialGuideRoutine = StartCoroutine(AnimateTutorialGuide(config));
                }
                else
                {
                    PositionTutorialGuide(config, 0f, 0f, 0f);
                }
            }
        }

        private bool TryGetTutorialGuideConfig(PuzzleBoard board, out TutorialGuideConfig config)
        {
            config = default;
            if (board.Level.LockedCats.Length == 0)
            {
                return false;
            }

            Coord starterCat = board.Level.LockedCats[0];
            string letterLabel = board.GetLetterForCat(starterCat.Row, starterCat.Column).ToString();
            if (TryGetLetterCrossGuide(board, starterCat, letterLabel, out config))
            {
                return true;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    if (board.HasHiddenCat(row, column) && !board.HasRevealedCat(row, column))
                    {
                        config = new TutorialGuideConfig(
                            new Coord(row, column),
                            new Coord(row, column),
                            TutorialGuideMode.DoubleTap,
                            "Double tap a square when you are sure a hidden cat is there.");
                        return true;
                    }
                }
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    if (!board.IsRevealed(row, column) && board.GetMark(row, column) != CellMark.Cross)
                    {
                        config = new TutorialGuideConfig(
                            new Coord(row, column),
                            new Coord(row, column),
                            TutorialGuideMode.Tap,
                            "Tap once to mark a square that cannot hold a cat.");
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetLetterCrossGuide(PuzzleBoard board, Coord catCoord, string letterLabel, out TutorialGuideConfig config)
        {
            if (!board.HasRevealedCat(catCoord.Row, catCoord.Column))
            {
                config = new TutorialGuideConfig(catCoord, catCoord, TutorialGuideMode.Tap, $"Letter {letterLabel} is already found. Place crosses in spaces it rules out.");
                return true;
            }

            if (!TutorialColumnHasAllCrosses(board, catCoord.Column))
            {
                config = new TutorialGuideConfig(
                    new Coord(0, catCoord.Column),
                    new Coord(board.Size - 1, catCoord.Column),
                    TutorialGuideMode.Drag,
                    $"Drag down column {catCoord.Column + 1} to place crosses to mark more ruled-out spaces.");
                return true;
            }

            if (!TutorialRowHasAllCrosses(board, catCoord.Row))
            {
                config = new TutorialGuideConfig(
                    new Coord(catCoord.Row, 0),
                    new Coord(catCoord.Row, board.Size - 1),
                    TutorialGuideMode.Drag,
                    $"Drag across row {catCoord.Row + 1} to place crosses in spaces ruled out by {letterLabel}.");
                return true;
            }

            if (TryGetTouchingCrossGuide(board, catCoord, out config))
            {
                return true;
            }

            config = default;
            return false;
        }

        private bool TryGetTouchingCrossGuide(PuzzleBoard board, Coord catCoord, out TutorialGuideConfig config)
        {
            config = default;
            for (int row = Mathf.Max(0, catCoord.Row - 1); row <= Mathf.Min(board.Size - 1, catCoord.Row + 1); row++)
            {
                int startColumn = -1;
                int endColumn = -1;
                for (int column = Mathf.Max(0, catCoord.Column - 1); column <= Mathf.Min(board.Size - 1, catCoord.Column + 1); column++)
                {
                    if (row == catCoord.Row && column == catCoord.Column)
                    {
                        continue;
                    }

                    if (board.IsRevealed(row, column) || board.HasHiddenCat(row, column))
                    {
                        continue;
                    }

                    if (board.GetMark(row, column) == CellMark.Cross)
                    {
                        continue;
                    }

                    if (startColumn < 0)
                    {
                        startColumn = column;
                    }

                    endColumn = column;
                }

                if (startColumn >= 0)
                {
                    config = new TutorialGuideConfig(
                        new Coord(row, startColumn),
                        new Coord(row, endColumn),
                        startColumn == endColumn ? TutorialGuideMode.Tap : TutorialGuideMode.Drag,
                        "Place crosses around the cat because cats cannot touch.");
                    return true;
                }
            }

            return false;
        }

        private bool TutorialGuideConfigMatches(TutorialGuideConfig left, TutorialGuideConfig right)
        {
            return left.StartCoord.Equals(right.StartCoord)
                && left.EndCoord.Equals(right.EndCoord)
                && left.Mode == right.Mode
                && left.Message == right.Message;
        }

        private bool TutorialRowHasAllCrosses(PuzzleBoard board, int row)
        {
            for (int column = 0; column < board.Size; column++)
            {
                if (board.HasHiddenCat(row, column) || board.IsRevealed(row, column))
                {
                    continue;
                }

                if (board.GetMark(row, column) != CellMark.Cross)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TutorialColumnHasAllCrosses(PuzzleBoard board, int column)
        {
            for (int row = 0; row < board.Size; row++)
            {
                if (board.HasHiddenCat(row, column) || board.IsRevealed(row, column))
                {
                    continue;
                }

                if (board.GetMark(row, column) != CellMark.Cross)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerator AnimateTutorialGuide(TutorialGuideConfig config)
        {
            while (tutorialGuideRoot != null && tutorialGuideRoot.gameObject.activeSelf)
            {
                float seconds = config.Mode == TutorialGuideMode.Drag ? 2.4f : 1.65f;
                float phase = Mathf.Repeat(Time.unscaledTime, seconds) / seconds;
                float travel = 0f;
                float press = 0f;

                if (config.Mode == TutorialGuideMode.Drag)
                {
                    travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 0.18f) / 0.62f));
                    press = phase >= 0.12f && phase <= 0.84f ? 1f : 0f;
                }
                else if (config.Mode == TutorialGuideMode.DoubleTap)
                {
                    press = Mathf.Max(TapPulse(phase, 0.18f, 0.18f), TapPulse(phase, 0.48f, 0.18f));
                }
                else
                {
                    press = TapPulse(phase, 0.24f, 0.24f);
                }

                float ringPulse = 0.5f + (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.1f) * 0.5f);
                PositionTutorialGuide(config, travel, press, ringPulse);
                yield return null;
            }

            tutorialGuideRoutine = null;
        }

        private float TapPulse(float phase, float start, float width)
        {
            if (phase < start || phase > start + width)
            {
                return 0f;
            }

            float local = Mathf.Clamp01((phase - start) / width);
            return Mathf.Sin(local * Mathf.PI);
        }

        private void PositionTutorialGuide(TutorialGuideConfig config, float travel, float press, float ringPulse)
        {
            if (!boardView.TryGetCellCenterIn(tutorialGuideRoot, config.StartCoord, out Vector2 startCenter, out float startSize)
                || !boardView.TryGetCellCenterIn(tutorialGuideRoot, config.EndCoord, out Vector2 endCenter, out _))
            {
                return;
            }

            Vector2 center = Vector2.Lerp(startCenter, endCenter, Mathf.Clamp01(travel));
            float cellSize = startSize;
            float ringSize = cellSize + TutorialFocusRingPadding + (ringPulse * 18f);
            RectTransform focusRect = focusRingImage.rectTransform;
            focusRect.anchoredPosition = center;
            focusRect.sizeDelta = new Vector2(ringSize, ringSize);
            focusRect.localScale = Vector3.one;

            RectTransform handRect = tutorialHandImage.rectTransform;
            handRect.anchoredPosition = center + new Vector2(cellSize * 0.34f, -cellSize * 0.34f);
            float handScale = Mathf.Lerp(1f, 0.94f, Mathf.Clamp01(press));
            handRect.localScale = Vector3.one * handScale;
            tutorialGuideText.text = config.Message;
        }

        public void StopGuide()
        {
            StopGuideRoutine();
            hasActiveTutorialGuideConfig = false;
            if (tutorialGuideRoot != null)
            {
                tutorialGuideRoot.gameObject.SetActive(false);
            }

            if (tutorialGuideText != null)
            {
                tutorialGuideText.text = string.Empty;
            }

            if (focusRingImage != null)
            {
                focusRingImage.rectTransform.localScale = Vector3.one;
            }

            if (tutorialHandImage != null)
            {
                tutorialHandImage.rectTransform.localScale = Vector3.one;
            }
        }

        private void StopGuideRoutine()
        {
            if (tutorialGuideRoutine != null)
            {
                StopCoroutine(tutorialGuideRoutine);
                tutorialGuideRoutine = null;
            }
        }
    }
}
