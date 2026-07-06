namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SnakeButtonHolder : Demo3dButtonHolder
    {
        private SnakeDemoController snakeDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            snakeDemoController = (SnakeDemoController) demoElement;
        }

        public void ToggleRotationMovement()
        {
            if(!snakeDemoController.IsOpen()) return;
            snakeDemoController.ToggleSnakeRotation();
        }
    }
}