using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private enum TutorialGuideMode
        {
            Tap,
            DoubleTap,
            Drag
        }

        private readonly struct TutorialGuideConfig
        {
            public readonly NekoCoord StartCoord;
            public readonly NekoCoord EndCoord;
            public readonly TutorialGuideMode Mode;
            public readonly string Message;

            public TutorialGuideConfig(NekoCoord startCoord, NekoCoord endCoord, TutorialGuideMode mode, string message)
            {
                StartCoord = startCoord;
                EndCoord = endCoord;
                Mode = mode;
                Message = message;
            }
        }

        private readonly struct CellUi
        {
            public readonly int Row;
            public readonly int Column;
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly Image Image;
            public readonly Outline Outline;
            public readonly Text MarkText;
            public readonly Text BadgeText;
            public readonly Image NekoImage;

            public CellUi(int row, int column, RectTransform rect, CanvasGroup group, Image image, Outline outline, Text markText, Text badgeText, Image nekoImage)
            {
                Row = row;
                Column = column;
                Rect = rect;
                Group = group;
                Image = image;
                Outline = outline;
                MarkText = markText;
                BadgeText = badgeText;
                NekoImage = nekoImage;
            }
        }

        private readonly struct CellIntroState
        {
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly float DelaySeconds;

            public CellIntroState(RectTransform rect, CanvasGroup group, float delaySeconds)
            {
                Rect = rect;
                Group = group;
                DelaySeconds = delaySeconds;
            }
        }

        private readonly struct CellSnapshot
        {
            public readonly int Row;
            public readonly int Column;
            public readonly NekoCellMark Mark;
            public readonly bool Revealed;

            public CellSnapshot(int row, int column, NekoCellMark mark, bool revealed)
            {
                Row = row;
                Column = column;
                Mark = mark;
                Revealed = revealed;
            }
        }

        private readonly struct BoardSnapshot
        {
            public readonly CellSnapshot[] Cells;
            public readonly NekoCoord[] VisibleLetters;

            public BoardSnapshot(CellSnapshot[] cells, NekoCoord[] visibleLetters)
            {
                Cells = cells;
                VisibleLetters = visibleLetters;
            }
        }
    }
}
