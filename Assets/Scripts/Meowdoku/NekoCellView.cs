using UnityEngine;
using UnityEngine.EventSystems;

namespace Meowdoku
{
    public sealed class NekoCellView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DoubleTapSeconds = 0.45f;

        private NekoGameController controller;
        private int row;
        private int column;
        private bool dragged;
        private bool hasRecentTap;
        private float lastTapTime;

        public void Bind(NekoGameController owner, int cellRow, int cellColumn)
        {
            controller = owner;
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
                controller.CommitCat(row, column);
                return;
            }

            hasRecentTap = true;
            lastTapTime = now;
            controller.ToggleCross(row, column);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            hasRecentTap = false;
            controller.BeginCrossDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragged = true;
            controller.CrossAtPointer(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            controller.EndCrossDrag();
        }
    }
}
