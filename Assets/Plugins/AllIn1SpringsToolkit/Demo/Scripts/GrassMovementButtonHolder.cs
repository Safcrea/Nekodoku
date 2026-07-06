using TMPro;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class GrassMovementButtonHolder : Demo3dButtonHolder
    {
        [SerializeField] private TextMeshProUGUI buttonText;
        private GrassMovementDemoController grassMovementDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            grassMovementDemoController = (GrassMovementDemoController) demoElement;
        }
        
        public void StopMoveRobotMovement()
        {
            if(!grassMovementDemoController.IsOpen()) return;
            bool isMoving = grassMovementDemoController.ToggleRobotMovement();
            buttonText.text = isMoving ? "Stop" : "Move";
        }
    }
}