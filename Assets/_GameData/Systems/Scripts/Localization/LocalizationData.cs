using System;

namespace Meowdoku
{
    /// <summary>One localized string, keyed by a dotted path (e.g. "levelComplete.text").</summary>
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string value;
    }

    /// <summary>One localized pool of interchangeable lines (e.g. "levelComplete.comments"),
    /// picked from at random by <see cref="LocalizationService.GetRandomFromPool"/>.</summary>
    [Serializable]
    public sealed class LocalizationPool
    {
        public string key;
        public string[] values;
    }

    /// <summary>
    /// Plain, JSON-serializable localization file for one language. This is the source-of-truth
    /// format for player-facing text: one file per language under
    /// Assets/_GameData/Systems/Data/Localization/, loaded by <see cref="LocalizationDatabase"/>.
    ///
    /// Uses flat dotted-key arrays instead of nested classes or a Dictionary because
    /// JsonUtility can't deserialize a Dictionary&lt;string,string&gt; directly, and hand-writing
    /// one nested serializable class per screen would be a lot of ceremony for something this
    /// loosely structured - the same reasoning LevelData already uses arrays instead of
    /// dictionaries for regions/cats.
    /// </summary>
    [Serializable]
    public sealed class LocalizationData
    {
        public string language;
        public LocalizationEntry[] strings;
        public LocalizationPool[] pools;
    }

    /// <summary>
    /// Optional multi-language wrapper for remote/localization JSON. A payload may either be this
    /// shape, or a single <see cref="LocalizationData"/> file for one language.
    /// </summary>
    [Serializable]
    public sealed class LocalizationConfig
    {
        public string defaultLanguage = "en";
        public LocalizationData[] languages;
    }
}
