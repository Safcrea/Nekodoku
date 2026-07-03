using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed partial class NekoGameController : MonoBehaviour
    {
        private const float BoardPixels = 900f;
        private const float BoardIntroCellSeconds = 0.34f;
        private const float BoardIntroDiagonalDelaySeconds = 0.045f;
        private const float LevelCompleteAdvanceSeconds = 1.2f;
        private const float LightHapticCooldownSeconds = 0.045f;
        private const float CatLetterRevealDelaySeconds = 1f;
        private const float CardFlipSeconds = 0.36f;
        private const int TutorialLevelCount = 1;
        private const float TutorialGuideHandSize = 168f;
        private const float TutorialFocusRingPadding = 34f;
        private const float CellPunchSeconds = 0.22f;
        private const float CrossJellySeconds = 0.34f;
        private const float CatFoundPopSeconds = 0.24f;
        private const float LetterFlySeconds = 0.54f;
        private const float SparkleSeconds = 0.58f;
        private const float BoardShakeSeconds = 0.28f;
        private static readonly Color PageColor = new Color(0.98f, 0.96f, 0.92f, 1f);
        private static readonly Color PanelColor = new Color(1f, 0.99f, 0.96f, 1f);
        private static readonly Color TileBorderColor = Color.white;
        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color SoftInkColor = new Color(0.43f, 0.39f, 0.36f, 1f);
        private static readonly Color ConflictColor = new Color(0.94f, 0.24f, 0.2f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color RuleChipColor = new Color(0.84f, 0.76f, 0.62f, 1f);
        private static readonly Color TutorialPanelColor = new Color(0.91f, 0.83f, 0.68f, 0.96f);
        private static readonly Color FocusRingColor = new Color(0.08f, 0.35f, 0.33f, 1f);
        private static readonly Color SparkleGoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color CrossFlashColor = new Color(1f, 0.97f, 0.72f, 1f);
        private static readonly Color[] RegionColors =
        {
            new Color(1.00f, 0.72f, 0.76f, 1f),
            new Color(0.67f, 0.88f, 0.77f, 1f),
            new Color(0.62f, 0.80f, 0.96f, 1f),
            new Color(1.00f, 0.82f, 0.58f, 1f),
            new Color(0.79f, 0.71f, 0.93f, 1f),
            new Color(0.96f, 0.91f, 0.55f, 1f),
            new Color(0.93f, 0.62f, 0.50f, 1f),
            new Color(0.53f, 0.82f, 0.85f, 1f),
            new Color(0.74f, 0.88f, 0.56f, 1f)
        };

        private readonly List<CellUi> cells = new List<CellUi>();
        private readonly Stack<BoardSnapshot> undoStack = new Stack<BoardSnapshot>();
        private readonly HashSet<NekoCoord> letterVisibleCats = new HashSet<NekoCoord>();
        private readonly Dictionary<NekoCoord, Coroutine> letterFlipRoutines = new Dictionary<NekoCoord, Coroutine>();
        private readonly Dictionary<NekoCoord, Coroutine> cellPunchRoutines = new Dictionary<NekoCoord, Coroutine>();
        private readonly Dictionary<NekoCoord, Coroutine> markScaleRoutines = new Dictionary<NekoCoord, Coroutine>();

        private Canvas canvas;
        private RectTransform boardRoot;
        private RectTransform rulesRoot;
        private RectTransform wordRoot;
        private RectTransform juiceRoot;
        private RectTransform tutorialGuideRoot;
        private RectTransform tutorialPanel;
        private RectTransform winPanel;
        private Text titleText;
        private Text countText;
        private Text statusText;
        private Text tutorialGuideText;
        private Text tutorialHeaderText;
        private Text tutorialBodyText;
        private Text[] wordSlotTexts;
        private Image[] wordSlotImages;
        private Image tutorialHandImage;
        private Image focusRingImage;
        private Text winWordText;
        private Text winSubtitleText;
        private Font defaultFont;
        private Sprite nekoCatSprite;
        private Sprite tutorialHandSprite;
        private Sprite focusRingSprite;
        private Sprite juiceDotSprite;
        private NekoPuzzleBoard board;
        private CellUi[,] cellGrid;
        private Coroutine boardIntroRoutine;
        private Coroutine levelAdvanceRoutine;
        private Coroutine catRevealRoutine;
        private Coroutine tutorialPulseRoutine;
        private Coroutine tutorialGuideRoutine;
        private Coroutine boardShakeRoutine;
        private Coroutine winPanelPopRoutine;
        private Vector2 boardRestPosition;
        private TutorialGuideConfig activeTutorialGuideConfig;
        private bool hasActiveTutorialGuideConfig;
        private int levelIndex;
        private bool inputLocked;
        private bool crossDragging;
        private bool crossDragPlacesCrosses;
        private bool crossDragHasUndoSnapshot;
        private int lastCrossDragRow = -1;
        private int lastCrossDragColumn = -1;
        private float lastLightHapticTime = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<NekoGameController>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Neko Logic Prototype");
            host.AddComponent<NekoGameController>();
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            nekoCatSprite = CreateNekoCatSprite();
            tutorialHandSprite = LoadTutorialHandSprite();
            focusRingSprite = CreateFocusRingSprite();
            juiceDotSprite = CreateJuiceDotSprite();
            EnsureEventSystem();
            BuildCanvas();
            LoadLevel(0);
        }
    }
}
