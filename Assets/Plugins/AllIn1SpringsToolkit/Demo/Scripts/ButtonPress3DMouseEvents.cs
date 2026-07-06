using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ButtonPress3DMouseEvents : MonoBehaviour
    {
        [SerializeField] private ButtonPress3DController buttonPress3DController;
        
        private void OnMouseDown()
        {
            buttonPress3DController.MouseDown();
        }
        
        private void OnMouseUp()
        {
            buttonPress3DController.MouseUp();
        }
    }
}