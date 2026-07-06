using AllIn1SpringsToolkit.Bones;
using System;
using System.Collections;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SpringBonesDemoController : DemoElement
    {
        [Space, Header("References")]
        [SerializeField] private Transform parentTransform;
		[SerializeField] private SpringSkeleton springSkeleton;

        [Space, Header("Movement Parameters")]
        [SerializeField] private float verticalMoveSpeed;
        [SerializeField] private float movementAmplitude;
        [SerializeField] private float rotateZMoveSpeed;
        [SerializeField] private float rotateYMoveSpeed;
        [SerializeField] private float rotationAmplitude;

        private enum MoveMode
        {
            //VerticalMove, //Hidden for now. Doesn't look as good as the other 2
            RotateZ,
            RotateY,
        }

        private MoveMode currentMoveMode;
        private float iniYPos;
        private float timeSinceEnter, currentRotationZTime, currentRotationYTime;
        //private float currentMoveTime;

		public override void Initialize(bool hideUi)
		{
			base.Initialize(hideUi);

			currentMoveMode = MoveMode.RotateZ;
			iniYPos = parentTransform.localPosition.y;
			//currentMoveTime = 0f;
			currentRotationZTime = 0f;
			currentRotationYTime = 0f;
			timeSinceEnter = 0f;

			StartCoroutine(EnableSkeletonAfterWait());
		}

		private IEnumerator EnableSkeletonAfterWait()
		{
			springSkeleton.Initialize();
			springSkeleton.SetSkeletonEnabled(false);

			yield return new WaitForSeconds(WAIT_TIME);

			springSkeleton.SetSkeletonEnabled(true);
		}

		private void Update()
        {
			if(!IsOpen())
            {
                return;
            }
            
            timeSinceEnter += Time.deltaTime;
            if(timeSinceEnter < WAIT_TIME + 0.1f)
            {
                return;
            }
    
            switch(currentMoveMode)
            {
                // case MoveMode.VerticalMove:
                //     currentMoveTime += Time.deltaTime;
                //     float verticalOffset = Mathf.Sin(currentMoveTime * verticalMoveSpeed) * movementAmplitude;
                //     parentTransform.localPosition = new Vector3(parentTransform.localPosition.x, iniYPos + verticalOffset, parentTransform.localPosition.z);
                //     break;
                case MoveMode.RotateZ:
                    currentRotationZTime += Time.deltaTime;
                    float rotationZ = Mathf.Sin(currentRotationZTime * rotateZMoveSpeed) * rotationAmplitude;
                    parentTransform.localRotation = Quaternion.Euler(parentTransform.localRotation.eulerAngles.x, parentTransform.localRotation.eulerAngles.y, rotationZ);
                    break;
                case MoveMode.RotateY:
                    currentRotationYTime += Time.deltaTime;
                    float rotationY = Mathf.Sin(currentRotationYTime * rotateYMoveSpeed) * rotationAmplitude;
                    parentTransform.localRotation = Quaternion.Euler(parentTransform.localRotation.eulerAngles.x, rotationY, parentTransform.localRotation.eulerAngles.z);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public void ToggleMoveMode()
        {
            currentMoveMode = (MoveMode)(((int)currentMoveMode + 1) % System.Enum.GetValues(typeof(MoveMode)).Length);
        }
    }
}