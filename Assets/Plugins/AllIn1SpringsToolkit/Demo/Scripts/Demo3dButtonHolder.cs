using UnityEngine;
using UnityEngine.EventSystems;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public abstract class Demo3dButtonHolder : MonoBehaviour
    {
        [SerializeField] private GameObject firstSelectedUiElement;
        [SerializeField] private CanvasGroup canvasGroup;
        
        public virtual void Initialize(DemoElement demoElement, bool hideUi)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedUiElement);
            
            if(hideUi)
            {
                canvasGroup.alpha = 0f;
            }
        }
    }
}