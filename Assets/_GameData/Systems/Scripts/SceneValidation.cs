using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Shared "is this serialized scene reference actually assigned" check. Every
    /// view validates its own fields with this so a misconfigured scene fails
    /// loudly, naming the exact field, instead of NRE-ing somewhere downstream.
    /// </summary>
    public static class SceneValidation
    {
        public static bool LogIfMissing(Object reference, string fieldLabel, Component context)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"{context.GetType().Name} on '{context.gameObject.name}' is missing its {fieldLabel} reference.", context);
            return false;
        }
    }
}
