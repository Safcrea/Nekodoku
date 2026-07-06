using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class OnCollisionPunchScale : MonoBehaviour
    {
        [SerializeField] private TransformSpringComponent scoreTransformSpring;
        [SerializeField] private Vector3 scaleVector;
        [SerializeField] private float maxVelocityMagnitude;
        [SerializeField] private float maxPunchForce;

        private void OnCollisionEnter(Collision other)
        {
            Rigidbody targetRigidbody = other.rigidbody;
    
            if (targetRigidbody != null)
            {
                Vector3 otherVelocity = targetRigidbody.linearVelocity;
                float forceFactor = Mathf.InverseLerp(0f, maxVelocityMagnitude, otherVelocity.magnitude);
                scoreTransformSpring.AddVelocityScale(scaleVector * forceFactor * maxPunchForce);
            }
        }
    }
}