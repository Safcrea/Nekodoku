namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void Undo()
        {
            if (!undoStack.TryPop(out BoardSnapshot snapshot))
            {
                return;
            }

            StopLevelAdvance();
            StopCatReveal();
            StopLetterFlipRoutines();
            StopJuiceEffects();
            StopTutorialGuide();
            NekoUndoStack.Apply(board, snapshot);
            RestoreLetterVisualState(snapshot.VisibleLetters);
            RefreshBoard();
            RefreshWordSlots();
            UpdateTutorialGuide(true);
        }

        private void SaveUndo()
        {
            undoStack.Save(board, letterVisibleCats);
        }

        private void RestoreLetterVisualState(NekoCoord[] visibleLetters)
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
    }
}
