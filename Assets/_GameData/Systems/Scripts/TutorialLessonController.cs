using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The once-ever, pre-Level-1 lessons that teach the three puzzle rules by interleaving them
    /// with the reveal of the board's own cats: tap to reveal cat 1, learn row/column, tap to reveal
    /// cat 2, learn touching, tap to reveal cat 3, learn color, then the player finds the fourth
    /// (and last) cat entirely on their own with no more guidance. Only ever one cell (or a rule's
    /// exact target cells) is interactable at a time - everything else is dimmed and click-blocked
    /// via <see cref="BoardView.SetTutorialRestriction"/> - and a full-screen overlay dims the board/HUD
    /// behind each rule card so focus stays on what's being taught. Reference cards pop in
    /// center-screen, then dock (crossfade, not reparent) into their slot in the always-on "Rules"
    /// tab strip. Gated by <see cref="LessonSeenPlayerPrefsKey"/> so it only ever plays on a
    /// player's first-ever launch; GameManager checks that before calling <see cref="Begin"/> at all.
    /// </summary>
    public sealed class TutorialLessonController : MonoBehaviour
    {
        public const string LessonSeenPlayerPrefsKey = "Nekodoku.TutorialLessonSeen";

        private const float CardPopSeconds = 0.4f;
        private const float CardDockSeconds = 0.65f;
        private const float CardStartScale = 0.7f;
        private const float TabDockScale = 0.35f;
        private const float FocusRingPadding = 34f;
        private const float DimOverlayFadeSeconds = 0.35f;
        private const float GuideFadeSeconds = 0.25f;
        private const float PhaseTransitionSeconds = 0.5f;
        private const float GuideMoveLerpSpeed = 10f;
        private const float InfoCardPopSeconds = 0.3f;
        private const float InfoCardFadeOutSeconds = 0.3f;

        private const float DragCycleSeconds = 2.4f;
        private const float DragTravelStart = 0.14f;
        private const float DragTravelEnd = 0.78f;
        private const float DragReleaseTime = 0.8f;

        private static readonly int DoubleClickTrigger = Animator.StringToHash("DoubleClick");
        private static readonly int SingleClickTrigger = Animator.StringToHash("SingleClick");
        private static readonly int FingerDownTrigger = Animator.StringToHash("FingerDown");
        private static readonly int FingerUpTrigger = Animator.StringToHash("FingerUp");

        /// <summary>All 8 neighbors, in reading order - the touching rule forbids orthogonal AND
        /// diagonal contact, so the teach step must cover every uncrossed neighbor, not just diagonals.</summary>
        private static readonly (int dr, int dc)[] NeighborOffsets =
        {
            (-1, -1), (-1, 0), (-1, 1),
            (0, -1), (0, 1),
            (1, -1), (1, 0), (1, 1)
        };

        private enum Phase
        {
            RevealCat0,
            TeachRowColumn,
            RevealCat1,
            TeachTouching,
            RevealCat2,
            TeachColor
        }

        private readonly struct SubGuide
        {
            public readonly Coord Start;
            public readonly Coord End;
            public readonly bool IsDrag;
            public readonly Coord[] Cells;

            public SubGuide(Coord start, Coord end, bool isDrag, Coord[] cells)
            {
                Start = start;
                End = end;
                IsDrag = isDrag;
                Cells = cells;
            }
        }

        private sealed class RuleStep
        {
            public Sprite ReferenceSprite;
            public string Title;
            public string Body;
            public RectTransform Tab;
            public SubGuide[] SubGuides;
        }

        [SerializeField] private TextAsset lessonLevelJson;
        [SerializeField] private TextAsset secondLessonLevelJson;
        [SerializeField] private BoardView boardView;
        [SerializeField] private CatCounter catCounter;

        [Header("Focus overlay (dims board + HUD while a rule card is up)")]
        [SerializeField] private CanvasGroup dimOverlayCanvasGroup;

        [Header("Hand / focus-ring guide")]
        [SerializeField] private RectTransform tutorialGuideRoot;
        [SerializeField] private Image tutorialHandImage;
        [SerializeField] private Animator tutorialHandAnimator;
        [Tooltip("Idle seconds after a SingleClick or DoubleClick clip finishes before the gesture is demonstrated again.")]
        [Min(0f)]
        [SerializeField] private float clickAnimationRepeatDelaySeconds = 0.35f;

        [Header("Step completion VFX")]
        [SerializeField] private ParticleSystem stepCompletionVfx;

        [Tooltip("Left-to-right in the existing 'Rules' strip today: Rule 1 (1) = Row/Column, Rule 1 (2) = Touching, Rule 1 = Color.")]
        [Header("Rule tabs (the existing always-on 'Rules' strip)")]
        [SerializeField] private RectTransform rowColumnTab;
        [SerializeField] private RectTransform touchingTab;
        [SerializeField] private RectTransform colorTab;

        [Header("Reference sprites")]
        [SerializeField] private Sprite rowColumnSprite;
        [SerializeField] private Sprite touchingSprite;
        [SerializeField] private Sprite colorSprite;

        [Header("Rule card (one reusable card, popped in/out per rule)")]
        [SerializeField] private RectTransform ruleCardRoot;
        [SerializeField] private CanvasGroup ruleCardCanvasGroup;
        [SerializeField] private Image ruleCardImage;
        [SerializeField] private TMP_Text ruleCardTitleText;
        [SerializeField] private TMP_Text ruleCardBodyText;
        [Tooltip("Optional - a Febucci TypewriterComponent on the same object as ruleCardBodyText. Types the rule explanation out instead of showing it instantly, which also naturally paces the reveal.")]
        [SerializeField] private TypewriterComponent ruleCardBodyTypewriter;
        [Tooltip("1 = Febucci's default typing speed. Higher = faster.")]
        [SerializeField] private float typewriterSpeedMultiplier = 2.5f;
        [Tooltip("Seconds the fully revealed rule card remains on screen before it docks into the Rules strip and fades away.")]
        [Min(0f)]
        [SerializeField] private float ruleCardHoldSeconds = 2f;

        [Header("Info card (one-off instruction, e.g. \"Double Tap To Reveal The Cat\" - pops in, types, then waits for step completion)")]
        [SerializeField] private RectTransform infoCardRoot;
        [SerializeField] private CanvasGroup infoCardCanvasGroup;
        [SerializeField] private TMP_Text infoCardText;
        [Tooltip("Optional - a Febucci TypewriterComponent on the same object as infoCardText. Types the instruction out instead of showing it instantly.")]
        [SerializeField] private TypewriterComponent infoCardTypewriter;

        private PuzzleBoard board;
        private Coord[] catCoords;
        private RuleStep rowColumnStep;
        private RuleStep touchingStep;
        private RuleStep colorStep;
        private Phase phase;
        private int ruleSubGuideIndex;
        private bool lessonActive;
        private Action onLessonComplete;
        private Vector2 ruleCardRestAnchoredPosition;
        private Vector3 ruleCardRestScale;
        private Sequence cardSequence;
        private Tween dimOverlayTween;
        private Coroutine guideRoutine;
        private Tween guideFadeTween;
        private bool guideVisible;
        private bool guideHasPosition;
        private Vector2 guideSmoothedCenter;
        private float guideSmoothedCellSize;
        private bool phaseTransitionPending;
        private Coord? hintedCatCoord;
        private Coord[] hintedCrossCells = Array.Empty<Coord>();
        private Sequence infoCardSequence;
        private bool infoCardActive;
        private Coroutine infoCardShowRoutine;
        private bool waitingForFinalCatInfoDismiss;
        private bool cardRestPoseCaptured;
        private int activeLessonIndex;
        private bool handIsPressed;
        private readonly HashSet<int> missingHandTriggerWarnings = new HashSet<int>();
        private readonly Dictionary<string, float> handClipLengths = new Dictionary<string, float>();

        public bool ShouldPlay => HasLesson(0) && !PlayerPrefs.HasKey(LessonSeenPlayerPrefsKey);
        public bool HasNextLesson => HasLesson(activeLessonIndex + 1);
        public bool IsFinalCatSearchActive =>
            !lessonActive
            && waitingForFinalCatInfoDismiss
            && board != null
            && board.RevealedCatCount() < board.Size;

        public PuzzleBoard Board => board;

        /// <summary>True while a double-tap should be allowed to commit the current scripted cat -
        /// false while a rule's crossing cells are the only allowed input, so an accidental double-tap
        /// on a crossing target can't reveal it as a miss and permanently lock that cell out of being
        /// crossed.</summary>
        public bool AllowsCommit { get; private set; }

        public bool AllowsCommitAt(int row, int column)
        {
            return lessonActive && AllowsCommit && IsCurrentRevealCat(new Coord(row, column));
        }

        public void MarkLessonsSeen()
        {
            PlayerPrefs.SetInt(LessonSeenPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (tutorialHandAnimator == null && tutorialHandImage != null)
            {
                tutorialHandAnimator = tutorialHandImage.GetComponent<Animator>();
            }

            if (tutorialHandAnimator != null)
            {
                tutorialHandAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                CacheHandClipLengths(tutorialHandAnimator);
            }

            if (tutorialHandImage != null)
            {
                tutorialHandImage.gameObject.SetActive(false);
            }
            if (ruleCardBodyTypewriter != null)
            {
                ruleCardBodyTypewriter.onTextShowed.AddListener(OnRuleCardBodyTextFullyShown);
            }

            if (infoCardTypewriter != null)
            {
                infoCardTypewriter.onTextShowed.AddListener(OnInfoCardTextFullyShown);
            }

            ResetRuleCardBodyText();
            ResetInfoCardText();
            SetGuideRaycastTargets(false);
        }

        /// <summary>
        /// Parses the bespoke lesson board and returns it, but does not start teaching yet - GameManager
        /// needs this board to rebuild BoardView's cells (and refresh/play its intro) *before*
        /// <see cref="Begin"/> starts the hand-guide coroutine. That coroutine queries BoardView for
        /// cell positions on its very first (synchronous) tick, so if the cells don't exist yet it
        /// null-refs immediately.
        /// </summary>
        public bool HasLesson(int lessonIndex)
        {
            return GetLessonJson(lessonIndex) != null;
        }

        public PuzzleBoard PrepareBoard(int lessonIndex)
        {
            TextAsset selectedLessonJson = GetLessonJson(lessonIndex);
            if (selectedLessonJson == null)
            {
                return null;
            }

            activeLessonIndex = lessonIndex;
            LevelData data = JsonUtility.FromJson<LevelData>(selectedLessonJson.text);
            Level level = data.ToLevel();
            board = new PuzzleBoard(level);
            catCoords = level.Solution;
            return board;
        }

        private TextAsset GetLessonJson(int lessonIndex)
        {
            return lessonIndex switch
            {
                0 => lessonLevelJson,
                1 => secondLessonLevelJson,
                _ => null,
            };
        }

        /// <summary>Begins teaching the three rules in order. Call only after the board returned by
        /// <see cref="PrepareBoard"/> has already been rebuilt/refreshed into BoardView's cells.</summary>
        public void Begin(Action onComplete)
        {
            if (board == null)
            {
                onComplete?.Invoke();
                return;
            }

            onLessonComplete = onComplete;
            ResetPresentationForNextLesson();
            tutorialHandImage.gameObject.SetActive(true);
            if (!cardRestPoseCaptured)
            {
                ruleCardRestAnchoredPosition = ruleCardRoot.anchoredPosition;
                ruleCardRestScale = ruleCardRoot.localScale;
                cardRestPoseCaptured = true;
            }

            ruleCardRoot.anchoredPosition = ruleCardRestAnchoredPosition;
            ruleCardRoot.localScale = ruleCardRestScale;

            BuildRuleSteps();
            HideAllTabs();
            SetDimOverlayImmediate(false);
            SetCatCounterVisible(false);
            waitingForFinalCatInfoDismiss = false;

            lessonActive = true;
            EnterPhase(Phase.RevealCat0);
        }

        private void ResetPresentationForNextLesson()
        {
            cardSequence?.Kill();
            cardSequence = null;
            ruleCardRoot.gameObject.SetActive(false);
            ResetRuleCardBodyText();

            if (infoCardShowRoutine != null)
            {
                StopCoroutine(infoCardShowRoutine);
                infoCardShowRoutine = null;
            }

            infoCardSequence?.Kill();
            infoCardSequence = null;
            infoCardActive = false;
            if (infoCardRoot != null)
            {
                infoCardRoot.gameObject.SetActive(false);
            }

            ResetInfoCardText();
            phaseTransitionPending = false;
            waitingForFinalCatInfoDismiss = false;
        }

        public void OnCatRevealCompleted(Coord cat)
        {
            if (board == null)
            {
                return;
            }

            if (lessonActive)
            {
                if (IsCurrentRevealCat(cat))
                {
                    HideInfoCard();
                }

                return;
            }

            if (!waitingForFinalCatInfoDismiss || board.RevealedCatCount() < board.Size)
            {
                return;
            }

            waitingForFinalCatInfoDismiss = false;
            HideInfoCard();
        }

        /// <summary>Call whenever the lesson board's marks change (from GameManager's own input path while the lesson is active).</summary>
        public void OnBoardChanged()
        {
            if (!lessonActive || board == null || phaseTransitionPending)
            {
                return;
            }

            switch (phase)
            {
                case Phase.RevealCat0:
                    CheckRevealCat(catCoords[0], Phase.TeachRowColumn);
                    break;
                case Phase.TeachRowColumn:
                    AdvanceRuleStep(rowColumnStep, Phase.RevealCat1);
                    break;
                case Phase.RevealCat1:
                    CheckRevealCat(catCoords[1], Phase.TeachTouching);
                    break;
                case Phase.TeachTouching:
                    AdvanceRuleStep(touchingStep, Phase.RevealCat2);
                    break;
                case Phase.RevealCat2:
                    CheckRevealCat(catCoords[2], Phase.TeachColor);
                    break;
                case Phase.TeachColor:
                    AdvanceRuleStep(colorStep, null);
                    break;
            }
        }

        private void CheckRevealCat(Coord cat, Phase nextPhase)
        {
            if (!board.HasRevealedCat(cat.Row, cat.Column))
            {
                return;
            }

            ClearHints();

            // Restriction stays on the just-revealed cell during the beat - a revealed cell is inert
            // (can't be toggled or re-committed), so it's a safe "nothing tappable" state.
            BeginPhaseTransition(nextPhase);
        }

        private void AdvanceRuleStep(RuleStep step, Phase? nextPhase)
        {
            SubGuide guide = step.SubGuides[ruleSubGuideIndex];
            if (!AllCrossed(guide.Cells))
            {
                if (guide.IsDrag && AnyCrossed(guide.Cells))
                {
                    PointAtSubGuide(step);
                }

                return;
            }

            ruleSubGuideIndex++;
            if (ruleSubGuideIndex < step.SubGuides.Length)
            {
                PointAtSubGuide(step);
                return;
            }

            ClearHints();
            PlayStepCompletionEffect();

            // Empty allowed set (not ClearTutorialRestriction) during the beat: the freshly crossed
            // cells must not be tappable, or a quick tap could un-cross one after the step already
            // counted it as done.
            boardView.SetTutorialRestriction(Array.Empty<Coord>(), TutorialRequiredCells());
            boardView.RefreshVisuals(board, false);
            BeginPhaseTransition(nextPhase);
        }

        /// <summary>
        /// The breath between phases: fades the hand guide out, waits a beat, then enters the next
        /// phase (or finishes) - instead of snapping everything on the exact frame a condition is met.
        /// OnBoardChanged is gated on <see cref="phaseTransitionPending"/> so board refreshes landing
        /// mid-beat can't double-advance.
        /// </summary>
        private void BeginPhaseTransition(Phase? nextPhase)
        {
            phaseTransitionPending = true;
            FadeOutGuide();
            DOVirtual.DelayedCall(PhaseTransitionSeconds, () =>
            {
                phaseTransitionPending = false;
                if (!lessonActive)
                {
                    return;
                }

                if (nextPhase.HasValue)
                {
                    EnterPhase(nextPhase.Value);
                }
                else
                {
                    FinishLesson();
                }
            }, false).SetUpdate(true);
        }

        private bool AllCrossed(Coord[] cells)
        {
            foreach (Coord cell in cells)
            {
                if (board.GetMark(cell.Row, cell.Column) != CellMark.Cross)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyCrossed(Coord[] cells)
        {
            foreach (Coord cell in cells)
            {
                if (board.GetMark(cell.Row, cell.Column) == CellMark.Cross)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnterPhase(Phase next)
        {
            phase = next;

#if USE_AVNADS_PLUGIN
            // Phase's declaration order IS the 6-step order the analytics wants (RevealCat0..TeachColor
            // = first cat click, first rule play, second cat click, second rule play, third cat click,
            // third rule play) - one call site instead of one per case below.
            GameAnalyticsEvents.TutorialAnalysis(activeLessonIndex, (int)phase + 1);
#endif

            switch (phase)
            {
                case Phase.RevealCat0:
                    BeginRevealCat(catCoords[0]);
                    break;
                case Phase.TeachRowColumn:
                    ruleSubGuideIndex = 0;
                    BeginRuleIntro(rowColumnStep);
                    break;
                case Phase.RevealCat1:
                    BeginRevealCat(catCoords[1]);
                    break;
                case Phase.TeachTouching:
                    ruleSubGuideIndex = 0;
                    BeginRuleIntro(touchingStep);
                    break;
                case Phase.RevealCat2:
                    BeginRevealCat(catCoords[2]);
                    break;
                case Phase.TeachColor:
                    ruleSubGuideIndex = 0;
                    BeginRuleIntro(colorStep);
                    break;
            }
        }

        private void BeginRevealCat(Coord cat)
        {
            AllowsCommit = true;
            boardView.SetTutorialRestriction(new[] { cat }, TutorialRequiredCells());
            boardView.SetTutorialInteractionEnabled(true);
            boardView.RefreshVisuals(board, false);
            ShowRevealGuide(cat);
            ShowInfoCard(LocalizationService.Get("tutorial.info.revealCat"));
        }

        private void BeginRuleIntro(RuleStep step)
        {
            AllowsCommit = false;
            boardView.ClearTutorialRestriction();
            boardView.RefreshVisuals(board, false);
            PlayCurrentStepIntro(step);
        }

        private RuleStep CurrentRuleStepOrNull()
        {
            return phase switch
            {
                Phase.TeachRowColumn => rowColumnStep,
                Phase.TeachTouching => touchingStep,
                Phase.TeachColor => colorStep,
                _ => null,
            };
        }

        private void PlayCurrentStepIntro(RuleStep step)
        {
            ruleCardImage.sprite = step.ReferenceSprite;
            ruleCardTitleText.text = step.Title;

            ruleCardRoot.gameObject.SetActive(true);
            ruleCardRoot.anchoredPosition = ruleCardRestAnchoredPosition;
            ruleCardRoot.localScale = ruleCardRestScale * CardStartScale;
            ruleCardCanvasGroup.alpha = 0f;

            SetDimOverlay(true, DimOverlayFadeSeconds);

            cardSequence?.Kill();
            cardSequence = DOTween.Sequence().SetUpdate(true);
            cardSequence.Append(ruleCardCanvasGroup.DOFade(1f, CardPopSeconds));
            cardSequence.Join(ruleCardRoot.DOScale(ruleCardRestScale, CardPopSeconds).SetEase(Ease.OutBack));
            // Start the typewriter exactly when the card becomes visible - starting it any earlier
            // would let it finish typing while still invisible (see the same fix in LevelCompleteScreen).
            cardSequence.OnComplete(() => BeginRuleCardBodyText(step));
        }

        private void BeginRuleCardBodyText(RuleStep step)
        {
            if (ruleCardBodyTypewriter != null)
            {
                ruleCardBodyTypewriter.SetTypewriterSpeed(typewriterSpeedMultiplier);
                ruleCardBodyTypewriter.ShowText(step.Body);
            }
            else
            {
                if (ruleCardBodyText != null)
                {
                    ruleCardBodyText.text = step.Body;
                }

                ScheduleRuleCardDock(step);
            }
        }

        /// <summary>
        /// Clears the body text the moment the card is hidden (not lazily right before the next show) -
        /// resetting on show left the *previous* rule's full body text sitting on the TMP component
        /// during the next pop-in fade, visible for a beat before <see cref="BeginRuleCardBodyText"/>
        /// finally overwrote it with the new text.
        /// </summary>
        private void ResetRuleCardBodyText()
        {
            if (ruleCardBodyTypewriter != null && ruleCardBodyTypewriter.gameObject.activeInHierarchy)
            {
                ruleCardBodyTypewriter.StopShowingText();
            }

            if (ruleCardBodyText != null)
            {
                ruleCardBodyText.text = string.Empty;
            }
        }

        private void OnRuleCardBodyTextFullyShown()
        {
            if (!lessonActive)
            {
                return;
            }

            RuleStep step = CurrentRuleStepOrNull();
            if (step == null)
            {
                return;
            }

            ScheduleRuleCardDock(step);
        }

        private void ScheduleRuleCardDock(RuleStep step)
        {
            DOVirtual.DelayedCall(
                    Mathf.Max(0f, ruleCardHoldSeconds),
                    () => PlayCurrentStepDock(step),
                    false)
                .SetUpdate(true);
        }

        /// <summary>
        /// Shrinks/moves the card toward the real tab's on-screen position and crossfades into it,
        /// rather than reparenting the real tab out of its HorizontalLayoutGroup - simpler and immune
        /// to that layout group fighting a reparented tab's tween every frame.
        /// </summary>
        private void PlayCurrentStepDock(RuleStep step)
        {
            CanvasGroup tabGroup = ResolveCanvasGroup(step.Tab);
            Vector2 targetAnchoredPosition = TargetLocalPosition(step.Tab);

            cardSequence?.Kill();
            cardSequence = DOTween.Sequence().SetUpdate(true);
            cardSequence.Append(ruleCardRoot.DOAnchorPos(targetAnchoredPosition, CardDockSeconds).SetEase(Ease.InOutSine));
            cardSequence.Join(ruleCardRoot.DOScale(ruleCardRestScale * TabDockScale, CardDockSeconds).SetEase(Ease.InOutSine));
            cardSequence.Insert(CardDockSeconds * 0.6f, ruleCardCanvasGroup.DOFade(0f, CardDockSeconds * 0.4f));
            if (tabGroup != null)
            {
                cardSequence.Insert(CardDockSeconds * 0.6f, tabGroup.DOFade(1f, CardDockSeconds * 0.4f));
            }

            // Full-screen dim only lifts once the card is fully docked - keeping the board hidden for
            // the whole dock motion instead of exposing it undimmed for the tail of the move, since the
            // per-cell tutorial dim/guide (started by PointAtSubGuide below) doesn't kick in until then either.
            cardSequence.OnComplete(() =>
            {
                ruleCardRoot.gameObject.SetActive(false);
                ResetRuleCardBodyText();
                SetDimOverlay(false, DimOverlayFadeSeconds);
                ruleSubGuideIndex = 0;
                PointAtSubGuide(step);
            });
        }

        private Vector2 TargetLocalPosition(RectTransform target)
        {
            RectTransform cardParent = ruleCardRoot.parent as RectTransform;
            if (target == null || cardParent == null)
            {
                return ruleCardRestAnchoredPosition;
            }

            Camera camera = cardParent.GetComponentInParent<Canvas>()?.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cardParent, screenPoint, camera, out Vector2 localPoint);
            return localPoint;
        }

        // ---- info card (transient instruction, e.g. "Double Tap To Reveal The Cat" - pops in, types,
        // then fades only when the step it describes is actually completed) ----

        /// <summary>
        /// Defers the actual show by one frame. The very first call happens synchronously inside
        /// GameManager.Start() (before the scene has rendered a single frame) - unlike the hand-guide,
        /// which is a looping coroutine that keeps re-asserting itself every frame and so self-corrects
        /// from any frame-0 hiccup, this is a one-shot animation that finishes and hides itself once,
        /// so a frame-0 rendering/layout hiccup on that first call was never getting a second chance.
        /// </summary>
        private void ShowInfoCard(string text)
        {
            if (infoCardShowRoutine != null)
            {
                StopCoroutine(infoCardShowRoutine);
            }

            infoCardShowRoutine = StartCoroutine(ShowInfoCardNextFrame(text));
        }

        private IEnumerator ShowInfoCardNextFrame(string text)
        {
            yield return null;
            infoCardShowRoutine = null;
            ShowInfoCardImmediate(text);
        }

        private void ShowInfoCardImmediate(string text)
        {
            if (infoCardRoot == null || infoCardCanvasGroup == null)
            {
                return;
            }

            infoCardSequence?.Kill();
            infoCardActive = true;

            infoCardRoot.gameObject.SetActive(true);
            infoCardRoot.localScale = Vector3.one * CardStartScale;
            infoCardCanvasGroup.alpha = 0f;

            infoCardSequence = DOTween.Sequence().SetUpdate(true);
            infoCardSequence.Append(infoCardCanvasGroup.DOFade(1f, InfoCardPopSeconds));
            infoCardSequence.Join(infoCardRoot.DOScale(1f, InfoCardPopSeconds).SetEase(Ease.OutBack));
            // Start the typewriter exactly when the card becomes visible - same reasoning as the rule card's body text.
            infoCardSequence.OnComplete(() => BeginInfoCardText(text));
        }

        private void BeginInfoCardText(string text)
        {
            if (infoCardTypewriter != null)
            {
                infoCardTypewriter.SetTypewriterSpeed(typewriterSpeedMultiplier);
                infoCardTypewriter.ShowText(text);
            }
            else
            {
                if (infoCardText != null)
                {
                    infoCardText.text = text;
                }
            }
        }

        private void OnInfoCardTextFullyShown()
        {
            // Info cards intentionally stay visible until the step's completion callback hides them.
        }

        private void HideInfoCard()
        {
            if (infoCardShowRoutine != null)
            {
                StopCoroutine(infoCardShowRoutine);
                infoCardShowRoutine = null;
            }

            if (!infoCardActive || infoCardRoot == null)
            {
                return;
            }

            infoCardActive = false;
            infoCardSequence?.Kill();
            infoCardSequence = DOTween.Sequence().SetUpdate(true);
            infoCardSequence.Append(infoCardCanvasGroup.DOFade(0f, InfoCardFadeOutSeconds));
            infoCardSequence.OnComplete(() =>
            {
                infoCardRoot.gameObject.SetActive(false);
                ResetInfoCardText();
            });
        }

        /// <summary>
        /// Clears the instruction text the moment the card is hidden (not lazily right before the next
        /// show) - same reasoning as <see cref="ResetRuleCardBodyText"/>.
        /// </summary>
        private void ResetInfoCardText()
        {
            if (infoCardTypewriter != null && infoCardTypewriter.gameObject.activeInHierarchy)
            {
                infoCardTypewriter.StopShowingText();
            }

            if (infoCardText != null)
            {
                infoCardText.text = string.Empty;
            }
        }

        private void PointAtSubGuide(RuleStep step)
        {
            SubGuide guide = step.SubGuides[ruleSubGuideIndex];
            Coord[] remainingCells = RemainingGuideCells(guide);
            Coord[] allowed = remainingCells.Length > 0 ? remainingCells : guide.Cells.Length > 0 ? guide.Cells : new[] { guide.Start };
            boardView.SetTutorialRestriction(allowed, TutorialRequiredCells());
            boardView.SetTutorialInteractionEnabled(true);
            boardView.RefreshVisuals(board, false);
            ShowCrossGuide(guide, remainingCells);
        }

        private Coord[] RemainingGuideCells(SubGuide guide)
        {
            Coord[] cells = guide.Cells.Length > 0 ? guide.Cells : new[] { guide.Start };
            List<Coord> remainingCells = new List<Coord>(cells.Length);
            foreach (Coord cell in cells)
            {
                if (board.GetMark(cell.Row, cell.Column) != CellMark.Cross)
                {
                    remainingCells.Add(cell);
                }
            }

            return remainingCells.ToArray();
        }

        private HashSet<Coord> TutorialRequiredCells()
        {
            HashSet<Coord> cells = new HashSet<Coord>();
            AddTutorialRequiredCells(rowColumnStep, cells);
            AddTutorialRequiredCells(touchingStep, cells);
            AddTutorialRequiredCells(colorStep, cells);
            return cells;
        }

        private void AddTutorialRequiredCells(RuleStep step, HashSet<Coord> cells)
        {
            if (step == null || step.SubGuides == null)
            {
                return;
            }

            foreach (SubGuide guide in step.SubGuides)
            {
                foreach (Coord cell in guide.Cells)
                {
                    cells.Add(cell);
                }
            }
        }

        private void HideAllTabs()
        {
            SetAlpha(rowColumnTab, 0f);
            SetAlpha(touchingTab, 0f);
            SetAlpha(colorTab, 0f);
        }

        private void SetGuideRaycastTargets(bool raycastTarget)
        {
            if (tutorialGuideRoot == null)
            {
                return;
            }

            Graphic[] graphics = tutorialGuideRoot.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                graphic.raycastTarget = raycastTarget;
            }
        }

        private static void SetAlpha(RectTransform rect, float alpha)
        {
            CanvasGroup group = ResolveCanvasGroup(rect);
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private void SetDimOverlay(bool visible, float seconds)
        {
            if (dimOverlayCanvasGroup == null)
            {
                return;
            }

            dimOverlayTween?.Kill();
            dimOverlayCanvasGroup.gameObject.SetActive(true);
            dimOverlayCanvasGroup.blocksRaycasts = visible;
            dimOverlayTween = dimOverlayCanvasGroup.DOFade(visible ? 1f : 0f, seconds)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (!visible)
                    {
                        dimOverlayCanvasGroup.gameObject.SetActive(false);
                    }
                });
        }

        private void SetDimOverlayImmediate(bool visible)
        {
            if (dimOverlayCanvasGroup == null)
            {
                return;
            }

            dimOverlayTween?.Kill();
            dimOverlayCanvasGroup.alpha = visible ? 1f : 0f;
            dimOverlayCanvasGroup.blocksRaycasts = visible;
            dimOverlayCanvasGroup.gameObject.SetActive(visible);
        }

        private void PlayStepCompletionEffect()
        {
            if (stepCompletionVfx == null)
            {
                return;
            }

            if (!stepCompletionVfx.gameObject.activeSelf)
            {
                stepCompletionVfx.gameObject.SetActive(true);
            }

            stepCompletionVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            stepCompletionVfx.Play(true);
        }

        private void FinishLesson()
        {
            lessonActive = false;
            StopGuide();
            boardView.ClearTutorialRestriction();
            boardView.RefreshVisuals(board, false);
            waitingForFinalCatInfoDismiss = true;
            ShowInfoCard(LocalizationService.Get("tutorial.info.findLastCat"));
            if (!HasNextLesson)
            {
                MarkLessonsSeen();
            }
            onLessonComplete?.Invoke();
            SetCatCounterVisible(true);
        }

        private bool IsCurrentRevealCat(Coord cat)
        {
            return phase switch
            {
                Phase.RevealCat0 => cat.Equals(catCoords[0]),
                Phase.RevealCat1 => cat.Equals(catCoords[1]),
                Phase.RevealCat2 => cat.Equals(catCoords[2]),
                _ => false,
            };
        }

        private static CanvasGroup ResolveCanvasGroup(RectTransform rect)
        {
            if (rect == null)
            {
                return null;
            }

            if (!rect.TryGetComponent(out CanvasGroup group))
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void SetCatCounterVisible(bool visible)
        {
            CatCounter counter = ResolveCatCounter();
            if (counter != null)
            {
                counter.SetVisible(visible);
            }
        }

        private CatCounter ResolveCatCounter()
        {
            if (catCounter != null)
            {
                return catCounter;
            }

            catCounter = UnityEngine.Object.FindFirstObjectByType<CatCounter>(FindObjectsInactive.Include);
            return catCounter;
        }

        // ---- hand / focus-ring guide ----

        private void ShowRevealGuide(Coord cat)
        {
            ClearHints();
            SetCatHint(cat, true);
            hintedCatCoord = cat;

            if (tutorialGuideRoot == null)
            {
                return;
            }

            ActivateGuideWithFadeIn();
            StopGuideRoutine();
            guideRoutine = StartCoroutine(AnimateRevealGuide(cat));
        }

        private void ShowCrossGuide(SubGuide guide, Coord[] cellsToHint = null)
        {
            ClearHints();
            Coord[] cells = cellsToHint != null && cellsToHint.Length > 0 ? cellsToHint : guide.Cells.Length > 0 ? guide.Cells : new[] { guide.Start };
            SetCrossHint(cells, true);
            hintedCrossCells = cells;

            if (tutorialGuideRoot == null)
            {
                return;
            }

            ActivateGuideWithFadeIn();
            StopGuideRoutine();
            guideRoutine = StartCoroutine(AnimateGuide(guide));
        }

        /// <summary>Stops any active cat/cross fade hint(s) on their currently hinted cell(s).</summary>
        private void ClearHints()
        {
            if (hintedCatCoord.HasValue)
            {
                SetCatHint(hintedCatCoord.Value, false);
                hintedCatCoord = null;
            }

            if (hintedCrossCells.Length > 0)
            {
                SetCrossHint(hintedCrossCells, false);
                hintedCrossCells = Array.Empty<Coord>();
            }
        }

        private void SetCatHint(Coord cat, bool active)
        {
            GridCell cell = boardView.GetCellView(cat.Row, cat.Column);
            if (cell != null)
            {
                cell.SetCatHint(active);
            }
        }

        private void SetCrossHint(Coord[] cells, bool active)
        {
            foreach (Coord coord in cells)
            {
                GridCell cell = boardView.GetCellView(coord.Row, coord.Column);
                if (cell != null)
                {
                    cell.SetCrossHint(active);
                }
            }
        }

        /// <summary>
        /// Activates the guide root and fades it in. If it was already visible (moving between
        /// sub-guides within one step), the fade is a no-op-ish top-up and - crucially -
        /// <see cref="guideHasPosition"/> is kept, so the hand *glides* to the new cell via the
        /// smoothing in <see cref="PlaceHand"/> instead of teleporting.
        /// </summary>
        private void ActivateGuideWithFadeIn()
        {
            CanvasGroup group = ResolveCanvasGroup(tutorialGuideRoot);
            tutorialGuideRoot.gameObject.SetActive(true);

            guideFadeTween?.Kill();
            if (!guideVisible)
            {
                guideHasPosition = false;
                if (group != null)
                {
                    group.alpha = 0f;
                }
            }

            guideVisible = true;
            if (group != null)
            {
                guideFadeTween = group.DOFade(1f, GuideFadeSeconds).SetUpdate(true);
            }
        }

        /// <summary>Fades the guide out, then deactivates it - the smooth counterpart of <see cref="StopGuide"/>.</summary>
        private void FadeOutGuide()
        {
            if (tutorialGuideRoot == null || !guideVisible)
            {
                return;
            }

            guideVisible = false;
            ReleaseHand();
            guideFadeTween?.Kill();

            CanvasGroup group = ResolveCanvasGroup(tutorialGuideRoot);
            if (group == null)
            {
                StopGuide();
                return;
            }

            guideFadeTween = group.DOFade(0f, GuideFadeSeconds).SetUpdate(true).OnComplete(() =>
            {
                StopGuideRoutine();
                tutorialGuideRoot.gameObject.SetActive(false);
                guideHasPosition = false;
            });
        }

        public void StopGuide()
        {
            ClearHints();
            ReleaseHand();
            StopGuideRoutine();
            guideFadeTween?.Kill();
            guideVisible = false;
            guideHasPosition = false;
            if (tutorialGuideRoot != null)
            {
                tutorialGuideRoot.gameObject.SetActive(false);
            }

        }

        private void StopGuideRoutine()
        {
            if (guideRoutine != null)
            {
                StopCoroutine(guideRoutine);
                guideRoutine = null;
            }
        }

        private IEnumerator AnimateRevealGuide(Coord cat)
        {
            float repeatInterval = GetHandGestureRepeatInterval("double click", 1.1f);

            float cycleStartedAt = Time.unscaledTime;
            int playedCycle = -1;

            while (tutorialGuideRoot != null && tutorialGuideRoot.gameObject.activeSelf)
            {
                int cycle = Mathf.FloorToInt((Time.unscaledTime - cycleStartedAt) / repeatInterval);
                if (cycle != playedCycle)
                {
                    PlayHandTrigger(DoubleClickTrigger, "DoubleClick");
                    playedCycle = cycle;
                }

                PositionGuideAt(cat);
                yield return null;
            }

            guideRoutine = null;
        }

        private IEnumerator AnimateGuide(SubGuide guide)
        {
            float singleClickRepeatInterval = GetHandGestureRepeatInterval("single click", 0.55f);
            float cycleStartedAt = Time.unscaledTime;
            int activeCycle = -1;
            bool releasedThisCycle = false;

            while (tutorialGuideRoot != null && tutorialGuideRoot.gameObject.activeSelf)
            {
                float cycleSeconds = guide.IsDrag ? DragCycleSeconds : singleClickRepeatInterval;
                float elapsed = Time.unscaledTime - cycleStartedAt;
                int cycle = Mathf.FloorToInt(elapsed / cycleSeconds);
                float phase01 = Mathf.Repeat(elapsed, cycleSeconds) / cycleSeconds;

                if (cycle != activeCycle)
                {
                    if (guide.IsDrag)
                    {
                        PressHand();
                        releasedThisCycle = false;
                    }
                    else
                    {
                        PlayHandTrigger(SingleClickTrigger, "SingleClick");
                    }

                    activeCycle = cycle;
                }

                float travel = 0f;
                if (guide.IsDrag)
                {
                    travel = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01((phase01 - DragTravelStart) / (DragTravelEnd - DragTravelStart)));

                    if (!releasedThisCycle && phase01 >= DragReleaseTime)
                    {
                        ReleaseHand();
                        releasedThisCycle = true;
                    }
                }

                PositionGuide(guide, travel);
                yield return null;
            }

            guideRoutine = null;
        }
        private void PositionGuide(SubGuide guide, float travel)
        {
            if (!boardView.TryGetCellCenterIn(tutorialGuideRoot, guide.Start, out Vector2 startCenter, out float startSize)
                || !boardView.TryGetCellCenterIn(tutorialGuideRoot, guide.End, out Vector2 endCenter, out _))
            {
                return;
            }

            Vector2 center = Vector2.Lerp(startCenter, endCenter, Mathf.Clamp01(travel));
            PlaceHand(center, startSize);
        }

        private void PositionGuideAt(Coord coord)
        {
            if (!boardView.TryGetCellCenterIn(tutorialGuideRoot, coord, out Vector2 center, out float cellSize))
            {
                return;
            }

            PlaceHand(center, cellSize);
        }

        private void PlaceHand(Vector2 center, float cellSize)
        {
            // Exponential smoothing toward the target: when the target cell changes (next sub-guide),
            // the hand/ring glide over instead of teleporting. First placement after a fade-in snaps.
            if (!guideHasPosition)
            {
                guideSmoothedCenter = center;
                guideSmoothedCellSize = cellSize;
                guideHasPosition = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-GuideMoveLerpSpeed * Time.unscaledDeltaTime);
                guideSmoothedCenter = Vector2.Lerp(guideSmoothedCenter, center, t);
                guideSmoothedCellSize = Mathf.Lerp(guideSmoothedCellSize, cellSize, t);
            }

            center = guideSmoothedCenter;
            cellSize = guideSmoothedCellSize;

            RectTransform handRect = tutorialHandImage.rectTransform;
            float offsetMagnitude = cellSize * 0.34f;
            handRect.anchoredPosition = center + new Vector2(offsetMagnitude, -offsetMagnitude);
        }

        private void PressHand()
        {
            if (!handIsPressed && PlayHandTrigger(FingerDownTrigger, "FingerDown"))
            {
                handIsPressed = true;
            }
        }

        private void ReleaseHand()
        {
            if (!handIsPressed)
            {
                return;
            }

            PlayHandTrigger(FingerUpTrigger, "FingerUp");
            handIsPressed = false;
        }

        /// <summary>
        /// Reads each clip's real length off the Animator Controller once at startup, so the
        /// SingleClick/DoubleClick retrigger cadence below is derived from how long the clip
        /// actually plays rather than a hand-guessed constant that silently drifts out of sync
        /// the next time an artist re-times "single click.anim"/"double click.anim" - which is
        /// exactly what caused the double-click gesture to keep cutting itself off before it
        /// could finish playing.
        /// </summary>
        private void CacheHandClipLengths(Animator animator)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                return;
            }

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null)
                {
                    handClipLengths[clip.name] = clip.length;
                }
            }
        }

        /// <summary>Seconds between repeats of a one-shot hand gesture: the clip's own length (so the
        /// retrigger never fires while it's still playing) plus <see cref="clickAnimationRepeatDelaySeconds"/>
        /// of idle hold afterward. Falls back to <paramref name="fallbackClipLength"/> if the clip isn't found.</summary>
        private float GetHandGestureRepeatInterval(string clipName, float fallbackClipLength)
        {
            float clipLength = handClipLengths.TryGetValue(clipName, out float length) ? length : fallbackClipLength;
            return clipLength + clickAnimationRepeatDelaySeconds;
        }

        private bool PlayHandTrigger(int triggerHash, string triggerName)
        {
            if (tutorialHandAnimator == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in tutorialHandAnimator.parameters)
            {
                if (parameter.nameHash == triggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    tutorialHandAnimator.ResetTrigger(triggerHash);
                    tutorialHandAnimator.SetTrigger(triggerHash);
                    return true;
                }
            }

            if (missingHandTriggerWarnings.Add(triggerHash))
            {
                Debug.LogWarning($"Tutorial hand Animator is missing the '{triggerName}' trigger.", tutorialHandAnimator);
            }

            return false;
        }

        // ---- lesson board rule-cell derivation (generic - reads the actual board/region data
        // rather than hardcoding coordinates, so it stays correct if the lesson board ever changes) ----

        private void BuildRuleSteps()
        {
            Coord cat0 = catCoords[0];
            Coord cat1 = catCoords[1];
            Coord cat2 = catCoords[2];

            Coord[] columnCells = ColumnCellsExceptCat(cat0);
            Coord[] rowCells = RowCellsExceptCat(cat0);

            HashSet<Coord> covered = new HashSet<Coord>(columnCells);
            covered.UnionWith(rowCells);

            Coord[] touchCells = NeighborCellsExceptCovered(cat1, covered);
            covered.UnionWith(touchCells);

            Coord[] colorCells = RegionCellsExceptCoveredAndCat(cat2, covered);

            rowColumnStep = new RuleStep
            {
                ReferenceSprite = rowColumnSprite,
                Title = LocalizationService.Get("tutorial.rules.rowColumn.title"),
                Body = LocalizationService.Get("tutorial.rules.rowColumn.body"),
                Tab = rowColumnTab,
                SubGuides = new[]
                {
                    new SubGuide(new Coord(0, cat0.Column), new Coord(board.Size - 1, cat0.Column), true, columnCells),
                    new SubGuide(new Coord(cat0.Row, 0), new Coord(cat0.Row, board.Size - 1), true, rowCells),
                },
            };

            touchingStep = new RuleStep
            {
                ReferenceSprite = touchingSprite,
                Title = LocalizationService.Get("tutorial.rules.touching.title"),
                Body = LocalizationService.Get("tutorial.rules.touching.body"),
                Tab = touchingTab,
                SubGuides = TapEachCell(touchCells),
            };

            colorStep = new RuleStep
            {
                ReferenceSprite = colorSprite,
                Title = LocalizationService.Get("tutorial.rules.color.title"),
                Body = LocalizationService.Get("tutorial.rules.color.body"),
                Tab = colorTab,
                SubGuides = TapEachCell(colorCells),
            };
        }

        private static SubGuide[] TapEachCell(Coord[] cells)
        {
            if (cells.Length == 0)
            {
                return new[] { new SubGuide(default, default, false, Array.Empty<Coord>()) };
            }

            SubGuide[] guides = new SubGuide[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                guides[i] = new SubGuide(cells[i], cells[i], false, new[] { cells[i] });
            }

            return guides;
        }

        private Coord[] ColumnCellsExceptCat(Coord cat)
        {
            List<Coord> cells = new List<Coord>();
            for (int row = 0; row < board.Size; row++)
            {
                if (row != cat.Row)
                {
                    cells.Add(new Coord(row, cat.Column));
                }
            }

            return cells.ToArray();
        }

        private Coord[] RowCellsExceptCat(Coord cat)
        {
            List<Coord> cells = new List<Coord>();
            for (int column = 0; column < board.Size; column++)
            {
                if (column != cat.Column)
                {
                    cells.Add(new Coord(cat.Row, column));
                }
            }

            return cells.ToArray();
        }

        private Coord[] NeighborCellsExceptCovered(Coord cat, HashSet<Coord> covered)
        {
            List<Coord> cells = new List<Coord>();
            foreach ((int dr, int dc) in NeighborOffsets)
            {
                int row = cat.Row + dr;
                int column = cat.Column + dc;
                if (row < 0 || row >= board.Size || column < 0 || column >= board.Size)
                {
                    continue;
                }

                Coord coord = new Coord(row, column);
                if (!covered.Contains(coord))
                {
                    cells.Add(coord);
                }
            }

            return cells.ToArray();
        }

        private Coord[] RegionCellsExceptCoveredAndCat(Coord cat, HashSet<Coord> covered)
        {
            int region = board.Level.RegionAt(cat.Row, cat.Column);
            List<Coord> cells = new List<Coord>();
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Coord coord = new Coord(row, column);
                    if (coord.Equals(cat) || board.Level.RegionAt(row, column) != region || covered.Contains(coord))
                    {
                        continue;
                    }

                    cells.Add(coord);
                }
            }

            return cells.ToArray();
        }
    }
}
