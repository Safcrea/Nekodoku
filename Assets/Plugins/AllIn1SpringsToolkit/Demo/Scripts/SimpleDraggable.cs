using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SimpleDraggable : MonoBehaviour,
        IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
    {
        private RectTransform rectTransform;
        private Vector2 initialPointerPosition;
        private Vector3 initialPosition;
        private bool canDrag = false;
        public bool isDragging = false;
        public bool isPointed = false;

        public UnityEvent onBeginDragEvent;
        public UnityEvent onDragEvent;
        public UnityEvent onEndDragEvent;
        public UnityEvent onPointerEnterEvent;
        public UnityEvent onPointerExitEvent;
        public UnityEvent onPointerUpEvent;
        public UnityEvent onPointerDownEvent;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Check if the mouse is over the RectTransform's rect, prevents phantom drags caused by Unity
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventData.pressEventCamera))
            {
                canDrag = true;
                initialPointerPosition = eventData.position;
                initialPosition = rectTransform.position;
                isDragging = true;
                onBeginDragEvent?.Invoke();
            }
            else
            {
                canDrag = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (canDrag)
            {
                Vector2 delta = eventData.position - initialPointerPosition;
                rectTransform.position = initialPosition + new Vector3(delta.x, delta.y, 0);
                onDragEvent?.Invoke();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if(canDrag)
            {
                isDragging = false;
                onEndDragEvent?.Invoke();   
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointed = true;
            onPointerEnterEvent?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointed = false;
            onPointerExitEvent?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            onPointerUpEvent?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            onPointerDownEvent?.Invoke();
        }
    }
}