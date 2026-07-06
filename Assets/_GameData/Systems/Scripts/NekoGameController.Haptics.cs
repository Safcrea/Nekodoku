using UnityEngine;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void PlayLightCrossHaptic()
        {
            if (Time.unscaledTime - lastLightHapticTime < LightHapticCooldownSeconds)
            {
                return;
            }

            lastLightHapticTime = Time.unscaledTime;
            NekoHaptics.Play(HapticStrength.Light);
        }
    }
}
