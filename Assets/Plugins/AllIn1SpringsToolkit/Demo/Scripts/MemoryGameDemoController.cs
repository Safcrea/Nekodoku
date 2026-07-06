using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class MemoryGameDemoController : DemoElement
    {
        //This demo is a simple memory game. We have a grid of memory tiles, each of them having a different icon and color
        //When a tile is clicked, it will flip and show its icon. If two tiles are clicked and they have the same icon, they will stay open
        //If they don't have the same icon, they will close again
        //The game is won when all the tiles are open
        
        //Spring wise, we have a TransformSpringComponent for the grid, and a TransformSpringComponent for each tile
        //The grid will scale up when the game is won
        //The tiles will rotate when they are clicked
        
        //The class MemoryTile will handle the tile behavior
        
        [Space, Header("Memory Tiles")]
        [SerializeField] private MemoryTile[] memoryTiles;
        [SerializeField] private Sprite[] icons;
        [SerializeField] private Color[] colors;

        [Space, Header("Other")]
        [SerializeField] private TransformSpringComponent gridTransformSpring;
        [SerializeField] private float winScaleNudge;
        
        private MemoryTile firstTileClicked;
        private MemoryTile secondTileClicked;
        private int solvedPairs;
        private bool isRandomizingTiles;

        private void Start()
        {
            isRandomizingTiles = true;
            RandomizeTiles();
        }

        private void RandomizeTiles()
        {
            //Shuffle memory tiles and then assign the memoryGameId to each tile and their corresponding icon and color
            ShuffleMemoryTiles();

            for(int i = 0; i < memoryTiles.Length; i++)
            {
                memoryTiles[i].memoryGameId = Mathf.FloorToInt(i / 2f);
                memoryTiles[i].SetIcon(icons[memoryTiles[i].memoryGameId], colors[memoryTiles[i].memoryGameId]);
                memoryTiles[i].memoryGameDemoController = this;
            }
            
            isRandomizingTiles = false;
        }

        public void TileClicked(MemoryTile tileClicked)
        {
            if(isRandomizingTiles)
            {
                return;
            }
            
            if(firstTileClicked != null && secondTileClicked != null)
            {
                //Check if the two tiles are the same
                if(firstTileClicked.memoryGameId != secondTileClicked.memoryGameId)
                {
                    //If they are not the same, we close them
                    firstTileClicked.FaceDown();
                    secondTileClicked.FaceDown();
                    firstTileClicked = null;
                    secondTileClicked = null;
                }
            }
            
            //If we have no tiles clicked, we set the clicked tile as the first one
            if(firstTileClicked == null)
            {
                firstTileClicked = tileClicked;
                firstTileClicked.FaceUp();
            }
            //If we have the first tile clicked, we set the clicked tile as the second one
            else if(secondTileClicked == null)
            {
                secondTileClicked = tileClicked;
                secondTileClicked.FaceUp();
                
                if(firstTileClicked.memoryGameId == secondTileClicked.memoryGameId)
                {
                    //If they are the same, we keep them open and discard the clicked tiles
                    firstTileClicked = null;
                    secondTileClicked = null;
                    solvedPairs++;
                }
            }
        }
        
        public void CheckIfGameIsWon()
        {
            if(solvedPairs == (memoryTiles.Length / 2))
            {
                solvedPairs = 0;
                gridTransformSpring.AddVelocityScale(Vector3.one * winScaleNudge);
                for(int i = 0; i < memoryTiles.Length; i++)
                {
                    memoryTiles[i].ResetTiles();
                }

                isRandomizingTiles = true;
                //Not the best practice, but keeping it simple
                Invoke(nameof(RandomizeTiles), 0.5f);
            }
        }

        private void ShuffleMemoryTiles()
        {
            System.Random random = new System.Random();
            int n = memoryTiles.Length;
    
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (memoryTiles[i], memoryTiles[j]) = (memoryTiles[j], memoryTiles[i]);
            }
        }
    }
}