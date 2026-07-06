using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class CozyBuildingDemoController : DemoElement
    {
        //This demo will handle the placing and removing of blocks on a grid, simulating some sort of cozy building game
        //Each time a block is placed, the grid will rotate 90 degrees
        //If the whole grid is filled, the grid will reset
        //All the interactions have some punches and spring motion to make the experience more dynamic (end of TryAddBlock method)
        //We mainly use the TransformSpringComponent for that
        //Although inside GridSquare we use the ShaderFloatSpringComponent to change the color of the grid square when we interact with it
        //GridSquare also handles the mouse input and the feedback when the mouse is hovered over the grid
        
        [Space, Header("Grid")]
        [SerializeField] private TransformSpringComponent gridTransformSpring;
        [SerializeField] private TransformSpringComponent[] gridSquareTransformSprings;
        [SerializeField] private GridSquare[] gridSquares;
        [SerializeField] private float verticalPunchOnRotate, shrinkPunch, resetPositionPunch, placeBlockPositionPunch;
        
        [Space, Header("Block placement")]
        [SerializeField] private Transform blocksParent;
        [SerializeField] private TransformSpringComponent[] blocksTransformSprings;
        [SerializeField] private Color[] gridColors;
        [SerializeField] private float floorOffset, blockOffset;
        
        private const int MAX_BLOCKS_PER_SQUARE = 4;
        private int nextBlockIndex;
        private Quaternion initialGridRotation;
        private Quaternion currentGridTargetRotation;
        private Vector3 blockInitialScale, gridInitialScale;
        private int[] blocksPerSquare;
        
        private void Start()
        {
            initialGridRotation = gridTransformSpring.transform.rotation;
            currentGridTargetRotation = initialGridRotation;
            blockInitialScale = blocksTransformSprings[0].transform.localScale;
            gridInitialScale = gridSquareTransformSprings[0].transform.localScale;

			ResetGridFunctionality();
		}

        //We'll try to add a block to the grid square at the given index
        public void TryAddBlock(int index)
        {
            if(!IsOpen())
            {
                return;
            }
            
            //If the index is out of bounds or the square is full, we'll check if all the squares are full and reset the grid
            if(index < 0 || index >= blocksTransformSprings.Length || blocksPerSquare[index] >= MAX_BLOCKS_PER_SQUARE)
            {
                bool allSquaresFull = false;
                for(int i = 0; i < blocksPerSquare.Length; i++)
                {
                    if(blocksPerSquare[i] < MAX_BLOCKS_PER_SQUARE)
                    {
                        allSquaresFull = false;
                        break;
                    }
                    allSquaresFull = true;
                }

                if(allSquaresFull)
                {
                   ResetGrid(); 
                }
                else
                {
                    //If it's just the square that is full, we'll scale it down and up to give some feedback
                    gridSquareTransformSprings[index].AddVelocityScale(-Vector3.one * shrinkPunch * 0.4f);
                }
                
                return;
            }

            //If the square is not full, we'll add a block to it
            
            //We start by flashing the grid square to give some feedback, to let the user know his click input was received
            gridSquares[index].Flash();
            
            //Since all blocks are pooled, we'll get the next available block and place it on the grid square
            nextBlockIndex++;
            TransformSpringComponent blockSpring = blocksTransformSprings[nextBlockIndex];
            TransformSpringComponent gridSquareSpring = gridSquareTransformSprings[index];
            Transform gridSquareTransform = gridSquareSpring.transform;

            blockSpring.gameObject.SetActive(true);
            blockSpring.transform.SetParent(gridSquareSpring.transform);

            blockSpring.transform.position = gridSquareTransform.position
                                                 + (gridSquareTransform.up * floorOffset) 
                                                 + (gridSquareTransform.up * (blockOffset * blocksPerSquare[index]));

            Color blockColor = gridColors[index];
            blockSpring.GetComponent<Renderer>().material.color = blockColor;

            blocksPerSquare[index]++;

            //Spring motion polish
            gridSquareSpring.AddVelocityPosition(Vector3.up * placeBlockPositionPunch);
            blockSpring.ReachEquilibrium(); //Reach equilibrium to make sure we have a clean spring state (since object is pooled)
            blockSpring.SetCurrentValueRotation(new Vector3(0f, 360f, 0f));
            blockSpring.SetCurrentValueScale(Vector3.zero);
            blockSpring.SetTargetScale(new Vector3(
                blockInitialScale.x / gridInitialScale.x,
                blockInitialScale.y / gridInitialScale.y,
                blockInitialScale.z / gridInitialScale.z
            ));
        }
        
        //We'll try to add a block to a random grid square. If all squares are full, we'll reset the grid
        public void TryAddRandom()
        {
            int randomIndex = Random.Range(0, gridSquareTransformSprings.Length);
            
            int tries = 0;
            while(blocksPerSquare[randomIndex] >= MAX_BLOCKS_PER_SQUARE && tries < gridSquareTransformSprings.Length)
            {
                tries++;
                randomIndex = (randomIndex + 1) % gridSquareTransformSprings.Length;
            }

            if(tries < gridSquareTransformSprings.Length)
            {
                TryAddBlock(randomIndex);
            }
            else
            {
                ResetGrid();
            }
        }
        
        public void RotateGrid()
        {
            currentGridTargetRotation *= Quaternion.Euler(0f, 90f, 0f);
            gridTransformSpring.SetTargetRotation(currentGridTargetRotation);
            gridTransformSpring.AddVelocityPosition(Vector3.up * verticalPunchOnRotate);
        }

        public void ResetGrid()
        {
            gridTransformSpring.AddVelocityPosition(Vector3.down * (verticalPunchOnRotate * 1.5f));
            gridTransformSpring.AddVelocityScale(-Vector3.one * shrinkPunch);
            foreach(GridSquare gridSquare in gridSquares)
            {
                gridSquare.Flash();
            }
            foreach(TransformSpringComponent gridSquareTransformSpring in gridSquareTransformSprings)
            {
                Vector3 punchDirection = Vector3.up * Random.Range(-resetPositionPunch, resetPositionPunch);
                gridSquareTransformSpring.AddVelocityPosition(punchDirection);
                gridSquareTransformSpring.SetCurrentValueScale(Vector3.zero);
            }
            
            ResetGridFunctionality();
        }

        private void ResetGridFunctionality()
        {
            nextBlockIndex = 0;
            blocksPerSquare = new int[gridSquareTransformSprings.Length];
            
            for(int i = 0; i < blocksTransformSprings.Length; i++)
            {
                TransformSpringComponent blockSpring = blocksTransformSprings[i];
                blockSpring.gameObject.SetActive(false);
                blockSpring.transform.SetParent(blocksParent);
                blocksTransformSprings[i].gameObject.SetActive(false);
            }
        }
        
#if UNITY_EDITOR
        [ContextMenu("Get References")]
        private void GetReferences()
        {
            blocksTransformSprings = blocksParent.GetComponentsInChildren<TransformSpringComponent>();
            gridSquareTransformSprings = gridTransformSpring.GetComponentsInChildren<TransformSpringComponent>()
                .Where(component => component.transform != gridTransformSpring.transform)
                .Where(component => component.gameObject.name.Contains("Grid"))
                .ToArray();
            
            gridSquares = gridTransformSpring.GetComponentsInChildren<GridSquare>();
            for(int i = 0; i < gridSquares.Length; i++)
            {
                gridSquares[i].index = i;
                gridSquares[i].cozyBuildingDemoController = this;
                EditorUtility.SetDirty(gridSquares[i]);
            }
            
            EditorUtility.SetDirty(this);
        }
#endif
    }
}