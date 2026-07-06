using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SpringBonesButtonHolder : Demo3dButtonHolder
    {
        private SpringBonesDemoController springBonesDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            springBonesDemoController = (SpringBonesDemoController) demoElement;
        }

        public void ToggleMoveModeButton()
        {
            if(!springBonesDemoController.IsOpen()) return;
            springBonesDemoController.ToggleMoveMode();
        }
    }
}
