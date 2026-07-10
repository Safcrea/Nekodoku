using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>Plain JSON mirror of the shared ordering manifest the web level editor also
    /// reads/writes - a flat array of level filename stems (not the level's own "id" field,
    /// which is allowed to drift from its filename once a level is edited in place).</summary>
    [Serializable]
    internal sealed class LevelManifestData
    {
        public int formatVersion;
        public string[] order;
    }

    /// <summary>
    /// Keeps <see cref="LevelDatabase"/>'s levelFiles order in sync with
    /// <see cref="ManifestPath"/> - the same manifest the web level editor's Level Library
    /// panel reads/writes - so reordering/adding/removing levels never requires hand-dragging
    /// TextAssets in the Inspector. <see cref="LevelManifestPostprocessor"/> triggers
    /// <see cref="SyncFromManifest"/> automatically whenever the manifest or any level file
    /// changes; the menu items here exist for a manual re-run and the one-time bootstrap.
    /// </summary>
    public static class LevelManifestSync
    {
        internal const string ManifestPath = "Assets/_GameData/Systems/Data/Levels/Default Levels/levels-manifest.json";
        internal const string LevelsFolderPath = "Assets/_GameData/Systems/Data/Levels/Default Levels";
        private const string DatabasePath = "Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset";

        [MenuItem("Meowdoku/Levels/Sync Level Database From Manifest")]
        public static void SyncFromManifestMenuItem()
        {
            SyncFromManifest();
        }

        /// <summary>Rebuilds LevelDatabase.levelFiles from the manifest's order. Safe to call
        /// repeatedly - it fully overwrites the list each time rather than incrementally
        /// patching it, so there's nothing to get out of sync with itself.</summary>
        internal static void SyncFromManifest()
        {
            TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifestAsset == null)
            {
                Debug.LogError($"LevelManifestSync: no manifest found at '{ManifestPath}'. Run 'Export Level Order To Manifest' first.");
                return;
            }

            LevelManifestData manifest;
            try
            {
                manifest = JsonUtility.FromJson<LevelManifestData>(manifestAsset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"LevelManifestSync: failed to parse manifest at '{ManifestPath}': {exception.Message}");
                return;
            }

            if (manifest?.order == null)
            {
                Debug.LogError($"LevelManifestSync: manifest at '{ManifestPath}' is malformed (missing 'order').");
                return;
            }

            if (!TryBuildStemLookup(out Dictionary<string, TextAsset> stemToAsset))
            {
                return;
            }

            List<TextAsset> resolved = new List<TextAsset>(manifest.order.Length);
            HashSet<string> seenStems = new HashSet<string>();
            int missingCount = 0;
            foreach (string stem in manifest.order)
            {
                seenStems.Add(stem);
                if (stemToAsset.TryGetValue(stem, out TextAsset asset))
                {
                    resolved.Add(asset);
                }
                else
                {
                    missingCount++;
                    Debug.LogWarning($"LevelManifestSync: manifest entry '{stem}' has no matching file under '{LevelsFolderPath}' - skipped.");
                }
            }

            int orphanCount = 0;
            foreach (string stem in stemToAsset.Keys)
            {
                if (!seenStems.Contains(stem))
                {
                    orphanCount++;
                    Debug.LogWarning($"LevelManifestSync: '{stem}.json' exists under '{LevelsFolderPath}' but isn't listed in the manifest - it will NOT be included in the game until added to '{ManifestPath}'.");
                }
            }

            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"LevelManifestSync: no LevelDatabase found at '{DatabasePath}'.");
                return;
            }

            database.levelFiles = resolved;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"LevelManifestSync: synced {resolved.Count} levels from manifest ({missingCount} missing, {orphanCount} orphaned).");
        }

        /// <summary>One-time bootstrap (also safe to re-run defensively): captures
        /// LevelDatabase.levelFiles' CURRENT order - the known-good, already-shipped order -
        /// into the manifest, rather than trusting filename numbering to have zero drift.</summary>
        [MenuItem("Meowdoku/Levels/Export Level Order To Manifest")]
        public static void ExportOrderToManifest()
        {
            LevelDatabase database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"LevelManifestSync: no LevelDatabase found at '{DatabasePath}'.");
                return;
            }

            string[] order = new string[database.levelFiles.Count];
            for (int i = 0; i < database.levelFiles.Count; i++)
            {
                TextAsset asset = database.levelFiles[i];
                if (asset == null)
                {
                    Debug.LogError($"LevelManifestSync: LevelDatabase entry {i} is missing its JSON file - fix that before exporting.");
                    return;
                }

                string assetPath = AssetDatabase.GetAssetPath(asset);
                order[i] = Path.GetFileNameWithoutExtension(assetPath);
            }

            LevelManifestData manifest = new LevelManifestData { formatVersion = 1, order = order };
            string json = JsonUtility.ToJson(manifest, true);

            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
            File.WriteAllText(absolutePath, json + "\n");
            AssetDatabase.ImportAsset(ManifestPath);

            Debug.Log($"LevelManifestSync: exported {order.Length} levels to '{ManifestPath}'.");
        }

        private static bool TryBuildStemLookup(out Dictionary<string, TextAsset> stemToAsset)
        {
            stemToAsset = new Dictionary<string, TextAsset>();
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == ManifestPath)
                {
                    continue;
                }

                string stem = Path.GetFileNameWithoutExtension(path);
                if (stemToAsset.TryGetValue(stem, out TextAsset existing) && existing != null)
                {
                    Debug.LogError($"LevelManifestSync: two files resolve to the same stem '{stem}' under '{LevelsFolderPath}' - aborting sync until that's resolved.");
                    stemToAsset = null;
                    return false;
                }

                stemToAsset[stem] = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }

            return true;
        }
    }
}
