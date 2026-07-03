using System.Collections;
using UnityEngine;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void ScheduleLetterFlip(int row, int column)
        {
            ScheduleLetterFlip(row, column, CatLetterRevealDelaySeconds, false);
        }

        private void ScheduleLetterFlip(int row, int column, float delaySeconds, bool allowLockedDelay)
        {
            NekoCoord coord = new NekoCoord(row, column);
            if (letterVisibleCats.Contains(coord) || letterFlipRoutines.ContainsKey(coord))
            {
                return;
            }

            if ((board.IsLocked(row, column) && !allowLockedDelay) || !isActiveAndEnabled)
            {
                letterVisibleCats.Add(coord);
                RefreshWordSlots();
                return;
            }

            letterFlipRoutines[coord] = StartCoroutine(AnimateLetterReveal(coord, delaySeconds));
        }

        private IEnumerator AnimateLetterReveal(NekoCoord coord, float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);

            if (board == null || !board.HasRevealedCat(coord.Row, coord.Column) || !TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                letterFlipRoutines.Remove(coord);
                RefreshBoard();
                yield break;
            }

            float halfSeconds = CardFlipSeconds * 0.5f;
            float elapsed = 0f;
            Vector3 baseScale = Vector3.one;
            StopCellPunch(coord, true);

            while (elapsed < halfSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfSeconds);
                float xScale = Mathf.SmoothStep(1f, 0f, t);
                cell.Rect.localScale = new Vector3(xScale, baseScale.y, baseScale.z);
                yield return null;
            }

            letterVisibleCats.Add(coord);
            RefreshWordSlots();
            RefreshBoard();

            elapsed = 0f;
            while (elapsed < halfSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfSeconds);
                float xScale = Mathf.SmoothStep(0f, 1f, t);
                cell.Rect.localScale = new Vector3(xScale, baseScale.y, baseScale.z);
                yield return null;
            }

            cell.Rect.localScale = Vector3.one;
            letterFlipRoutines.Remove(coord);
            RefreshWordSlots();
            RefreshBoard();
        }

        private void StopLetterFlipRoutines()
        {
            foreach (Coroutine routine in letterFlipRoutines.Values)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }

            letterFlipRoutines.Clear();
            foreach (CellUi cell in cells)
            {
                cell.Rect.localScale = Vector3.one;
            }
        }

        private void ResetLetterVisualStateToLockedCats()
        {
            letterVisibleCats.Clear();
            if (board == null)
            {
                return;
            }

            foreach (NekoCoord coord in board.Level.LockedCats)
            {
                letterVisibleCats.Add(coord);
            }
        }

        private void SyncLetterVisualStateToRevealedCats()
        {
            letterVisibleCats.Clear();
            if (board == null)
            {
                return;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    if (board.HasRevealedCat(row, column))
                    {
                        letterVisibleCats.Add(new NekoCoord(row, column));
                    }
                }
            }
        }
    }
}
