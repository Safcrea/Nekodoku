using Sych.QuickActionsAssets.Runtime;
using UnityEngine;

/// <summary>
/// Optional scene component for explicitly registering Nekodoku's Home Screen actions. The runtime
/// also registers them automatically, so keeping this prefab in a scene is safe but not required.
/// GameManager owns action handling because it has the gameplay state needed to perform each action.
/// </summary>
public sealed class QuickActionManager : MonoBehaviour
{
    public static QuickActionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        QuickActions.AddGameQuickActions();
        Debug.Log($"[Quick Action] Platform supported: {QuickActions.IsPlatformSupported}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
