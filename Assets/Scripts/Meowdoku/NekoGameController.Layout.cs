using System;
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
    public sealed partial class NekoGameController
    {
        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Neko Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
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

            titleText = CreateText(root, "Neko Logic", 58, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(900f, 76f));

            countText = CreateText(root, string.Empty, 34, FontStyle.Bold, TextAnchor.MiddleCenter, SoftInkColor);
            SetRect(countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(900f, 48f));

            BuildRulesStrip(root);
            BuildWordSlotRoot(root);

            boardRoot = CreatePanel(root, "Board", TileBorderColor);
            SetRect(boardRoot, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoardPixels, BoardPixels));
            boardRestPosition = boardRoot.anchoredPosition;

            statusText = CreateText(root, string.Empty, 30, FontStyle.Bold, TextAnchor.MiddleCenter, SoftInkColor);
            SetRect(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 404f), new Vector2(920f, 56f));

            RectTransform controls = CreatePanel(root, "Controls", new Color(1f, 1f, 1f, 0f));
            SetRect(controls, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(940f, 132f));
            HorizontalLayoutGroup layout = controls.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            CreateButton(controls, "<", PreviousLevel);
            CreateButton(controls, "Undo", Undo);
            CreateButton(controls, "Restart", RestartLevel);
            CreateButton(controls, ">", NextLevel);

            BuildTutorialPanel(root);
            BuildJuiceLayer(root);
            BuildTutorialGuide(root);
            BuildWinPanel(root);
        }

        private void BuildRulesStrip(RectTransform root)
        {
            rulesRoot = CreatePanel(root, "Rules", new Color(1f, 1f, 1f, 0f));
            SetRect(rulesRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(940f, 72f));

            HorizontalLayoutGroup layout = rulesRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            CreateRuleChip(rulesRoot, "1 Cat in each row and Column");
            CreateRuleChip(rulesRoot, "Cats Cannot touch");
            CreateRuleChip(rulesRoot, "1 Cat in each color");
        }

        private void BuildWordSlotRoot(RectTransform root)
        {
            wordRoot = CreatePanel(root, "Word Slots", new Color(1f, 1f, 1f, 0f));
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
        }

        private void BuildWordSlotsForLevel()
        {
            if (wordRoot == null || board == null)
            {
                return;
            }

            for (int i = wordRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(wordRoot.GetChild(i).gameObject);
            }

            wordSlotTexts = new Text[board.Size];
            wordSlotImages = new Image[board.Size];
            for (int i = 0; i < board.Size; i++)
            {
                RectTransform slot = CreatePanel(wordRoot, $"Word Slot {i + 1}", new Color(1f, 0.99f, 0.96f, 0.92f));
                Outline outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.26f);
                outline.effectDistance = new Vector2(2f, -2f);

                Text text = CreateText(slot, string.Empty, 44, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
                text.raycastTarget = false;
                SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                wordSlotImages[i] = slot.GetComponent<Image>();
                wordSlotTexts[i] = text;
            }

            RefreshWordSlots();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(wordRoot);
        }

        private void RefreshWordSlots()
        {
            if (board == null || wordSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < wordSlotTexts.Length; i++)
            {
                NekoCoord coord = board.Level.Solution[i];
                bool visible = board.HasRevealedCat(coord.Row, coord.Column) && letterVisibleCats.Contains(coord);
                wordSlotTexts[i].text = visible ? board.Level.TargetWord[i].ToString() : string.Empty;
                wordSlotTexts[i].color = visible ? InkColor : SoftInkColor;
                if (wordSlotImages != null && i < wordSlotImages.Length && wordSlotImages[i] != null)
                {
                    wordSlotImages[i].color = visible ? new Color(1f, 0.96f, 0.82f, 0.96f) : new Color(1f, 0.99f, 0.96f, 0.92f);
                }
            }
        }

        private bool TryGetWordSlotCenter(int wordIndex, out Vector2 center, out float slotSize)
        {
            center = Vector2.zero;
            slotSize = 0f;
            if (juiceRoot == null
                || wordSlotTexts == null
                || wordIndex < 0
                || wordIndex >= wordSlotTexts.Length
                || wordSlotTexts[wordIndex] == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            RectTransform slotRect = (RectTransform)wordSlotTexts[wordIndex].transform.parent;
            Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
            center = juiceRoot.InverseTransformPoint(worldCenter);

            Vector3 worldMin = slotRect.TransformPoint(new Vector3(slotRect.rect.xMin, slotRect.rect.yMin, 0f));
            Vector3 worldMax = slotRect.TransformPoint(new Vector3(slotRect.rect.xMax, slotRect.rect.yMax, 0f));
            Vector3 localMin = juiceRoot.InverseTransformPoint(worldMin);
            Vector3 localMax = juiceRoot.InverseTransformPoint(worldMax);
            slotSize = Mathf.Min(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));
            return slotSize > 0f;
        }

        private void CreateRuleChip(RectTransform parent, string label)
        {
            RectTransform chip = CreatePanel(parent, label, RuleChipColor);
            Outline outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.36f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text text = CreateText(chip, label, 26, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            text.raycastTarget = false;
            SetRect(text.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void BuildTutorialPanel(RectTransform root)
        {
            tutorialPanel = CreatePanel(root, "Tutorial Panel", TutorialPanelColor);
            SetRect(tutorialPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 132f));

            Outline outline = tutorialPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.34f, 0.24f, 0.28f);
            outline.effectDistance = new Vector2(3f, -3f);

            tutorialHeaderText = CreateText(tutorialPanel, string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleCenter, SoftInkColor);
            SetRect(tutorialHeaderText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(0f, 34f));

            tutorialBodyText = CreateText(tutorialPanel, string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            SetRect(tutorialBodyText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(0f, 88f));

            tutorialPanel.gameObject.SetActive(false);
        }

        private void BuildJuiceLayer(RectTransform root)
        {
            juiceRoot = CreatePanel(root, "Juice Layer", new Color(1f, 1f, 1f, 0f));
            SetRect(juiceRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image image = juiceRoot.GetComponent<Image>();
            image.raycastTarget = false;
        }

        private void BuildTutorialGuide(RectTransform root)
        {
            tutorialGuideRoot = CreatePanel(root, "Tutorial Guide", new Color(1f, 1f, 1f, 0f));
            SetRect(tutorialGuideRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image rootImage = tutorialGuideRoot.GetComponent<Image>();
            rootImage.raycastTarget = false;

            focusRingImage = CreateGuideImage(tutorialGuideRoot, "Focus Ring", focusRingSprite, new Vector2(170f, 170f));
            tutorialHandImage = CreateGuideImage(tutorialGuideRoot, "Tutorial Hand", tutorialHandSprite, new Vector2(TutorialGuideHandSize, TutorialGuideHandSize));

            tutorialGuideText = CreateText(tutorialGuideRoot, string.Empty, 25, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            tutorialGuideText.raycastTarget = false;
            SetRect(tutorialGuideText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(920f, 58f));

            tutorialGuideRoot.gameObject.SetActive(false);
        }

        private Image CreateGuideImage(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return image;
        }

        private void BuildWinPanel(RectTransform root)
        {
            winPanel = CreatePanel(root, "Win Word Panel", new Color(1f, 0.99f, 0.96f, 0.96f));
            SetRect(winPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 220f));

            Outline panelOutline = winPanel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = TileBorderColor;
            panelOutline.effectDistance = new Vector2(4f, -4f);

            Text heading = CreateText(winPanel, "WORD FOUND", 24, FontStyle.Bold, TextAnchor.MiddleCenter, SoftInkColor);
            SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 42f));

            winWordText = CreateText(winPanel, string.Empty, 62, FontStyle.Bold, TextAnchor.MiddleCenter, InkColor);
            SetRect(winWordText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(0f, 82f));

            winSubtitleText = CreateText(winPanel, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleCenter, RevealedColor);
            SetRect(winSubtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(0f, 42f));

            winPanel.gameObject.SetActive(false);
        }

        private Image CreateNekoImage(RectTransform parent)
        {
            GameObject imageObject = new GameObject("Neko Cat", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = nekoCatSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            SetRect(image.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return image;
        }
    }
}
