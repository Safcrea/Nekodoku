using TMPro;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ScoreCounterFloaty : MonoBehaviour
    {
        //Floaties will have a color spring that will drive the fade in and out of the text
        //They will also have a transform spring that will drive the scale punch
        [SerializeField] private ColorSpringComponent colorSpring;
        [SerializeField] private TransformSpringComponent transformSpring;
        [SerializeField] private float scalePunch;
        [SerializeField] private TextMeshProUGUI scoreText;
        
        private Color baseColor;
        private int accumulatedScore;
        
        public void Initialize(Color textColor)
        {
            baseColor = textColor;
            
            //The floaty starts with the text color set to the base color and fully transparent
            //With this target, later on, we can set a current value that isn't transparent and let it automatically go back to transparent
            colorSpring.SetTarget(ColorWithAlpha(baseColor, 0f));
            colorSpring.ReachEquilibrium();
            accumulatedScore = 0;
        }
        
        //When the score changes we set the color spring to the base color and we let it fade out
        //We also punch the scale of the floaty
        public void ChangeScore(int score, bool isAddition)
        {
            accumulatedScore += score;
            scoreText.text = isAddition ? $"+{accumulatedScore}" : $"-{accumulatedScore}";
            
            colorSpring.SetCurrentValue(baseColor);
            
            transformSpring.AddVelocityScale(Vector3.one * scalePunch);
        }

        private void Update()
        {
            if(accumulatedScore > 0 && scoreText.color.a < 0.1f)
            {
                accumulatedScore = 0;
            }
        }

        private Color ColorWithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}