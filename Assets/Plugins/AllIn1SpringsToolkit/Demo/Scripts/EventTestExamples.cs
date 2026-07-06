using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class EventTestExamples : MonoBehaviour
    {
        public TransformSpringComponent transformSpringComponent;
        public ColorSpringComponent colorSpringComponent;

        //We can subscribe to the events in the Awake method, OnEnable or wherever we want
        private void Awake()
        {
            transformSpringComponent.PositionEvents.OnTargetReached += OnPositionSpringTargetReached;
            transformSpringComponent.PositionEvents.OnClampingApplied += OnClampingApplied;
            transformSpringComponent.PositionEvents.OnCurrentValueChanged += OnCurrentValueChanged;
        
            colorSpringComponent.Events.OnTargetReached += OnColorSpringTargetReached;
        }
    
        //We must remember to unsubscribe from the events when the script is destroyed
        private void OnDestroy()
        {
            transformSpringComponent.PositionEvents.OnTargetReached -= OnPositionSpringTargetReached;
            transformSpringComponent.PositionEvents.OnClampingApplied -= OnClampingApplied;
            transformSpringComponent.PositionEvents.OnCurrentValueChanged -= OnCurrentValueChanged;
        
            colorSpringComponent.Events.OnTargetReached -= OnColorSpringTargetReached;
        }

        //Custom methods to handle the events. We can do whatever we want here
        private void OnPositionSpringTargetReached()
        {
            Debug.Log("Position Spring Event: Target Reached");
        }

        private void OnClampingApplied()
        {
            Debug.Log("Position Spring Event: Clamping Applied");
        }

        private void OnCurrentValueChanged()
        {
            Debug.Log("Position Spring Event: Current Value Changed");
        }
    
        private void OnColorSpringTargetReached()
        {
            Debug.Log("Color Spring Event: Target Reached");
        }
    }
}