using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Static lookup for all player-facing text. Firebase Remote Config may provide the JSON at
    /// runtime; if it does not, the service falls back to the bundled Resources JSON.
    /// </summary>
    public static class LocalizationService
    {
        private const string LanguagePlayerPrefsKey = "Nekodoku.Language";
        private const string DefaultResourcesPath = "Localization/default";

        private static Dictionary<string, Dictionary<string, string>> stringsByLanguage;
        private static Dictionary<string, Dictionary<string, string[]>> poolsByLanguage;
        private static string defaultLanguage = "en";
        private static string currentLanguage = "en";

        public static event Action Changed;

        public static bool IsInitialized { get; private set; }
        public static bool IsRemoteConfigured { get; private set; }

        public static void InitializeDefaultIfNeeded()
        {
            if (!IsInitialized)
            {
                InitializeFromDefaultResources();
            }
        }

        public static bool InitializeFromDefaultResources()
        {
            TextAsset defaultAsset = Resources.Load<TextAsset>(DefaultResourcesPath);
            if (defaultAsset == null)
            {
                Debug.LogError($"Default localization JSON missing from Resources/{DefaultResourcesPath}.json.");
                InitializeEmptyFallback();
                return false;
            }

            return InitializeFromJson(defaultAsset.text, $"Resources/{DefaultResourcesPath}.json", false);
        }

        public static bool InitializeFromRemoteJson(string json)
        {
            return InitializeFromJson(json, "Firebase Remote Config localization", true);
        }

        public static bool InitializeFromJson(string json, string sourceDescription, bool remote = false)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"Localization JSON from {sourceDescription} was empty.");
                return false;
            }

            if (!TryParseLanguages(json, sourceDescription, out Dictionary<string, LocalizationData> languages, out string parsedDefaultLanguage))
            {
                return false;
            }

            return ApplyLanguages(languages, parsedDefaultLanguage, sourceDescription, remote);
        }

        public static string Get(string key)
        {
            EnsureInitialized();

            if (TryGetString(currentLanguage, key, out string value))
            {
                return value;
            }

            if (TryGetString(defaultLanguage, key, out string fallback))
            {
                return fallback;
            }

            return $"!{key}!";
        }

        public static string GetFormat(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static string GetRandomFromPool(string key)
        {
            EnsureInitialized();

            if (!TryGetPool(currentLanguage, key, out string[] pool) &&
                !TryGetPool(defaultLanguage, key, out pool))
            {
                return $"!{key}!";
            }

            if (pool == null || pool.Length == 0)
            {
                return $"!{key}!";
            }

            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>Replaces a named command such as {x} in localized text. Keeping this separate
        /// from string.Format lets localization authors use readable command names without escaping
        /// unrelated TextMesh Pro or Text Animator tags.</summary>
        public static string ReplaceCommand(string text, string command, object value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(command))
            {
                return text;
            }

            string replacement = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return text.Replace($"{{{command}}}", replacement);
        }

        private static bool TryParseLanguages(
            string json,
            string sourceDescription,
            out Dictionary<string, LocalizationData> languages,
            out string parsedDefaultLanguage)
        {
            languages = new Dictionary<string, LocalizationData>();
            parsedDefaultLanguage = null;

            try
            {
                LocalizationConfig config = JsonUtility.FromJson<LocalizationConfig>(json);
                if (config != null && config.languages != null && config.languages.Length > 0)
                {
                    parsedDefaultLanguage = config.defaultLanguage;
                    for (int i = 0; i < config.languages.Length; i++)
                    {
                        AddLanguage(config.languages[i], $"{sourceDescription} languages[{i}]", languages);
                    }

                    return languages.Count > 0;
                }

                LocalizationData singleLanguage = JsonUtility.FromJson<LocalizationData>(json);
                if (AddLanguage(singleLanguage, sourceDescription, languages))
                {
                    parsedDefaultLanguage = singleLanguage.language;
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to parse localization JSON from {sourceDescription}: {exception.Message}");
                return false;
            }

            Debug.LogError($"Localization JSON from {sourceDescription} did not contain any languages.");
            return false;
        }

        private static bool ApplyLanguages(
            Dictionary<string, LocalizationData> languages,
            string nextDefaultLanguage,
            string sourceDescription,
            bool remote)
        {
            if (languages == null || languages.Count == 0)
            {
                Debug.LogError($"Localization source '{sourceDescription}' has no valid languages.");
                return false;
            }

            stringsByLanguage = new Dictionary<string, Dictionary<string, string>>();
            poolsByLanguage = new Dictionary<string, Dictionary<string, string[]>>();

            foreach (KeyValuePair<string, LocalizationData> entry in languages)
            {
                LocalizationData data = entry.Value;
                Dictionary<string, string> strings = new Dictionary<string, string>();
                if (data.strings != null)
                {
                    foreach (LocalizationEntry stringEntry in data.strings)
                    {
                        if (stringEntry == null || string.IsNullOrEmpty(stringEntry.key))
                        {
                            continue;
                        }

                        strings[stringEntry.key] = stringEntry.value;
                    }
                }

                stringsByLanguage[entry.Key] = strings;

                Dictionary<string, string[]> pools = new Dictionary<string, string[]>();
                if (data.pools != null)
                {
                    foreach (LocalizationPool poolEntry in data.pools)
                    {
                        if (poolEntry == null || string.IsNullOrEmpty(poolEntry.key))
                        {
                            continue;
                        }

                        pools[poolEntry.key] = poolEntry.values;
                    }
                }

                poolsByLanguage[entry.Key] = pools;
            }

            defaultLanguage = ResolveDefaultLanguage(languages, nextDefaultLanguage);
            string savedLanguage = PlayerPrefs.GetString(LanguagePlayerPrefsKey, defaultLanguage);
            currentLanguage = languages.ContainsKey(savedLanguage) ? savedLanguage : defaultLanguage;
            IsInitialized = true;
            IsRemoteConfigured = remote;

            Debug.Log($"Localization initialized from {sourceDescription} with {languages.Count} language(s).");
            Changed?.Invoke();
            return true;
        }

        private static bool AddLanguage(LocalizationData data, string sourceDescription, Dictionary<string, LocalizationData> languages)
        {
            if (data == null)
            {
                Debug.LogWarning($"Localization entry from {sourceDescription} was null.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.language))
            {
                Debug.LogWarning($"Localization entry from {sourceDescription} has no language code.");
                return false;
            }

            languages[data.language] = data;
            return true;
        }

        private static string ResolveDefaultLanguage(Dictionary<string, LocalizationData> languages, string requestedDefaultLanguage)
        {
            if (!string.IsNullOrWhiteSpace(requestedDefaultLanguage) && languages.ContainsKey(requestedDefaultLanguage))
            {
                return requestedDefaultLanguage;
            }

            foreach (string language in languages.Keys)
            {
                if (!string.IsNullOrWhiteSpace(requestedDefaultLanguage))
                {
                    Debug.LogWarning($"Default localization language '{requestedDefaultLanguage}' is missing; using '{language}'.");
                }

                return language;
            }

            return "en";
        }

        private static void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                InitializeFromDefaultResources();
            }
        }

        private static void InitializeEmptyFallback()
        {
            stringsByLanguage = new Dictionary<string, Dictionary<string, string>>();
            poolsByLanguage = new Dictionary<string, Dictionary<string, string[]>>();
            defaultLanguage = "en";
            currentLanguage = "en";
            IsInitialized = true;
            IsRemoteConfigured = false;
            Changed?.Invoke();
        }

        private static bool TryGetString(string language, string key, out string value)
        {
            value = null;
            return stringsByLanguage != null &&
                   language != null &&
                   stringsByLanguage.TryGetValue(language, out Dictionary<string, string> strings) &&
                   strings.TryGetValue(key, out value);
        }

        private static bool TryGetPool(string language, string key, out string[] pool)
        {
            pool = null;
            return poolsByLanguage != null &&
                   language != null &&
                   poolsByLanguage.TryGetValue(language, out Dictionary<string, string[]> pools) &&
                   pools.TryGetValue(key, out pool);
        }
    }
}
