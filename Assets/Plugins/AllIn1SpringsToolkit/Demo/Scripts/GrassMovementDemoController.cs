using UnityEngine;
using Random = UnityEngine.Random;
using AllIn1SpringsToolkit.Bones;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class GrassMovementDemoController : DemoElement
    {
        //This demo simulates grass being pushed by a robot. Ideally, this sort of logic and deformation would be done with a shader
        //But this is a simple way to show how springs can react to other objects in creative ways
        
        //The robot moves from side to side. The TransformSpringComponent is set to follow robotTargetTransform (in the inspector)
        [Space, Header("Robot")]
        [SerializeField] private TransformSpringComponent robotSpringComponent;
		[SerializeField] private SpringSkeleton robotSkeleton;
        [SerializeField] private Transform robotTargetTransform;
        [SerializeField] private float robotSpeed;

        //The grass is a bunch of TransformSpringComponents that will be pushed by the robot
        //The variables below control how the grass is placed and how it reacts to the robot
        [Space, Header("Grass")]
        [SerializeField] private TransformSpringComponent[] grassSpringComponents;
        [SerializeField] private Transform grassParent;
        [SerializeField] private float maxPushAmount;
        [SerializeField] private float pushProximity;
        [SerializeField] private float maxRotationAngle;
        [SerializeField] private float grassWidth;
        [SerializeField] private float grassRandomXVariance;
        [SerializeField] private Vector2 scaleRange;
        [SerializeField] private float grassSwaySpeed, grassSwayAmount, grassPositionInfluenceMultiplier;

        private Vector3 robotStartPosition;
        private float robotMoveTime;
        [SerializeField] private bool robotIsMoving;
        
        private void Start()
        {

		}

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			robotStartPosition = robotSpringComponent.transform.localPosition;
			robotIsMoving = true;
			robotMoveTime = 0f;

			robotSkeleton.Initialize();
			robotSkeleton.SetSkeletonEnabled(false);

			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			yield return new WaitForSeconds(0.5f);

			robotSkeleton.SetSkeletonEnabled(true);
		}

		//This method is called by the button in the UI. It will toggle the robot movement
		public bool ToggleRobotMovement()
        {
            robotIsMoving = !robotIsMoving;
            return robotIsMoving;
        }

        private void Update()
        {
            if(!isOpen)
            {
                return;
            }
            
            // Move the robot from side to side
            if(robotIsMoving)
            {
                robotMoveTime += Time.deltaTime;
            }
            float sinInput = robotMoveTime * robotSpeed;
            Vector3 offset = robotStartPosition + new Vector3(Mathf.Sin(sinInput), 0f, 0f) * (grassWidth / 2.2f);
            robotTargetTransform.localPosition = offset;
            
            // Iterate over all grass spring components and calculate push amount based on proximity to robot
            for(int i = 0; i < grassSpringComponents.Length; i++)
            {
                TransformSpringComponent grassSpring = grassSpringComponents[i];
                // Calculate distance along X-axis from robot to grass
                float xDistance = Mathf.Abs(grassSpring.transform.position.x - robotSpringComponent.transform.position.x);

                // Calculate push amount based on proximity
                float pushAmount = Mathf.Clamp01(1 - (xDistance / pushProximity)) * maxPushAmount;

                // Calculate rotation to face away from the robot
                if(pushAmount > 0)
                {
                    // Determine direction based on which side of the robot the grass is on
                    // Reversed direction to push towards the robot instead of away
                    float direction = -Mathf.Sign(grassSpring.transform.position.x - robotSpringComponent.transform.position.x);

                    // Calculate rotation angle, using maxRotationAngle to limit the inclination
                    float rotationAngle = pushAmount * maxRotationAngle * direction;

                    // Set the rotation target for the grass spring component
                    grassSpring.SetTargetRotation(Quaternion.Euler(0f, 0f, rotationAngle));
                }
                else
                {
                    // Reset rotation when not being pushed
                    grassSpring.SetTargetRotation(Quaternion.identity);
                    
                    //Sway the grass back and forth when not influenced by the robot
                    Vector3 grassRotation = Vector3.zero;
                    grassRotation.z = Mathf.Sin((Time.time + (grassSpring.transform.position.x * grassPositionInfluenceMultiplier)) 
                                                * grassSwaySpeed) * grassSwayAmount;
                    grassSpring.SetVelocityRotation(grassRotation);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Get Grass Spring Components")]
        private void GetGrassSpringComponents()
        {
            grassSpringComponents = grassParent.GetComponentsInChildren<TransformSpringComponent>();
        }
        
        [ContextMenu("Scatter Grass")]
        private void ScatterGrass()
        {
            float startXPosition = -grassWidth / 2f;
            float grassSpacing = grassWidth / (grassSpringComponents.Length - 1);
            for(int i = 0; i < grassSpringComponents.Length; i++)
            {
                TransformSpringComponent grassSpringComponent = grassSpringComponents[i];
                Vector3 spawnPosition = (startXPosition + (i * grassSpacing)) * Vector3.right;
                spawnPosition.y = 0f;
                spawnPosition.x += Random.Range(-grassRandomXVariance, grassRandomXVariance);
                grassSpringComponent.transform.localPosition = spawnPosition;
                grassSpringComponent.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            }
            
            EditorUtility.SetDirty(this);
        }
#endif
    }
}