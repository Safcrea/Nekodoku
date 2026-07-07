using System;
using AllIn1SpringsToolkit;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The win-celebration bucket: rises into view, then reveals the authored cat slots in-place.
    /// Each slot starts from a random local Y offset, fades into its rest position, then the slot
    /// players start their Revealed loop with tiny random delays so they do not all sync up.
    /// </summary>
    public sealed class LevelCompleteBucket : MonoBehaviour
    {
        private const string RevealedAnimationName = "Revealed";

        [Header("References")]
        [SerializeField] private RectTransform bucketRoot;
        [SerializeField] private Image[] catSlotImages;
        [SerializeField] private SpriteSheetAnimator catSlotAnimationLibrary;
        [SerializeField] private SpriteSheetAnimationPlayer[] catSlotAnimationPlayers;
        [SerializeField] private TransformSpringComponent bucketSpring;
        [SerializeField] private TransformSpringComponent[] catSlotSprings;

        [Header("Tuning")]
        [SerializeField] private float riseSeconds = 0.5f;
        [SerializeField] private float hiddenOffsetY = 900f;
        [SerializeField] private float slotMoveSeconds = 0.36f;
        [SerializeField] private float slotFadeSeconds = 0.22f;
        [SerializeField] private float slotStartYOffsetMin = 110f;
        [SerializeField] private float slotStartYOffsetMax = 260f;
        [SerializeField] private float revealedAnimationMaxRandomDelaySeconds = 0.14f;
        [SerializeField] private float bucketRiseVelocity = 2600f;
        [SerializeField] private float bucketLandingScaleImpulse = 2.2f;
        [SerializeField] private float bucketLandingPositionImpulse = 80f;
        [SerializeField] private float slotMovePositionImpulse = 90f;

        private Vector3 restLocalPosition;
        private Vector3[] slotRestLocalPositions;
        private bool hasCachedRest;
        private Sequence gatherSequence;

        private void Awake()
        {
            CacheRestPosition();
            CacheSlotRestPositions();
            ResolveSprings();
            ResolveSlotAnimationPlayers();
            ResetSlots();

            if (bucketRoot != null)
            {
                bucketRoot.gameObject.SetActive(false);
            }
        }

        private void CacheRestPosition()
        {
            if (hasCachedRest || bucketRoot == null)
            {
                return;
            }

            restLocalPosition = bucketRoot.localPosition;
            hasCachedRest = true;
        }

        private void CacheSlotRestPositions()
        {
            if (catSlotImages == null)
            {
                slotRestLocalPositions = null;
                return;
            }

            if (slotRestLocalPositions != null && slotRestLocalPositions.Length == catSlotImages.Length)
            {
                return;
            }

            slotRestLocalPositions = new Vector3[catSlotImages.Length];
            for (int i = 0; i < catSlotImages.Length; i++)
            {
                slotRestLocalPositions[i] = catSlotImages[i] != null
                    ? catSlotImages[i].rectTransform.localPosition
                    : Vector3.zero;
            }
        }

        private void ResolveSprings()
        {
            bucketSpring = ResolveSpring(bucketRoot, bucketSpring, 130f, 11f);

            if (catSlotImages == null)
            {
                return;
            }

            if (catSlotSprings == null || catSlotSprings.Length != catSlotImages.Length)
            {
                Array.Resize(ref catSlotSprings, catSlotImages.Length);
            }

            for (int i = 0; i < catSlotImages.Length; i++)
            {
                Image slot = catSlotImages[i];
                if (slot == null)
                {
                    continue;
                }

                catSlotSprings[i] = ResolveSpring(slot.rectTransform, catSlotSprings[i], 170f, 12f);
            }
        }

        private void ResolveSlotAnimationPlayers()
        {
            if (catSlotImages == null)
            {
                return;
            }

            if (catSlotAnimationPlayers == null || catSlotAnimationPlayers.Length != catSlotImages.Length)
            {
                Array.Resize(ref catSlotAnimationPlayers, catSlotImages.Length);
            }

            for (int i = 0; i < catSlotImages.Length; i++)
            {
                Image slot = catSlotImages[i];
                if (slot == null)
                {
                    continue;
                }

                SpriteSheetAnimationPlayer player = catSlotAnimationPlayers[i];
                if (player == null && !slot.TryGetComponent(out player))
                {
                    player = slot.gameObject.AddComponent<SpriteSheetAnimationPlayer>();
                }

                catSlotAnimationPlayers[i] = player;
                if (player == null)
                {
                    continue;
                }

                player.SetTargetImage(slot);
                if (catSlotAnimationLibrary != null)
                {
                    player.SetAnimationLibrary(catSlotAnimationLibrary);
                }
            }
        }

        /// <summary>Rises the bucket into view, reveals the fixed slot images, then invokes onComplete.</summary>
        public void PlayGatherCats(Action onComplete)
        {
            if (bucketRoot == null)
            {
                onComplete?.Invoke();
                return;
            }

            CacheRestPosition();
            CacheSlotRestPositions();
            ResolveSlotAnimationPlayers();

            bucketRoot.DOKill();
            gatherSequence?.Kill();
            gatherSequence = null;
            bucketRoot.gameObject.SetActive(true);
            ResetSlots();

            SnapSpring(bucketSpring, bucketRoot, HiddenLocalPosition(), Vector3.one, Quaternion.identity);
            bucketSpring?.SetTargetPosition(restLocalPosition);
            bucketSpring?.AddVelocityPosition(Vector3.up * bucketRiseVelocity);
            SoundManager.PlaySound(SFX.BucketRise);

            gatherSequence = DOTween.Sequence().SetUpdate(true);
            gatherSequence.AppendInterval(riseSeconds);

            int revealSlotCount = catSlotImages?.Length ?? 0;
            float slotMoveStart = riseSeconds;
            for (int i = 0; i < revealSlotCount; i++)
            {
                Tween revealTween = RevealSlot(i, i == 0);
                if (revealTween != null)
                {
                    gatherSequence.Insert(slotMoveStart, revealTween);
                }
            }

            float animationStart = slotMoveStart + Mathf.Max(slotMoveSeconds, slotFadeSeconds);
            for (int i = 0; i < revealSlotCount; i++)
            {
                int slotIndex = i;
                float delay = UnityEngine.Random.Range(0f, revealedAnimationMaxRandomDelaySeconds);
                gatherSequence.InsertCallback(animationStart + delay, () => PlaySlotRevealedAnimation(slotIndex));
            }

            gatherSequence.InsertCallback(animationStart + revealedAnimationMaxRandomDelaySeconds, () => { });
            gatherSequence.OnComplete(() =>
            {
                gatherSequence = null;
                onComplete?.Invoke();
            });
        }

        private Tween RevealSlot(int index, bool playBucketImpact)
        {
            if (catSlotImages == null || index < 0 || index >= catSlotImages.Length)
            {
                return null;
            }

            Image slot = catSlotImages[index];
            if (slot == null)
            {
                return null;
            }

            RectTransform rect = slot.rectTransform;
            TransformSpringComponent slotSpring = catSlotSprings != null && index < catSlotSprings.Length ? catSlotSprings[index] : null;
            Vector3 restPosition = SlotRestPosition(index, rect);
            Vector3 startPosition = restPosition + Vector3.up * RandomSlotYOffset();

            rect.DOKill();
            slot.DOKill();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendCallback(() =>
            {
                SpriteSheetAnimationPlayer player = SlotAnimationPlayer(index);
                player?.Stop();

                Color hiddenColor = slot.color;
                hiddenColor.a = 0f;
                slot.color = hiddenColor;

                if (slotSpring != null)
                {
                    SnapSpring(slotSpring, rect, startPosition, Vector3.one, rect.localRotation);
                    slotSpring.SetTargetPosition(restPosition);
                    slotSpring.AddVelocityPosition(Vector3.up * -Mathf.Sign(startPosition.y - restPosition.y) * slotMovePositionImpulse);
                }
                else
                {
                    rect.localPosition = startPosition;
                    rect.localScale = Vector3.one;
                }

                if (playBucketImpact)
                {
                    bucketSpring?.AddVelocityScale(Vector3.one * bucketLandingScaleImpulse);
                    bucketSpring?.AddVelocityPosition(Vector3.up * bucketLandingPositionImpulse);
                    GameHaptics.LightImpact();
                    SoundManager.PlaySound(SFX.CatGathered);
                }
            });

            Tween moveTween = slotSpring != null
                ? DOVirtual.DelayedCall(slotMoveSeconds, () => { }, false).SetUpdate(true)
                : rect.DOLocalMove(restPosition, slotMoveSeconds).SetEase(Ease.OutCubic).SetUpdate(true);

            sequence.Append(moveTween);
            sequence.Join(slot.DOFade(1f, slotFadeSeconds).SetEase(Ease.OutSine));
            sequence.AppendCallback(() => SnapSpring(slotSpring, rect, restPosition, Vector3.one, rect.localRotation));
            return sequence;
        }

        private void PlaySlotRevealedAnimation(int index)
        {
            SpriteSheetAnimationPlayer player = SlotAnimationPlayer(index);
            if (player == null)
            {
                return;
            }

            player.Play(RevealedAnimationName, true);
        }

        /// <summary>Resets the bucket and its slots so the next win plays cleanly.</summary>
        public void Hide()
        {
            gatherSequence?.Kill();
            gatherSequence = null;

            if (bucketRoot != null)
            {
                bucketRoot.DOKill();
                if (hasCachedRest)
                {
                    SnapSpring(bucketSpring, bucketRoot, HiddenLocalPosition(), Vector3.one, Quaternion.identity);
                }

                bucketRoot.gameObject.SetActive(false);
            }

            ResetSlots();
        }

        private void ResetSlots()
        {
            if (catSlotImages == null)
            {
                return;
            }

            CacheSlotRestPositions();
            ResolveSlotAnimationPlayers();

            for (int i = 0; i < catSlotImages.Length; i++)
            {
                Image slot = catSlotImages[i];
                if (slot == null)
                {
                    continue;
                }

                slot.DOKill();
                slot.rectTransform.DOKill();
                SlotAnimationPlayer(i)?.Stop();

                TransformSpringComponent slotSpring = catSlotSprings != null && i < catSlotSprings.Length
                    ? catSlotSprings[i]
                    : null;
                SnapSpring(slotSpring, slot.rectTransform, SlotRestPosition(i, slot.rectTransform), Vector3.one, slot.rectTransform.localRotation);
                Color hiddenColor = slot.color;
                hiddenColor.a = 0f;
                slot.color = hiddenColor;
            }
        }

        private Vector3 SlotRestPosition(int index, RectTransform rect)
        {
            if (slotRestLocalPositions != null && index >= 0 && index < slotRestLocalPositions.Length)
            {
                return slotRestLocalPositions[index];
            }

            return rect != null ? rect.localPosition : Vector3.zero;
        }

        private SpriteSheetAnimationPlayer SlotAnimationPlayer(int index)
        {
            if (catSlotAnimationPlayers == null || index < 0 || index >= catSlotAnimationPlayers.Length)
            {
                return null;
            }

            return catSlotAnimationPlayers[index];
        }

        private float RandomSlotYOffset()
        {
            float min = Mathf.Max(0f, slotStartYOffsetMin);
            float max = Mathf.Max(min, slotStartYOffsetMax);
            float magnitude = UnityEngine.Random.Range(min, max);
            return UnityEngine.Random.value < 0.5f ? -magnitude : magnitude;
        }

        private Vector3 HiddenLocalPosition()
        {
            return restLocalPosition + new Vector3(0f, -hiddenOffsetY, 0f);
        }

        private static TransformSpringComponent ResolveSpring(RectTransform rect, TransformSpringComponent spring, float force, float drag)
        {
            if (rect == null)
            {
                return spring;
            }

            bool created = false;
            if (spring == null && !rect.TryGetComponent(out spring))
            {
                spring = rect.gameObject.AddComponent<TransformSpringComponent>();
                created = true;
            }

            if (spring == null)
            {
                return null;
            }

            spring.followerTransform = rect;
            spring.spaceType = TransformSpringComponent.SpaceType.LocalSpace;
            spring.useScaledTime = false;
            spring.SetUnifiedForceAndDragPosition(force, drag);
            spring.SetUnifiedForceAndDragScale(force, drag);
            spring.SetUnifiedForceAndDragRotation(force, drag);

            if (created || !spring.doesAutoInitialize)
            {
                spring.Initialize();
            }

            return spring;
        }

        private static void SnapSpring(
            TransformSpringComponent spring,
            RectTransform rect,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            if (rect == null)
            {
                return;
            }

            rect.localPosition = localPosition;
            rect.localScale = localScale;
            rect.localRotation = localRotation;

            if (spring == null)
            {
                return;
            }

            spring.SetCurrentValuePosition(localPosition);
            spring.SetTargetPosition(localPosition);
            spring.SetVelocityPosition(Vector3.zero);
            spring.SetCurrentValueScale(localScale);
            spring.SetTargetScale(localScale);
            spring.SetVelocityScale(Vector3.zero);
            spring.SetCurrentValueRotation(localRotation);
            spring.SetTargetRotation(localRotation);
            spring.SetVelocityRotation(Vector3.zero);
        }
    }
}
