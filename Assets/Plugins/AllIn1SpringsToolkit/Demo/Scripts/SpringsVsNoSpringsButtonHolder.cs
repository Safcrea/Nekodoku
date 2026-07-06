namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This class is used to control the buttons of the SpringsVsNoSprings demo and it's not important
    public class SpringsVsNoSpringsButtonHolder : Demo3dButtonHolder
    {
        private SpringsVsNoSpringsDemoController springsVsNoSpringsDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            springsVsNoSpringsDemoController = (SpringsVsNoSpringsDemoController) demoElement;
        }

        public void RandomHitsButtonPress()
        {
            if(!springsVsNoSpringsDemoController.IsOpen()) return;
            springsVsNoSpringsDemoController.RandomHitsButtonPress();
        }
    }
}