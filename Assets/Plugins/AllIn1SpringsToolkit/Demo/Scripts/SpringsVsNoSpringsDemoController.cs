using System;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SpringsVsNoSpringsDemoController : DemoElement
    {
        //All the springs and logic is placed in the DummyMovement script
        //Here we just calculate the random hit direction and apply it to all the dummies
        
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
            
            Vector3 randomHitDirection = dummyMovements[0].GetRandomHitDirection();
            foreach(DummyMovement dummy in dummyMovements)
            {
                dummy.DummyHitByDirection(randomHitDirection, avoidEvent: true);
            }
        }
        
        //From here on we make sure that if any of the 2 dummies is hit, the other one will also be hit
        private void Start()
        {
            foreach(DummyMovement dummyMovement in dummyMovements)
            {
                dummyMovement.OnDummyHit += OnDummyHit;
            }
        }

        private void OnDestroy()
        {
            foreach(DummyMovement dummyMovement in dummyMovements)
            {
                dummyMovement.OnDummyHit -= OnDummyHit;
            }
        }

        private void OnDummyHit(Vector3 hitDirection, DummyMovement dummyBeingHit)
        {
            foreach(DummyMovement currentDummy in dummyMovements)
            {
                if(dummyBeingHit.Equals(currentDummy))
                {
                    continue;
                }
                
                currentDummy.DummyHitByDirection(hitDirection, avoidEvent: true);
            }
        }
    }
}