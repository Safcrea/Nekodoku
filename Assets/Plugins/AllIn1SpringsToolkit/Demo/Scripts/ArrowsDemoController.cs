using UnityEngine;
using Random = UnityEngine.Random;
using AllIn1SpringsToolkit.Bones;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ArrowsDemoController : DemoElement
    {
        //We'll shoot arrows to the target
        //The arrows are pooled and we'll reset them when we run out of arrows or the reset button is pressed
        //The target will rotate and do a position punch when impacted by an arrow
        //The arrows will stick to the target and have some secondary motion driven by a spring bone
        //The Arrow class handles the arrow collision and bone spring setup
        
        [Space, Header("Arrows")]
        [SerializeField] private Transform arrowsParent;
        [SerializeField] private Arrow[] arrows;
        [SerializeField] private float maxRotation, rotationSpeed;

		[Space, Header("Target")]
		public SpringSkeleton targetSkeleton;
		[SerializeField] private TransformSpringComponent targetTransformSpring;
		[SerializeField] private float impactPositionPunch;
        
        private int currentArrowIndex;
        private float randomArrowRotationOffset;

		private Vector3 currentHitEulerRotation;

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			targetTransformSpring.Initialize();
			targetSkeleton.Initialize();

			InitArrows();
			ResetArrows();

			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			targetSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(WAIT_TIME);

			targetSkeleton.SetSkeletonEnabled(true);
		}

		private void Update()
        {
            arrowsParent.gameObject.SetActive(isOpen);
            
            if(!isOpen || currentArrowIndex >= arrows.Length)
            {
                return;
            }

			//When we have an arrow ready, we'll rotate it back and forth
			if (!arrows[currentArrowIndex].HasBeenShot())
            {
                Transform currentArrowTransform = arrows[currentArrowIndex].transform;
                float rotation = Mathf.Sin((Time.time + randomArrowRotationOffset) * rotationSpeed) * maxRotation;
                currentArrowTransform.localRotation = Quaternion.Euler(rotation, 180f, 0f);   
            }
        }

        private void ReadyCurrentArrow()
        {
            randomArrowRotationOffset = UnityEngine.Random.Range(-100f, 100f);
            arrows[currentArrowIndex].ReadyArrow(arrowsParent.position, arrowsParent);
        }

        public void TryShootArrow()
        {
            if(currentArrowIndex >= arrows.Length)
            {
                ResetArrows();
            }
            else
            {
                arrows[currentArrowIndex].TryShootArrow();
            }
        }

		private void InitArrows()
		{
			for(int i = 0; i < arrows.Length; i++)
			{
				arrows[i].Init(this);
			}
		}

        //Remove the arrows from the target and reset them
        public void ResetArrows()
        {
            currentArrowIndex = 0;
            foreach(Arrow arrow in arrows)
            {
				arrow.Release();
            }
            ReadyCurrentArrow();
        }
        
        //Rotate the target and punch its position
        public void TargetHit()
        {
            RandomTargetRotate();

			Transform targetTransform = targetTransformSpring.transform;
            targetTransformSpring.AddVelocityPosition(targetTransform.InverseTransformVector(Vector3.right) * impactPositionPunch);
        }
        
        public void ArrowReleased()
        {
            currentArrowIndex++;
            if(currentArrowIndex < arrows.Length)
            {
                ReadyCurrentArrow();
            }
        }
        
        public void RandomTargetRotate()
        {
            int randomAxis = Random.Range(0, 3);
            currentHitEulerRotation[randomAxis] += 90f * (Random.value > 0.5f ? 1f : -1f);
            targetTransformSpring.SetTargetRotation(currentHitEulerRotation);
        }
        
#if UNITY_EDITOR
        [ContextMenu("Get Arrow References")]
        private void GetArrowReferences()
        {
            int childCount = arrowsParent.childCount;
            arrows = new Arrow[childCount];
            
            for(int i = 0; i < childCount; i++)
            {
                Transform child = arrowsParent.GetChild(i);
                arrows[i] = child.GetComponent<Arrow>();
            }
            
            EditorUtility.SetDirty(this);
        }
#endif
    }
}