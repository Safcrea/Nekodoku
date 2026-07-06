using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SliderPressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Slider slider;
        private bool isPressed = false;

        private void Start()
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public bool IsSliderPressed()
        {
            return isPressed;
        }
    }
}