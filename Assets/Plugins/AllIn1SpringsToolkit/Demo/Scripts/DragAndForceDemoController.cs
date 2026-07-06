using AllIn1SpringsToolkit.Bones;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DragAndForceDemoController : DemoElement
    {
		//Simple demo where we have a robot that is idle in the center, but can be punched to the right
		//The TransformSpringComponent is only used to drive the position of the robot, scale and rotation are disabled
		//The robot will have a force slider and a drag slider that will control the force and drag of the spring
		//This allows us to see how the robot behaves with different forces and drags at playtime

		[Space, Header("Robot")]
		[SerializeField] private SpringSkeleton robotSkeleton;
        [SerializeField] private TransformSpringComponent robotSpringComponent;
        [SerializeField] private float positionNudgeForce;

        [Space, Header("Drag And Force Sliders")]
        [SerializeField] private Vector2 forceRange;
        [SerializeField] private Vector2 dragRange;
        
        [Space, Header("Secondary Motion Polish")]
        [SerializeField] private Transform smileTransform;
        [SerializeField] private float smileRotateAmount;
        [SerializeField] private Transform scaleTransform;
        [SerializeField] private float scaleMotionMultiplier, maxScale, minScale;

        private Vector3 pushVector;
        private Slider forceSlider, dragSlider;
        private TextMeshProUGUI forceText, dragText;
        private Quaternion initialSmileRotation;
        
		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			initialSmileRotation = smileTransform.localRotation;
			pushVector = robotSpringComponent.transform.InverseTransformVector(Vector3.right);

			robotSkeleton.Initialize();

			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			robotSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(0.5f);

			robotSkeleton.SetSkeletonEnabled(true);
		}

		//This method is called by the button in the UI. It will punch the robot to the right
		public void RobotSpringPunch()
        {
            robotSpringComponent.AddVelocityPosition(pushVector * positionNudgeForce);
        }
        
        //This method is called by the button in the UI. It will randomize the force and drag sliders
        public void RandomizeForceAndDrag()
        {
            forceSlider.value = Random.Range(0f, 1f);
            dragSlider.value = Random.Range(0f, 1f);
        }
        
        private void Update()
        {
            if(!isOpen)
            {
                return;
            }
            
            //We first make sure the force and drag of the spring are updated based on the sliders
            UpdateForceAndDrag();

            //From this point on you'll only see not essential extra polish. The important toolkit usage parts are above
            
            //Here we use the velocity of the robot to rotate the smile
            //It's an example of how we can read the spring values to drive other components and behaviors
            RobotSmileSecondaryMotion();
            
            //Here we use the velocity of the robot to scale it
            ScaleWobbleSecondaryMotion();
        }

        private void UpdateForceAndDrag()
        {
            if(forceSlider == null || dragSlider == null)
            {
                return;
            }

			float unifiedForcePosition = Mathf.Lerp(forceRange.x, forceRange.y, forceSlider.value);
			float unifiedDragPosition = Mathf.Lerp(dragRange.x, dragRange.y, dragSlider.value);
			robotSpringComponent.SetUnifiedForceAndDragPosition(unifiedForcePosition, unifiedDragPosition);

			forceText.text = unifiedForcePosition.ToString("F0");
            dragText.text = unifiedDragPosition.ToString("F0");
        }

        private void RobotSmileSecondaryMotion()
        {
            Vector3 localVelocity = robotSpringComponent.GetVelocityPosition();
            localVelocity = smileTransform.InverseTransformDirection(localVelocity);
            Vector3 eulerVelocity = localVelocity * smileRotateAmount;
            eulerVelocity.x = Mathf.Clamp(eulerVelocity.x, -45, 45);
            smileTransform.localRotation = initialSmileRotation * Quaternion.Euler(localVelocity * smileRotateAmount);
        }
        
        private void ScaleWobbleSecondaryMotion()
        {
            float velocityX = robotSpringComponent.GetVelocityPosition().x * scaleMotionMultiplier;
            float scaleX = Mathf.Clamp(1f + velocityX, minScale, maxScale);
            scaleTransform.localScale = new Vector3(scaleX, Mathf.Max(minScale, 1f - velocityX), scaleX);
        }

        //Initializes the sliders with the force and drag values of the spring
        public void PassInUiReferences(Slider newForceSlider, Slider newDragSlider,
            TextMeshProUGUI newForceText, TextMeshProUGUI newDragText)
        {
            forceSlider = newForceSlider;
            dragSlider = newDragSlider;
            
            forceSlider.value = RemapUnclamped(robotSpringComponent.GetUnifiedForcePosition(), forceRange.x, forceRange.y, 0f, 1f);
            dragSlider.value = RemapUnclamped(robotSpringComponent.GetUnifiedDragPosition(), dragRange.x, dragRange.y, 0f, 1f);
            
            forceText = newForceText;
            dragText = newDragText;
        }
        
        private float RemapUnclamped(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float normalizedValue = (value - fromMin) / (fromMax - fromMin);
            return Mathf.Lerp(toMin, toMax, normalizedValue);
        }
    }
}
