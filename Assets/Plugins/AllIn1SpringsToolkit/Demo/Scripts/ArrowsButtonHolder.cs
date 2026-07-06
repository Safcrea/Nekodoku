namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ArrowsButtonHolder : Demo3dButtonHolder
    {
        private ArrowsDemoController arrowsDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            arrowsDemoController = (ArrowsDemoController) demoElement;
        }

        public void TryShootArrow()
        {
            if(!arrowsDemoController.IsOpen()) return;
            arrowsDemoController.TryShootArrow();
        }
        
        public void ResetArrows()
        {
            if(!arrowsDemoController.IsOpen()) return;
            arrowsDemoController.ResetArrows();
        }
        
        public void RandomRotateTarget()
        {
            if(!arrowsDemoController.IsOpen()) return;
            arrowsDemoController.RandomTargetRotate();
        }
    }
}