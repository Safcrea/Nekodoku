using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ShockwaveDemoButton : MonoBehaviour
    {
        //This script function is to track the state and references of the buttons in the ShockwaveDemoController
        //Additionally, it handles the button click event and the button icon and color
        //And it also has a reference to the TransformSpringComponent that will be used to animate the button by punching its scale
        
        [Header("Main Properties")]
        [SerializeField] private int index;
        [SerializeField] private ShockwaveDemoController shockwaveDemoController;
        [SerializeField] private Image iconImage;

        [Space, Header("Scale Animation")]
        [SerializeField] private TransformSpringComponent iconTransformSpringComponent;
        
        private int iconAndColorIndex;
        
        public void OnClicked()
        {
            shockwaveDemoController.ButtonClicked(index);
        }
        
        public void Setup(int newIndex, ShockwaveDemoController newShockwaveDemoController)
        {
            index = newIndex;
            shockwaveDemoController = newShockwaveDemoController;
        }
        
        public void SetAndColorIcon(int newIconAndColorIndex, Sprite icon, Color color)
        {
            iconAndColorIndex = newIconAndColorIndex;
            iconImage.sprite = icon;
            iconImage.color = color;
        }
        
        public void ClickButtonFeedback()
        {
            iconTransformSpringComponent.SetCurrentValueScale(Vector3.zero);
        }
        
        public int GetIconAndColorIndex()
        {
            return iconAndColorIndex;
        }
    }
}