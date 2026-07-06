using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The top-line gameplay status: level title, found-cat count, and the
    /// status message, plus the four nav buttons. Pure presentation - it never
    /// mutates game state, only reflects the validation result GameManager
    /// hands it each refresh. The win panel is its own <see cref="WinScreen"/>
    /// and hearts are their own <see cref="LifeHearts"/>.
    /// </summary>
    public sealed class GameplayScreen : MonoBehaviour
    {
        private static readonly Color ConflictColor = new Color(0.94f, 0.24f, 0.2f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color SoftInkColor = new Color(0.43f, 0.39f, 0.36f, 1f);

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text countText;

        [SerializeField]
        private Text statusText;

        public void Refresh(int levelIndex, Level level, ValidationResult validation)
        {
            titleText.text = $"Level {levelIndex + 1}: {level.Title}";
            countText.text = $"Found {validation.RevealedCatCount}/{level.Size}";
            statusText.text = validation.IsSolved ? "Solved" : validation.Message;
            statusText.color = validation.IsFailed || validation.HasConflict ? ConflictColor : validation.IsSolved ? RevealedColor : SoftInkColor;
        }
    }
}
