using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class AllIn1SpringToolkitDemoController : MonoBehaviour
    {
        [SerializeField] private EventSystem oldInputEventSystem;
        [SerializeField] private EventSystem newInputEventSystem;
        
        [Space, Header("Asset Title Intro")]
        [SerializeField] private bool skipAssetHeaderIntro;
        [SerializeField] private GameObject assetHeaderGameObject, pressToPlayGameObject;
        [SerializeField] private TransformSpringComponent titleTransformSpring, splotchTransformSpring, pressToPlayTransformSpring;
        [SerializeField] private float initialWait, timeToWaitBeforeShowingPressToPlay, timeToWaitBeforeShowingDemo;

        [Space, Header("Demo Elements and Header")]
        [SerializeField] private DemoElementSo[] demoElements;
        [SerializeField] private Transform uiDemoParent, worldDemoParent;
        [SerializeField] private int indexToTest = 0;
        [SerializeField] private GameObject headerGameObject;
        [SerializeField] private RectTransform headerRectTransform;
        [SerializeField] private AnchoredPositionSpringComponent headerAnchoredPosSpring;
        [SerializeField] private TransformSpringComponent[] headerLabelTransformSprings;
        [SerializeField] private float headerScalePunch;
        [SerializeField] private TextMeshProUGUI headerText;

        [Space, Header("Info Panel and Others")]
        [SerializeField] private AnchoredPositionSpringComponent infoPanelAnchoredPositionSpring;
        [SerializeField] private TransformSpringComponent infoPanelTransformSpring;
        [SerializeField] private RectTransform infoPanelRectTransform;
        [SerializeField] private float infoPanelScalePunch;
        [SerializeField] private TextMeshProUGUI infoPanelText;
        [SerializeField] private RectTransform demoElementsParent;
        
        [Space, Header("Hide Ui Settings")]
        [SerializeField] private bool hideUi;
        [SerializeField] private CanvasGroup headerCanvasGroup;
        
        private Camera mainCamera;
        private bool newIndexToTestIsBigger = true;
        private bool isFirstDemoElement = true;
        private Vector2 initialInfoPanelAnchoredPosition;
        private bool infoPanelOn = false;
        
        private IEnumerator Start()
        {
            DisableEverything();
            EnableCorrectInputSystem();
            mainCamera = Camera.main;
            initialInfoPanelAnchoredPosition = infoPanelRectTransform.anchoredPosition;
            
            DestroyChildren(uiDemoParent, withAnimation: !isFirstDemoElement);
            DestroyChildren(worldDemoParent, withAnimation: !isFirstDemoElement);
            
            if(!skipAssetHeaderIntro)
            {
                yield return new WaitForSeconds(initialWait);
                
                assetHeaderGameObject.SetActive(true);
                titleTransformSpring.gameObject.SetActive(true);
                titleTransformSpring.SetCurrentValueScale(Vector3.zero);
                titleTransformSpring.SetCurrentValueRotation(new Vector3(0f, 0f, -150f));
                yield return new WaitForSeconds(0.3f);
                splotchTransformSpring.gameObject.SetActive(true);
                splotchTransformSpring.SetCurrentValueScale(Vector3.zero);
                yield return new WaitForSeconds(timeToWaitBeforeShowingPressToPlay);
                pressToPlayGameObject.SetActive(true);
                pressToPlayTransformSpring.SetCurrentValueScale(new Vector3(0f, 1f, 1f));
                pressToPlayTransformSpring.SetTargetScale(Vector3.one);
                yield return new WaitUntil(IsAnyKeyOrMouseButtonPressed);
                splotchTransformSpring.SetTargetScale(Vector3.zero);
                yield return new WaitForSeconds(0.05f);
                titleTransformSpring.SetTargetScale(Vector3.zero);
                yield return new WaitForSeconds(0.15f);
                pressToPlayTransformSpring.SetTargetScale(Vector3.zero);
                yield return new WaitForSeconds(timeToWaitBeforeShowingDemo);
            }
            
            headerGameObject.SetActive(true);
            Vector2 newAnchorPosition = headerRectTransform.anchoredPosition + new Vector2(0f, demoElementsParent.rect.height);
            headerAnchoredPosSpring.SetCurrentValue(newAnchorPosition);
            
            SetAndActivateIndexToTest(indexToTest);
            
			infoPanelTransformSpring.gameObject.SetActive(true);
			
			infoPanelAnchoredPositionSpring.SetTarget(initialInfoPanelAnchoredPosition - new Vector2(0f, demoElementsParent.rect.height / 2f));
			infoPanelAnchoredPositionSpring.ReachEquilibrium();
			
			if (hideUi)
            {
                headerCanvasGroup.alpha = 0f;
            }
        }

        private void DisableEverything()
        {
            assetHeaderGameObject.SetActive(false);
            pressToPlayGameObject.SetActive(false);
            titleTransformSpring.gameObject.SetActive(false);
            splotchTransformSpring.gameObject.SetActive(false);
            headerGameObject.SetActive(false);
        }

        private string GetCorrectDemoName(string demoName)
        {
            return demoName.Replace("-SpringsDemo", "");
        }

        private void EnableCorrectInputSystem()
        {
            bool newInputSystemEnabled = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            newInputSystemEnabled = true;
#endif
            oldInputEventSystem.gameObject.SetActive(!newInputSystemEnabled);
            newInputEventSystem.gameObject.SetActive(newInputSystemEnabled);
        }
        
        private bool IsAnyKeyOrMouseButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            bool anyKeyPressed = Keyboard.current != null && Keyboard.current.anyKey.isPressed;
            bool anyMouseButtonPressed = Mouse.current != null && (
                Mouse.current.leftButton.isPressed ||
                Mouse.current.rightButton.isPressed ||
                Mouse.current.middleButton.isPressed
            );
            
            return anyKeyPressed || anyMouseButtonPressed;
#else
            return Input.anyKey || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#endif
        }
        
        public void ChangeDemoElement(int change)
        {
            newIndexToTestIsBigger = change > 0;
            indexToTest += change;
            ValidateIndexToTest();
            SetAndActivateIndexToTest(indexToTest);
        }
        
        private void SetAndActivateIndexToTest(int newIndex)
        {
            indexToTest = newIndex;
            ActivateIndexToTest();
            SetHeaderTextAndPunchScale();
            SetInfoPanelText();
        }

        private void ActivateIndexToTest()
        {
            indexToTest = LoopAroundIndex(indexToTest, demoElements.Length);
            DemoElement demoElement = demoElements[indexToTest].demoElement;
            bool is3dUiElement = demoElements[indexToTest].is3d;
            
            DestroyChildren(uiDemoParent, withAnimation: !isFirstDemoElement);
            DestroyChildren(worldDemoParent, withAnimation: !isFirstDemoElement);
            
            DemoElement newUiDemoElement = Instantiate(demoElement, is3dUiElement ? worldDemoParent : uiDemoParent);
            newUiDemoElement.Initialize(hideUi);
            if(is3dUiElement)
            {
                GameObject demoElementUiGo = Instantiate(demoElements[indexToTest].buttonPrefabOf3dElement, uiDemoParent);
                UiDemoElementInAnimation(demoElementUiGo);

                GameObject demoElementGo = newUiDemoElement.gameObject;
                TransformSpringComponent demoElementTransformSpring = demoElementGo.GetComponent<TransformSpringComponent>();
                if(demoElementTransformSpring != null)
                {
                    Vector3 offScreenPosition = isFirstDemoElement ? GetTopOrBottomScreenWorldPosition(false) : GetLeftOrRightScreenWorldPosition(true);
                    Vector3 demoElementGoPosition = demoElementGo.transform.position;
                    if(isFirstDemoElement)
                    {
                        demoElementGoPosition.y = newIndexToTestIsBigger ? offScreenPosition.y : -offScreenPosition.y;
                    }
                    else
                    {
                        demoElementGoPosition.x = newIndexToTestIsBigger ? offScreenPosition.x : -offScreenPosition.x;   
                    }
                    demoElementTransformSpring.SetCurrentValuePosition(demoElementGoPosition);
                }
                
                Demo3dButtonHolder demo3dButtonHolder = demoElementUiGo.GetComponent<Demo3dButtonHolder>();
                if(demo3dButtonHolder != null)
                {
                    demo3dButtonHolder.Initialize(newUiDemoElement, hideUi);
                }
                else
                {
                    Debug.LogError($"You forgot to add a button holder for the 3d demo element {demoElementGo.name}", demoElementGo);
                }
            }
            else
            {
                UiDemoElementInAnimation(newUiDemoElement.gameObject);
            }
            
            isFirstDemoElement = false;
        }
        
        private void UiDemoElementInAnimation(GameObject demoElementUiGo)
        {
            AnchoredPositionSpringComponent anchoredPositionSpring = demoElementUiGo.GetComponent<AnchoredPositionSpringComponent>();
            if(anchoredPositionSpring != null)
            {
                float offScreenPositionOffset = isFirstDemoElement ? demoElementsParent.rect.height : demoElementsParent.rect.width;
                Vector2 demoElementUiPosition = demoElementUiGo.GetComponent<RectTransform>().anchoredPosition;
                if(isFirstDemoElement)
                {
                    demoElementUiPosition.y += newIndexToTestIsBigger ? -offScreenPositionOffset : offScreenPositionOffset;
                }
                else
                {
                    demoElementUiPosition.x += newIndexToTestIsBigger ? -offScreenPositionOffset : offScreenPositionOffset;
                }
                anchoredPositionSpring.SetCurrentValue(demoElementUiPosition);
            }
        }
        
        private void UiDemoElementOutAnimation(GameObject demoElementUiGo)
        {
            StartCoroutine(UiDemoElementOutAnimationCoroutine(demoElementUiGo));
        }
        
        private IEnumerator UiDemoElementOutAnimationCoroutine(GameObject demoElementUiGo)
        {
            AnchoredPositionSpringComponent anchoredPositionSpring = demoElementUiGo.GetComponent<AnchoredPositionSpringComponent>();
            if(anchoredPositionSpring != null)
            {
                float offScreenPositionOffset = demoElementsParent.rect.width;
                Vector2 demoElementUiPosition = Vector2.zero;
                demoElementUiPosition.x += newIndexToTestIsBigger ? offScreenPositionOffset : -offScreenPositionOffset;
                anchoredPositionSpring.SetTarget(demoElementUiPosition);
            }

            yield return new WaitForSeconds(0.1f);
            while (anchoredPositionSpring.GetVelocity().magnitude > 0.5f)
            {
                yield return null;
            }
            
            Destroy(demoElementUiGo);
        }

        private void DemoElementOutAnimation(GameObject demoElementUiGo)
        {
            StartCoroutine(DemoElementOutAnimationCoroutine(demoElementUiGo));
        }

        private IEnumerator DemoElementOutAnimationCoroutine(GameObject demoElementUiGo)
        {
            TransformSpringComponent demoElementTransformSpring = demoElementUiGo.GetComponent<TransformSpringComponent>();
            if(demoElementTransformSpring != null)
            {
                Vector3 offScreenPosition = GetLeftOrRightScreenWorldPosition(!newIndexToTestIsBigger);
                Vector3 demoElementGoPosition = demoElementUiGo.transform.position;
                demoElementGoPosition.x = offScreenPosition.x;
                demoElementTransformSpring.SetTargetPosition(demoElementGoPosition);
            }

            yield return new WaitForSeconds(0.1f);
            while (demoElementTransformSpring.GetVelocityPosition().magnitude > 0.5f)
            {
                yield return null;
            }
            
            Destroy(demoElementUiGo);
        }

        private void SetHeaderTextAndPunchScale()
        {
            headerText.text = GetCorrectDemoName(demoElements[indexToTest].demoElement.gameObject.name);
            foreach(TransformSpringComponent headerLabelTransformSpring in headerLabelTransformSprings)
            {
                headerLabelTransformSpring.AddVelocityScale(Vector3.one * headerScalePunch);
            }
        }
        
        private void SetInfoPanelText()
        {
            infoPanelText.text = demoElements[indexToTest].description;
            infoPanelTransformSpring.AddVelocityScale(Vector3.one * -infoPanelScalePunch);
        }
        
        public void ToggleInfoPanel()
        {
            infoPanelOn = !infoPanelOn;
            Vector2 newAnchorPosition = infoPanelOn ? initialInfoPanelAnchoredPosition : 
                initialInfoPanelAnchoredPosition - new Vector2(0f, demoElementsParent.rect.height / 2f);
            infoPanelAnchoredPositionSpring.SetTarget(newAnchorPosition);
        }
        
        private int LoopAroundIndex(int i, int arrayLength)
        {
            if(i < 0) return arrayLength - 1;
            if(i >= arrayLength) return 0;
            return i;
        }
    
        private void ValidateIndexToTest()
        {
            indexToTest = LoopAroundIndex(indexToTest, demoElements.Length);
        }
        
        private void DestroyChildren(Transform parent, bool withAnimation = false)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                if(withAnimation)
                {
                    GameObject demoElementUiGo = parent.GetChild(i).gameObject;
                    TransformSpringComponent transformSpringComponent = demoElementUiGo.GetComponent<TransformSpringComponent>();
                    if(transformSpringComponent != null)
                    {
                        if(!demoElementUiGo.name.Equals("Destroying"))
                        {
                            demoElementUiGo.name = "Destroying";
                            SetUiElementToCloseIfPossible(demoElementUiGo);
                            DemoElementOutAnimation(demoElementUiGo);   
                        }
                    }
                    else
                    {
                        AnchoredPositionSpringComponent anchoredPositionSpring = demoElementUiGo.GetComponent<AnchoredPositionSpringComponent>();
                        if(anchoredPositionSpring != null)
                        {
                            if(!demoElementUiGo.name.Equals("Destroying"))
                            {
                                demoElementUiGo.name = "Destroying";
                                SetUiElementToCloseIfPossible(demoElementUiGo);
                                UiDemoElementOutAnimation(demoElementUiGo);     
                            }
                        }
                        else
                        {
                            Debug.LogError($"You forgot to add a spring for the demo element transition for {demoElementUiGo.name}", demoElementUiGo);
                        }
                    }
                }
                else
                {
                    DestroyImmediate(parent.GetChild(i).gameObject);
                }
            }
        }
        
        private void SetUiElementToCloseIfPossible(GameObject demoElementGo)
        {
            DemoElement demoElement = demoElementGo.GetComponent<DemoElement>();
            if(demoElement != null)
            {
                demoElement.Close();
            }
        }
        
        private Vector3 GetTopOrBottomScreenWorldPosition(bool isTop)
        {
            Vector3 screenTargetPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, -mainCamera.transform.position.z);
            screenTargetPosition.y += (isTop ? Screen.height : -Screen.height) * 1.5f;
            return mainCamera.ScreenToWorldPoint(screenTargetPosition);
        }
        
        private Vector3 GetLeftOrRightScreenWorldPosition(bool isLeft)
        {
            Vector3 screenTargetPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, -mainCamera.transform.position.z);
            screenTargetPosition.x += (isLeft ? -Screen.width : Screen.width) * 1.5f;
            return mainCamera.ScreenToWorldPoint(screenTargetPosition);
        }
    }
}