using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using TMPro;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DemoCardLogic : MonoBehaviour
    {
        //This class is still very complex since it's handling a lot of gameplay logic on top of the spring animations
        
        //Much of these variables are used for gameplay logic and are not important for the spring animations
        public RectTransform targetRectTransform;
        public RectTransform cardShadowRectTransform;
        public RectTransform myRectTransform;
        [HideInInspector] public bool isHighlighted;
        [SerializeField] private RectTransform reactorRotationParentRectTransform, tiltRotationParentRectTransform;
        [SerializeField] private float autoRotationTiltSpeed, autoRotationTiltAmount;
        [SerializeField] private float reactorRotationXAmount, reactorRotationYAmount;
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite[] possibleSprites;
        [SerializeField] private float shadowLateralPositionInfluence, maxXShadowOffset;
        [SerializeField] private SimpleDraggable simpleDraggable;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color[] cardColors;
        [SerializeField] private Image[] cardImages;
        [SerializeField] private TextMeshProUGUI[] cardTexts;
        [SerializeField] private GameObject backFaceIconGameObject, selectedIndicatorGameObject;
        [SerializeField] private float colorHighlightFactor;
        
        //We have several springs that will animate the card. We have a TransformSpringComponent for the position and rotation of the card
        //We also have a color spring that will animate the background color of the card
        [Header("Springs")]
        [SerializeField] private TransformSpringComponent transformSpringComponent;
        [SerializeField] private TransformSpringComponent childSpringComponent;
        [SerializeField] private float maxPunchVerticalVelocity;
        [SerializeField] private float posSpringDragWhenDragging, posSpringForceWhenDragging;
        [SerializeField] private float posSpringDragWhenNotDragging, posSpringForceWhenNotDragging, posSpringForceWhenClosing;
        [SerializeField] private float scaleSpringTargetWhenPointed;
        [SerializeField] private float scaleSpringPunchWhenPointed;
        [SerializeField] private float positionSpringPunchWhenPointed;
        [SerializeField] private ColorSpringComponent backgroundColorSpringComponent;
        private Color highlightedCardColor, normalCardColor;
        
        private float initialShadowYOffset, demoCardControllerXPosition;
        private DemoCardController demoCardController;
        private Transform rotationSpringTransform;
        private int cardIndex;
        private Color currentCardColor;

		private bool isFlipped;
        
        public void Initialize(DemoCardController newDemoCardController, int newCardIndex)
        {
            cardIndex = newCardIndex;
            isHighlighted = false;
            demoCardController = newDemoCardController;
            demoCardController.OnClose += SetCardAtClosingState;
            demoCardControllerXPosition = demoCardController.transform.position.x;
            initialShadowYOffset = myRectTransform.position.y - cardShadowRectTransform.position.y;
            iconImage.sprite = possibleSprites[UnityEngine.Random.Range(0, possibleSprites.Length)];

            rotationSpringTransform = childSpringComponent.transform;
            normalCardColor = backgroundImage.color;

			childSpringComponent.SetCurrentValueRotation(new Vector3(0f, 180f, 0f));
			childSpringComponent.Update();

			CardDressWithRandomColors();
			SetCardAtRestState();

			SetBackfaceOrFrontFace();
		}

        private void Update()
        {
            UpdateShadowTransform();
            SetBackfaceOrFrontFace();
            RotationReactsToPositionMovement();
            AutoTiltRotation();
            backgroundImage.color = backgroundColorSpringComponent.GetCurrentValue();

            if(IsBeingDragged() && !isHighlighted)
            {
                SetCardAtHighlightState();
            }
        }

        private void UpdateShadowTransform()
        {
            Vector3 targetPosition = myRectTransform.position;
            Vector3 shadowPosition = cardShadowRectTransform.position;
            float shadowXOffset = shadowLateralPositionInfluence * (targetPosition.x - demoCardControllerXPosition);
            shadowXOffset = Mathf.Clamp(shadowXOffset, -maxXShadowOffset, maxXShadowOffset);
            shadowPosition.x = targetPosition.x + shadowXOffset;
            shadowPosition.y = targetPosition.y - initialShadowYOffset;
            cardShadowRectTransform.position = shadowPosition;
            cardShadowRectTransform.rotation = reactorRotationParentRectTransform.rotation;
            cardShadowRectTransform.localScale = rotationSpringTransform.localScale;
        }
        
        //This method will check if the card is facing up or down (based on rotation) and will show the back face icon accordingly
        private void SetBackfaceOrFrontFace()
        {
            Vector3 cardForward = rotationSpringTransform.forward;
            Vector3 upVector = Vector3.forward;
            float dotProduct = Vector3.Dot(cardForward, upVector);
            bool isFrontFace = dotProduct >= 0;
            backFaceIconGameObject.SetActive(!isFrontFace);

			isFlipped = !isFrontFace;
		}
        
        //Good example of how we can use a spring velocity to animate another object
        //In this case we're using the position spring velocity to rotate the card
        private void RotationReactsToPositionMovement()
        {
            Vector3 movementVelocity = transformSpringComponent.GetVelocityPosition() / 100f;

			float sign = isFlipped ? -1f : 1f;
			reactorRotationParentRectTransform.localEulerAngles =
				new Vector3(movementVelocity.y * reactorRotationYAmount, 0, movementVelocity.x * reactorRotationXAmount) * sign;
		}
        
        //Subtle rotation animation that will make the card tilt (no springs here)
        private void AutoTiltRotation()
        {
			float sine = Mathf.Sin((Time.time + (cardIndex * 0.33f)) * autoRotationTiltSpeed) * autoRotationTiltAmount;
            float cosine = Mathf.Cos((Time.time + (cardIndex * 0.33f)) * autoRotationTiltSpeed) * autoRotationTiltAmount;
            Vector3 newRotation = new Vector3(sine, cosine, 0f);
            tiltRotationParentRectTransform.localEulerAngles = newRotation;
        }
        
        public bool IsBeingDragged()
        {
            return simpleDraggable.isDragging;
        }
        
        public bool IsBeingPointed()
        {
			return simpleDraggable.isPointed;
        }
        
        public void PointerEnter()
        {
			SetCardAtHighlightState();
			demoCardController.CardWasHighlighted(this);
		}
        
        public void PointerExit()
        {
            if(!simpleDraggable.isDragging)
            {
                SetCardAtRestState();
            }
        }
        
        public void DragStarted()
        {
            demoCardController.SomeCardIsBeingDragged(true, this);
			targetRectTransform.rotation = Quaternion.identity;
        }

        public void DragMoved()
        {
            demoCardController.UpdateCardSlots(this);
        }
        
        public void DragEnded()
        {
            demoCardController.UpdateCardSlots(null);
            demoCardController.SomeCardIsBeingDragged(false, null);
            
        }

        //When the cards are dealt we punch them up a bit
        public void ShuffleSpringPunch()
        {
            transformSpringComponent.AddVelocityPosition(Vector3.up * Random.Range(-maxPunchVerticalVelocity, maxPunchVerticalVelocity));
        }

        //Use springs to animate the card flipping
        public void ToggleFlipState()
        {

            Vector3 currentSpringEuler = childSpringComponent.GetCurrentValueRotation().eulerAngles;
            Vector3 targetEuler = currentSpringEuler;
            targetEuler.y = targetEuler.y >= 180f ? 0f : 180f;

			childSpringComponent.SetTargetRotation(targetEuler);
        }
        
        //Set the card to the unflipped state with a spring
        public void SetCardToUnflipped()
        {
            childSpringComponent.SetTargetRotation(Vector3.zero);
        }
        
        //Set the card to the flipped state with a spring
        public void SetCardToFlipped()
        {
			childSpringComponent.SetTargetRotation(Vector3.up * 180f);
        }
        
        //When highlighted cards will have different force and drag values
        //The scale and background color will also change (driven by springs)
        private void SetCardAtHighlightState()
        {
            if(demoCardController.draggedCard != null && !demoCardController.draggedCard.Equals(this))
            {
                return;
            }
            transformSpringComponent.SetUnifiedDragPosition(posSpringDragWhenDragging);
            transformSpringComponent.SetUnifiedForcePosition(posSpringForceWhenDragging);
            transformSpringComponent.AddVelocityPosition(Vector3.up * positionSpringPunchWhenPointed);
            childSpringComponent.SetTargetScale(Vector3.one * scaleSpringTargetWhenPointed);
            childSpringComponent.AddVelocityScale(Vector3.one * scaleSpringPunchWhenPointed);
            backgroundColorSpringComponent.SetTarget(highlightedCardColor);
            selectedIndicatorGameObject.SetActive(true);
            isHighlighted = true;
        }
        
        //When at rest, cards will have different force and drag values
        //The scale and background color will also change (driven by springs)
        public void SetCardAtRestState()
        {
            if(demoCardController.IsAnyCardBeingDragged() && demoCardController.GetSelectedCard().Equals(this))
            {
                return;
            }

            transformSpringComponent.SetUnifiedDragPosition(posSpringDragWhenNotDragging);
            transformSpringComponent.SetUnifiedForcePosition(posSpringForceWhenNotDragging);
            childSpringComponent.SetTargetScale(Vector3.one);
            backgroundColorSpringComponent.SetTarget(normalCardColor);
            selectedIndicatorGameObject.SetActive(false);
            isHighlighted = false;
        }
        
        public void SetCardAtClosingState()
        {
            transformSpringComponent.SetUnifiedForcePosition(posSpringForceWhenClosing);
        }
        
        private void CardDressWithRandomColors()
        {
            currentCardColor = cardColors[cardIndex % cardColors.Length];
            highlightedCardColor = Color.Lerp(normalCardColor, currentCardColor, colorHighlightFactor);
            
            foreach (Image cardImage in cardImages)
            {
                cardImage.color = currentCardColor;
            }
            
            foreach (TextMeshProUGUI cardText in cardTexts)
            {
                cardText.color = currentCardColor;
            }
        }

        private void OnDestroy()
        {
            demoCardController.OnClose -= SetCardAtClosingState;
        }
    }
}