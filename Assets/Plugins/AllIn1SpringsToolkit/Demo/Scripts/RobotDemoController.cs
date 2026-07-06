using AllIn1SpringsToolkit.Bones;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class RobotDemoController : DemoElement
    {
		//Example that shows most usages of the TransformSpringComponent
		//On top of that we add some extra flourish with the targetTransform used to add a subtle bobbing effect
		//And the smileTransform used to rotate the smile based on the velocity of the robot
		[Space, Header("Robot")]
		[SerializeField] private SpringSkeleton robotSkeleton;
        [SerializeField] private TransformSpringComponent robotSpringComponent;
        [SerializeField] private Transform targetTransform, smileTransform;
        
        //The rest of the variables are used to control the randomization and punch effects
        [SerializeField] private float maxMoveRadius, punchPositionStrength;
        [SerializeField] private float scaleMin, scaleMax, punchScaleStrength;
        [SerializeField] private float punchRotationStrength;
        [SerializeField] private float upAndDownSpeed, upAndDownAmplitude;
        [SerializeField] private float smileRotateAmount;

        private Vector3 initialPosition, currentPosition;
        private float bobOffset;
        private Quaternion initialSmileRotation;

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			initialPosition = targetTransform.localPosition;
			currentPosition = initialPosition;
			initialSmileRotation = smileTransform.localRotation;

			robotSkeleton.Initialize();

			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			robotSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(0.5f);

			robotSkeleton.SetSkeletonEnabled(true);
		}

		private void Update()
        {
            //Here we do the bobbing effect to the robot
            //currentRandomPosition is the position that will be used to move the robot and that is set by the RandomPosition method
            //Then the TransformSpringComponent is set to follow targetTransform (configured in the inspector)
            bobOffset = Mathf.Sin(Time.time * upAndDownSpeed) * upAndDownAmplitude;
            Vector3 bobPosition = currentPosition + Vector3.up * bobOffset;
            targetTransform.localPosition = bobPosition;
            
            //Here we use the velocity of the robot to rotate the smile
            //It's an example of how we can read the spring values to drive other components and behaviors
            Vector3 localVelocity = robotSpringComponent.GetVelocityPosition();
            localVelocity = smileTransform.InverseTransformDirection(localVelocity);
            Vector3 eulerVelocity = localVelocity * smileRotateAmount;
            eulerVelocity.x = Mathf.Clamp(eulerVelocity.x, -45, 45);
            smileTransform.localRotation = initialSmileRotation * Quaternion.Euler(localVelocity * smileRotateAmount);
        }

        //It gets a new position and saved it in currentRandomPosition. This position is used in the bobbing effect that will end up positioning the robot
        public void RandomPosition()
        {
            float currentDistanceToInitial = Vector3.Distance(currentPosition, initialPosition);
            bool isTargetInInnerCircle = currentDistanceToInitial < maxMoveRadius / 2f;
            Vector3 newOffset = new Vector3(Random.Range(-maxMoveRadius, maxMoveRadius) * 2f, Random.Range(-maxMoveRadius, maxMoveRadius), 0f);
            bool isNewInInnerCircle = Vector3.Distance(newOffset, initialPosition) < maxMoveRadius / 2f;
            while(isTargetInInnerCircle == isNewInInnerCircle)
            {
                newOffset = new Vector3(Random.Range(-maxMoveRadius, maxMoveRadius) * 2f, Random.Range(-maxMoveRadius, maxMoveRadius), 0f);
                isNewInInnerCircle = Vector3.Distance(newOffset, initialPosition) < maxMoveRadius / 2f;
            }
            currentPosition = initialPosition + newOffset;
            robotSpringComponent.SetTargetPosition(currentPosition);
        }

        //Punch effect to the position
        public void PunchPosition()
        {
            robotSpringComponent.AddVelocityPosition(Random.insideUnitSphere.normalized * (punchPositionStrength * GetRandomPunchMultiplier()));
        }

        //We set the scale of the targetTransform the Robot is set to follow
        //So by changing the scale of the targetTransform the Robot will spring to that scale
        public void RandomScale()
        {
            float currentScale = robotSpringComponent.transform.localScale.x;
            float randomScale = Random.Range(scaleMin, scaleMax);
            float maxScaleGap = scaleMax - scaleMin;
            while(Mathf.Abs(currentScale - randomScale) < (maxScaleGap / 3f))
            {
                randomScale = Random.Range(scaleMin, scaleMax);
            }

            robotSpringComponent.SetTargetScale(new Vector3(randomScale, randomScale, randomScale));
        }

        //Punch effect to the scale
        public void PunchScale()
        {
            float randomPunchMultiplier = GetRandomPunchMultiplier();
            randomPunchMultiplier = Random.value > 0.5f ? randomPunchMultiplier : -randomPunchMultiplier;
            robotSpringComponent.AddVelocityScale(Vector3.one * (punchScaleStrength * randomPunchMultiplier));
        }

        //We set the rotation of the targetTransform the Robot is set to follow
        public void RandomRotation()
        {
            targetTransform.localPosition = Vector3.zero;
            if(targetTransform.rotation == Quaternion.identity)
            {
                robotSpringComponent.SetTargetRotation(Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360)));
            }
            else
            {
                robotSpringComponent.SetTargetRotation(Quaternion.identity);
            }
        }

        //Punch effect to the rotation
        public void PunchRotation()
        {
            robotSpringComponent.AddVelocityRotation(Random.insideUnitSphere.normalized * (punchRotationStrength * GetRandomPunchMultiplier()));
        }

        //Some variation to the punches strength
        private float GetRandomPunchMultiplier()
        {
            return Random.Range(0.5f, 1.5f);
        }
    }
}