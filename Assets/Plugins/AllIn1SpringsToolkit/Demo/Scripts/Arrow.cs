using AllIn1SpringsToolkit.Bones;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class Arrow : MonoBehaviour
    {
        //This class is used to control the arrows of the Arrows demo
        //It mostly handles the collision with the target and the bone spring setup
        
        [SerializeField] private Rigidbody arrowsRigidbody;
        [SerializeField] private TransformSpringComponent arrowsScaleTransformSpring;
        [SerializeField] private float shootForce;
        [SerializeField] private GameObject colliderStopperGameObject;

        [Space, Header("Bone Spring Setup")]
        [SerializeField] private TransformSpringComponent arrowsRotationTransformSpring;
        [SerializeField] private SpringBone arrowsSpringBone;
        
        private bool hasCollided;
        private bool hasBeenShot;
        private float timeSinceReady;
        private ArrowsDemoController arrowsDemoController;

		public void Init(ArrowsDemoController arrowsDemoController)
		{
			this.arrowsDemoController = arrowsDemoController;
		}

        public void ReadyArrow(Vector3 newPos, Transform newParent)
        {
            gameObject.SetActive(true);
            colliderStopperGameObject.SetActive(true);
            hasCollided = false;
            hasBeenShot = false;
            timeSinceReady = 0f;
            
            transform.position = newPos;
            transform.SetParent(newParent, true);
            
            arrowsRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            arrowsRigidbody.isKinematic = true;
            /* arrowsSpringBone.enabled = false; */
            arrowsRotationTransformSpring.enabled = false;
            
            arrowsScaleTransformSpring.ReachEquilibrium();
            arrowsScaleTransformSpring.SetCurrentValueScale(Vector3.zero);
            arrowsScaleTransformSpring.SetTargetScale(Vector3.one);
        }

        public void TryShootArrow()
        {
            if(hasBeenShot || timeSinceReady < 0.75f)
            {
                return;
            }
            hasBeenShot = true;
            
            arrowsRigidbody.linearVelocity = Vector3.zero;
            arrowsRigidbody.angularVelocity = Vector3.zero;
            arrowsRigidbody.isKinematic = false;
            arrowsRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            arrowsRigidbody.AddForce(-transform.forward * shootForce, ForceMode.Impulse);
        }
        
        public bool HasBeenShot()
        {
            return hasBeenShot;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore collisions that are not with the target
            // This would be done with layers in a real project, but we don't want to add layers dependencies to this demo
            if(!other.gameObject.name.Equals("Target"))
            {
                return;
            }
            
            //Fail safe to avoid multiple collisions
            if(hasCollided)
            {
                return;
            }
            hasCollided = true;
            colliderStopperGameObject.SetActive(false);
            
            //We set the bone spring to the target, this will give us some nice secondary motion when the arrow is attached to the target
            Transform targetTransform = other.transform;
            SetUpBoneSpringWithTarget(targetTransform);
			arrowsRotationTransformSpring.enabled = false;
			

            //We disable the rigidbody and notify the controller that the target has been hit
            arrowsRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            arrowsRigidbody.isKinematic = true;
            arrowsDemoController.TargetHit();
            arrowsDemoController.ArrowReleased();
        }

		public void Release()
		{
			arrowsDemoController.targetSkeleton.rootBone.RemoveChild(this.arrowsSpringBone);

			SetScaleTransformToZero();
			gameObject.SetActive(false);
		}

		private void SetUpBoneSpringWithTarget(Transform targetTransform)
        {
			SpringBone boneParent = arrowsDemoController.targetSkeleton.rootBone;
			boneParent.AddChild(this.arrowsSpringBone);

			//transform.SetParent(targetTransform, true);

			//We tell the bone spring to react to the target and initialize it

			/*
			arrowsSpringBone.SetNewTransformToReactTo(targetTransform);
            arrowsSpringBone.Initialize();
            */

			//Enable the bone spring now that it's ready
			/* arrowsSpringBone.enabled = true; */
		}

        private void Update()
        {
            timeSinceReady += Time.deltaTime;
            
            //If we shoot and we miss, we eventually disable the arrow and recover the arrow
            if(hasBeenShot && !hasCollided)
            {
                if(transform.position.y < -5f)
                {
                    gameObject.SetActive(false);
                    arrowsDemoController.ArrowReleased();
                }
            }
        }
        
        public void SetScaleTransformToZero()
        {
            arrowsScaleTransformSpring.transform.localScale = Vector3.zero;
        }
    }
}