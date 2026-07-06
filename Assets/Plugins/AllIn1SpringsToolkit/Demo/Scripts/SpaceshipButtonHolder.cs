namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This class is used to control the buttons of the Spaceship demo and it's not important
    public class SpaceshipButtonHolder : Demo3dButtonHolder
    {
        private SpaceshipController spaceshipController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            spaceshipController = (SpaceshipController) demoElement;
        }

        public void ShakeRotation()
        {
            if(!spaceshipController.IsOpen()) return;
            spaceshipController.ShakeRotation();
        }
    }
}