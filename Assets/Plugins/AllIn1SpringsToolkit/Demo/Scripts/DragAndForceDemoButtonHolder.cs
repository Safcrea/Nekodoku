using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DragAndForceDemoButtonHolder : Demo3dButtonHolder
    {
        [SerializeField] private Slider forceSlider;
        [SerializeField] private Slider dragSlider;
        [SerializeField] private TextMeshProUGUI forceText;
        [SerializeField] private TextMeshProUGUI dragText;
        
        private DragAndForceDemoController dragAndForceDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            dragAndForceDemoController = (DragAndForceDemoController) demoElement;
            dragAndForceDemoController.PassInUiReferences(forceSlider, dragSlider, forceText, dragText);
        }

        public void RobotSpringPunch()
        {
            if(!dragAndForceDemoController.IsOpen()) return;
            dragAndForceDemoController.RobotSpringPunch();
        }
        
        public void RandomizeForceAndDrag()
        {
            if(!dragAndForceDemoController.IsOpen()) return;
            dragAndForceDemoController.RandomizeForceAndDrag();
        }
    }
}
