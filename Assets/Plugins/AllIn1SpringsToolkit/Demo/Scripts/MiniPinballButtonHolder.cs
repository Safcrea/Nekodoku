namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class MiniPinballButtonHolder : Demo3dButtonHolder
    {
        private MiniPinballDemoController miniPinballDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            miniPinballDemoController = (MiniPinballDemoController) demoElement;
        }

        public void UsePaddle()
        {
            if(!miniPinballDemoController.IsOpen()) return;
            miniPinballDemoController.UsePaddle();
        }
    }
}