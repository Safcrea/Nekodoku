using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void RebuildBoardCells()
        {
            for (int i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }

            cells.Clear();
            cellGrid = new CellUi[board.Size, board.Size];

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    CellUi cell = CreateCell(boardRoot, row, column);
                    cells.Add(cell);
                    cellGrid[row, column] = cell;
                }
            }

            ApplyBoardLayoutImmediate(false);
        }

        private CellUi CreateCell(RectTransform parent, int row, int column)
        {
            GameObject cellObject = new GameObject($"Cell {row},{column}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline), typeof(NekoCellView));
            cellObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)cellObject.transform;
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CanvasGroup group = cellObject.GetComponent<CanvasGroup>();

            Image image = cellObject.GetComponent<Image>();
            image.color = RegionColors[board.Level.RegionAt(row, column) % RegionColors.Length];
            image.raycastTarget = true;

            Outline outline = cellObject.GetComponent<Outline>();
            outline.effectColor = TileBorderColor;
            outline.effectDistance = new Vector2(3f, -3f);

            NekoCellView view = cellObject.GetComponent<NekoCellView>();
            view.Bind(this, row, column);

            Text mark = CreateText((RectTransform)cellObject.transform, string.Empty, board.Size >= 7 ? 28 : 35, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            mark.raycastTarget = false;
            SetRect(mark.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image nekoImage = CreateNekoImage((RectTransform)cellObject.transform);
            nekoImage.enabled = false;

            Text badge = CreateText((RectTransform)cellObject.transform, string.Empty, board.Size >= 7 ? 30 : 38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            badge.raycastTarget = false;
            Outline badgeOutline = badge.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.1f, 0.09f, 0.08f, 0.65f);
            badgeOutline.effectDistance = new Vector2(2f, -2f);
            SetRect(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            return new CellUi(row, column, rect, group, image, outline, mark, badge, nekoImage);
        }

        private void ApplyBoardLayoutImmediate(bool showRemovedCells)
        {
            if (board == null || boardRoot == null)
            {
                return;
            }

            boardRoot.localScale = Vector3.one;
            foreach (CellUi cell in cells)
            {
                bool active = board.IsActiveCell(cell.Row, cell.Column);
                cell.Rect.gameObject.SetActive(active || showRemovedCells);
                if (active && TryGetCellLayout(cell.Row, cell.Column, out Vector2 position, out float cellSize))
                {
                    cell.Rect.anchoredPosition = position;
                    cell.Rect.sizeDelta = new Vector2(cellSize, cellSize);
                }

                cell.Rect.localRotation = Quaternion.identity;
                cell.Rect.localScale = Vector3.one;
                cell.Group.alpha = active ? 1f : 0f;
                cell.Group.blocksRaycasts = active && !inputLocked;
            }
        }

        private bool TryGetCellLayout(int row, int column, out Vector2 position, out float cellSize)
        {
            position = Vector2.zero;
            cellSize = 0f;
            if (board == null || !board.IsActiveCell(row, column))
            {
                return false;
            }

            int size = Mathf.Max(1, board.Size);
            float gap = ActiveBoardGap(size);
            cellSize = (BoardPixels - (gap * (size + 1))) / size;
            float startX = (-BoardPixels * 0.5f) + gap + (cellSize * 0.5f);
            float startY = (BoardPixels * 0.5f) - gap - (cellSize * 0.5f);
            position = new Vector2(
                startX + (column * (cellSize + gap)),
                startY - (row * (cellSize + gap)));
            return true;
        }

        private float ActiveBoardGap(int boardSize)
        {
            return boardSize <= 2 ? 12f : 8f;
        }

        private void PlayBoardIntro()
        {
            StopBoardIntro();
            if (!isActiveAndEnabled || cells.Count == 0)
            {
                return;
            }

            boardIntroRoutine = StartCoroutine(AnimateBoardIntro());
        }

        private IEnumerator AnimateBoardIntro()
        {
            Canvas.ForceUpdateCanvases();

            CellIntroState[] states = new CellIntroState[cells.Count];
            float totalSeconds = BoardIntroCellSeconds + ((board.Size - 1) * 2 * BoardIntroDiagonalDelaySeconds);

            for (int i = 0; i < cells.Count; i++)
            {
                CellUi cell = cells[i];
                states[i] = new CellIntroState(
                    cell.Rect,
                    cell.Group,
                    (cell.Row + cell.Column) * BoardIntroDiagonalDelaySeconds);

                cell.Rect.localScale = Vector3.zero;
                cell.Group.alpha = 0f;
                cell.Group.blocksRaycasts = false;
            }

            // Diagonal pop-in reveal: every cell starts at scale 0 and springs to full
            // size, staggered by row + column so the fill sweeps across the board.
            float elapsed = 0f;
            while (elapsed < totalSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < states.Length; i++)
                {
                    CellIntroState state = states[i];
                    float t = Mathf.Clamp01((elapsed - state.DelaySeconds) / BoardIntroCellSeconds);
                    float easedScale = EaseOutBack(t);

                    state.Rect.localScale = Vector3.one * easedScale;
                    state.Group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.1f));
                }

                yield return null;
            }

            FinishBoardIntroLayout();
            boardIntroRoutine = null;
        }

        private void StopBoardIntro()
        {
            if (boardIntroRoutine != null)
            {
                StopCoroutine(boardIntroRoutine);
                boardIntroRoutine = null;
            }

            FinishBoardIntroLayout();
        }


        private void FinishBoardIntroLayout()
        {
            if (boardRoot == null)
            {
                return;
            }

            ApplyBoardLayoutImmediate(false);

            foreach (CellUi cell in cells)
            {
                if (!board.IsActiveCell(cell.Row, cell.Column))
                {
                    continue;
                }

                cell.Rect.localScale = Vector3.one;
                cell.Group.alpha = 1f;
                cell.Group.blocksRaycasts = !inputLocked;
            }
        }

        private float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.7f;
            float shifted = value - 1f;
            return 1f + ((overshoot + 1f) * shifted * shifted * shifted) + (overshoot * shifted * shifted);
        }

        private void RefreshBoard()
        {
            NekoValidationResult validation = board.Validate();
            titleText.text = $"Level {levelIndex + 1}: {board.Level.Title}";
            countText.text = $"Tier {board.Level.DifficultyTier}   Found {validation.RevealedCatCount}/{board.Size}   Hearts {HeartLabel(validation.HeartsRemaining)}";
            statusText.text = validation.IsSolved ? "Solved" : validation.Message;
            statusText.color = validation.IsFailed || validation.HasConflict ? ConflictColor : validation.IsSolved ? RevealedColor : SoftInkColor;
            RefreshWordSlots();

            foreach (CellUi cell in cells)
            {
                bool active = board.IsActiveCell(cell.Row, cell.Column);
                cell.Rect.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                if (TryGetCellLayout(cell.Row, cell.Column, out Vector2 position, out float cellSize))
                {
                    cell.Rect.anchoredPosition = position;
                    cell.Rect.sizeDelta = new Vector2(cellSize, cellSize);
                }

                NekoCellMark mark = board.GetMark(cell.Row, cell.Column);
                bool revealed = board.IsRevealed(cell.Row, cell.Column);
                bool revealedCat = board.HasRevealedCat(cell.Row, cell.Column);
                bool revealedMiss = board.HasRevealedMiss(cell.Row, cell.Column);
                NekoCoord coord = new NekoCoord(cell.Row, cell.Column);
                bool conflict = validation.ConflictCells.Contains(coord);
                bool markScaleAnimating = markScaleRoutines.ContainsKey(coord);

                cell.NekoImage.enabled = revealedCat;
                cell.NekoImage.color = Color.white;
                cell.MarkText.text = revealedCat ? string.Empty : MarkLabel(mark, revealedMiss);
                if (!markScaleAnimating)
                {
                    cell.MarkText.rectTransform.localScale = Vector3.one;
                }

                cell.MarkText.color = conflict || revealedMiss ? ConflictColor : revealedCat ? RevealedColor : InkColor;
                cell.BadgeText.text = string.Empty;
                cell.Outline.effectColor = TileBorderColor;
                cell.Outline.effectDistance = conflict || revealed ? new Vector2(5f, -5f) : new Vector2(3f, -3f);
                cell.Image.color = AdjustCellColor(RegionColors[board.Level.RegionAt(cell.Row, cell.Column) % RegionColors.Length], mark, revealedCat, revealedMiss);
                cell.Group.alpha = 1f;
                cell.Group.blocksRaycasts = !inputLocked;
            }

            UpdateTutorialPanel(false);
            if (validation.IsSolved)
            {
                StopTutorialGuide();
            }
            else
            {
                UpdateTutorialGuide(false);
            }

            SetWinPanelVisible(validation);
            UpdateSolvedLevelAdvance(validation.IsSolved);
        }

        private void SetWinPanelVisible(NekoValidationResult validation)
        {
            if (!validation.IsSolved || letterFlipRoutines.Count > 0 || inputLocked || catRevealRoutine != null)
            {
                HideWinPanel();
                return;
            }

            if (winPanel == null)
            {
                return;
            }

            winWordText.text = board.Level.TargetWord;
            winSubtitleText.text = $"{StarLabel(validation.MistakeCount)}   Hearts {HeartLabel(validation.HeartsRemaining)}";
            bool wasVisible = winPanel.gameObject.activeSelf;
            winPanel.gameObject.SetActive(true);
            if (!wasVisible)
            {
                PlayWinPanelPop();
            }
        }

        private void HideWinPanel()
        {
            StopWinPanelPop();
            if (winPanel != null)
            {
                winPanel.gameObject.SetActive(false);
                winPanel.localScale = Vector3.one;
            }
        }

        private bool TryGetCellUi(int row, int column, out CellUi cellUi)
        {
            if (cellGrid != null
                && row >= 0
                && column >= 0
                && row < cellGrid.GetLength(0)
                && column < cellGrid.GetLength(1))
            {
                cellUi = cellGrid[row, column];
                return cellUi.Rect != null;
            }

            cellUi = default;
            return false;
        }

        private string StarLabel(int mistakes)
        {
            if (mistakes <= 0)
            {
                return "3 Stars";
            }

            return mistakes <= 2 ? "2 Stars" : "1 Star";
        }

        private string MarkLabel(NekoCellMark mark, bool revealedMiss)
        {
            if (revealedMiss)
            {
                return "X";
            }

            switch (mark)
            {
                case NekoCellMark.Cross:
                    return "X";
                default:
                    return string.Empty;
            }
        }

        private string HeartLabel(int hearts)
        {
            switch (hearts)
            {
                case 3:
                    return "3/3";
                case 2:
                    return "2/3";
                case 1:
                    return "1/3";
                default:
                    return "0/3";
            }
        }

        private Color AdjustCellColor(Color baseColor, NekoCellMark mark, bool revealedCat, bool revealedMiss)
        {
            if (revealedCat)
            {
                return Color.Lerp(baseColor, RevealedColor, 0.14f);
            }

            if (revealedMiss)
            {
                return Color.Lerp(baseColor, ConflictColor, 0.12f);
            }

            if (mark == NekoCellMark.Cross)
            {
                return baseColor;
            }

            return baseColor;
        }
    }
}
