using UnityEngine;

namespace Meowdoku
{
    /// <summary>Shared easing curves used across the board intro, win-panel pop, and letter-flight animations.</summary>
    public static class NekoEasing
    {
        public static float OutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.7f;
            float shifted = value - 1f;
            return 1f + ((overshoot + 1f) * shifted * shifted * shifted) + (overshoot * shifted * shifted);
        }

        public static float OutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }
    }
}
