namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This class is used to control the buttons of the Dummies demo and it's not important
    public class DummyButtonHolder : Demo3dButtonHolder
    {
        private DummyDemoController dummyDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            dummyDemoController = (DummyDemoController) demoElement;
        }

        public void RandomHitsButtonPress()
        {
            if(!dummyDemoController.IsOpen()) return;
            dummyDemoController.RandomHitsButtonPress();
        }
    }
}