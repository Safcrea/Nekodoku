using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void UpdateTutorialPanel(bool animate)
        {
            if (tutorialPanel == null)
            {
                return;
            }

            bool showTutorial = levelIndex < TutorialLevelCount;
            tutorialPanel.gameObject.SetActive(showTutorial);
            if (!showTutorial)
            {
                StopTutorialPulse();
                StopTutorialGuide();
                return;
            }

            tutorialHeaderText.text = "How to Play";
            tutorialBodyText.text = TutorialMessageForLevel(levelIndex);

            if (animate)
            {
                StopTutorialPulse();
                if (isActiveAndEnabled)
                {
                    tutorialPulseRoutine = StartCoroutine(AnimateTutorialPulse());
                }
            }
        }

        private string TutorialMessageForLevel(int index)
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

        private void StopTutorialPulse()
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

        private void UpdateTutorialGuide(bool animate)
        {
            if (tutorialGuideRoot == null || levelIndex >= TutorialLevelCount || board == null)
            {
                StopTutorialGuide();
                return;
            }

            if (!TryGetTutorialGuideConfig(out TutorialGuideConfig config))
            {
                StopTutorialGuide();
                return;
            }

            bool configChanged = !hasActiveTutorialGuideConfig || !TutorialGuideConfigMatches(activeTutorialGuideConfig, config);
            activeTutorialGuideConfig = config;
            hasActiveTutorialGuideConfig = true;

            tutorialGuideRoot.gameObject.SetActive(true);
            tutorialGuideText.text = config.Message;

            if (animate || configChanged || tutorialGuideRoutine == null)
            {
                StopTutorialGuideRoutine();
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

        private bool TryGetTutorialGuideConfig(out TutorialGuideConfig config)
        {
            config = default;
            if (board == null || board.Level.LockedCats.Length == 0)
            {
                return false;
            }

            NekoCoord starterCat = board.Level.LockedCats[0];
            string letterLabel = board.GetLetterForCat(starterCat.Row, starterCat.Column).ToString();
            if (TryGetLetterCrossGuide(starterCat, letterLabel, out config))
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
                            new NekoCoord(row, column),
                            new NekoCoord(row, column),
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
                    if (!board.IsRevealed(row, column) && board.GetMark(row, column) != NekoCellMark.Cross)
                    {
                        config = new TutorialGuideConfig(
                            new NekoCoord(row, column),
                            new NekoCoord(row, column),
                            TutorialGuideMode.Tap,
                            "Tap once to mark a square that cannot hold a cat.");
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetLetterCrossGuide(NekoCoord catCoord, string letterLabel, out TutorialGuideConfig config)
        {
            if (!letterVisibleCats.Contains(catCoord))
            {
                config = new TutorialGuideConfig(catCoord, catCoord, TutorialGuideMode.Tap, $"Letter {letterLabel} is already found. Place crosses in spaces it rules out.");
                return true;
            }

            if (!TutorialColumnHasAllCrosses(catCoord.Column))
            {
                config = new TutorialGuideConfig(
                    new NekoCoord(0, catCoord.Column),
                    new NekoCoord(board.Size - 1, catCoord.Column),
                    TutorialGuideMode.Drag,
                    $"Drag down column {catCoord.Column + 1} to place crosses to mark more ruled-out spaces.");
                return true;
            }

            if (!TutorialRowHasAllCrosses(catCoord.Row))
            {
                config = new TutorialGuideConfig(
                    new NekoCoord(catCoord.Row, 0),
                    new NekoCoord(catCoord.Row, board.Size - 1),
                    TutorialGuideMode.Drag,
                    $"Drag across row {catCoord.Row + 1} to place crosses in spaces ruled out by {letterLabel}.");
                return true;
            }

            if (TryGetTouchingCrossGuide(catCoord, out config))
            {
                return true;
            }

            config = default;
            return false;
        }

        private bool TryGetTouchingCrossGuide(NekoCoord catCoord, out TutorialGuideConfig config)
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

                    if (board.GetMark(row, column) == NekoCellMark.Cross)
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
                        new NekoCoord(row, startColumn),
                        new NekoCoord(row, endColumn),
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

        private bool TutorialRowHasAllCrosses(int row)
        {
            for (int column = 0; column < board.Size; column++)
            {
                if (board.HasHiddenCat(row, column) || board.IsRevealed(row, column))
                {
                    continue;
                }

                if (board.GetMark(row, column) != NekoCellMark.Cross)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TutorialColumnHasAllCrosses(int column)
        {
            for (int row = 0; row < board.Size; row++)
            {
                if (board.HasHiddenCat(row, column) || board.IsRevealed(row, column))
                {
                    continue;
                }

                if (board.GetMark(row, column) != NekoCellMark.Cross)
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
            if (!TryGetCellCenter(config.StartCoord, out Vector2 startCenter, out float startSize)
                || !TryGetCellCenter(config.EndCoord, out Vector2 endCenter, out _))
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

        private bool TryGetCellCenter(NekoCoord coord, out Vector2 center, out float cellSize)
        {
            return TryGetCellCenterIn(tutorialGuideRoot, coord, out center, out cellSize);
        }

        private bool TryGetCellCenterIn(RectTransform root, NekoCoord coord, out Vector2 center, out float cellSize)
        {
            center = Vector2.zero;
            cellSize = 0f;

            if (root == null || !TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                return false;
            }

            Vector3 worldCenter = cell.Rect.TransformPoint(cell.Rect.rect.center);
            center = root.InverseTransformPoint(worldCenter);

            Vector3 worldMin = cell.Rect.TransformPoint(new Vector3(cell.Rect.rect.xMin, cell.Rect.rect.yMin, 0f));
            Vector3 worldMax = cell.Rect.TransformPoint(new Vector3(cell.Rect.rect.xMax, cell.Rect.rect.yMax, 0f));
            Vector3 localMin = root.InverseTransformPoint(worldMin);
            Vector3 localMax = root.InverseTransformPoint(worldMax);
            cellSize = Mathf.Min(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));
            return cellSize > 0f;
        }

        private void StopTutorialGuide()
        {
            StopTutorialGuideRoutine();
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

        private void StopTutorialGuideRoutine()
        {
            if (tutorialGuideRoutine != null)
            {
                StopCoroutine(tutorialGuideRoutine);
                tutorialGuideRoutine = null;
            }
        }
    }
}
