using System;
using TMPro;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class MiniPinballDemoController : DemoElement
    {
        //This demo showcases a simple usage of the RigidbodySpringComponent in a pinball-like setup
        //This class manages the paddle, the ball and the score text
        //The bumper logic is handled by each bumper individually
        //Many of this demo objects have a separate "Visuals" child so that the object can grow without making the collider bigger
        [Space, Header("Pinball Paddle")]
        [SerializeField] private RigidbodySpringComponent paddleRigidbodySpring;
        [SerializeField] private float paddleVerticalForce;
        [SerializeField] private ColorSpringComponent paddleColorSpring; //AutoUpdated in the inspector

        [Space, Header("Score Text")]
        [SerializeField] private TextMeshPro scoreText;
        [SerializeField] private TransformSpringComponent scoreTransformSpring;
        [SerializeField] private ColorSpringComponent scoreColorSpring; //AutoUpdated in the inspector
        [SerializeField] private FloatSpringComponent floatScoreTextSpring;
        [SerializeField] private float addScoreScalePunch, addScorePosPunch;
        [SerializeField] private Color addScoreColor;
        
        [Space, Header("Ball")]
        [SerializeField] private TransformSpringComponent ballTransformSpring;
        [SerializeField] private Rigidbody ballRigidbody;
        [SerializeField] private float minSpeed, maxSpeed, squashAmountMult, maxRatio, velocityRatioExponent;
        
        private int currentScore = 0;

        public void UsePaddle()
        {
            //Add vertical force to the paddle and change its color immediately to white so that it can then animate back to its original color
            paddleRigidbodySpring.AddVelocityPosition(Vector3.up * paddleVerticalForce);
            paddleColorSpring.SetCurrentValue(Color.white);
        }
        
        public void AddScore(int score)
        {
            //Add score to the current score and animate the score text and color
            currentScore += score;
            floatScoreTextSpring.SetTarget(currentScore);
            scoreColorSpring.SetCurrentValue(addScoreColor);
            scoreTransformSpring.AddVelocityScale(Vector3.up * addScoreScalePunch);
            scoreTransformSpring.AddVelocityPosition(Vector3.up * addScorePosPunch);
        }

        //Update gets the current score value from the float spring and updates the score text
        private void Update()
        {
            scoreText.text = Mathf.RoundToInt(floatScoreTextSpring.GetCurrentValue()).ToString();

            BallElongateAlongVelocity();
        }

        private void BallElongateAlongVelocity()
        {
            //We first set the rotation we want to elongate in
            float velocityMagnitude = ballRigidbody.linearVelocity.magnitude;
            if(velocityMagnitude < 0.01f)
            {
                ballTransformSpring.transform.localRotation = Quaternion.identity;
            }
            else
            {
                ballTransformSpring.transform.right = ballRigidbody.linearVelocity.normalized;
            }

            //We then calculate the velocity ratio and set the scale target with a spring
            float velocityRatio = (velocityMagnitude - minSpeed) / (maxSpeed - minSpeed);
            velocityRatio = Mathf.Pow(velocityRatio, velocityRatioExponent);
            velocityRatio = Mathf.Clamp(velocityRatio, 0f, maxRatio);
            ballTransformSpring.SetTargetScale(
                new Vector3(1f + velocityRatio, Mathf.Max(0.2f, 1f - (velocityRatio * squashAmountMult)), 1f + velocityRatio));
        }
    }
}