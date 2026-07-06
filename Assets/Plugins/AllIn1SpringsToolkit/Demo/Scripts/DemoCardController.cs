using System;
using System.Collections;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class DemoCardController : DemoElement
    {
        //This demo is the most complex one, checking other demos first is recommended
        //There's a lot of logic in here that handles card dragging and position that isn't important for the toolkit
        //Once that logic is removed, the remaining logic is similar to the Robot Demo where we use a TransformSpringComponent to animate the position, scale and rotation
        
        //If you want to see how the cards use the springs please check the DemoCardLogic script instead and move away from this one
        
        [Space, Header("Main properties")]
        [SerializeField] private int numberOfCards;
        [SerializeField] private float timeBetweenCards;
        [SerializeField] private RectTransform slotParentRectTransform, cardsParentRectTransform, cardTargetsParentRectTransform, cardShadowsParentRectTransform;
        [SerializeField] private Transform spawnPointTransform;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TransformSpringComponent buttonHolderSpringComponent;
        [SerializeField] private CanvasGroup buttonHolderCanvasGroup;

        [Space, Header("Card Slots Position and Rotation")]
        [SerializeField] private float maxOffset;
        [SerializeField] private float maxRotation;
        
        private RectTransform[] cardSlotsRectTransforms;
        private DemoCardLogic[] cardsLogics;
        
        private bool someCardIsBeingDragged, allCardsDealt;
        [HideInInspector] public DemoCardLogic draggedCard;
        private Coroutine instantiateCardsCoroutine;
        public Action OnClose;

        //Instantiate and distribute the card slots. This starts the animation from the bottom spawn point and moves the cards to their slots
        public override void Initialize(bool hideUi)
        {
            base.Initialize(hideUi);
            
            DestroyChildren(cardsParentRectTransform);
            allCardsDealt = false;
            InstantiateAndDistributeCardSlots();
            if(instantiateCardsCoroutine != null) StopCoroutine(instantiateCardsCoroutine);
            instantiateCardsCoroutine = StartCoroutine(InstantiateCards());
            buttonHolderCanvasGroup.interactable = true;
            buttonHolderCanvasGroup.blocksRaycasts = true;
        }

        public override void Close()
        {
            base.Close();
            OnClose?.Invoke();
            if(instantiateCardsCoroutine != null) StopCoroutine(instantiateCardsCoroutine);
        }

        private void Update()
        {
            if(!allCardsDealt) return;
            SortCardsChildOrder();
        }

        //Since the demo is UI we need to sort the cards each frame to make sure the dragged card is always on top
        private void SortCardsChildOrder()
        {
            Array.Sort(cardsLogics, SortCardsVisibility);
            for (int i = 0; i < numberOfCards; i++)
            {
                cardsLogics[i].transform.SetSiblingIndex(i);
            }
        }

        //Comparison function that isn't relevant to the toolkit
        private int SortCardsVisibility(DemoCardLogic a, DemoCardLogic b)
        {
            bool aIsDragged = a.IsBeingDragged();
            bool bIsDragged = b.IsBeingDragged();

            // If one card is being dragged, prioritize it over the other card
            if (aIsDragged && !bIsDragged) return 1;
            else if (!aIsDragged && bIsDragged) return -1;
            // If neither card is being dragged, check if they are being pointed at
            else
            {
                bool aIsPointed = a.IsBeingPointed() && !someCardIsBeingDragged;
                bool bIsPointed = b.IsBeingPointed() && !someCardIsBeingDragged;
                
                // If one card is being pointed at and the other is not, prioritize the pointed card
                if (aIsPointed && !bIsPointed) return 1;
                else if (!aIsPointed && bIsPointed) return -1;
                // If both cards are being pointed at or neither is being pointed at, compare their positions
                else return a.myRectTransform.position.x.CompareTo(b.myRectTransform.position.x);
            }
        }

        private void InstantiateAndDistributeCardSlots()
        {
            DestroyChildren(slotParentRectTransform);

            cardSlotsRectTransforms = new RectTransform[numberOfCards];
            for (int i = 0; i < numberOfCards; i++)
            {
                GameObject childObject = new GameObject("CardSlot" + i, typeof(RectTransform));
                childObject.transform.SetParent(slotParentRectTransform, false);
                cardSlotsRectTransforms[i] = childObject.GetComponent<RectTransform>();
            }

            DistributeCardSlots();
        }
        
        private IEnumerator InstantiateCards()
        {
            //We hide the buttonHolder while we instantiate the cards
            buttonHolderSpringComponent.SetCurrentValueScale(Vector3.zero);
            buttonHolderSpringComponent.SetTargetScale(Vector3.zero);
            
            yield return new WaitForSeconds(0.2f);
            
            cardsLogics = new DemoCardLogic[numberOfCards];
            for (int i = 0; i < numberOfCards; i++)
            {
                //For each card we instantiate it, separate the target and shadow into a different parent,
                //and set the target position to the slot position
                GameObject cardObject = Instantiate(cardPrefab, spawnPointTransform.position, 
                   /* Quaternion.Euler(0f, -180f, 0f)*/Quaternion.identity, cardsParentRectTransform);
                cardObject.name = "Card" + i;
                DemoCardLogic cardLogic = cardObject.GetComponent<DemoCardLogic>();
                cardLogic.Initialize(this, i);
                cardsLogics[i] = cardLogic;
                Transform cardTargetTransform = cardLogic.targetRectTransform;
                RectTransform targetRectTransform = (RectTransform)cardTargetTransform;
                RectTransform cardObjectRectTransform = cardObject.GetComponent<RectTransform>();
                CopyRectTransform(cardObjectRectTransform, targetRectTransform);
                cardTargetTransform.SetParent(cardTargetsParentRectTransform, false);
                cardTargetTransform.localPosition = cardSlotsRectTransforms[i].localPosition;
                cardTargetTransform.localRotation = cardSlotsRectTransforms[i].localRotation;
                cardTargetTransform.gameObject.name += i.ToString();
                cardLogic.cardShadowRectTransform.SetParent(cardShadowsParentRectTransform, true);
                cardLogic.cardShadowRectTransform.gameObject.name += i.ToString();
                yield return new WaitForSeconds(timeBetweenCards);
            }
            
            allCardsDealt = true;
            
            //We show the buttonHolder after all cards have been instantiated
            buttonHolderSpringComponent.SetTargetScale(Vector3.one);
        }

        private void CopyRectTransform(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
        }

        private void DestroyChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private void DistributeCardSlots()
        {
            float parentWidth = slotParentRectTransform.rect.width;
            float childWidth = parentWidth / numberOfCards;

            for (int i = 0; i < numberOfCards; i++)
            {
                RectTransform childRect = cardSlotsRectTransforms[i];
                childRect.anchorMin = new Vector2(0f, 0.5f);
                childRect.anchorMax = new Vector2(0f, 0.5f);
                childRect.pivot = new Vector2(0.5f, 0.5f);

                float closeToCenterRatio = Mathf.Abs((numberOfCards - 1) / 2f - Mathf.Abs(i - (numberOfCards - 1) / 2f)) / ((numberOfCards - 1) / 2f);
                float offset = Mathf.Clamp(closeToCenterRatio, 0f, 0.9f) * maxOffset;
                childRect.anchoredPosition = new Vector2((i * childWidth) + childWidth / 2f, offset);
                
                float rotation = (i - (numberOfCards - 1) / 2f) / ((numberOfCards - 1) / 2f) * maxRotation;
                childRect.localRotation = Quaternion.Euler(0f, 0f, -rotation);

                childRect.sizeDelta = new Vector2(childWidth, slotParentRectTransform.rect.height);
            }
        }

        public void UpdateCardSlots(DemoCardLogic cardBeingMoved)
        {
            if(!allCardsDealt) return;
            Array.Sort(cardsLogics, SortCardsByX);
            for (int i = 0; i < cardsLogics.Length; i++)
            {
                DemoCardLogic cardLogic = cardsLogics[i];
                if(!cardLogic.Equals(cardBeingMoved))
                {
                    cardLogic.targetRectTransform.position = cardSlotsRectTransforms[i].position;
                    cardLogic.targetRectTransform.rotation = cardSlotsRectTransforms[i].rotation;
                }
            }
        }
        
        private int SortCardsByX(DemoCardLogic a, DemoCardLogic b)
        {
            return a.targetRectTransform.position.x.CompareTo(b.myRectTransform.position.x);
        }
        
        public void SomeCardIsBeingDragged(bool isBeingDragged, DemoCardLogic newSelectedCard)
        {
            someCardIsBeingDragged = isBeingDragged;
            draggedCard = newSelectedCard;
            bool hasNoSelectedCard = draggedCard == null;
            buttonHolderCanvasGroup.interactable = hasNoSelectedCard;
            buttonHolderCanvasGroup.blocksRaycasts = hasNoSelectedCard;
        }
        
        public bool IsAnyCardBeingDragged()
        {
            return someCardIsBeingDragged;
        }
        
        public DemoCardLogic GetSelectedCard()
        {
            return draggedCard;
        }
                
        public void ShuffleCards()
        {
            if(!allCardsDealt) return;
            Array.Sort(cardsLogics, SortRandom);
            
            // Store the initial positions of the cards
            Vector3[] initialPositions = new Vector3[cardsLogics.Length];
            for (int i = 0; i < cardsLogics.Length; i++)
            {
                initialPositions[i] = cardsLogics[i].targetRectTransform.position;
            }

            for (int i = 0; i < cardsLogics.Length; i++)
            {
                DemoCardLogic cardLogic = cardsLogics[i];
                if (draggedCard == null || !cardLogic.Equals(draggedCard))
                {
                    cardLogic.targetRectTransform.position = cardSlotsRectTransforms[i].position;
                    cardLogic.targetRectTransform.rotation = cardSlotsRectTransforms[i].rotation;

                    // Call ShuffleSpringPunch() only if the new position is different from the initial position
                    if (cardLogic.targetRectTransform.position != initialPositions[i])
                    {
                        cardLogic.ShuffleSpringPunch();
                    }
                }
            }
        }

        private int SortRandom(DemoCardLogic x, DemoCardLogic y)
        {
            return UnityEngine.Random.Range(-1, 2);
        }
        
        public void FlipRandomCard()
        {
            if(!allCardsDealt) return;
            int numberOfFlips = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < numberOfFlips; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, numberOfCards);
                cardsLogics[randomIndex].ToggleFlipState();
            }
        }
        
        public void FlipAllCards()
        {
            if(!allCardsDealt) return;
            for (int i = 0; i < cardsLogics.Length; i++)
            {
                cardsLogics[i].SetCardToFlipped();
            }
        }
        
        public void UnflipAllCards()
        {
            if(!allCardsDealt) return;
            for (int i = 0; i < cardsLogics.Length; i++)
            {
                cardsLogics[i].SetCardToUnflipped();
            }
        }

        public void CardWasHighlighted(DemoCardLogic demoCardLogic)
        {
            if(!allCardsDealt) return;
            for (int i = 0; i < cardsLogics.Length; i++)
            {
                if (!cardsLogics[i].Equals(demoCardLogic))
                {
                    cardsLogics[i].SetCardAtRestState();
                }
            }
        }
    }
}