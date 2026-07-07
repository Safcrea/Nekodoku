using System;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// One named sprite-sheet animation: an ordered set of frames, default playback speed,
    /// and whether it loops or plays once.
    /// </summary>
    [Serializable]
    public sealed class SpriteSheetAnimation
    {
        public const float DefaultFrameRate = 12f;

        public string name;
        public Sprite[] frames;

        [Min(0.01f)]
        public float frameRate = DefaultFrameRate;
        public bool loop = true;
    }

    /// <summary>
    /// Reusable sprite-sheet animation library. Create one asset, add named animations,
    /// then point any <see cref="SpriteSheetAnimationPlayer"/> at it to play those clips
    /// on that player's target Image.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Sprite Sheet Animator",
        menuName = "Meowdoku/Sprite Sheet Animator")]
    public sealed class SpriteSheetAnimator : ScriptableObject
    {
        [SerializeField]
        private SpriteSheetAnimation[] animations;

        public SpriteSheetAnimation[] Animations => animations;

        public bool TryGetAnimation(string animationName, out SpriteSheetAnimation animation)
        {
            animation = FindAnimation(animationName);
            return animation != null;
        }

        public SpriteSheetAnimation FindAnimation(string animationName)
        {
            if (animations == null)
            {
                return null;
            }

            foreach (SpriteSheetAnimation animation in animations)
            {
                if (animation != null && animation.name == animationName)
                {
                    return animation;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            if (animations == null)
            {
                return;
            }

            foreach (SpriteSheetAnimation animation in animations)
            {
                if (animation != null && animation.frameRate <= 0f)
                {
                    animation.frameRate = SpriteSheetAnimation.DefaultFrameRate;
                }
            }
        }
    }
}
