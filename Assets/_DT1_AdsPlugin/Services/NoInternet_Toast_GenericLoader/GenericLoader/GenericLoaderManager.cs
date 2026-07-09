using System;
using UnityEngine;

public class GenericLoaderManager : MonoBehaviour
{
    [SerializeField] public GenericLoader GenericLoader;

    [ContextMenu("ShowLoader")]
    private void ShowLoader()
    {
        ShowLoader("Loading...", 2f);
    }
    
    public void ShowLoader(string message, float timeToDisplay = 2f, Action onLoadComplete = null)
    {
        if (GenericLoader == null)
        {
            Debug.LogError("GenericLoader is not assigned in GenericLoaderManager.");
            return;
        }
        
        // Set the message and time to display
        GenericLoader.textToDisplay = message;
        GenericLoader.timeToDisplay = timeToDisplay;
        
        // Instantiate the popup
        Instantiate(GenericLoader.gameObject).TryGetComponent(out GenericLoader component);
        component.OnLoadComplete = onLoadComplete;
    }
}
