using UnityEngine;
using UnityEngine.UI;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class MemoryTile : MonoBehaviour
    {
        //This class handles the tile "dressing"
        //It handles the rotation of the spring
        //Holds the state data of the tile
        //It receives the click callback and sends it to the MemoryGameDemoController
        
        [SerializeField] private Image iconImage;
        [SerializeField] private Image ringImage;
        [SerializeField] private Image backImage;
        [SerializeField] private GameObject backfaceGameObject;
        [SerializeField] private TransformSpringComponent transformSpring;
        public int memoryGameId;
        
        private Transform visualsTransform;
        [HideInInspector] public MemoryGameDemoController memoryGameDemoController;
        [HideInInspector] public bool isFaceUp;

        private void Start()
        {
            visualsTransform = transformSpring.transform;
            ResetTiles();
            transformSpring.ReachEquilibrium();
        }

        public void ResetTiles()
        {
            isFaceUp = false;
            FaceDown();
        }

        private void Update()
        {
            SetBackfaceOrFrontFace();
        }

        private void SetBackfaceOrFrontFace()
        {
            Vector3 cardForward = visualsTransform.forward;
            Vector3 upVector = Vector3.forward;
            float dotProduct = Vector3.Dot(cardForward, upVector);
            bool isFrontFace = dotProduct >= 0;
            backfaceGameObject.SetActive(!isFrontFace);
        }
        
        public void SetIcon(Sprite icon, Color iconColor)
        {
            iconImage.sprite = icon;
            iconImage.color = iconColor;
            ringImage.color = iconColor;
            backImage.color = Color.Lerp(iconColor, Color.white, 0.85f);
        }
        
        public void FaceUp()
        {
            isFaceUp = true;
            transformSpring.SetTargetRotation(new Vector3(0f, 0f, 0f));
        }
        
        public void FaceDown()
        {
            isFaceUp = false;
            transformSpring.SetTargetRotation(new Vector3(0f, 180f, 0f));
        }
        
        public void OnClick()
        {
            if(isFaceUp)
            {
                memoryGameDemoController.CheckIfGameIsWon();
                return;
            }
            memoryGameDemoController.TileClicked(this);
        }
    }
}