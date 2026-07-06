using System;
using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class GridSquare : MonoBehaviour
    {
        //GridSquare uses a ShaderFloatSpringComponent to change the color of the grid square when we interact with it
        //It also handles the mouse input and the feedback when the mouse is hovered over the grid
        
        public int index;
        public CozyBuildingDemoController cozyBuildingDemoController;
        [SerializeField] private ShaderFloatSpringComponent shaderFloatSpring;
        [SerializeField] private TransformSpringComponent gridSquareTransformSpring;
        [SerializeField] private float verticalPunchOnRotate, flashPunch;
        
        private void OnMouseDown()
        {
            cozyBuildingDemoController.TryAddBlock(index);
        }

        private void OnMouseEnter()
        {
            shaderFloatSpring.SetTarget(1f);
            gridSquareTransformSpring.AddVelocityPosition(Vector3.up * verticalPunchOnRotate);
        }
        
        private void OnMouseExit()
        {
            shaderFloatSpring.SetTarget(0f);
        }

        public void Flash()
        {
            shaderFloatSpring.AddVelocity(flashPunch);
        }
    }
}