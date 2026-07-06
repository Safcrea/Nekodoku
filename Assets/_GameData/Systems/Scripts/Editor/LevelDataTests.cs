using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Meowdoku.Tests
{
    public sealed class LevelDataTests
    {
        private const string DatabaseAssetPath = "Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset";


        [Test]
        public void RoundTripThroughJsonPreservesLevelData()
        {
            foreach (Level original in NekoSampleLevels.Levels)
            {
                LevelData data = LevelData.FromLevel("round-trip-test", original);
                string json = JsonUtility.ToJson(data);
                LevelData parsed = JsonUtility.FromJson<LevelData>(json);
                Level restored = parsed.ToLevel();

                Assert.AreEqual(original.Title, restored.Title);
                Assert.AreEqual(original.TargetWord, restored.TargetWord);
                Assert.AreEqual(original.Size, restored.Size);
                CollectionAssert.AreEqual(original.Regions, restored.Regions, $"{original.Title} regions changed across a JSON round trip.");
                CollectionAssert.AreEqual(original.Solution, restored.Solution, $"{original.Title} solution changed across a JSON round trip.");
                CollectionAssert.AreEqual(original.LockedCats, restored.LockedCats, $"{original.Title} locked cats changed across a JSON round trip.");
            }
        }

        [Test]
        public void ExportedLevelDatabaseMatchesSampleLevels()
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabaseAssetPath);
            Assert.IsNotNull(database, $"No level database found at {DatabaseAssetPath}. Run Meowdoku > Levels > Export Sample Levels To JSON first.");

            Level[] loaded = database.LoadLevels();
            Level[] original = NekoSampleLevels.Levels;
            Assert.AreEqual(original.Length, loaded.Length, "Exported level count does not match NekoSampleLevels. Re-run the exporter.");

            for (int i = 0; i < original.Length; i++)
            {
                List<string> issues = NekoLevelValidator.FindStructuralIssues(loaded[i]);
                Assert.IsEmpty(issues, string.Join("\n", issues));
                Assert.AreEqual(original[i].Title, loaded[i].Title, $"Level {i} title mismatch.");
                Assert.AreEqual(original[i].TargetWord, loaded[i].TargetWord, $"Level {i} target word mismatch.");
                Assert.AreEqual(original[i].Size, loaded[i].Size, $"Level {i} size mismatch.");
                CollectionAssert.AreEqual(original[i].Regions, loaded[i].Regions, $"Level {i} regions mismatch.");
                CollectionAssert.AreEqual(original[i].Solution, loaded[i].Solution, $"Level {i} solution mismatch.");
                CollectionAssert.AreEqual(original[i].LockedCats, loaded[i].LockedCats, $"Level {i} locked cats mismatch.");
            }
        }
    }
}
