using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Lives on the root of the Cell prefab (Assets/_GameData/Systems/Prefabs/NekoCell.prefab).
    /// Serialized fields are wired up by Meowdoku &gt; Scene &gt; Build Cell Prefab; do not
    /// rename them without re-running that tool.
    /// </summary>
    public sealed class CellView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DoubleTapSeconds = 0.45f;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Outline backgroundOutline;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Text markText;

        [SerializeField]
        private Text badgeText;

        [SerializeField]
        private Image nekoImage;

        public Image BackgroundImage => backgroundImage;
        public Outline BackgroundOutline => backgroundOutline;
        public CanvasGroup CanvasGroup => canvasGroup;
        public Text MarkText => markText;
        public Text BadgeText => badgeText;
        public Image NekoImage => nekoImage;

        private BoardInputHandler inputHandler;
        private int row;
        private int column;
        private bool dragged;
        private bool hasRecentTap;
        private float lastTapTime;

        public void Bind(BoardInputHandler owner, int cellRow, int cellColumn)
        {
            inputHandler = owner;
            row = cellRow;
            column = cellColumn;
            hasRecentTap = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragged)
            {
                dragged = false;
                hasRecentTap = false;
                return;
            }

            float now = Time.unscaledTime;
            bool isDoubleTap = eventData.clickCount >= 2 || (hasRecentTap && now - lastTapTime <= DoubleTapSeconds);
            hasRecentTap = false;

            if (isDoubleTap)
            {
                inputHandler.CommitCat(row, column);
                return;
            }

            hasRecentTap = true;
            lastTapTime = now;
            inputHandler.ToggleCross(row, column);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            hasRecentTap = false;
            inputHandler.BeginCrossDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragged = true;
            inputHandler.CrossAtPointer(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            inputHandler.EndCrossDrag();
        }
    }
}
