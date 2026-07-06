using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ButtonPress3DController : DemoElement
    {
        //We'll use 2 springs for the button, one for the button itself and one for the base of the button
        //We also animate the color and camera fov
        //When the button is pressed we'll immediately set the values we want and animate them back when the button is released
        [Space, Header("3D Button")]
        [SerializeField] private TransformSpringComponent buttonTransformSpring;
        [SerializeField] private TransformSpringComponent buttonBaseTransformSpring;
        [SerializeField] private ColorSpringComponent colorSpringComponent;
        [SerializeField] private CamFovOrSizeSpringComponent camFovSpringComponent;
        [SerializeField] private float camFovPressed;
        [SerializeField] private Vector3 pressedScale, basePressedScale;
        [SerializeField] private float pressedPunchScale;
        [SerializeField] private Color pressedColor;
        
        private bool isPressed;
        private Color startColor;
        private float startFov;

        private void Start()
        {
            startColor = colorSpringComponent.GetCurrentValue();
            startFov = camFovSpringComponent.GetCurrentValue();
        }

        //Here we immediately set the values we want the button to have. With no transition, we change them and keep them there immediately
        public void MouseDown()
        {
            //Immediately set the target scale to the pressed scale. We do this immediately to give the user instant feedback and weight to the button press
            buttonTransformSpring.SetTargetScale(pressedScale);
            buttonTransformSpring.ReachEquilibrium();
            buttonBaseTransformSpring.SetTargetScale(basePressedScale);
            buttonBaseTransformSpring.ReachEquilibrium();
            
            //Additionally, we punch the scale of the button for a more dynamic effect
            Vector3 punchScaleVector = new Vector3(1f, 0f, 1f) * pressedPunchScale;
            buttonTransformSpring.AddVelocityScale(punchScaleVector);
            buttonBaseTransformSpring.AddVelocityScale(punchScaleVector);
            
            //Set the button to the pressed color
            colorSpringComponent.SetTarget(pressedColor);
            colorSpringComponent.ReachEquilibrium();

			//Set the camera fov to the pressed fov
			camFovSpringComponent.SetTarget(camFovPressed);
			camFovSpringComponent.ReachEquilibrium();
        }
        
        //When the button is released, we allow the values to animate back to their original values
        public void MouseUp()
        {
            //Set the target scale back to the original scale so we smoothly animate back to it
            buttonTransformSpring.SetTargetScale(Vector3.one);
            buttonBaseTransformSpring.SetTargetScale(Vector3.one);
            
            //Animate button back to original color
            colorSpringComponent.SetTarget(startColor);
            
            //Set the camera fov back to the original fov
            camFovSpringComponent.SetTarget(startFov);
        }

        public void ButtonPress()
        {
            MouseDown();
            MouseUp();
        }

        private void Update()
        {
            if(!isOpen)
            {
				camFovSpringComponent.SetTarget(startFov);
				camFovSpringComponent.ReachEquilibrium();
			}
        }
    }
}