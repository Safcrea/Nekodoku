using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    //This is a example of a not real use case, just to show how to add Spring Components in runtime
    //See how we:
    //1. Add a SpringComponent in runtime
    //2. Setup the SpringComponent
    //3. Initialize the SpringComponent
    //4. Use it normally
    
    public class ExampleAddComponent : MonoBehaviour
    {
        [SerializeField] private Image uiImage;
        [SerializeField] private Image uiImageTarget;

        public TransformSpringComponent transformSpringComponent;
        public ColorSpringComponent colorSpring;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            colorSpring = gameObject.AddComponent<ColorSpringComponent>();
            colorSpring.SetAutoUpdateToTrue(isRenderer: false, uiImage.gameObject);
            colorSpring.Initialize();
            colorSpring.SetCurrentValue(Color.white);
            colorSpring.SetTarget(Color.yellow);

            yield return new WaitForSeconds(0.2f);
            transformSpringComponent = uiImage.gameObject.AddComponent<TransformSpringComponent>();
            transformSpringComponent.useTransformAsTarget = true;
            transformSpringComponent.followerTransform = uiImage.transform;
            transformSpringComponent.targetTransform = uiImageTarget.transform;
            transformSpringComponent.SpringScaleEnabled = false;
            transformSpringComponent.Initialize();
        }

        private void Update()
        {
            if(Time.timeSinceLevelLoad > 2f) colorSpring.SetTarget(uiImageTarget.color);
        }
    }
}