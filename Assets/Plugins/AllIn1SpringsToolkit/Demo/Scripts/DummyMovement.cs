using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;
using AllIn1SpringsToolkit.Bones;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DummyMovement : MonoBehaviour
    {
		//BaseTransformSpring is the main spring that moves the dummy back and forth
		//Additionally we have the eyesTransformSprings that move the eyes
		//We also have a prefab for the hit effect, it's the white ring that appears (also driven by a spring)

		[SerializeField] private SpringSkeleton springSkeleton;
		[SerializeField] private TransformSpringComponent baseTransformSpring;
		[SerializeField] private TransformSpringComponent[] eyesTransformSprings;
        [SerializeField] private GameObject whiteRingHitPrefab;
        
        //These are the values we use to interact with the springs above
        [Space, Header("Hit Properties")]
        [SerializeField] private float scalePunch;
        [SerializeField] private float angleHitRandomVariance, byDirectionVariance;
        [SerializeField] private float minRotationPunch, maxRotationPunch;
        [SerializeField] private float minEyeRotationPunch, maxEyeRotationPunch;
        [SerializeField] private float minEyeScalePunch, maxEyeScalePunch;
        
        //These are the properties we use to do the non-spring hit animation
        [Space, Header("No Spring Hit")]
        [SerializeField] private bool doNoSpringAnimation;
        [SerializeField] private float rotationAngle, animationDuration;
        [SerializeField] private int oscillations;
        
        private Quaternion initialLocalBaseRotation;
        private Coroutine noSpringHitCoroutine;
        private Camera myCamera;
        public event Action<Vector3, DummyMovement> OnDummyHit;

		public void Initialize()
		{
			myCamera = Camera.main;
			initialLocalBaseRotation = transform.localRotation;

			if (baseTransformSpring != null)
			{
				baseTransformSpring.Initialize();
			}

			if (springSkeleton != null)
			{
				springSkeleton.Initialize();
				StartCoroutine(EnableSkeletonAfterWait());
			}
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			springSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(0.5f);

			springSkeleton.SetSkeletonEnabled(true);
		}

		public void DummyHitRandom()
        {
            Vector3 hitDirection = GetRandomHitDirection();
            DummyHitByDirection(hitDirection);
        }

        public Vector3 GetRandomHitDirection()
        {
            float randomAngle = Random.Range(-angleHitRandomVariance, angleHitRandomVariance);
            Vector3 hitDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.back;
            return hitDirection;
        }

        //This method handles the instantiation of the hit effect and calculates the hit direction
        private void OnMouseDown()
        {
            Vector3 mousePosition = Input.mousePosition;
            Vector3 dummyScreenPosition = myCamera.WorldToScreenPoint(transform.position);
            
            Vector3 mouseWorldPosition = myCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, dummyScreenPosition.z));
            Instantiate(whiteRingHitPrefab, mouseWorldPosition, Quaternion.identity);

            float xOffset = dummyScreenPosition.x - mousePosition.x;
            float xDirection = Mathf.Sign(xOffset);
            Vector3 hitDirection = Quaternion.AngleAxis(xDirection * Mathf.Abs(xOffset) * 900f / Screen.width, Vector3.up) * Vector3.back;
            
            hitDirection = RandomizeHitDirection(hitDirection);

            DummyHitByDirection(hitDirection);
        }
        
        public void DummyHitByDirection(Vector3 hitDirection, bool avoidEvent = false)
        {
            if(!avoidEvent)
            {
                OnDummyHit?.Invoke(hitDirection, this);   
            }
            
            //This is the non-spring hit animation
            if(doNoSpringAnimation)
            {
                Vector3 rotationAxis = Vector3.Cross(hitDirection.normalized, Vector3.up);
                if(noSpringHitCoroutine != null)
                {
                    transform.localRotation = initialLocalBaseRotation;
                    StopCoroutine(noSpringHitCoroutine);
                }
                noSpringHitCoroutine = StartCoroutine(BackAndForthRotation(transform, rotationAxis));
            }
            else
            {
				//This is the spring hit animation
				//We nudge the scale, although it's not really noticeable
				/*springSkeleton.AddVelocityScale(Vector3.one * scalePunch);*/
                
                //We nudge the rotation in the hit direction to drive the dummy back
                Vector3 rotationAxis = Vector3.Cross(hitDirection.normalized, Vector3.up);
				baseTransformSpring.AddVelocityRotation(rotationAxis * Random.Range(minRotationPunch, maxRotationPunch));
				//springSkeleton.AddVelocityRotation(rotationAxis * Random.Range(minRotationPunch, maxRotationPunch));

				//We add some extra flair to the eyes so they look wobbly and silly
				foreach (TransformSpringComponent eyeSpring in eyesTransformSprings)
                {
                    eyeSpring.AddVelocityScale(Vector3.one * Random.Range(minEyeScalePunch, maxEyeScalePunch));
                    eyeSpring.AddVelocityRotation(Random.onUnitSphere * Random.Range(minEyeRotationPunch, maxEyeRotationPunch));
                }   
            }
        }

        //The no spring hit animation
        private IEnumerator BackAndForthRotation(Transform targetTransform, Vector3 rotationAxis)
        {
            Quaternion initialRotation = targetTransform.rotation;
            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                float t = elapsedTime / animationDuration;
                float angle = Mathf.Sin(t * oscillations * Mathf.PI) * rotationAngle;
            
                targetTransform.rotation = initialRotation * Quaternion.AngleAxis(angle, rotationAxis);
            
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            targetTransform.rotation = initialRotation;
        }

        private Vector3 RandomizeHitDirection(Vector3 hitDirection)
        {
            hitDirection = new Vector3(Random.Range(-byDirectionVariance, byDirectionVariance),
                               Random.Range(-byDirectionVariance, byDirectionVariance),
                               Random.Range(-byDirectionVariance, byDirectionVariance))
                           + hitDirection;
            return hitDirection;
        }
    }
}