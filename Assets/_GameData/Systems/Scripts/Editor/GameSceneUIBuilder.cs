using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Builds the "Neko Canvas" hierarchy (title, rules strip, word slots, board panel,
    /// status text, controls, tutorial panel, juice layer, tutorial guide, win panel) as
    /// real scene content, and wires it onto the Neko Game Controller already placed in the
    /// scene. Replaces what used to be built from scratch in code on every launch.
    ///
    /// Safe to re-run: it destroys and rebuilds any existing "Neko Canvas" first.
    ///
    /// Colors/sizes here are duplicated from NekoGameController's private theme constants
    /// (there's no shared theme asset yet - that's a follow-up step). Keep them in sync.
    /// </summary>
    public static class GameSceneUIBuilder
    {
        private const float BoardPixels = 900f;
        private const float TutorialGuideHandSize = 168f;

        private static readonly Color PageColor = new Color(0.98f, 0.96f, 0.92f, 1f);
        private static readonly Color TileBorderColor = Color.white;
        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color SoftInkColor = new Color(0.43f, 0.39f, 0.36f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color RuleChipColor = new Color(0.84f, 0.76f, 0.62f, 1f);
        private static readonly Color TutorialPanelColor = new Color(0.91f, 0.83f, 0.68f, 0.96f);

        [MenuItem("Meowdoku/Scene/Build Game UI")]
        public static void BuildGameUi()
        {
            NekoGameController controller = Object.FindFirstObjectByType<NekoGameController>();
            if (controller == null)
            {
                Debug.LogError("No Neko Game Controller found in the open scene. Add one first, then run this again.");
                return;
            }

            GameObject existingCanvas = GameObject.Find("Neko Canvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = new GameObject("Neko Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = canvasObject.AddComponent<Image>();
            background.color = PageColor;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Text titleText = CreateText(root, font, "Neko Logic", 58, InkColor);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(900f, 76f));

            Text countText = CreateText(root, font, string.Empty, 34, SoftInkColor);
            SetRect(countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(900f, 48f));

            BuildRulesStrip(root, font);
            RectTransform wordRoot = BuildWordSlotRoot(root);

            RectTransform boardRoot = CreatePanel(root, "Board", TileBorderColor);
            SetRect(boardRoot, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoardPixels, BoardPixels));

            Text statusText = CreateText(root, font, string.Empty, 30, SoftInkColor);
            SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 404f), new Vector2(920f, 56f));

            (Button previousButton, Button undoButton, Button restartButton, Button nextButton) = BuildControls(root, font);
            (RectTransform tutorialPanel, Text tutorialHeaderText, Text tutorialBodyText) = BuildTutorialPanel(root, font);
            RectTransform juiceRoot = BuildJuiceLayer(root);
            (RectTransform tutorialGuideRoot, Image focusRingImage, Image tutorialHandImage, Text tutorialGuideText) = BuildTutorialGuide(root, font);
            (RectTransform winPanel, Text winWordText, Text winSubtitleText) = BuildWinPanel(root, font);

            SerializedObject serializedController = new SerializedObject(controller);
            Assign(serializedController, "boardRoot", boardRoot);
            Assign(serializedController, "wordRoot", wordRoot);
            Assign(serializedController, "juiceRoot", juiceRoot);
            Assign(serializedController, "tutorialGuideRoot", tutorialGuideRoot);
            Assign(serializedController, "tutorialPanel", tutorialPanel);
            Assign(serializedController, "winPanel", winPanel);
            Assign(serializedController, "titleText", titleText);
            Assign(serializedController, "countText", countText);
            Assign(serializedController, "statusText", statusText);
            Assign(serializedController, "tutorialGuideText", tutorialGuideText);
            Assign(serializedController, "tutorialHeaderText", tutorialHeaderText);
            Assign(serializedController, "tutorialBodyText", tutorialBodyText);
            Assign(serializedController, "tutorialHandImage", tutorialHandImage);
            Assign(serializedController, "focusRingImage", focusRingImage);
            Assign(serializedController, "winWordText", winWordText);
            Assign(serializedController, "winSubtitleText", winSubtitleText);
            Assign(serializedController, "previousLevelButton", previousButton);
            Assign(serializedController, "undoButton", undoButton);
            Assign(serializedController, "restartButton", restartButton);
            Assign(serializedController, "nextLevelButton", nextButton);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("Built Neko Canvas and wired it to Neko Game Controller. Save the scene to keep it.");
        }

        private static void BuildRulesStrip(RectTransform root, Font font)
        {
            RectTransform rulesRoot = CreatePanel(root, "Rules", new Color(1f, 1f, 1f, 0f));
            SetRect(rulesRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(940f, 72f));

            HorizontalLayoutGroup layout = rulesRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            CreateRuleChip(rulesRoot, font, "1 Cat in each row and Column");
            CreateRuleChip(rulesRoot, font, "Cats Cannot touch");
            CreateRuleChip(rulesRoot, font, "1 Cat in each color");
        }

        private static void CreateRuleChip(RectTransform parent, Font font, string label)
        {
            RectTransform chip = CreatePanel(parent, label, RuleChipColor);
            Outline outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.36f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text text = CreateText(chip, font, label, 26, InkColor);
            text.raycastTarget = false;
            SetRect(text.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static RectTransform BuildWordSlotRoot(RectTransform root)
        {
            RectTransform wordRoot = CreatePanel(root, "Word Slots", new Color(1f, 1f, 1f, 0f));
            SetRect(wordRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(900f, 86f));
            Image image = wordRoot.GetComponent<Image>();
            image.raycastTarget = false;

            HorizontalLayoutGroup layout = wordRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            return wordRoot;
        }

        private static (Button previous, Button undo, Button restart, Button next) BuildControls(RectTransform root, Font font)
        {
            RectTransform controls = CreatePanel(root, "Controls", new Color(1f, 1f, 1f, 0f));
            SetRect(controls, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(940f, 132f));
            HorizontalLayoutGroup layout = controls.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            Button previous = CreateButtonObject(controls, font, "<");
            Button undo = CreateButtonObject(controls, font, "Undo");
            Button restart = CreateButtonObject(controls, font, "Restart");
            Button next = CreateButtonObject(controls, font, ">");
            return (previous, undo, restart, next);
        }

        private static Button CreateButtonObject(RectTransform parent, Font font, string label)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.19f, 0.17f, 0.16f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText((RectTransform)buttonObject.transform, font, label, label.Length > 5 ? 26 : 34, Color.white);
            text.raycastTarget = false;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static (RectTransform panel, Text header, Text body) BuildTutorialPanel(RectTransform root, Font font)
        {
            RectTransform tutorialPanel = CreatePanel(root, "Tutorial Panel", TutorialPanelColor);
            SetRect(tutorialPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 132f));

            Outline outline = tutorialPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.28f);
            outline.effectDistance = new Vector2(3f, -3f);

            Text header = CreateText(tutorialPanel, font, string.Empty, 24, SoftInkColor);
            SetRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 34f));

            Text body = CreateText(tutorialPanel, font, string.Empty, 24, InkColor);
            SetRect(body.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(0f, 88f));

            tutorialPanel.gameObject.SetActive(false);
            return (tutorialPanel, header, body);
        }

        private static RectTransform BuildJuiceLayer(RectTransform root)
        {
            RectTransform juiceRoot = CreatePanel(root, "Juice Layer", new Color(1f, 1f, 1f, 0f));
            SetRect(juiceRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image image = juiceRoot.GetComponent<Image>();
            image.raycastTarget = false;
            return juiceRoot;
        }

        private static (RectTransform guideRoot, Image focusRing, Image hand, Text guideText) BuildTutorialGuide(RectTransform root, Font font)
        {
            RectTransform tutorialGuideRoot = CreatePanel(root, "Tutorial Guide", new Color(1f, 1f, 1f, 0f));
            SetRect(tutorialGuideRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image rootImage = tutorialGuideRoot.GetComponent<Image>();
            rootImage.raycastTarget = false;

            Image focusRingImage = CreateGuideImage(tutorialGuideRoot, "Focus Ring", new Vector2(170f, 170f));
            Image tutorialHandImage = CreateGuideImage(tutorialGuideRoot, "Tutorial Hand", new Vector2(TutorialGuideHandSize, TutorialGuideHandSize));

            Text tutorialGuideText = CreateText(tutorialGuideRoot, font, string.Empty, 25, InkColor);
            tutorialGuideText.raycastTarget = false;
            SetRect(tutorialGuideText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(920f, 58f));

            tutorialGuideRoot.gameObject.SetActive(false);
            return (tutorialGuideRoot, focusRingImage, tutorialHandImage, tutorialGuideText);
        }

        private static Image CreateGuideImage(RectTransform parent, string name, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return image;
        }

        private static (RectTransform panel, Text word, Text subtitle) BuildWinPanel(RectTransform root, Font font)
        {
            RectTransform winPanel = CreatePanel(root, "Win Word Panel", new Color(1f, 0.99f, 0.96f, 0.96f));
            SetRect(winPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 220f));

            Outline panelOutline = winPanel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = TileBorderColor;
            panelOutline.effectDistance = new Vector2(4f, -4f);

            Text heading = CreateText(winPanel, font, "WORD FOUND", 24, SoftInkColor);
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 42f));

            Text winWordText = CreateText(winPanel, font, string.Empty, 62, InkColor);
            SetRect(winWordText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(0f, 82f));

            Text winSubtitleText = CreateText(winPanel, font, string.Empty, 26, RevealedColor);
            SetRect(winSubtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(0f, 42f));

            winPanel.gameObject.SetActive(false);
            return (winPanel, winWordText, winSubtitleText);
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Color color)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            Image image = panelObject.GetComponent<Image>();
            image.color = color;
            return panelObject.GetComponent<RectTransform>();
        }

        private static Text CreateText(RectTransform parent, Font font, string value, int fontSize, Color color)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Assign(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Neko Game Controller has no serialized field named '{propertyName}'.");
                return;
            }

            property.objectReferenceValue = value;
        }
    }
}
