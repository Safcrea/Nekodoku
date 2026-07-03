using System.Collections;
using UnityEngine;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void LoadLevel(int nextLevelIndex)
        {
            inputLocked = false;
            StopBoardIntro();
            StopLevelAdvance();
            StopCatReveal();
            StopLetterFlipRoutines();
            StopJuiceEffects();
            StopTutorialPulse();
            StopTutorialGuide();
            HideWinPanel();
            levelIndex = Mathf.Clamp(nextLevelIndex, 0, NekoSampleLevels.Levels.Length - 1);
            board = new NekoPuzzleBoard(NekoSampleLevels.Levels[levelIndex]);
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
            LoadLevel(levelIndex <= 0 ? NekoSampleLevels.Levels.Length - 1 : levelIndex - 1);
        }

        private void NextLevel()
        {
            LoadLevel((levelIndex + 1) % NekoSampleLevels.Levels.Length);
        }

        private void RestartLevel()
        {
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
