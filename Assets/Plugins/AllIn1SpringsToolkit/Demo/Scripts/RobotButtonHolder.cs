namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This class is used to control the buttons of the Robot demo and it's not important
    public class RobotButtonHolder : Demo3dButtonHolder
    {
        private RobotDemoController robotDemoController;

        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            robotDemoController = (RobotDemoController) demoElement;
        }

        public void RandomPosition()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.RandomPosition();
        }
        
        public void PunchPosition()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.PunchPosition();
        }
        
        public void RandomScale()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.RandomScale();
        }
        
        public void PunchScale()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.PunchScale();
        }
        
        public void RandomRotation()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.RandomRotation();
        }
        
        public void PunchRotation()
        {
            if(!robotDemoController.IsOpen()) return;
            robotDemoController.PunchRotation();
        }
    }
}