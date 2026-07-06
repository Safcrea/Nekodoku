using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Ordered list of level JSON files. List order is level order (index == levelIndex).
    /// Populated by Meowdoku &gt; Levels &gt; Export Sample Levels To JSON, or by hand/future
    /// level editor tooling.
    /// </summary>
    [CreateAssetMenu(menuName = "Meowdoku/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        public List<TextAsset> levelFiles = new List<TextAsset>();

        public NekoLevel[] LoadLevels()
        {
            List<NekoLevel> levels = new List<NekoLevel>(levelFiles.Count);
            for (int i = 0; i < levelFiles.Count; i++)
            {
                TextAsset asset = levelFiles[i];
                if (asset == null)
                {
                    Debug.LogError($"Level database entry {i} is missing its JSON file.");
                    continue;
                }

                try
                {
                    LevelData data = JsonUtility.FromJson<LevelData>(asset.text);
                    levels.Add(data.ToLevel());
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Failed to load level '{asset.name}': {exception.Message}");
                }
            }

            return levels.ToArray();
        }
    }
}
