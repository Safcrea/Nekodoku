namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This class is used to control the buttons of the ButtonPress3D demo and it's not important
    public class ButtonPress3DButtonHolder : Demo3dButtonHolder
    {
        private ButtonPress3DController buttonPress3DController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            buttonPress3DController = (ButtonPress3DController) demoElement;
        }

        public void ButtonPress()
        {
            if(!buttonPress3DController.IsOpen()) return;
            buttonPress3DController.ButtonPress();
        }
    }
}