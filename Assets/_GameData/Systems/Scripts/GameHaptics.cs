using Voodoo.Utils;

namespace Meowdoku
{
    public static class GameHaptics
    {
        public static void Selection()
        {
            Play(HapticTypes.Selection);
        }

        public static void LightImpact()
        {
            Play(HapticTypes.MediumImpact);
        }

        public static void Success()
        {
            Play(HapticTypes.Success);
        }

        public static void Failure()
        {
            Play(HapticTypes.Failure);
        }

        private static void Play(HapticTypes type)
        {
            Vibrations.Haptic(type);
        }
    }
}
