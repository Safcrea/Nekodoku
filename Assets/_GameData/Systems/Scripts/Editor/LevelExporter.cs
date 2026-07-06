using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// One-time (and re-runnable) migration tool: converts the hardcoded <see cref="NekoSampleLevels"/>
    /// seed data into the JSON <see cref="LevelData"/> format and rebuilds the <see cref="LevelDatabase"/>
    /// asset that the game loads at runtime.
    /// </summary>
    public static class LevelExporter
    {
        private const string LevelsFolder = "Assets/_GameData/Systems/Data/Levels";
        private const string DatabaseAssetPath = "Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset";

        [MenuItem("Meowdoku/Levels/Export Sample Levels To JSON")]
        public static void ExportSampleLevels()
        {
            Directory.CreateDirectory(LevelsFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(DatabaseAssetPath));

            NekoLevel[] levels = NekoSampleLevels.Levels;
            List<string> errors = new List<string>();
            List<string> filePaths = new List<string>(levels.Length);

            for (int i = 0; i < levels.Length; i++)
            {
                NekoLevel level = levels[i];
                List<string> issues = NekoLevelValidator.FindStructuralIssues(level);
                if (issues.Count > 0)
                {
                    errors.Add($"Level {i} ({level.Title}): {string.Join("; ", issues)}");
                    continue;
                }

                if (NekoLevelValidator.CountLegalSolutions(level, 2) != 1)
                {
                    errors.Add($"Level {i} ({level.Title}): does not have exactly one legal solution.");
                    continue;
                }

                string id = $"{i:000}_{Slugify(level.Title)}";
                LevelData data = LevelData.FromLevel(id, level);
                string json = JsonUtility.ToJson(data, true);
                string filePath = $"{LevelsFolder}/{id}.json";
                File.WriteAllText(filePath, json);
                filePaths.Add(filePath);
            }

            if (errors.Count > 0)
            {
                Debug.LogError($"Level export aborted: {errors.Count} level(s) failed validation.\n{string.Join("\n", errors)}");
                return;
            }

            AssetDatabase.Refresh();
            BuildDatabase(filePaths);
            Debug.Log($"Exported {filePaths.Count} levels to {LevelsFolder} and rebuilt {DatabaseAssetPath}.");
        }

        private static void BuildDatabase(List<string> filePaths)
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            database.levelFiles.Clear();
            foreach (string path in filePaths)
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                database.levelFiles.Add(asset);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static string Slugify(string title)
        {
            string lower = title.ToLowerInvariant();
            string slug = Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "level" : slug;
        }
    }
}
