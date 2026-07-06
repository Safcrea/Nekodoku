using UnityEngine;
using Random = UnityEngine.Random;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class ShockwaveDemoController : DemoElement
    {
        //In this demo we have a grid of buttons, each of them having a TransformSpringComponent and a ColorSpringComponent
        //When a button is clicked, it will send a shockwave to the other buttons, making them move and change color
        //The buttons will also change their icon and color to a random one. It has no real purpose, it's just for fun
        //The shockwave is calculated based on the distance between the buttons
        [Space, Header("Main properties")]
        [SerializeField] private Transform gridTransform;
        [SerializeField] private TransformSpringComponent[] buttonSpringComponents;
        [SerializeField] private ColorSpringComponent[] buttonColorSprings;
        [SerializeField] private ShockwaveDemoButton[] shockwaveDemoButtons;

        [Space, Header("Buttons Icons And Colors")]
        [SerializeField] private Sprite[] buttonIcons;
        [SerializeField] private Color[] buttonColors;
        
        [Space, Header("Spring Nudges")]
        [SerializeField] private float scalePunchMax;
        [SerializeField] private float positionPunchMax;
        [SerializeField] private float maxShockwaveDistance;
        [SerializeField] private float colorMaxPunch;

        private void Start()
        {
            //We set the icons and colors of the buttons
            RandomizeAllIcons();
        }

        private void RandomizeAllIcons()
        {
            for(int i = 0; i < shockwaveDemoButtons.Length; i++)
            {
                int randomIconIndex = Random.Range(0, buttonIcons.Length);
                shockwaveDemoButtons[i].SetAndColorIcon(randomIconIndex, buttonIcons[randomIconIndex], buttonColors[randomIconIndex]);
                buttonColorSprings[i].SetTarget(Color.Lerp(buttonColors[randomIconIndex], Color.white, 0.85f));
            }
        }

        public void RandomButtonPress()
        {
            int randomIndex = Random.Range(0, buttonSpringComponents.Length);
            ButtonClicked(randomIndex);
        }

        public void ButtonClicked(int index)
        {
            if(!isOpen)
            {
                return;
            }

            //If all are the same we reset the demo
            if(CheckIfAllIconsAreTheSame())
            {
                ResetDemo();
                return;
            }
            
            //First of all, we punch the scale of the button that was clicked
            buttonSpringComponents[index].AddVelocityScale(Vector3.one * scalePunchMax);
            Vector3 targetButtonPosition = buttonSpringComponents[index].transform.localPosition;
            
            //The buttons affected by the shockwave will change their icon and color to a random one
            int currentIconAndColorIndex = shockwaveDemoButtons[index].GetIconAndColorIndex();
            int newRandomColorAndIconIndex = Random.Range(0, buttonIcons.Length);
            if(newRandomColorAndIconIndex == currentIconAndColorIndex)
            {
                newRandomColorAndIconIndex = (newRandomColorAndIconIndex + 1) % buttonIcons.Length;
            }
            
            //We then calculate the shockwave for the other buttons
            for (int i = 0; i < buttonSpringComponents.Length; i++)
            {
                if(i == index)
                {
                    buttonColorSprings[i].AddVelocity(Color.white * colorMaxPunch);
                    buttonColorSprings[i].SetTarget(Color.Lerp(buttonColors[newRandomColorAndIconIndex], Color.white, 0.75f));
                    shockwaveDemoButtons[i].SetAndColorIcon(newRandomColorAndIconIndex, buttonIcons[newRandomColorAndIconIndex], buttonColors[newRandomColorAndIconIndex]);
                    shockwaveDemoButtons[i].ClickButtonFeedback();
                    continue;
                }
                
                //We calculate the distance between the current button and the button that was clicked (the target button)
                Vector3 otherButtonPosition = buttonSpringComponents[i].transform.localPosition;
                Vector3 direction = otherButtonPosition - targetButtonPosition;
                float distance = direction.magnitude;
                if (distance < maxShockwaveDistance)
                {
                    //Finally, we punch the position and scale based on the distance
                    //And set the color so that it can then animate back to white
                    float shockwavePower = 1 - distance / maxShockwaveDistance;
                    shockwavePower = Mathf.Max(0.2f, shockwavePower); //We don't want the shockwave to be too weak
                    buttonColorSprings[i].AddVelocity(Color.white * colorMaxPunch* shockwavePower);
                    buttonColorSprings[i].SetTarget(Color.Lerp(buttonColors[newRandomColorAndIconIndex], Color.white, 0.75f));
                    buttonSpringComponents[i].AddVelocityPosition(direction.normalized * positionPunchMax * shockwavePower);
                    buttonSpringComponents[i].AddVelocityScale(Vector3.one * scalePunchMax * shockwavePower);
                    shockwaveDemoButtons[i].SetAndColorIcon(newRandomColorAndIconIndex, buttonIcons[newRandomColorAndIconIndex], buttonColors[newRandomColorAndIconIndex]);
                    shockwaveDemoButtons[i].ClickButtonFeedback();
                }
            }
        }
        
        private bool CheckIfAllIconsAreTheSame()
        {
            int firstIconIndex = shockwaveDemoButtons[0].GetIconAndColorIndex();
            for (int i = 1; i < shockwaveDemoButtons.Length; i++)
            {
                if(shockwaveDemoButtons[i].GetIconAndColorIndex() != firstIconIndex)
                {
                    return false;
                }
            }

            return true;
        }

        [ContextMenu("ResetDemo")]
        private void ResetDemo()
        {
            RandomizeAllIcons();
            
            //Add some punch to the buttons when they reset
            for(int index = 0; index < buttonSpringComponents.Length; index++)
            {
                //Scale punch
                TransformSpringComponent springComponent = buttonSpringComponents[index];
                springComponent.AddVelocityScale(Vector3.one * (scalePunchMax * 0.75f));
                shockwaveDemoButtons[index].ClickButtonFeedback();
                
                //Position punch outwards from the center
                Vector3 gridPosition = gridTransform.localPosition;
                Vector3 direction = buttonSpringComponents[index].transform.localPosition - gridPosition;
                buttonSpringComponents[index].AddVelocityPosition(direction.normalized * 4000f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Set Grid Texts And Fetch Components")]
        public void SetGridTextsAndFetchComponents()
        {
            buttonSpringComponents = new TransformSpringComponent[gridTransform.childCount];
            buttonColorSprings = new ColorSpringComponent[gridTransform.childCount];
            shockwaveDemoButtons = new ShockwaveDemoButton[gridTransform.childCount];
            
            for (int i = 0; i < gridTransform.childCount; i++)
            {
                Transform child = gridTransform.GetChild(i);
                                
                buttonSpringComponents[i] = child.GetComponent<TransformSpringComponent>();
                buttonColorSprings[i] = child.GetComponent<ColorSpringComponent>();
                shockwaveDemoButtons[i] = child.GetComponent<ShockwaveDemoButton>();
                shockwaveDemoButtons[i].Setup(i, this);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
#endif
    }
}