using TMPro;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ScoreCounterDemo : DemoElement
    {
        //When the score changes we'll animate the score text with springs
        //TransformSpringComponent will animate the scale and rotation of the score text
        //AnchoredPositionSpringComponent will animate the position of the score text
        //ColorSpringComponent will animate the color of the score text
        //FloatSpringComponent will animate the score value that we show in the text
        
        //So when the score changes by calling some method of each spring everything will automatically animate in a stateless robust way
        [Space, Header("Score Springs")]
        [SerializeField] private TransformSpringComponent transformSpring;
        [SerializeField] private AnchoredPositionSpringComponent anchoredPositionSpring;
        [SerializeField] private ColorSpringComponent scoreColorSpring;
        [SerializeField] private FloatSpringComponent floatScoreTextSpring;

        [Space, Header("Configuration")]
        [SerializeField] private int minPointsChange;
        [SerializeField] private int maxPointsChange;
        [SerializeField] private int startScore;
        [SerializeField] private float minScoreRatio;
        [SerializeField] private Color addColor;
        [SerializeField] private Color subtractColor;
        [SerializeField] private TextMeshProUGUI scoreText;
        
        [Space, Header("Nudges")]
        [SerializeField] private Vector3 subtractScalePunchVector;
        [SerializeField] private Vector3 addScalePunchVector;
        [SerializeField] private float scalePunchMagnitude;
        [SerializeField] private float rotationPunchOnAdd;
        [SerializeField] private float anchorPunchMagnitude;

        //A "Floaty" is the floating text that appears when the score changes
        //We have two floaties, one for addition and one for substraction
        //Their logic is handled in the ScoreCounterFloaty script
        [Space, Header("Score Floating Text")]
        [SerializeField] private ScoreCounterFloaty scoreCounterFloatyAdd;
        [SerializeField] private ScoreCounterFloaty scoreCounterFloatySubtract;
        
        private int currentScore;

        private void Start()
        {
            //Set initial state with the score set to the start score
            currentScore = startScore;
            floatScoreTextSpring.SetTarget(currentScore);
            floatScoreTextSpring.ReachEquilibrium();
            scoreText.text = Mathf.RoundToInt(currentScore).ToString();
            
            //Initialize the floaties with their colors
            scoreCounterFloatyAdd.Initialize(addColor);
            scoreCounterFloatySubtract.Initialize(subtractColor);
        }

        private void ChangeScoreRandomly(bool isAddition)
        {
            int randomPoints = UnityEngine.Random.Range(minPointsChange, maxPointsChange);
            if (!isAddition)
            {
                randomPoints *= -1;
            }
            FloatyFeedback(randomPoints);
            currentScore += randomPoints;
            currentScore = Mathf.Max(0, currentScore);
            
            //This is the core of the effect, here we set the springs to animate the score change
            
            //Set target score that we'll use to animate the score in the text
            floatScoreTextSpring.SetTarget(currentScore);
            //Set Color, then the spring will automatically animate to white (it's the Target)
            scoreColorSpring.SetCurrentValue(isAddition ? addColor : subtractColor);
            
            //Get ratio, the bigger the change the bigger the punch
            float scoreChangeRatio = (float) randomPoints / maxPointsChange;
            scoreChangeRatio = Mathf.Max(minScoreRatio, scoreChangeRatio);
            
            //Scale punch, we have different vectors for addition and subtraction
            Vector3 punchVector = isAddition ? addScalePunchVector * scalePunchMagnitude : subtractScalePunchVector * scalePunchMagnitude;
            transformSpring.AddVelocityScale(punchVector * scoreChangeRatio);

            if(isAddition)
            {
                //On addition we just slightly push the score text up
                anchoredPositionSpring.AddVelocity(Vector2.up * anchorPunchMagnitude);
            }
            else
            {
                //On subtraction we push the score text down and shake the rotation randomly
                float randomSign = UnityEngine.Random.value > 0.5f ? -1 : 1;
                transformSpring.AddVelocityRotation(randomSign * Vector3.forward * rotationPunchOnAdd);
                
                anchoredPositionSpring.AddVelocity(Vector2.down * anchorPunchMagnitude);
            }
        }

        private void Update()
        {
            if(isOpen)
            {
                //We make sure to reflect the floatScoreTextSpring value that animates the score in the text
                scoreText.text = Mathf.RoundToInt(floatScoreTextSpring.GetCurrentValue()).ToString();
            }
        }

        public void AddScore()
        {
            ChangeScoreRandomly(true);
        }

        public void SubtractScore()
        {
            ChangeScoreRandomly(false);
        }
        
        //We let the floaties know that the score has changed so that they can handle their own animation
        private void FloatyFeedback(int score)
        {
            bool isAddition = score > 0;
            ScoreCounterFloaty scoreCounterFloaty = isAddition ? scoreCounterFloatyAdd : scoreCounterFloatySubtract;
            scoreCounterFloaty.ChangeScore(Mathf.Abs(score), isAddition);
        }

        //Editor helper (ignore)
        [ContextMenu("Normalize Vectors")]
        private void NormalizeVectors()
        {
            subtractScalePunchVector = subtractScalePunchVector.normalized;
            addScalePunchVector = addScalePunchVector.normalized;
        }
    }
}