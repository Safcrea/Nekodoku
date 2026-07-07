using System;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Runtime player for a UI Image. Point it at a <see cref="SpriteSheetAnimator"/>
    /// asset, then call Play/Pause/Resume/Restart/Stop by animation name.
    /// </summary>
    [AddComponentMenu("Meowdoku/Sprite Sheet Animation Player")]
    public sealed class SpriteSheetAnimationPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image targetImage;
        [SerializeField] private SpriteSheetAnimator animationLibrary;

        [Header("Autoplay")]
        [SerializeField] private bool playOnAwake;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private string autoPlayAnimation;
        [SerializeField] private bool restartAutoPlay = true;

        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool overrideFrameRate;

        [Min(0.01f)]
        [SerializeField] private float frameRateOverride = SpriteSheetAnimation.DefaultFrameRate;

        /// <summary>Fires once when a non-looping or one-shot animation reaches its last frame.</summary>
        public event Action<string> AnimationCompleted;

        private SpriteSheetAnimation current;
        private int frameIndex;
        private float frameTimer;
        private float runtimeFrameRateOverride;
        private bool forceCurrentNonLoop;
        private string queuedAnimationName;

        public bool IsPlaying { get; private set; }
        public string CurrentAnimationName => current?.name;
        public SpriteSheetAnimator AnimationLibrary => animationLibrary;

        private void Awake()
        {
            ResolveTargetImage();

            if (playOnAwake)
            {
                PlayAutoAnimation();
            }
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                PlayAutoAnimation();
            }
        }

        private void OnValidate()
        {
            if (frameRateOverride <= 0f)
            {
                frameRateOverride = SpriteSheetAnimation.DefaultFrameRate;
            }
        }

        private void Update()
        {
            if (!IsPlaying || current == null || current.frames == null || current.frames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / EffectiveFrameRate();
            frameTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            bool advanced = false;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                advanced = true;

                if (frameIndex >= current.frames.Length)
                {
                    if (current.loop && !forceCurrentNonLoop)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        string completedAnimationName = current.name;
                        string nextAnimationName = queuedAnimationName;
                        frameIndex = current.frames.Length - 1;
                        IsPlaying = false;
                        SetFrame();
                        forceCurrentNonLoop = false;
                        queuedAnimationName = null;
                        AnimationCompleted?.Invoke(completedAnimationName);

                        if (!string.IsNullOrEmpty(nextAnimationName))
                        {
                            Play(nextAnimationName);
                        }

                        return;
                    }
                }
            }

            if (advanced)
            {
                SetFrame();
            }
        }

        public void SetAnimationLibrary(SpriteSheetAnimator library)
        {
            animationLibrary = library;
        }

        public void SetTargetImage(Image image)
        {
            targetImage = image;
        }

        /// <summary>Plays using the animation's default FPS, or the Inspector override if enabled.</summary>
        public bool Play(string animationName, bool restart = true)
        {
            runtimeFrameRateOverride = 0f;
            return PlayInternal(animationLibrary, animationName, restart);
        }

        /// <summary>Plays at a caller-provided FPS, ignoring the animation asset's default FPS for this run.</summary>
        public bool PlayAtFrameRate(string animationName, float frameRate, bool restart = true)
        {
            runtimeFrameRateOverride = Mathf.Max(0.01f, frameRate);
            return PlayInternal(animationLibrary, animationName, restart);
        }

        /// <summary>Plays one full cycle, then starts another animation on the same library.</summary>
        public bool PlayOnceThen(string animationName, string nextAnimationName, bool restart = true)
        {
            runtimeFrameRateOverride = 0f;
            return PlayInternal(animationLibrary, animationName, restart, true, nextAnimationName);
        }

        /// <summary>Plays one full cycle at the caller-provided FPS, then starts another animation.</summary>
        public bool PlayOnceThenAtFrameRate(string animationName, float frameRate, string nextAnimationName, bool restart = true)
        {
            runtimeFrameRateOverride = Mathf.Max(0.01f, frameRate);
            return PlayInternal(animationLibrary, animationName, restart, true, nextAnimationName);
        }

        public bool Play(SpriteSheetAnimator library, string animationName, bool restart = true)
        {
            runtimeFrameRateOverride = 0f;
            return PlayInternal(library, animationName, restart);
        }

        public bool PlayAtFrameRate(SpriteSheetAnimator library, string animationName, float frameRate, bool restart = true)
        {
            runtimeFrameRateOverride = Mathf.Max(0.01f, frameRate);
            return PlayInternal(library, animationName, restart);
        }

        public bool PlayOnceThen(SpriteSheetAnimator library, string animationName, string nextAnimationName, bool restart = true)
        {
            runtimeFrameRateOverride = 0f;
            return PlayInternal(library, animationName, restart, true, nextAnimationName);
        }

        public bool PlayOnceThenAtFrameRate(
            SpriteSheetAnimator library,
            string animationName,
            float frameRate,
            string nextAnimationName,
            bool restart = true)
        {
            runtimeFrameRateOverride = Mathf.Max(0.01f, frameRate);
            return PlayInternal(library, animationName, restart, true, nextAnimationName);
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Resume()
        {
            if (current != null)
            {
                IsPlaying = true;
            }
        }

        public void Restart()
        {
            if (current == null)
            {
                return;
            }

            frameIndex = 0;
            frameTimer = 0f;
            IsPlaying = true;
            SetFrame();
        }

        public void Stop()
        {
            IsPlaying = false;
            frameIndex = 0;
            frameTimer = 0f;
            forceCurrentNonLoop = false;
            queuedAnimationName = null;
            SetFrame();
        }

        private bool PlayInternal(
            SpriteSheetAnimator library,
            string animationName,
            bool restart,
            bool forceNonLoop = false,
            string nextAnimationName = null)
        {
            if (library == null)
            {
                Debug.LogWarning($"SpriteSheetAnimationPlayer on {gameObject.name}: no animation library assigned.");
                return false;
            }

            SpriteSheetAnimation animation = library.FindAnimation(animationName);
            if (animation == null)
            {
                Debug.LogWarning($"SpriteSheetAnimationPlayer on {gameObject.name}: no animation named '{animationName}' in {library.name}.");
                return false;
            }

            bool sameAnimation = current == animation;
            animationLibrary = library;
            current = animation;
            IsPlaying = true;
            forceCurrentNonLoop = forceNonLoop;
            queuedAnimationName = string.IsNullOrEmpty(nextAnimationName) ? null : nextAnimationName;

            if (restart || !sameAnimation)
            {
                frameIndex = 0;
                frameTimer = 0f;
                SetFrame();
            }

            return true;
        }

        private void PlayAutoAnimation()
        {
            if (!string.IsNullOrEmpty(autoPlayAnimation))
            {
                Play(autoPlayAnimation, restartAutoPlay);
            }
        }

        private void ResolveTargetImage()
        {
            if (targetImage == null)
            {
                TryGetComponent(out targetImage);
            }
        }

        private void SetFrame()
        {
            ResolveTargetImage();
            if (targetImage == null || current?.frames == null || current.frames.Length == 0)
            {
                return;
            }

            targetImage.sprite = current.frames[Mathf.Clamp(frameIndex, 0, current.frames.Length - 1)];
        }

        private float EffectiveFrameRate()
        {
            if (runtimeFrameRateOverride > 0f)
            {
                return runtimeFrameRateOverride;
            }

            if (overrideFrameRate)
            {
                return Mathf.Max(0.01f, frameRateOverride);
            }

            return current.frameRate > 0f ? current.frameRate : SpriteSheetAnimation.DefaultFrameRate;
        }
    }
}
