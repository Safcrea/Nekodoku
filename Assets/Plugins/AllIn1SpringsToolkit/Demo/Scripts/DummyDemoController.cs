using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DummyDemoController : DemoElement
    {
        //All the springs and logic is placed in the DummyMovement script
        //Here we just call the DummyHitRandom method on all the dummies
        
        [Space, Header("Dummies")]
        [SerializeField] private DummyMovement[] dummyMovements;

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			for(int i = 0; i < dummyMovements.Length; i++)
			{
				dummyMovements[i].Initialize();
			}
		}

		public void RandomHitsButtonPress()
        {
            if(!isOpen)
            {
                return;
            }
            
            foreach(DummyMovement dummy in dummyMovements)
            {
                dummy.DummyHitRandom();
            }
        }
    }
}