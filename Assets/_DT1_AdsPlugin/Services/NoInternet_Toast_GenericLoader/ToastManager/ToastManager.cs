using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class ToastManager : MonoBehaviour
{
    [SerializeField] private GenericPopup genericPopup;
    [SerializeField] private int poolLimit = 5;

    private Queue<GenericPopup> popupPool;

    public void Initialize()
    {
        Debug.Log("Initializing Toast Pool with limit: " + poolLimit);
        popupPool = new Queue<GenericPopup>(poolLimit);

        for (int i = 0; i < poolLimit; i++)
        {
            GenericPopup popup = Instantiate(genericPopup, transform);
            popup.gameObject.SetActive(false);
            popup.OnAnimationComplete += () => ReturnToPool(popup);
            popupPool.Enqueue(popup);
        }
    }
    [Button("Show Toast")]
    public void ShowToast(string message, Color toastColor)
    {
        Debug.Log(message);
        GenericPopup popup = GetOrCreatePopup();
        popup.Init(message, toastColor);
        popup.Animate();
    }

    private GenericPopup GetOrCreatePopup()
    {
        if (popupPool.Count > 0)
        {
            return popupPool.Dequeue();
        }

        // If pool is empty, create a new one (beyond pool limit)
        GenericPopup popup = Instantiate(genericPopup, transform);
        popup.OnAnimationComplete += () => ReturnToPool(popup);
        return popup;
    }

    private void ReturnToPool(GenericPopup popup)
    {
        if (popupPool.Count < poolLimit)
        {
            popupPool.Enqueue(popup);
        }
        else
        {
            Destroy(popup.gameObject);
        }
    }

    public void ShowNoRewardedAdToast()
    {
        ShowToast("No Ad Available", Color.red);
    }
    public void ShowRewardCanceledToast()
    {
        ShowToast("Ad Canceled", Color.yellow);
    }
    public void ShowRewardGrantedToast()
    {
        ShowToast("Reward Granted.", Color.green);
    }
}
