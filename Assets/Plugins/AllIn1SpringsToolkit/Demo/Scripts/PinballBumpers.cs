using System;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class PinballBumpers : MonoBehaviour
    {
        [SerializeField] private MiniPinballDemoController miniPinballDemoController;
        [SerializeField] private int scorePerCollision;
        [SerializeField] private TransformSpringComponent scoreTransformSpring;
        [SerializeField] private ColorSpringComponent scoreColorSpring; //AutoUpdated in the inspector
        [SerializeField] private float addScoreScalePunch, forceAddedToOtherRigidbody;
        [SerializeField] private Color addScoreColor;

        private void OnCollisionEnter(Collision other)
        {
            if(!miniPinballDemoController.IsOpen())
            {
                return;
            }
            
            Rigidbody otherRigidbody = other.rigidbody;
            if(otherRigidbody != null)
            {
                Vector3 impactDirection = otherRigidbody.transform.position - transform.position;
                impactDirection = impactDirection.normalized;
                
                //We push the ball away from the bumper (pinball logic)
                otherRigidbody.AddForce(impactDirection * forceAddedToOtherRigidbody, ForceMode.Impulse);
                
                //Color feedback
                scoreColorSpring.SetCurrentValue(addScoreColor);
             
                //Scale feedback by swapping the y and z components of the impact direction and punching the scale spring
                impactDirection.z = impactDirection.y;
                impactDirection.y = 0f;
                scoreTransformSpring.AddVelocityScale(impactDirection * addScoreScalePunch);
                
                //We notify the MiniPinballDemoController to add score so that it can update the score
                miniPinballDemoController.AddScore(scorePerCollision);
            }
        }
    }
}