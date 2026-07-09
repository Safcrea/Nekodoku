using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class GenericLoader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Public Variables")]
    public String textToDisplay = "";
    public float timeToDisplay = 0.5f;
    public Action OnLoadComplete;
    
    // private Variables
    private Tween FadeTween;
    private Coroutine destroyCoroutine;
    
   void OnEnable()
   {
       loadingText.text = textToDisplay;
       
       if (!canvasGroup) // if canvasGroup is not assigned, get it
           canvasGroup = GetComponent<CanvasGroup>();
   
       if (canvasGroup)
       {
           if(timeToDisplay <= 0)
               timeToDisplay = 2f; // default time to display if not set
           
           FadeTween?.Kill(); // kill any previous fade tween
           canvasGroup.alpha = 0.5f;
           FadeTween = canvasGroup.DOFade(1f, timeToDisplay/2).SetUpdate(true);
       }
       
       // Use coroutine instead of Destroy with delay for more reliability
       if(destroyCoroutine != null)
           StopCoroutine(destroyCoroutine);
        destroyCoroutine = null;
       destroyCoroutine = StartCoroutine(DestroyAfterDelay(timeToDisplay));
   }
   
   private IEnumerator DestroyAfterDelay(float delay)
   {
       yield return new WaitForSecondsRealtime(delay); // Uses real time, not affected by Time.timeScale
       Destroy(gameObject);
   }
    
    private void OnDestroy()
    {
        if (canvasGroup)
        {
            OnLoadComplete?.Invoke();
            FadeTween?.Kill(); // kill any previous fade tween
            // canvasGroup.alpha = 1f;
            // FadeTween = canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
        }
    }
}
