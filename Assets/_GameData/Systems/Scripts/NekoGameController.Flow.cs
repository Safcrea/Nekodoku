using System.Collections;
using UnityEngine;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void LoadLevel(int nextLevelIndex)
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            inputLocked = false;
            StopBoardIntro();
            StopLevelAdvance();
            StopCatReveal();
            StopLetterFlipRoutines();
            StopJuiceEffects();
            StopTutorialPulse();
            StopTutorialGuide();
            HideWinPanel();
            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            board = new NekoPuzzleBoard(levels[levelIndex]);
            BuildWordSlotsForLevel();
            undoStack.Clear();
            ResetLetterVisualStateToLockedCats();
            RebuildBoardCells();
            RefreshBoard();
            UpdateTutorialPanel(true);
            PlayBoardIntro();
            UpdateTutorialGuide(true);
        }

        private void PreviousLevel()
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            LoadLevel(levelIndex <= 0 ? levels.Length - 1 : levelIndex - 1);
        }

        private void NextLevel()
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            LoadLevel((levelIndex + 1) % levels.Length);
        }

        private void RestartLevel()
        {
            if (board == null)
            {
                return;
            }

            inputLocked = false;
            StopLevelAdvance();
            StopCatReveal();
            StopLetterFlipRoutines();
            StopJuiceEffects();
            StopTutorialPulse();
            StopTutorialGuide();
            HideWinPanel();
            board.Clear();
            undoStack.Clear();
            ResetLetterVisualStateToLockedCats();
            BuildWordSlotsForLevel();
            RefreshBoard();
            UpdateTutorialPanel(true);
            PlayBoardIntro();
            UpdateTutorialGuide(true);
        }

        private void StopLevelAdvance()
        {
            if (levelAdvanceRoutine == null)
            {
                return;
            }

            StopCoroutine(levelAdvanceRoutine);
            levelAdvanceRoutine = null;
        }

        private void UpdateSolvedLevelAdvance(bool isSolved)
        {
            if (!isSolved)
            {
                StopLevelAdvance();
                return;
            }

            if (letterFlipRoutines.Count > 0)
            {
                StopLevelAdvance();
                return;
            }

            if (inputLocked || catRevealRoutine != null)
            {
                StopLevelAdvance();
                return;
            }

            if (levelAdvanceRoutine == null && isActiveAndEnabled)
            {
                levelAdvanceRoutine = StartCoroutine(AdvanceToNextLevelAfterDelay());
            }
        }

        private IEnumerator AdvanceToNextLevelAfterDelay()
        {
            yield return new WaitForSecondsRealtime(LevelCompleteAdvanceSeconds);
            levelAdvanceRoutine = null;
            NextLevel();
        }
    }
}
