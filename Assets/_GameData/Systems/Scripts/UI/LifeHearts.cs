using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The three life-heart icons. Scene-placed and serialized like any normal
    /// Unity reference - never spawned at runtime, since the count is always
    /// exactly <see cref="PuzzleBoard"/>'s starting-hearts total.
    /// </summary>
    public sealed class LifeHearts : MonoBehaviour
    {
        private const float IntroSeconds = 0.3f;
        private const float IntroStaggerSeconds = 0.08f;
        private const float OutroSeconds = 0.22f;
        private const float LostPunchSeconds = 0.32f;
        private const float GainedPopSeconds = 0.34f;

        private static readonly Color AliveColor = Color.white;
        private static readonly Color LostColor = new Color(1f, 1f, 1f, 0.25f);

        [SerializeField]
        private Image[] hearts = new Image[3];

        private bool wasFailed;

        /// <summary>Reflects current hearts remaining and plays the outro once, the moment the board becomes failed.</summary>
        public void Refresh(int heartsRemaining, bool isFailed)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null)
                {
                    continue;
                }

                hearts[i].color = i < heartsRemaining ? AliveColor : LostColor;
            }

            if (isFailed && !wasFailed)
            {
                PlayOutro();
            }

            wasFailed = isFailed;
        }

        public void PlayIntro()
        {
            wasFailed = false;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null)
                {
                    continue;
                }

                RectTransform rect = hearts[i].rectTransform;
                rect.DOKill();
                rect.localScale = Vector3.zero;
                rect.DOScale(1f, IntroSeconds).SetEase(Ease.OutBack).SetDelay(i * IntroStaggerSeconds).SetUpdate(true);
            }
        }

        public void PlayOutro()
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null)
                {
                    continue;
                }

                RectTransform rect = hearts[i].rectTransform;
                rect.DOKill();
                rect.DOScale(0f, OutroSeconds).SetEase(Ease.InBack).SetDelay(i * (IntroStaggerSeconds * 0.5f)).SetUpdate(true);
            }
        }

        /// <summary>Punches the heart that was just lost. <paramref name="heartsRemaining"/> is the count after losing it.</summary>
        public void PlayHeartLostEffect(int heartsRemaining)
        {
            int lostIndex = heartsRemaining;
            if (lostIndex < 0 || lostIndex >= hearts.Length || hearts[lostIndex] == null)
            {
                return;
            }

            RectTransform rect = hearts[lostIndex].rectTransform;
            rect.DOKill();
            rect.localScale = Vector3.one;
            rect.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), LostPunchSeconds, 6, 0.6f).SetUpdate(true);
        }

        public void PlayHeartGainedEffect(int heartsRemaining)
        {
            int gainedIndex = heartsRemaining - 1;
            if (gainedIndex < 0 || gainedIndex >= hearts.Length || hearts[gainedIndex] == null)
            {
                return;
            }

            Image heart = hearts[gainedIndex];
            heart.color = AliveColor;

            RectTransform rect = heart.rectTransform;
            rect.DOKill();
            rect.localScale = Vector3.zero;

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(rect.DOScale(1.16f, GainedPopSeconds * 0.58f).SetEase(Ease.OutBack));
            sequence.Append(rect.DOScale(1f, GainedPopSeconds * 0.42f).SetEase(Ease.InOutSine));
        }
    }
}
