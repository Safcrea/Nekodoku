using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Visual editor for the JSON level format: paint regions, place cats, type the target
    /// word, and see live solvability feedback via <see cref="NekoLevelValidator"/> before
    /// saving to Assets/_GameData/Systems/Data/Levels and registering with the LevelDatabase.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private enum PaintMode
        {
            Regions,
            Cats
        }

        private const string LevelsFolder = "Assets/_GameData/Systems/Data/Levels";
        private const string DatabaseAssetPath = "Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset";
        private const int MinSize = 3;
        private const int MaxSize = 9;
        private const float CellSize = 40f;

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

        private string loadedFilePath;
        private string id = string.Empty;
        private string title = "New Level";
        private int size = 5;
        private string targetWord = string.Empty;
        private int[] regions;
        private NekoCoord?[] catPositions;
        private bool[] catLocked;

        private PaintMode paintMode = PaintMode.Regions;
        private int selectedRegion;
        private int selectedLetterIndex;

        private readonly List<string> validationIssues = new List<string>();
        private int legalSolutionCount = -1;

        [MenuItem("Meowdoku/Levels/Level Editor")]
        public static void Open()
        {
            GetWindow<LevelEditorWindow>("Meowdoku Level Editor");
        }

        private void OnEnable()
        {
            if (regions == null)
            {
                ResetToNewLevel();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();
            DrawFields();
            EditorGUILayout.Space();
            DrawPaintControls();
            EditorGUILayout.Space();
            DrawGrid();
            EditorGUILayout.Space();
            DrawValidation();
        }

        private void ResetToNewLevel()
        {
            loadedFilePath = null;
            id = string.Empty;
            title = "New Level";
            targetWord = string.Empty;
            selectedRegion = 0;
            selectedLetterIndex = 0;
            SetSize(5);
            Validate();
        }

        private void SetSize(int newSize)
        {
            newSize = Mathf.Clamp(newSize, MinSize, MaxSize);
            int[] newRegions = new int[newSize * newSize];
            NekoCoord?[] newCats = new NekoCoord?[newSize];
            bool[] newLocked = new bool[newSize];

            if (regions != null)
            {
                int oldSize = size;
                int copySize = Mathf.Min(oldSize, newSize);
                for (int row = 0; row < copySize; row++)
                {
                    for (int column = 0; column < copySize; column++)
                    {
                        newRegions[(row * newSize) + column] = regions[(row * oldSize) + column];
                    }
                }

                int copyCats = Mathf.Min(catPositions.Length, newCats.Length);
                for (int i = 0; i < copyCats; i++)
                {
                    NekoCoord? coord = catPositions[i];
                    if (coord.HasValue && coord.Value.Row < newSize && coord.Value.Column < newSize)
                    {
                        newCats[i] = coord;
                        newLocked[i] = catLocked[i];
                    }
                }
            }

            size = newSize;
            regions = newRegions;
            catPositions = newCats;
            catLocked = newLocked;

            if (targetWord != null && targetWord.Length > size)
            {
                targetWord = targetWord.Substring(0, size);
            }

            selectedLetterIndex = Mathf.Clamp(selectedLetterIndex, 0, size - 1);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton))
                {
                    ResetToNewLevel();
                }

                if (GUILayout.Button("Load...", EditorStyles.toolbarButton))
                {
                    ShowLoadMenu();
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    Save();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(string.IsNullOrEmpty(loadedFilePath) ? "(unsaved)" : loadedFilePath, EditorStyles.miniLabel);
            }
        }

        private void DrawFields()
        {
            EditorGUI.BeginChangeCheck();
            string newTitle = EditorGUILayout.TextField("Title", title);
            string newId = EditorGUILayout.TextField("Id (filename)", id);
            int newSize = EditorGUILayout.IntSlider("Size", size, MinSize, MaxSize);
            string newWord = EditorGUILayout.TextField("Target Word", targetWord);
            if (EditorGUI.EndChangeCheck())
            {
                title = newTitle;
                id = newId;
                if (newSize != size)
                {
                    SetSize(newSize);
                }

                targetWord = (newWord ?? string.Empty).ToUpperInvariant();
                if (targetWord.Length > size)
                {
                    targetWord = targetWord.Substring(0, size);
                }

                Validate();
            }
        }

        private void DrawPaintControls()
        {
            EditorGUILayout.LabelField("Paint Mode", EditorStyles.boldLabel);
            paintMode = (PaintMode)GUILayout.Toolbar((int)paintMode, new[] { "Regions", "Cats" });

            if (paintMode == PaintMode.Regions)
            {
                EditorGUILayout.LabelField("Selected region (click a swatch, then click cells)");
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < size; i++)
                    {
                        Color previous = GUI.backgroundColor;
                        GUI.backgroundColor = RegionColors[i % RegionColors.Length];
                        if (GUILayout.Toggle(selectedRegion == i, i.ToString(), EditorStyles.miniButton, GUILayout.Width(28f)))
                        {
                            selectedRegion = i;
                        }

                        GUI.backgroundColor = previous;
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Selected letter (click a letter, then click its cell)");
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < size; i++)
                    {
                        char letterChar = i < targetWord.Length ? targetWord[i] : '?';
                        if (GUILayout.Toggle(selectedLetterIndex == i, letterChar.ToString(), EditorStyles.miniButton, GUILayout.Width(28f)))
                        {
                            selectedLetterIndex = i;
                        }
                    }
                }

                bool locked = EditorGUILayout.Toggle("Starts revealed (locked)", catLocked[selectedLetterIndex]);
                if (locked != catLocked[selectedLetterIndex])
                {
                    catLocked[selectedLetterIndex] = locked;
                    Validate();
                }
            }
        }

        private void DrawGrid()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width((size * CellSize) + 10f)))
            {
                for (int row = 0; row < size; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int column = 0; column < size; column++)
                        {
                            DrawCell(row, column);
                        }
                    }
                }
            }
        }

        private void DrawCell(int row, int column)
        {
            int region = regions[(row * size) + column];
            int catIndex = IndexOfCatAt(row, column);
            string label = catIndex >= 0 ? (catLocked[catIndex] ? $"*{LetterAt(catIndex)}" : LetterAt(catIndex).ToString()) : string.Empty;

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = RegionColors[region % RegionColors.Length];
            if (GUILayout.Button(label, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
            {
                OnCellClicked(row, column);
            }

            GUI.backgroundColor = previousColor;
        }

        private void OnCellClicked(int row, int column)
        {
            if (paintMode == PaintMode.Regions)
            {
                regions[(row * size) + column] = selectedRegion;
            }
            else
            {
                int existingIndex = IndexOfCatAt(row, column);
                catPositions[selectedLetterIndex] = existingIndex == selectedLetterIndex ? null : new NekoCoord(row, column);
            }

            Validate();
            Repaint();
        }

        private char LetterAt(int index)
        {
            return index < targetWord.Length ? targetWord[index] : '?';
        }

        private int IndexOfCatAt(int row, int column)
        {
            for (int i = 0; i < catPositions.Length; i++)
            {
                if (catPositions[i].HasValue && catPositions[i].Value.Row == row && catPositions[i].Value.Column == column)
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (validationIssues.Count == 0 && legalSolutionCount == 1)
            {
                EditorGUILayout.HelpBox("Valid - exactly one legal solution.", MessageType.Info);
                return;
            }

            foreach (string issue in validationIssues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Error);
            }

            if (validationIssues.Count == 0 && legalSolutionCount >= 0)
            {
                string message = legalSolutionCount == 0
                    ? "No legal solution exists for this region/cat layout."
                    : $"{legalSolutionCount} legal solutions exist - add distinguishing regions so there is exactly one.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }
        }

        private void Validate()
        {
            validationIssues.Clear();
            legalSolutionCount = -1;

            if (!TryBuildLevel(out NekoLevel level, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    validationIssues.Add(error);
                }

                return;
            }

            validationIssues.AddRange(NekoLevelValidator.FindStructuralIssues(level));
            if (validationIssues.Count == 0)
            {
                legalSolutionCount = NekoLevelValidator.CountLegalSolutions(level, 2);
            }
        }

        private bool TryBuildLevel(out NekoLevel level, out string error)
        {
            level = null;
            error = null;

            if (string.IsNullOrWhiteSpace(title))
            {
                error = "Title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetWord) || targetWord.Length != size)
            {
                error = $"Target word must be exactly {size} letters.";
                return false;
            }

            NekoCoord[] solution = new NekoCoord[size];
            List<NekoCoord> locked = new List<NekoCoord>();
            for (int i = 0; i < size; i++)
            {
                if (!catPositions[i].HasValue)
                {
                    error = $"Letter {i + 1} ('{targetWord[i]}') has no cat placed yet.";
                    return false;
                }

                solution[i] = catPositions[i].Value;
                if (catLocked[i])
                {
                    locked.Add(solution[i]);
                }
            }

            try
            {
                level = new NekoLevel(title, targetWord, regions, solution, locked.ToArray());
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void ShowLoadMenu()
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                Debug.LogError($"No level database found at {DatabaseAssetPath}.");
                return;
            }

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < database.levelFiles.Count; i++)
            {
                TextAsset asset = database.levelFiles[i];
                if (asset == null)
                {
                    continue;
                }

                int index = i;
                menu.AddItem(new GUIContent($"{index:000} {asset.name}"), false, () => LoadFromAsset(asset));
            }

            menu.ShowAsContext();
        }

        private void LoadFromAsset(TextAsset asset)
        {
            try
            {
                LevelData data = JsonUtility.FromJson<LevelData>(asset.text);
                NekoLevel level = data.ToLevel();
                LoadFromLevel(level, data.id, AssetDatabase.GetAssetPath(asset));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to load {asset.name}: {exception.Message}");
            }
        }

        private void LoadFromLevel(NekoLevel level, string levelId, string filePath)
        {
            loadedFilePath = filePath;
            id = levelId;
            title = level.Title;
            targetWord = level.TargetWord;
            SetSize(level.Size);
            regions = (int[])level.Regions.Clone();

            for (int i = 0; i < size; i++)
            {
                catPositions[i] = level.Solution[i];
            }

            foreach (NekoCoord lockedCoord in level.LockedCats)
            {
                int index = level.IndexOfCat(lockedCoord.Row, lockedCoord.Column);
                if (index >= 0)
                {
                    catLocked[index] = true;
                }
            }

            selectedLetterIndex = 0;
            Validate();
            Repaint();
        }

        private void Save()
        {
            if (!TryBuildLevel(out NekoLevel level, out string error))
            {
                Debug.LogError($"Cannot save: {error}");
                return;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                id = Slugify(title);
            }

            Directory.CreateDirectory(LevelsFolder);
            string path = $"{LevelsFolder}/{id}.json";
            LevelData data = LevelData.FromLevel(id, level);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            AddOrUpdateDatabaseEntry(asset);

            loadedFilePath = path;
            Debug.Log($"Saved {path}.");
            Validate();
        }

        private static void AddOrUpdateDatabaseEntry(TextAsset asset)
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                Debug.LogWarning($"No level database found at {DatabaseAssetPath}; saved the JSON file but did not register it.");
                return;
            }

            if (!database.levelFiles.Contains(asset))
            {
                database.levelFiles.Add(asset);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
        }

        private static string Slugify(string value)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            string slug = Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "level" : slug;
        }
    }
}
