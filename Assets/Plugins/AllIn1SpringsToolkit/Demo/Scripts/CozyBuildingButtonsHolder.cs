namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class CozyBuildingButtonsHolder : Demo3dButtonHolder
    {
        private CozyBuildingDemoController cozyBuildingDemoController;
        
        public override void Initialize(DemoElement demoElement, bool hideUi)
        {
            base.Initialize(demoElement, hideUi);
            cozyBuildingDemoController = (CozyBuildingDemoController) demoElement;
        }

        public void AddRandom()
        {
            if(!cozyBuildingDemoController.IsOpen()) return;
            cozyBuildingDemoController.TryAddRandom();
        }
        
        public void Rotate()
        {
            if(!cozyBuildingDemoController.IsOpen()) return;
            cozyBuildingDemoController.RotateGrid();
        }
        
        public void ResetGrid()
        {
            if(!cozyBuildingDemoController.IsOpen()) return;
            cozyBuildingDemoController.ResetGrid();
        }
    }
}