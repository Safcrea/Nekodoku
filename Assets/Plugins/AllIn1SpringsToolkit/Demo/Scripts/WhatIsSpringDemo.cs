using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class WhatIsSpringDemo : DemoElement
    {
        //Here we show how the CurrentValue goes towards the TargetValue
        //The CurrentValue holds the anchoredPosition that we'll use to chase targetRectTransform
        //And additionally, in the editor, it also has a TransformSpringComponent that animates the scale towards targetTransform scale
        //So since the CurrentValue object is a UI element we use the AnchoredPositionSpringComponent to animate the position
        //And the TransformSpringComponent to animate the rotation and scale
        [SerializeField] private AnchoredPositionSpringComponent anchoredPositionSpring;
        [SerializeField] private RectTransform targetRectTransform, moveAreaRectTransform;
        [SerializeField] private float minDistanceThreshold;
        [SerializeField] private float scaleVarianceMin, scaleVarianceMax;
        [SerializeField] private float rotationVariance;

        //When we teleport the targetTransform to a new position, we also animate the scale and color for visual polish
        [Space, Header("Target Object Polish")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private TransformSpringComponent targetTransformSpring;
        [SerializeField] private ColorSpringComponent targetColorSpring;
        [SerializeField] private float scalePunchAfterTeleport;
        [SerializeField] private Color colorAfterTeleport;

        private float lastPositionChangeTime, timeOpenStarted;
        private bool useRightHalf = false;

        private void Update()
        {
            //We wait a tiny bit with the target not moving in the center before we randomize the target position
            if(timeOpenStarted < 0.6f)
            {
                if(isOpen)
                {
                    timeOpenStarted += Time.deltaTime;   
                }
                return;
            }
            
            //Then, once the current target has reached the target position, we randomize the target position
            if (anchoredPositionSpring.GetVelocity().magnitude < 0.05f && lastPositionChangeTime + 0.1f < Time.time)
            {
                RandomizeTarget();
            }
        }

        //Here we set the Target to a new random position and scale
        //So that the AnchoredPositionSpringComponent will animate towards that position
        //And the TransformSpringComponent will animate towards the new scale
        private void RandomizeTarget()
        {
            lastPositionChangeTime = Time.time;
            
            //Randomize targetTransform position so that CurrentValue will chase it
            Vector2 newPosition = GenerateNewPosition();
            targetRectTransform.anchoredPosition = newPosition;
            
            //Randomize targetTransform rotation
            targetTransform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, rotationVariance) * (useRightHalf ? 1f : -1f));
            
            //When we teleport the target, we also animate the scale and color for visual polish
            targetTransformSpring.SetTargetScale(
                Vector3.one * UnityEngine.Random.Range(1f - scaleVarianceMin, 1f + scaleVarianceMax)
            );
            targetTransformSpring.AddVelocityScale(Vector3.one * scalePunchAfterTeleport);
            targetColorSpring.SetCurrentValue(colorAfterTeleport);
        }

        //Generate a new position ensuring it's far enough from the current position
        private Vector2 GenerateNewPosition()
        {
            float rectWidth = moveAreaRectTransform.rect.width;
            float rectHeight = moveAreaRectTransform.rect.height;
            Vector2 currentPosition = targetRectTransform.anchoredPosition;

            Vector2 newPosition = NewPosition(rectWidth, rectHeight);
            int tries = 0;
            while(tries < 10 && Vector2.Distance(newPosition, currentPosition) < minDistanceThreshold)
            {
                newPosition = NewPosition(rectWidth, rectHeight);
                tries++;
            }

            useRightHalf = !useRightHalf;

            return newPosition;
        }

        //Calculate a random position within the opposite half we are on
        private Vector2 NewPosition(float rectWidth, float rectHeight)
        {
            float xMin = useRightHalf ? 0 : -rectWidth / 2f;
            float xMax = useRightHalf ? rectWidth / 2f : 0;

            Vector2 newPosition = new Vector2(
                UnityEngine.Random.Range(xMin, xMax),
                UnityEngine.Random.Range(-rectHeight / 2f, rectHeight / 2f)
            );
            return newPosition;
        }
    }
}