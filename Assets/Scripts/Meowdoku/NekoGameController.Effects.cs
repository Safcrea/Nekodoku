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
        private void PunchCell(int row, int column, float peakScale)
        {
            NekoCoord coord = new NekoCoord(row, column);
            StopCellPunch(coord, false);
            if (!isActiveAndEnabled || !TryGetCellUi(row, column, out CellUi cell))
            {
                return;
            }

            cellPunchRoutines[coord] = StartCoroutine(AnimateCellPunch(coord, Mathf.Max(1f, peakScale)));
        }

        private IEnumerator AnimateCellPunch(NekoCoord coord, float peakScale)
        {
            float elapsed = 0f;
            while (elapsed < CellPunchSeconds)
            {
                if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CellPunchSeconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                cell.Rect.localScale = Vector3.one * Mathf.Lerp(1f, peakScale, wave);
                yield return null;
            }

            if (TryGetCellUi(coord.Row, coord.Column, out CellUi finishedCell))
            {
                finishedCell.Rect.localScale = Vector3.one;
            }

            cellPunchRoutines.Remove(coord);
        }

        private void StopCellPunch(NekoCoord coord, bool resetScale)
        {
            if (cellPunchRoutines.TryGetValue(coord, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            cellPunchRoutines.Remove(coord);
            if (resetScale && TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                cell.Rect.localScale = Vector3.one;
            }
        }

        private void SpawnFloatingLabel(int row, int column, string label, Color color)
        {
            if (juiceRoot == null || !TryGetCellCenterIn(juiceRoot, new NekoCoord(row, column), out Vector2 center, out float cellSize))
            {
                return;
            }

            Text text = CreateText(juiceRoot, label, Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.36f, 30f, 46f)), FontStyle.Bold, TextAnchor.MiddleCenter, color);
            text.raycastTarget = false;
            SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(cellSize, cellSize));
            StartCoroutine(AnimateFloatingLabel(text, center));
        }

        private void PlayCrossJelly(int row, int column)
        {
            NekoCoord coord = new NekoCoord(row, column);
            StopMarkScale(coord, true);
            if (!isActiveAndEnabled || !TryGetCellUi(row, column, out CellUi cell))
            {
                return;
            }

            markScaleRoutines[coord] = StartCoroutine(AnimateCrossJelly(coord));
        }

        private IEnumerator AnimateCrossJelly(NekoCoord coord)
        {
            float elapsed = 0f;
            while (elapsed < CrossJellySeconds)
            {
                if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CrossJellySeconds);
                float scale = CrossJellyScale(t);
                float flash = Mathf.Sin(t * Mathf.PI);
                Color baseColor = CellDisplayColor(coord.Row, coord.Column);
                cell.Rect.localScale = Vector3.one * scale;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = Color.Lerp(baseColor, CrossFlashColor, flash * 0.58f);
                yield return null;
            }

            if (TryGetCellUi(coord.Row, coord.Column, out CellUi finishedCell))
            {
                finishedCell.Rect.localScale = Vector3.one;
                finishedCell.MarkText.rectTransform.localScale = Vector3.one;
                finishedCell.Image.color = CellDisplayColor(coord.Row, coord.Column);
            }

            markScaleRoutines.Remove(coord);
        }

        private float CrossJellyScale(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.38f)
            {
                return Mathf.Lerp(1f, 1.15f, Mathf.SmoothStep(0f, 1f, t / 0.38f));
            }

            if (t < 0.68f)
            {
                return Mathf.Lerp(1.15f, 0.95f, Mathf.SmoothStep(0f, 1f, (t - 0.38f) / 0.3f));
            }

            return Mathf.Lerp(0.95f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.68f) / 0.32f));
        }

        private void StopMarkScale(NekoCoord coord, bool resetScale)
        {
            if (markScaleRoutines.TryGetValue(coord, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            markScaleRoutines.Remove(coord);
            if (resetScale && TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                cell.Rect.localScale = Vector3.one;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = CellDisplayColor(coord.Row, coord.Column);
            }
        }

        private Color CellDisplayColor(int row, int column)
        {
            if (board == null || !board.IsActiveCell(row, column))
            {
                return Color.white;
            }

            return AdjustCellColor(
                RegionColors[board.Level.RegionAt(row, column) % RegionColors.Length],
                board.GetMark(row, column),
                board.HasRevealedCat(row, column),
                board.HasRevealedMiss(row, column));
        }

        private IEnumerator AnimateFloatingLabel(Text text, Vector2 start)
        {
            float elapsed = 0f;
            Color startColor = text.color;
            while (elapsed < 0.52f)
            {
                if (text == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.52f);
                text.rectTransform.anchoredPosition = start + new Vector2(0f, Mathf.SmoothStep(0f, 72f, t));
                text.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.12f, Mathf.Sin(t * Mathf.PI));
                text.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            if (text != null)
            {
                Destroy(text.gameObject);
            }
        }

        private void SpawnCatBurst(int row, int column)
        {
            if (juiceRoot == null || juiceDotSprite == null || !TryGetCellCenterIn(juiceRoot, new NekoCoord(row, column), out Vector2 center, out float cellSize))
            {
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 10f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = (cellSize * 0.42f) + ((i % 2) * 18f);
                Color color = i % 2 == 0 ? SparkleGoldColor : RevealedColor;
                Image dot = CreateJuiceDot(center, Mathf.Clamp(cellSize * 0.1f, 12f, 20f), color);
                StartCoroutine(AnimateSparkle(dot, center, center + (direction * distance), i * 0.018f));
            }
        }

        private Image CreateJuiceDot(Vector2 center, float size, Color color)
        {
            GameObject dotObject = new GameObject("Juice Dot", typeof(RectTransform), typeof(Image));
            dotObject.transform.SetParent(juiceRoot, false);
            Image dot = dotObject.GetComponent<Image>();
            dot.sprite = juiceDotSprite;
            dot.raycastTarget = false;
            dot.color = color;
            SetRect(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(size, size));
            return dot;
        }

        private IEnumerator AnimateSparkle(Image dot, Vector2 start, Vector2 end, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (dot == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color startColor = dot.color;
            while (elapsed < SparkleSeconds)
            {
                if (dot == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SparkleSeconds);
                dot.rectTransform.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                dot.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.2f, t);
                dot.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            if (dot != null)
            {
                Destroy(dot.gameObject);
            }
        }

        private void PlayCatRevealForCat(int row, int column)
        {
            StopCatReveal();
            inputLocked = true;
            crossDragging = false;
            StopTutorialGuide();
            catRevealRoutine = StartCoroutine(AnimateCatRevealForCat(new NekoCoord(row, column)));
        }

        private IEnumerator AnimateCatRevealForCat(NekoCoord catCoord)
        {
            yield return AnimateCatFoundPop(catCoord);
            SpawnCatBurst(catCoord.Row, catCoord.Column);
            yield return AnimateLetterFlight(catCoord);

            inputLocked = false;
            catRevealRoutine = null;
            NekoHaptics.Play(HapticStrength.Light);
            RefreshBoard();
        }

        private IEnumerator AnimateCatFoundPop(NekoCoord coord)
        {
            if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                yield break;
            }

            StopCellPunch(coord, true);
            Color startOutlineColor = cell.Outline.effectColor;
            Vector2 startOutlineDistance = cell.Outline.effectDistance;
            float elapsed = 0f;
            while (elapsed < CatFoundPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CatFoundPopSeconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                cell.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, wave);
                cell.Outline.effectColor = Color.Lerp(startOutlineColor, SparkleGoldColor, wave);
                cell.Outline.effectDistance = Vector2.Lerp(startOutlineDistance, new Vector2(6f, -6f), wave);
                yield return null;
            }

            cell.Rect.localScale = Vector3.one;
            cell.Outline.effectColor = startOutlineColor;
            cell.Outline.effectDistance = startOutlineDistance;
        }

        private IEnumerator AnimateLetterFlight(NekoCoord coord)
        {
            int wordIndex = board.GetCatIndex(coord.Row, coord.Column);
            if (wordIndex < 0
                || !TryGetCellCenterIn(juiceRoot, coord, out Vector2 start, out float cellSize)
                || !TryGetWordSlotCenter(wordIndex, out Vector2 end, out float slotSize))
            {
                letterVisibleCats.Add(coord);
                RefreshWordSlots();
                yield break;
            }

            Text flyingLetter = CreateText(
                juiceRoot,
                board.GetLetterForCat(coord.Row, coord.Column).ToString(),
                Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.4f, 36f, 58f)),
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                InkColor);
            flyingLetter.raycastTarget = false;
            SetRect(flyingLetter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), start, new Vector2(slotSize, slotSize));

            float elapsed = 0f;
            float trailDelay = 0f;
            int trailIndex = 0;
            while (elapsed < LetterFlySeconds)
            {
                if (flyingLetter == null)
                {
                    yield break;
                }

                float delta = Time.unscaledDeltaTime;
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / LetterFlySeconds);
                float eased = EaseOutCubic(t);
                Vector2 arc = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 84f);
                Vector2 currentPosition = Vector2.Lerp(start, end, eased) + arc;
                flyingLetter.rectTransform.anchoredPosition = currentPosition;
                flyingLetter.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.08f, EaseOutBack(t));

                trailDelay -= delta;
                if (trailDelay <= 0f)
                {
                    SpawnLetterTrailDot(currentPosition, Mathf.Clamp(slotSize * 0.1f, 8f, 15f), trailIndex++);
                    trailDelay = 0.055f;
                }

                yield return null;
            }

            if (flyingLetter != null)
            {
                Destroy(flyingLetter.gameObject);
            }

            letterVisibleCats.Add(coord);
            RefreshWordSlots();
            SpawnWordSlotBurst(end, slotSize);
            yield return AnimateWordSlotBounce(wordIndex);
        }

        private void SpawnLetterTrailDot(Vector2 center, float size, int index)
        {
            if (juiceRoot == null || juiceDotSprite == null)
            {
                return;
            }

            float angle = index * 2.399963f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color color = index % 2 == 0 ? SparkleGoldColor : RevealedColor;
            Image dot = CreateJuiceDot(center, size, color);
            StartCoroutine(AnimateSparkle(dot, center, center + (direction * (size * 1.4f)), 0f));
        }

        private void SpawnWordSlotBurst(Vector2 center, float slotSize)
        {
            if (juiceRoot == null || juiceDotSprite == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 8f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = (slotSize * 0.28f) + ((i % 2) * 10f);
                Color color = i % 2 == 0 ? SparkleGoldColor : RevealedColor;
                Image dot = CreateJuiceDot(center, Mathf.Clamp(slotSize * 0.08f, 8f, 14f), color);
                StartCoroutine(AnimateSparkle(dot, center, center + (direction * distance), i * 0.015f));
            }
        }

        private IEnumerator AnimateWordSlotBounce(int wordIndex)
        {
            if (wordSlotTexts == null || wordIndex < 0 || wordIndex >= wordSlotTexts.Length || wordSlotTexts[wordIndex] == null)
            {
                yield break;
            }

            RectTransform slot = (RectTransform)wordSlotTexts[wordIndex].transform.parent;
            float elapsed = 0f;
            const float seconds = 0.18f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                slot.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, wave);
                yield return null;
            }

            slot.localScale = Vector3.one;
        }

        private void StopCatReveal()
        {
            if (catRevealRoutine != null)
            {
                StopCoroutine(catRevealRoutine);
                catRevealRoutine = null;
            }

            inputLocked = false;
            crossDragging = false;
            ResetWordSlotScales();
            if (board != null && boardRoot != null)
            {
                ApplyBoardLayoutImmediate(false);
                RefreshWordSlots();
            }
        }

        private void ResetWordSlotScales()
        {
            if (wordSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < wordSlotTexts.Length; i++)
            {
                if (wordSlotTexts[i] == null)
                {
                    continue;
                }

                wordSlotTexts[i].transform.parent.localScale = Vector3.one;
            }
        }

        private float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }

        private void ShakeBoard()
        {
            if (!isActiveAndEnabled || boardRoot == null)
            {
                return;
            }

            if (boardShakeRoutine != null)
            {
                StopCoroutine(boardShakeRoutine);
            }

            boardShakeRoutine = StartCoroutine(AnimateBoardShake());
        }

        private IEnumerator AnimateBoardShake()
        {
            float elapsed = 0f;
            boardRoot.anchoredPosition = boardRestPosition;
            while (elapsed < BoardShakeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BoardShakeSeconds);
                float falloff = 1f - t;
                float x = Mathf.Sin(elapsed * 82f) * 18f * falloff;
                float y = Mathf.Sin(elapsed * 47f) * 5f * falloff;
                boardRoot.anchoredPosition = boardRestPosition + new Vector2(x, y);
                yield return null;
            }

            boardRoot.anchoredPosition = boardRestPosition;
            boardShakeRoutine = null;
        }

        private void PlayWinPanelPop()
        {
            StopWinPanelPop();
            if (isActiveAndEnabled && winPanel != null)
            {
                winPanelPopRoutine = StartCoroutine(AnimateWinPanelPop());
            }
        }

        private IEnumerator AnimateWinPanelPop()
        {
            float elapsed = 0f;
            const float seconds = 0.34f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float scale = Mathf.Lerp(0.84f, 1f, EaseOutBack(t));
                winPanel.localScale = Vector3.one * scale;
                yield return null;
            }

            winPanel.localScale = Vector3.one;
            winPanelPopRoutine = null;
        }

        private void StopWinPanelPop()
        {
            if (winPanelPopRoutine != null)
            {
                StopCoroutine(winPanelPopRoutine);
                winPanelPopRoutine = null;
            }
        }

        private void StopJuiceEffects()
        {
            if (boardShakeRoutine != null)
            {
                StopCoroutine(boardShakeRoutine);
                boardShakeRoutine = null;
            }

            if (boardRoot != null)
            {
                boardRoot.anchoredPosition = boardRestPosition;
            }

            foreach (KeyValuePair<NekoCoord, Coroutine> punch in cellPunchRoutines)
            {
                if (punch.Value != null)
                {
                    StopCoroutine(punch.Value);
                }
            }

            cellPunchRoutines.Clear();
            foreach (KeyValuePair<NekoCoord, Coroutine> markScale in markScaleRoutines)
            {
                if (markScale.Value != null)
                {
                    StopCoroutine(markScale.Value);
                }
            }

            markScaleRoutines.Clear();
            foreach (CellUi cell in cells)
            {
                cell.Rect.localScale = Vector3.one;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = CellDisplayColor(cell.Row, cell.Column);
            }

            if (juiceRoot != null)
            {
                for (int i = juiceRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(juiceRoot.GetChild(i).gameObject);
                }
            }
        }
    }
}
