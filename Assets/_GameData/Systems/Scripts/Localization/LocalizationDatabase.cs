using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Unordered set of per-language JSON files (one <see cref="LocalizationData"/> each),
    /// keyed by their own "language" field once loaded. Kept as a ScriptableObject option for
    /// authored language sets; runtime startup now uses remote config or the bundled Resources
    /// JSON fallback.
    /// </summary>
    [CreateAssetMenu(menuName = "Meowdoku/Localization Database", fileName = "LocalizationDatabase")]
    public sealed class LocalizationDatabase : ScriptableObject
    {
        public List<TextAsset> languageFiles = new List<TextAsset>();
        public string defaultLanguage = "en";

        public Dictionary<string, LocalizationData> LoadLanguages()
        {
            Dictionary<string, LocalizationData> languages = new Dictionary<string, LocalizationData>();
            for (int i = 0; i < languageFiles.Count; i++)
            {
                TextAsset asset = languageFiles[i];
                if (asset == null)
                {
                    Debug.LogError($"Localization database entry {i} is missing its JSON file.");
                    continue;
                }

                try
                {
                    LocalizationData data = JsonUtility.FromJson<LocalizationData>(asset.text);
                    if (string.IsNullOrEmpty(data.language))
                    {
                        Debug.LogError($"Localization file '{asset.name}' has no language code.");
                        continue;
                    }

                    languages[data.language] = data;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Failed to load localization file '{asset.name}': {exception.Message}");
                }
            }

            return languages;
        }
    }
}
